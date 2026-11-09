using System.Globalization;

namespace Talk.Core.Transcription;

/// <summary>
/// Limit Angaben aus den Kopfzeilen einer Antwort des Dienstes.
/// </summary>
/// <param name="LimitRequests">Erlaubte Anfragen im Zeitraum oder null.</param>
/// <param name="RemainingRequests">Verbleibende Anfragen im Zeitraum oder null.</param>
/// <param name="ResetRequests">Zeit bis zum Zurücksetzen des Zählers oder null.</param>
/// <param name="RetryAfter">Vom Dienst genannte Wartezeit bei erreichtem Limit oder null.</param>
public sealed record RateLimitInfo(int? LimitRequests, int? RemainingRequests, TimeSpan? ResetRequests, TimeSpan? RetryAfter)
{
    /// <summary>Name der Kopfzeile für die erlaubten Anfragen.</summary>
    public const string HeaderLimitRequests = "x-ratelimit-limit-requests";

    /// <summary>Name der Kopfzeile für die verbleibenden Anfragen.</summary>
    public const string HeaderRemainingRequests = "x-ratelimit-remaining-requests";

    /// <summary>Name der Kopfzeile für die Zeit bis zum Zurücksetzen.</summary>
    public const string HeaderResetRequests = "x-ratelimit-reset-requests";

    /// <summary>Name der Kopfzeile für die Wartezeit.</summary>
    public const string HeaderRetryAfter = "retry-after";

    private const double MaxSeconds = 1_000_000_000;

    /// <summary>
    /// Liest die Angaben über eine Funktion, die zu einem Kopfzeilennamen den Wert oder null liefert.
    /// Ist keine der vier Kopfzeilen auswertbar, kommt null zurück.
    /// </summary>
    public static RateLimitInfo? FromHeaders(Func<string, string?> getHeader)
    {
        ArgumentNullException.ThrowIfNull(getHeader);

        int? limit = ParseCount(getHeader(HeaderLimitRequests));
        int? remaining = ParseCount(getHeader(HeaderRemainingRequests));
        TimeSpan? reset = ParseDuration(getHeader(HeaderResetRequests));
        TimeSpan? retryAfter = ParseDuration(getHeader(HeaderRetryAfter));

        if (limit is null && remaining is null && reset is null && retryAfter is null) return null;
        return new RateLimitInfo(limit, remaining, reset, retryAfter);
    }

    /// <summary>
    /// Liest eine Dauer. Erlaubt sind eine reine Zahl in Sekunden ("7", "7.5") und die Schreibweise aus Zahl und Einheit
    /// mit h, m, s und ms, auch zusammengesetzt ("2m59.56s", "1h0m0s", "500ms"). Leer, negativ oder unlesbar ergibt null.
    /// </summary>
    public static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string s = value.Trim();

        if (double.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double plain))
        {
            return ToTimeSpan(plain);
        }

        double totalSeconds = 0;
        int i = 0;
        bool any = false;

        while (i < s.Length)
        {
            int numberStart = i;
            while (i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] == '.')) i++;
            if (i == numberStart) return null;

            if (!double.TryParse(s.AsSpan(numberStart, i - numberStart), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double number))
            {
                return null;
            }

            int unitStart = i;
            while (i < s.Length && char.IsAsciiLetter(s[i])) i++;

            double factor = s[unitStart..i] switch
            {
                "h" => 3600,
                "m" => 60,
                "s" => 1,
                "ms" => 0.001,
                _ => -1,
            };

            if (factor < 0) return null;

            totalSeconds += number * factor;
            any = true;
        }

        return any ? ToTimeSpan(totalSeconds) : null;
    }

    private static int? ParseCount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : null;
    }

    private static TimeSpan? ToTimeSpan(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0 || seconds > MaxSeconds) return null;
        return TimeSpan.FromSeconds(seconds);
    }
}
