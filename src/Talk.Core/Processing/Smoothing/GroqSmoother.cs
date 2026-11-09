using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Talk.Core.Transcription;

namespace Talk.Core.Processing.Smoothing;

/// <summary>
/// Einstellungen für die Glättung über Groq. Modellname und Adresse sind Einstellungen, kein fester Code, weil beides sich ändert.
/// </summary>
/// <param name="Endpoint">Adresse der Chat Schnittstelle.</param>
/// <param name="Model">Name des Sprachmodells.</param>
/// <param name="TimeoutMs">Zeitlimit für den ganzen Aufruf. Danach gilt der ungeglättete Text.</param>
/// <param name="Temperature">Wie frei das Modell formuliert. Niedrig, damit es beim Text bleibt.</param>
/// <param name="ReasoningEffort">Wie viel das Modell vorab überlegt. Niedrig, damit es schnell bleibt.</param>
/// <param name="SwissModel">Sprachmodell für Schweizerdeutsch und Mundart (Playbook Abschnitt 26): Das kleine Modell ergänzt
/// verschluckte Endungen kaum und trifft die Dialekte schlecht.</param>
public sealed record GroqSmoothingOptions(
    string Endpoint = GroqSmoothingOptions.DefaultEndpoint,
    string Model = GroqSmoothingOptions.DefaultModel,
    int TimeoutMs = 4000,
    double Temperature = 0.2,
    string ReasoningEffort = "low",
    string SwissModel = GroqSmoothingOptions.DefaultSwissModel)
{
    /// <summary>Die Chat Schnittstelle von Groq (console.groq.com/docs/api-reference, gelesen am 11.10.2026).</summary>
    public const string DefaultEndpoint = "https://api.groq.com/openai/v1/chat/completions";

    /// <summary>Das schnellste Produktionsmodell für Text bei Groq (console.groq.com/docs/models, gelesen am 11.10.2026).</summary>
    public const string DefaultModel = "openai/gpt-oss-20b";

    /// <summary>Das größere Modell derselben Reihe für Schweizerdeutsch und Mundart.</summary>
    public const string DefaultSwissModel = "openai/gpt-oss-120b";
}

/// <summary>
/// Glättet Text über die Chat Schnittstelle von Groq. An den Dienst gehen nur die feste Anweisung aus
/// <see cref="SmoothingPrompt"/> und der Text. Wirft nie, außer bei Abbruch durch den Aufrufer.
/// </summary>
public sealed class GroqSmoother : ISmoother
{
    private readonly HttpClient _http;
    private readonly Func<string?> _apiKey;
    private readonly GroqSmoothingOptions _options;

    /// <summary>
    /// Legt den Glätter an.
    /// </summary>
    /// <param name="http">Der HTTP Client. In der App lässt er nur api.groq.com zu.</param>
    /// <param name="apiKey">Liefert den Schlüssel oder null. Er wird je Aufruf frisch gelesen und nie gespeichert.</param>
    /// <param name="options">Einstellungen oder null für die Standardwerte.</param>
    public GroqSmoother(HttpClient http, Func<string?> apiKey, GroqSmoothingOptions? options = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _options = options ?? new GroqSmoothingOptions();
    }

    /// <inheritdoc />
    public async Task<SmoothingResult> SmoothAsync(string text, SmoothingLevel level, string language, CancellationToken ct)
    {
        string input = text ?? string.Empty;
        if (level == SmoothingLevel.Off) return new SmoothingResult(input, SmoothingOutcome.Off, 0);
        bool dialect = LanguageCodes.IsDialect(language);
        int minWords = dialect ? SmoothingGuard.MinDialectWords : SmoothingGuard.MinWords;
        if (!SmoothingGuard.IsLongEnough(input, minWords)) return new SmoothingResult(input, SmoothingOutcome.TooShort, 0);
        if (input.Length > SmoothingGuard.MaxChars) return new SmoothingResult(input, SmoothingOutcome.TooLong, 0);

        string? key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new SmoothingResult(input, SmoothingOutcome.NoKey, 0);

        ct.ThrowIfCancellationRequested();
        Stopwatch watch = Stopwatch.StartNew();
        SmoothingResult Failed(SmoothingOutcome outcome, int status = 0) => new(input, outcome, watch.ElapsedMilliseconds, status);

        try
        {
            using HttpRequestMessage message = new(HttpMethod.Post, _options.Endpoint)
            {
                Content = new StringContent(BuildBody(input, level, language), Encoding.UTF8, "application/json"),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_options.TimeoutMs);

            using HttpResponseMessage response = await _http
                .SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Der Statuscode geht als Zahl mit zurück, damit sich ein abgelehnter Modellname (404) von einer abgelehnten
                // Anfrage (400) und einem Ausfall (5xx) unterscheiden lässt. Der Rumpf der Fehlermeldung wird nie gelesen.
                return Failed(
                    response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SmoothingOutcome.Unauthorized,
                        HttpStatusCode.TooManyRequests => SmoothingOutcome.RateLimited,
                        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => SmoothingOutcome.Timeout,
                        _ => SmoothingOutcome.ServiceError,
                    },
                    (int)response.StatusCode);
            }

            string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            string? content = ReadContent(body, out bool complete);
            if (content is null) return Failed(SmoothingOutcome.ServiceError);

            // Eine abgeschnittene Antwort (Tokengrenze erreicht) wird nie übernommen: Sonst fehlte das Ende des Diktats.
            if (!complete) return Failed(SmoothingOutcome.Rejected);

            string? accepted = SmoothingGuard.Accept(input, content, dialect);
            return accepted is null
                ? Failed(SmoothingOutcome.Rejected)
                : new SmoothingResult(accepted, SmoothingOutcome.Applied, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(SmoothingOutcome.Timeout);
        }
        catch (HttpRequestException)
        {
            return Failed(SmoothingOutcome.Network);
        }
        catch (Exception)
        {
            return Failed(SmoothingOutcome.ServiceError);
        }
    }

    // Die Anfrage: Anweisung als Systemnachricht, der Text als Nutzernachricht, sonst nichts.
    private string BuildBody(string text, SmoothingLevel level, string language)
    {
        // Großzügig gerechnet: Die Antwort darf gut doppelt so lang werden wie der Eingang, bei Umlauten und Namen ist ein Token
        // oft kürzer als drei Zeichen, und die Vorüberlegungen des Modells zählen in dasselbe Budget.
        int maxTokens = Math.Clamp(text.Length + 800, 1024, 8192);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", LanguageCodes.IsSwiss(language) ? _options.SwissModel : _options.Model);
            writer.WriteNumber("temperature", _options.Temperature);
            writer.WriteNumber("max_completion_tokens", maxTokens);
            writer.WriteString("reasoning_effort", _options.ReasoningEffort);
            writer.WriteBoolean("include_reasoning", false);
            writer.WriteBoolean("stream", false);
            writer.WriteStartArray("messages");
            WriteMessage(writer, "system", SmoothingPrompt.Build(level, language));
            WriteMessage(writer, "user", text);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string content)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", content);
        writer.WriteEndObject();
    }

    // Liest choices[0].message.content. Alles andere ergibt null. complete ist nur wahr, wenn das Modell von selbst fertig wurde
    // (finish_reason "stop").
    private static string? ReadContent(string body, out bool complete)
    {
        complete = false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("choices", out JsonElement choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = choices[0];
            complete = first.TryGetProperty("finish_reason", out JsonElement reason)
                && reason.ValueKind == JsonValueKind.String
                && string.Equals(reason.GetString(), "stop", StringComparison.Ordinal);
            if (!first.TryGetProperty("message", out JsonElement message)) return null;
            if (!message.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.String) return null;
            return content.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
