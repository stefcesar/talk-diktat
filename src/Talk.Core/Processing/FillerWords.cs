// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Processing;

/// <summary>
/// Die Füllwörter je Sprache. Drei Gruppen: Zögerlaute, die überall entfallen; Einstiege, die am Satzanfang immer entfallen;
/// Einstiege, die nur mit folgendem Komma entfallen. Die Listen sind bewusst kurz und sprachgetrennt, weil ein Zögerlaut der
/// einen Sprache in der anderen ein echtes Wort sein kann (englisch "um" und "er" sind im Deutschen Wörter).
/// </summary>
public static class FillerWords
{
    // In keiner der drei Sprachen ein Wort.
    private static readonly string[] CommonHesitations =
    [
        "äh", "ähh", "ähm", "ähhm", "öh", "öhm", "hm", "hmm", "hmmm", "mhm", "ehm", "uhm", "erm",
    ];

    private static readonly string[] EnglishHesitations = ["uh", "uhh", "um", "umm", "er"];
    private static readonly string[] ItalianHesitations = ["mh"];

    private static readonly string[] GermanStartAlways = ["also", "naja", "na ja", "tja"];
    private static readonly string[] GermanStartWithComma = ["so"];
    private static readonly string[] EnglishStartWithComma = ["well", "so"];
    private static readonly string[] ItalianStartWithComma = ["allora", "dunque"];

    /// <summary>
    /// Vereinheitlicht eine Sprachangabe auf "de", "en" oder "it". Angenommen werden Kürzel ("de", "de-DE"), englische und
    /// eigene Namen ("german", "deutsch", "english", "italian", "italiano"), Schweizerdeutsch und Mundart ("de-ch", "gsw") als "de".
    /// Alles andere, auch leer und "auto", ergibt leer.
    /// </summary>
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return string.Empty;
        string l = language.Trim().ToLowerInvariant();

        // Schweizerdeutsch ("de-ch") und Mundart ("gsw", "gsw-be"): Whisper liefert Hochdeutsch, also gelten die deutschen Listen.
        if (l == "german" || l.StartsWith("de", StringComparison.Ordinal) || l.StartsWith("gsw", StringComparison.Ordinal)) return "de";
        if (l.StartsWith("en", StringComparison.Ordinal)) return "en";
        if (l.StartsWith("it", StringComparison.Ordinal)) return "it";
        return string.Empty;
    }

    /// <summary>Zögerlaute, die an jeder Stelle entfallen. Für unbekannte Sprache nur die gemeinsamen.</summary>
    public static IReadOnlyList<string> Hesitations(string? language) => NormalizeLanguage(language) switch
    {
        "en" => [.. CommonHesitations, .. EnglishHesitations],
        "it" => [.. CommonHesitations, .. ItalianHesitations],
        _ => CommonHesitations,
    };

    /// <summary>Einstiege, die am Satzanfang auch ohne Komma entfallen. Nur für Deutsch.</summary>
    public static IReadOnlyList<string> StartFillersAlways(string? language) =>
        NormalizeLanguage(language) == "de" ? GermanStartAlways : [];

    /// <summary>Einstiege, die nur mit folgendem Komma entfallen.</summary>
    public static IReadOnlyList<string> StartFillersWithComma(string? language) => NormalizeLanguage(language) switch
    {
        "de" => GermanStartWithComma,
        "en" => EnglishStartWithComma,
        "it" => ItalianStartWithComma,
        _ => [],
    };
}
