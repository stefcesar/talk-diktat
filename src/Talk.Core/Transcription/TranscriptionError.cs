namespace Talk.Core.Transcription;

/// <summary>
/// Warum eine Erkennung gescheitert ist. Die App übersetzt das in eine verständliche Meldung und in das Protokoll.
/// </summary>
public enum TranscriptionError
{
    /// <summary>Es ist kein Schlüssel hinterlegt.</summary>
    NoKey,

    /// <summary>Der Schlüssel wurde abgelehnt.</summary>
    Unauthorized,

    /// <summary>Das Limit des Dienstes ist erreicht.</summary>
    RateLimited,

    /// <summary>Der Dienst ist nicht erreichbar (kein Internet, Verbindungsfehler).</summary>
    Network,

    /// <summary>Der Dienst hat nicht rechtzeitig geantwortet.</summary>
    Timeout,

    /// <summary>Der Dienst meldet einen eigenen Fehler.</summary>
    ServerError,

    /// <summary>Die Aufnahme ist für den Dienst zu groß.</summary>
    TooLarge,

    /// <summary>Der Dienst lehnt die Anfrage als fehlerhaft ab.</summary>
    BadRequest,

    /// <summary>Die Antwort des Dienstes ist nicht auswertbar.</summary>
    BadResponse,

    /// <summary>Das lokale Modell fehlt.</summary>
    ModelMissing,

    /// <summary>Es ist kein lokaler Erkenner vorhanden, obwohl nur lokal erkannt werden soll.</summary>
    LocalUnavailable,

    /// <summary>Der Erkenner ist aus einem anderen Grund gescheitert.</summary>
    EngineFailed,
}

/// <summary>
/// Ausnahme einer gescheiterten Erkennung. Die Meldung enthält nie den Schlüssel und nie erkannten Text.
/// </summary>
public sealed class TranscriptionException : Exception
{
    private const int MaxServerCodeLength = 40;

    /// <summary>Erzeugt die Ausnahme.</summary>
    /// <param name="error">Der Grund.</param>
    /// <param name="message">Verständliche Meldung ohne Schlüssel und ohne erkannten Text.</param>
    /// <param name="inner">Auslösende Ausnahme oder null.</param>
    /// <param name="retryAfter">Vom Dienst genannte Wartezeit oder null.</param>
    /// <param name="serverCode">Fehlerkürzel des Dienstes; wird auf Kleinbuchstaben, Ziffern und Unterstrich beschränkt.</param>
    public TranscriptionException(
        TranscriptionError error,
        string message,
        Exception? inner = null,
        TimeSpan? retryAfter = null,
        string? serverCode = null)
        : base(message, inner)
    {
        Error = error;
        RetryAfter = retryAfter;
        ServerCode = SanitizeCode(serverCode);
    }

    /// <summary>Der Grund.</summary>
    public TranscriptionError Error { get; }

    /// <summary>Vom Dienst genannte Wartezeit oder null.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Fehlerkürzel des Dienstes (nur Kleinbuchstaben, Ziffern, Unterstrich, höchstens 40 Zeichen) oder null.</summary>
    public string? ServerCode { get; }

    /// <summary>
    /// Beschränkt ein Kürzel auf Kleinbuchstaben a bis z, Ziffern und Unterstrich und kürzt auf 40 Zeichen.
    /// So kann über dieses Feld nie freier Text ins Protokoll gelangen. Leer ergibt null.
    /// </summary>
    public static string? SanitizeCode(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;

        Span<char> buffer = stackalloc char[MaxServerCodeLength];
        int count = 0;
        foreach (char raw in code)
        {
            if (count == MaxServerCodeLength) break;
            char c = char.ToLowerInvariant(raw);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') buffer[count++] = c;
        }

        return count == 0 ? null : new string(buffer[..count]);
    }
}
