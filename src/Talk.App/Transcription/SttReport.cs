// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Globalization;

using Talk.App.Diagnostics;
using Talk.App.Ui;
using Talk.Core.Transcription;

namespace Talk.App.Transcription;

/// <summary>
/// Die eine Quelle für Protokollzeilen und Anzeigetexte der Erkennung, damit Testfenster und Befehlszeile wortgleich berichten.
/// </summary>
/// <remarks>
/// Im Protokoll stehen nur Zahlen, feste Wörter und Zustandsnamen. Der erkannte Text geht nie hinein, nur seine Länge.
/// Der Schlüssel kommt in dieser Klasse nicht vor. Namen von Erkenner und Sprache werden auf Kleinbuchstaben beschränkt,
/// damit über diese Felder kein freier Text ins Protokoll gelangen kann.
/// </remarks>
internal static class SttReport
{
    /// <summary>Ersatzwort für Angaben, die fehlen oder nicht ins Protokoll dürfen.</summary>
    public const string Unknown = "unbekannt";

    private const string GroqEngine = GroqTranscriber.EngineName;
    private const int MinWordLength = 2;
    private const int MaxWordLength = 12;

    // Ersatzwörter im Feld code der Zeile stt | error: kein Kürzel vorhanden, oder ein Kürzel, das nicht auf der festen Liste steht.
    private const string NoCode = "kein";
    private const string OtherCode = "sonstiges";

    // Die feste Liste der Fehlerkürzel des Dienstes, die wörtlich ins Protokoll dürfen. Alles andere wird "sonstiges",
    // damit über dieses Feld nichts ins Protokoll gelangt, was der Dienst frei wählen kann.
    private static readonly string[] KnownErrorCodes =
    [
        "invalid_api_key",
        "rate_limit_exceeded",
        "model_not_found",
        "invalid_request_error",
    ];

    /// <summary>
    /// Das Detail der Protokollzeile <c>stt | result</c>, zum Beispiel
    /// "engine=groq ms=640 encode_ms=2 upload_ms=80 wait_ms=520 audio_ms=3100 chars=58 segments=1 filtered=0 lang=german fallback=nein".
    /// </summary>
    /// <param name="result">Das Ergebnis der Erkennung. Vom Text wird nur die Länge benutzt.</param>
    /// <param name="audioMs">Dauer des gesendeten Clips in Millisekunden.</param>
    public static string LogDetail(TranscriptionResult result, long audioMs)
    {
        ArgumentNullException.ThrowIfNull(result);

        TranscriptionTimings? timings = result.Timings;
        string fallback = result.FallbackReason is TranscriptionError reason ? reason.ToString() : "nein";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"engine={SanitizeWord(result.Engine)} ms={TotalMs(result)} encode_ms={timings?.EncodeMs ?? 0} upload_ms={timings?.UploadMs ?? 0} wait_ms={timings?.WaitMs ?? 0} audio_ms={Math.Max(0, audioMs)} chars={result.Text?.Length ?? 0} segments={result.Segments?.Count ?? 0} filtered={result.FilteredSegments} lang={SanitizeLanguage(result.DetectedLanguage)} fallback={fallback}");
    }

    /// <summary>
    /// Das Detail der Protokollzeile <c>stt | error</c>, zum Beispiel "grund=Unauthorized code=invalid_api_key" oder "grund=NoKey code=kein".
    /// Im Feld <c>code</c> steht nur ein Kürzel von der festen Liste (<c>invalid_api_key</c>, <c>rate_limit_exceeded</c>,
    /// <c>model_not_found</c>, <c>invalid_request_error</c>); jedes andere vorhandene Kürzel wird "sonstiges", ohne Kürzel steht "kein".
    /// </summary>
    /// <param name="ex">Die Ausnahme der gescheiterten Erkennung. Ihre Meldung wird nicht protokolliert.</param>
    public static string ErrorDetail(TranscriptionException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return $"grund={ex.Error} code={CodeWord(ex.ServerCode)}";
    }

    /// <summary>
    /// Das Detail der Protokollzeile <c>stt | limit</c>, zum Beispiel "remaining=1987 reset_s=43". Fehlende Werte heißen "unbekannt".
    /// </summary>
    /// <param name="rateLimit">Die Limit Angaben des Dienstes oder null.</param>
    public static string LimitDetail(RateLimitInfo? rateLimit)
    {
        string remaining = rateLimit?.RemainingRequests is int count
            ? count.ToString(CultureInfo.InvariantCulture)
            : Unknown;

        string reset = rateLimit?.ResetRequests is TimeSpan time
            ? ((long)Math.Round(time.TotalSeconds, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
            : Unknown;

        return $"remaining={remaining} reset_s={reset}";
    }

    /// <summary>
    /// Die Zeile fürs Diagnosefenster in der Sprache der Oberfläche (Playbook Abschnitt 29), auf Deutsch zum Beispiel
    /// "Groq, 640 ms (Upload 80 ms, Antwort 520 ms)" oder bei einem Wechsel "Groq nicht verfügbar (Limit erreicht), lokal erkannt, 900 ms".
    /// Zahlen in <see cref="Lang.Culture"/> (Regel 8). Die Prüfschalter benutzen diese Zeile nicht.
    /// </summary>
    /// <param name="result">Das Ergebnis der Erkennung.</param>
    public static string DisplayText(TranscriptionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        string engine = SanitizeWord(result.Engine);
        string ms = TotalMs(result).ToString(Lang.Culture);
        bool local = engine == LocalWhisperTranscriber.EngineName;
        string text;

        if (result.FallbackReason is TranscriptionError reason && local)
        {
            text = Lang.F($"msg.dictation.stt.fallback.{FallbackKey(reason)}", ms);
        }
        else if (engine == GroqEngine && result.Timings is TranscriptionTimings timings)
        {
            text = Lang.F("msg.dictation.stt.groq", ms, timings.UploadMs.ToString(Lang.Culture), timings.WaitMs.ToString(Lang.Culture));
        }
        else if (local)
        {
            text = Lang.F("msg.dictation.stt.local", ms);
        }
        else
        {
            // Andere Erkenner zeigen ihr festes Wort, wie es aus dem Protokoll kommt.
            text = Lang.F("msg.dictation.stt.engine", Capitalize(engine), ms);
        }

        if (result.FilteredSegments == 1) text += ", " + Lang.T("msg.dictation.stt.filtered.one");
        else if (result.FilteredSegments > 1) text += ", " + Lang.F("msg.dictation.stt.filtered.many", result.FilteredSegments.ToString(Lang.Culture));

        return text;
    }

    /// <summary>Ein kurzer Satz je Fehler in der Sprache der Oberfläche, für die Anzeige im Fenster.</summary>
    /// <param name="error">Der Grund.</param>
    public static string ErrorText(TranscriptionError error) => Lang.T(error switch
    {
        TranscriptionError.NoKey => "msg.dictation.stt.error.no_key",
        TranscriptionError.Unauthorized => "msg.dictation.stt.error.unauthorized",
        TranscriptionError.RateLimited => "msg.dictation.stt.error.rate_limited",
        TranscriptionError.Network => "msg.dictation.stt.error.network",
        TranscriptionError.Timeout => "msg.dictation.stt.error.timeout",
        TranscriptionError.ServerError => "msg.dictation.stt.error.server",
        TranscriptionError.TooLarge => "msg.dictation.stt.error.too_large",
        TranscriptionError.BadRequest => "msg.dictation.stt.error.bad_request",
        TranscriptionError.BadResponse => "msg.dictation.stt.error.bad_response",
        TranscriptionError.ModelMissing => "msg.dictation.stt.error.model_missing",
        TranscriptionError.LocalUnavailable => "msg.dictation.stt.error.local_unavailable",
        _ => "msg.dictation.stt.error.failed",
    });

    /// <summary>
    /// Die Sprache fürs Protokoll: nur Kleinbuchstaben a bis z, 2 bis 12 Zeichen, sonst "unbekannt".
    /// Großbuchstaben und Leerzeichen an den Rändern werden vorher angeglichen ("DE" wird "de").
    /// </summary>
    /// <param name="language">Die vom Erkenner gemeldete Sprache oder null.</param>
    public static string SanitizeLanguage(string? language) => SanitizeWord(language);

    /// <summary>Das feste Wort einer Schlüsselprüfung fürs Protokoll: gueltig, ungueltig, keine_verbindung oder fehlt.</summary>
    /// <param name="result">Ergebnis der Prüfung.</param>
    public static string KeyCheckWord(KeyCheckResult result) => result switch
    {
        KeyCheckResult.Valid => "gueltig",
        KeyCheckResult.Invalid => "ungueltig",
        KeyCheckResult.Missing => "fehlt",
        _ => "keine_verbindung",
    };

    /// <summary>Die Anzeige einer Schlüsselprüfung im Fenster.</summary>
    /// <param name="result">Ergebnis der Prüfung.</param>
    public static string KeyCheckGerman(KeyCheckResult result) => result switch
    {
        KeyCheckResult.Valid => "Schlüssel gültig",
        KeyCheckResult.Invalid => "Schlüssel abgelehnt",
        KeyCheckResult.Missing => "Kein Schlüssel",
        _ => "Keine Bestätigung von Groq",
    };

    /// <summary>
    /// Schreibt <c>stt | result</c> und, wenn der Dienst Limit Angaben geliefert hat, <c>stt | limit</c>.
    /// </summary>
    /// <param name="result">Das Ergebnis der Erkennung.</param>
    /// <param name="audioMs">Dauer des gesendeten Clips in Millisekunden.</param>
    public static void WriteResult(TranscriptionResult result, long audioMs)
    {
        AppLog.Write("stt", "result", LogDetail(result, audioMs));
        if (result.RateLimit is not null) AppLog.Write("stt", "limit", LimitDetail(result.RateLimit));
    }

    /// <summary>Schreibt <c>stt | error</c>.</summary>
    /// <param name="ex">Die Ausnahme der gescheiterten Erkennung.</param>
    public static void WriteError(TranscriptionException ex)
    {
        AppLog.Write("stt", "error", ErrorDetail(ex));
    }

    /// <summary>Schreibt <c>stt | skipped | grund=keine_sprache</c>: Die Stilleerkennung fand keine Sprache, es wurde nichts gesendet.</summary>
    public static void WriteSkippedNoSpeech()
    {
        AppLog.Write("stt", "skipped", "grund=keine_sprache");
    }

    /// <summary>
    /// Das Detail der Protokollzeile <c>stt | local</c> nach dem Laden des lokalen Modells, zum Beispiel "geladen ms=1200 laufzeit=vulkan".
    /// </summary>
    /// <param name="loadMs">Dauer des Ladens in Millisekunden.</param>
    /// <param name="runtime">
    /// Die geladene Laufzeit aus <see cref="WhisperRuntime.LoadedName"/>. Alles außer <c>vulkan</c> und <c>cpu</c> wird "unbekannt".
    /// </param>
    /// <param name="swiss">Das Schweizer Modell ist geladen (Playbook Abschnitt 26); dann folgt "modell=schweiz".</param>
    public static string LocalLoadedDetail(long loadMs, string? runtime, bool swiss = false)
    {
        string word = runtime is WhisperRuntime.Vulkan or WhisperRuntime.Cpu ? runtime : Unknown;
        string model = swiss ? " modell=schweiz" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"geladen ms={Math.Max(0, loadMs)} laufzeit={word}{model}");
    }

    /// <summary>
    /// Schreibt <c>stt | local | geladen ms=N laufzeit=vulkan/cpu/unbekannt</c>, beim Schweizer Modell mit <c>modell=schweiz</c>:
    /// Das lokale Modell ist geladen.
    /// </summary>
    /// <param name="loadMs">Dauer des Ladens in Millisekunden.</param>
    /// <param name="runtime">Die geladene Laufzeit aus <see cref="WhisperRuntime.LoadedName"/>.</param>
    /// <param name="swiss">Das Schweizer Modell ist geladen.</param>
    public static void WriteLocalLoaded(long loadMs, string? runtime, bool swiss = false)
    {
        AppLog.Write("stt", "local", LocalLoadedDetail(loadMs, runtime, swiss));
    }

    /// <summary>Schreibt <c>stt | local | fehlt</c>: Die Modelldatei der lokalen Erkennung ist nicht da. Nachgeladen wird nie.</summary>
    public static void WriteLocalMissing()
    {
        AppLog.Write("stt", "local", "fehlt");
    }

    /// <summary>Schreibt <c>stt | local | fehler</c>: Das lokale Modell ist da, lässt sich aber nicht laden.</summary>
    public static void WriteLocalFailed()
    {
        AppLog.Write("stt", "local", "fehler");
    }

    // Der letzte Teil des Schlüssels der Wechselzeile msg.dictation.stt.fallback.* je Grund, je Grund ein ganzer Satz.
    private static string FallbackKey(TranscriptionError error) => error switch
    {
        TranscriptionError.NoKey => "no_key",
        TranscriptionError.Unauthorized => "unauthorized",
        TranscriptionError.RateLimited => "rate_limited",
        TranscriptionError.Network => "network",
        TranscriptionError.Timeout => "timeout",
        TranscriptionError.ServerError => "server",
        TranscriptionError.TooLarge => "too_large",
        TranscriptionError.BadRequest => "bad_request",
        TranscriptionError.BadResponse => "bad_response",
        _ => "other",
    };

    // Das Wort fürs Feld code: ein Kürzel von der festen Liste, sonst "sonstiges", ohne Kürzel "kein".
    // Das Kürzel ist schon beim Erzeugen der Ausnahme beschränkt worden; die zweite Beschränkung kostet nichts.
    private static string CodeWord(string? serverCode)
    {
        string? code = TranscriptionException.SanitizeCode(serverCode);
        if (code is null) return NoCode;

        return Array.IndexOf(KnownErrorCodes, code) >= 0 ? code : OtherCode;
    }

    private static long TotalMs(TranscriptionResult result)
    {
        return Math.Max(0, (long)Math.Round(result.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero));
    }

    // Lässt nur Wörter aus Kleinbuchstaben a bis z mit 2 bis 12 Zeichen durch, alles andere wird "unbekannt".
    private static string SanitizeWord(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Unknown;

        string word = value.Trim().ToLowerInvariant();
        if (word.Length < MinWordLength || word.Length > MaxWordLength) return Unknown;

        foreach (char c in word)
        {
            if (c < 'a' || c > 'z') return Unknown;
        }

        return word;
    }

    private static string Capitalize(string word)
    {
        return word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
    }
}
