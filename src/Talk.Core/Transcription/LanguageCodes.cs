// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Transcription;

/// <summary>
/// Die Sprachkürzel von Talk, die Whisper nicht kennt, und was davon an den Erkenner geht (Playbook Abschnitt 26).
/// </summary>
public static class LanguageCodes
{
    /// <summary>Schweizerdeutsch gesprochen, geschrieben als Schweizer Hochdeutsch (nie ß, Helvetismen bleiben).</summary>
    public const string SwissStandard = "de-ch";

    /// <summary>Schweizerdeutsch gesprochen, geschrieben als Mundart (Versuch).</summary>
    public const string SwissDialect = "gsw";

    /// <summary>Das Kürzel, das Whisper für Schweizerdeutsch bekommt: Es kennt nur Deutsch.</summary>
    public const string WhisperGerman = "de";

    /// <summary>
    /// True für <see cref="SwissStandard"/> und <see cref="SwissDialect"/>, auch mit Dialekt ("gsw-be", siehe <see cref="DialectTag"/>),
    /// Groß und Klein egal.
    /// </summary>
    /// <param name="language">Sprachkürzel oder null.</param>
    public static bool IsSwiss(string? language)
    {
        string l = Clean(language);
        return l is SwissStandard || IsDialect(l);
    }

    /// <summary>True für Mundart, mit oder ohne Dialekt ("gsw", "gsw-zh").</summary>
    /// <param name="language">Sprachkürzel oder null.</param>
    public static bool IsDialect(string? language)
    {
        string l = Clean(language);
        return l == SwissDialect || l.StartsWith(SwissDialect + "-", StringComparison.Ordinal);
    }

    /// <summary>
    /// Das Kürzel für den Erkenner: Schweizerdeutsch und Mundart werden "de", alles andere bleibt (klein geschrieben, ohne Ränder).
    /// </summary>
    /// <param name="language">Sprachkürzel von Talk.</param>
    public static string ToWhisper(string? language) => IsSwiss(language) ? WhisperGerman : Clean(language);

    /// <summary>
    /// Mundart mit Dialekt als ein Kürzel für die Glättung, zum Beispiel "gsw-be". So braucht die Schnittstelle der Glättung kein
    /// eigenes Feld für den Dialekt.
    /// </summary>
    /// <param name="dialect">zh, be, bs oder mix; leer gilt als mix.</param>
    public static string DialectTag(string? dialect)
    {
        string d = Clean(dialect);
        return $"{SwissDialect}-{(d.Length == 0 ? "mix" : d)}";
    }

    /// <summary>Der Dialekt aus einem Kürzel wie "gsw-be"; ohne Dialekt oder bei einer anderen Sprache "mix".</summary>
    /// <param name="language">Sprachkürzel.</param>
    public static string DialectOf(string? language)
    {
        string l = Clean(language);
        return IsDialect(l) && l.Length > SwissDialect.Length + 1 ? l[(SwissDialect.Length + 1)..] : "mix";
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
