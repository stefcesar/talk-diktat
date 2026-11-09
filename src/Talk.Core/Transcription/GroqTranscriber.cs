using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Talk.Core.Audio;

namespace Talk.Core.Transcription;

/// <summary>
/// Erkennung über die Groq Schnittstelle (kompatibel zur Transkription von OpenAI). Sendet den Clip als WAV aus dem
/// Arbeitsspeicher, verlangt die ausführliche Antwort mit Kennzahlen je Abschnitt und filtert erfundene Sätze.
/// Der Schlüssel kommt je Anfrage aus einer Funktion und steht nie in einer Meldung oder Ausnahme.
/// </summary>
public sealed class GroqTranscriber : ITranscriber
{
    /// <summary>Name dieses Erkenners in Ergebnissen und im Protokoll.</summary>
    public const string EngineName = "groq";

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKey;
    private readonly GroqOptions _options;
    private readonly HallucinationFilterOptions _filter;

    /// <summary>Erzeugt den Erkenner.</summary>
    /// <param name="http">Gemeinsamer HTTP Client der App (Verbindungen werden wiederverwendet).</param>
    /// <param name="apiKey">Liefert den Schlüssel oder null, wenn keiner hinterlegt ist. Wird je Anfrage gerufen.</param>
    /// <param name="options">Einstellungen oder null für die Standardwerte.</param>
    /// <param name="filter">Schwellen des Filters gegen erfundene Sätze oder null für die Standardwerte.</param>
    public GroqTranscriber(HttpClient http, Func<string?> apiKey, GroqOptions? options = null, HallucinationFilterOptions? filter = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _options = options ?? new GroqOptions();
        _options.Validate();
        _filter = filter ?? new HallucinationFilterOptions();
    }

    /// <inheritdoc />
    public string Name => EngineName;

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);

        // Ein leerer Clip wird nie gesendet.
        if (clip.Samples.Length == 0)
        {
            return new TranscriptionResult(string.Empty, null, TimeSpan.Zero, EngineName, [], new TranscriptionTimings(0, 0, 0, 0));
        }

        string? key = _apiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new TranscriptionException(TranscriptionError.NoKey, "Es ist kein Groq Schlüssel hinterlegt.");
        }

        Stopwatch watch = Stopwatch.StartNew();
        byte[] wav = WavEncoder.Encode(clip);
        int encodeMs = (int)watch.ElapsedMilliseconds;

        try
        {
            return await SendAsync(wav, watch, encodeMs, key, request, ct).ConfigureAwait(false);
        }
        finally
        {
            // Die Kopie des Audios für die Übertragung bleibt nicht im Arbeitsspeicher liegen.
            Array.Clear(wav);
        }
    }

    private async Task<TranscriptionResult> SendAsync(byte[] wav, Stopwatch watch, int encodeMs, string key, TranscriptionRequest request, CancellationToken ct)
    {
        using MultipartFormDataContent form = new();

        // Feldnamen stehen ausdrücklich in Anführungszeichen, so wie es der Standard für Formulardaten vorsieht.
        TimedFileContent file = new(wav, watch);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"file\"", FileName = "\"audio.wav\"" };
        form.Add(file);

        AddField(form, "model", _options.Model);
        AddField(form, "response_format", "verbose_json");
        AddField(form, "temperature", "0");
        if (!request.AutoDetect) AddField(form, "language", request.WhisperLanguage);

        string? prompt = VocabularyPrompt.Build(request.Vocabulary, _options.MaxPromptChars);
        if (prompt is not null) AddField(form, "prompt", prompt);

        using HttpRequestMessage message = new(HttpMethod.Post, _options.Endpoint) { Content = form };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.TimeoutMs);

        long sendStartMs = watch.ElapsedMilliseconds;
        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TranscriptionException(TranscriptionError.Timeout, "Groq hat nicht rechtzeitig geantwortet.");
        }
        catch (HttpRequestException ex)
        {
            throw new TranscriptionException(TranscriptionError.Network, "Groq ist nicht erreichbar.", ex);
        }

        using (response)
        {
            long responseDoneMs = watch.ElapsedMilliseconds;

            RateLimitInfo? limits = RateLimitInfo.FromHeaders(name =>
                response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null);

            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new TranscriptionException(TranscriptionError.Network, "Die Antwort von Groq konnte nicht gelesen werden.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw MapStatus(response.StatusCode, limits, body);
            }

            (string rawText, string? language, List<TranscriptionSegment> segments) = Parse(body);

            string text;
            IReadOnlyList<TranscriptionSegment> kept;
            int filtered;

            if (segments.Count > 0)
            {
                HallucinationFilterResult result = HallucinationFilter.Apply(segments, _filter);
                text = result.Text;
                kept = result.Kept;
                filtered = result.FilteredCount;
            }
            else
            {
                text = HallucinationFilter.FilterText(rawText, _filter);
                kept = [];
                filtered = text.Length == 0 && !string.IsNullOrWhiteSpace(rawText) ? 1 : 0;
            }

            long uploadDoneMs = Math.Clamp(file.WrittenAtMs ?? sendStartMs, sendStartMs, responseDoneMs);
            long totalMs = watch.ElapsedMilliseconds;

            TranscriptionTimings timings = new(
                encodeMs,
                (int)(uploadDoneMs - sendStartMs),
                (int)(responseDoneMs - uploadDoneMs),
                (int)totalMs);

            return new TranscriptionResult(
                text,
                string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant(),
                TimeSpan.FromMilliseconds(totalMs),
                EngineName,
                kept,
                timings,
                limits,
                filtered);
        }
    }

    private static void AddField(MultipartFormDataContent form, string name, string value)
    {
        StringContent content = new(value);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"" + name + "\"" };
        form.Add(content);
    }

    private static TranscriptionException MapStatus(HttpStatusCode status, RateLimitInfo? limits, string body)
    {
        int code = (int)status;
        string? serverCode = TryReadErrorCode(body);

        TranscriptionError error = code switch
        {
            401 or 403 => TranscriptionError.Unauthorized,
            429 => TranscriptionError.RateLimited,
            413 => TranscriptionError.TooLarge,
            408 => TranscriptionError.Timeout,
            400 or 404 or 422 => TranscriptionError.BadRequest,
            >= 500 => TranscriptionError.ServerError,
            _ => TranscriptionError.BadResponse,
        };

        string message = error switch
        {
            TranscriptionError.Unauthorized => "Groq lehnt den Schlüssel ab.",
            TranscriptionError.RateLimited => "Das Limit bei Groq ist erreicht.",
            TranscriptionError.TooLarge => "Die Aufnahme ist für Groq zu groß.",
            TranscriptionError.Timeout => "Groq hat nicht rechtzeitig geantwortet.",
            TranscriptionError.BadRequest => "Groq lehnt die Anfrage ab.",
            TranscriptionError.ServerError => "Groq meldet einen eigenen Fehler.",
            _ => "Groq antwortet unerwartet.",
        };

        return new TranscriptionException(
            error,
            message + " Status " + code + ".",
            null,
            error == TranscriptionError.RateLimited ? limits?.RetryAfter : null,
            serverCode);
    }

    // Liest das Fehlerkürzel aus {"error":{"code":"...","type":"..."}}; Kürzel vor Typ. Unlesbares ergibt null.
    private static string? TryReadErrorCode(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("error", out JsonElement error) || error.ValueKind != JsonValueKind.Object) return null;

            // Ist das Kürzel leer oder nach dem Bereinigen leer, gilt der Typ.
            return TranscriptionException.SanitizeCode(GetString(error, "code"))
                ?? TranscriptionException.SanitizeCode(GetString(error, "type"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string Text, string? Language, List<TranscriptionSegment> Segments) Parse(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) throw BadResponse();

            string? text = GetString(root, "text");
            if (text is null) throw BadResponse();

            string? language = GetString(root, "language");
            List<TranscriptionSegment> segments = [];

            if (root.TryGetProperty("segments", out JsonElement array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    segments.Add(new TranscriptionSegment(
                        GetDouble(item, "start") ?? 0,
                        GetDouble(item, "end") ?? 0,
                        GetString(item, "text") ?? string.Empty,
                        GetDouble(item, "avg_logprob"),
                        GetDouble(item, "no_speech_prob"),
                        GetDouble(item, "compression_ratio")));
                }
            }

            return (text, language, segments);
        }
        catch (JsonException ex)
        {
            throw new TranscriptionException(TranscriptionError.BadResponse, "Die Antwort von Groq ist nicht auswertbar.", ex);
        }
    }

    private static TranscriptionException BadResponse() =>
        new(TranscriptionError.BadResponse, "Die Antwort von Groq ist nicht auswertbar.");

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? GetDouble(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double d) ? d : null;

    // Merkt sich, wann die Audiodaten vollständig an die Verbindung übergeben wurden (für die Messung des Uploads).
    private sealed class TimedFileContent : ByteArrayContent
    {
        private readonly Stopwatch _watch;

        public TimedFileContent(byte[] content, Stopwatch watch)
            : base(content)
        {
            _watch = watch;
        }

        public long? WrittenAtMs { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await base.SerializeToStreamAsync(stream, context).ConfigureAwait(false);
            WrittenAtMs = _watch.ElapsedMilliseconds;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await base.SerializeToStreamAsync(stream, context, cancellationToken).ConfigureAwait(false);
            WrittenAtMs = _watch.ElapsedMilliseconds;
        }

        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            base.SerializeToStream(stream, context, cancellationToken);
            WrittenAtMs = _watch.ElapsedMilliseconds;
        }
    }
}
