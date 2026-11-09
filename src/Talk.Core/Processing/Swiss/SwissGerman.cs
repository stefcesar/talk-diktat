// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Talk.Core.Transcription;

namespace Talk.Core.Processing.Swiss;

/// <summary>Ergebnis des Helvetismen Ersatzes.</summary>
/// <param name="Text">Der Text mit den Schweizer Wörtern.</param>
/// <param name="Replacements">Anzahl der ersetzten Wörter.</param>
public sealed record SwissCorrectionResult(string Text, int Replacements);

/// <summary>
/// Die festen Wörterbücher für Schweizerdeutsch und Mundart (Playbook Abschnitt 26). Sie liegen als Textdateien in der Bibliothek
/// (<c>Processing\Swiss\*.txt</c>) und werden einmal gelesen. Fehlt eine Datei, gilt sie als leer; es gibt nie eine Ausnahme.
/// </summary>
public static class SwissWordLists
{
    private const string ResourcePrefix = "Talk.Core.Swiss.";

    private static readonly Lazy<IReadOnlyList<string>> HelvetismLines = new(() => Read("helvetismen.txt"));

    private static readonly Dictionary<string, Lazy<IReadOnlyList<string>>> MundartLines = new(StringComparer.Ordinal)
    {
        ["zh"] = new(() => Read("mundart-zh.txt")),
        ["be"] = new(() => Read("mundart-be.txt")),
        ["bs"] = new(() => Read("mundart-bs.txt")),
        ["mix"] = new(() => Read("mundart-mix.txt")),
    };

    /// <summary>
    /// Wenige Schweizer Wörter für den Hinweis an den Erkenner, nach den eigenen Begriffen des Nutzers. Bewusst keine Grußformeln:
    /// Bei Stille gibt das Modell gelegentlich Wörter aus dem Hinweis als Text aus.
    /// </summary>
    public static IReadOnlyList<string> HintTerms { get; } = ["Velo", "Trottoir", "parkieren", "Spital", "Billett", "Perron", "Poulet", "Rüebli"];

    /// <summary>Die Zeilen der Helvetismen, im Format von <see cref="VocabularyEntry.Parse"/>: Schweizer Wort = Formen aus Deutschland.</summary>
    public static IReadOnlyList<string> Helvetisms => HelvetismLines.Value;

    /// <summary>Die Paare Mundart = Hochdeutsch des Dialekts; ein unbekannter Dialekt gilt als gemischt.</summary>
    /// <param name="dialect">zh, be, bs oder mix.</param>
    public static IReadOnlyList<string> Mundart(string? dialect) =>
        MundartLines.TryGetValue(NormalizeDialect(dialect), out Lazy<IReadOnlyList<string>>? lines) ? lines.Value : MundartLines["mix"].Value;

    /// <summary>Der Name des Dialekts für die Anweisung an das Sprachmodell.</summary>
    /// <param name="dialect">zh, be, bs oder mix.</param>
    public static string DialectName(string? dialect) => NormalizeDialect(dialect) switch
    {
        "zh" => "Zürichdeutsch",
        "be" => "Berndeutsch",
        "bs" => "Baseldeutsch",
        _ => "allgemein verständliche Deutschschweizer Mundart, nahe am Zürichdeutschen",
    };

    private static string NormalizeDialect(string? dialect)
    {
        string d = (dialect ?? string.Empty).Trim().ToLowerInvariant();
        return d is "zh" or "be" or "bs" ? d : "mix";
    }

    private static IReadOnlyList<string> Read(string fileName)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + fileName);
            if (stream is null) return [];
            using StreamReader reader = new(stream, Encoding.UTF8);
            return VocabularyEntry.ReadLines(reader.ReadToEnd());
        }
        catch
        {
            return [];
        }
    }
}

/// <summary>
/// Schweizer Schreibregeln, die ohne Sprachmodell sicher gelten (Playbook Abschnitt 26, Schritte 2 und 3).
/// </summary>
public static partial class SwissText
{
    /// <summary>
    /// Schweizer Schreibung: nie ß, immer ss; das große ẞ wird SS. Dazu werden der typografische und der geschützte Bindestrich
    /// (U+2010, U+2011), die das Sprachmodell gern setzt, zum normalen Bindestrich. Null ergibt leer.
    /// </summary>
    /// <param name="text">Der Text.</param>
    public static string Spelling(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("ß", "ss", StringComparison.Ordinal).Replace("ẞ", "SS", StringComparison.Ordinal).Replace('\u2010', '-').Replace('\u2011', '-');

    /// <summary>
    /// Ersetzt Wörter aus Deutschland durch das Schweizer Wort. Jede Zeile hat die Form "Schweizer Wort = Form | Form"; verglichen wird
    /// die genaue Wortform ohne Groß und Klein (Umlaute zählen), nur ganze Wörter aus Buchstaben, nie ein Teil eines Worts.
    /// Beginnt das Wort im Text groß, beginnt auch der Ersatz groß; ist es ganz groß geschrieben, wird der Ersatz ganz groß.
    /// Varianten aus mehreren Wörtern werden übergangen. Bei gleicher Variante gewinnt die erste Zeile. Alles außerhalb der Treffer bleibt.
    /// </summary>
    /// <param name="text">Der Text. Null ergibt leer.</param>
    /// <param name="lines">Die Zeilen, zum Beispiel <see cref="SwissWordLists.Helvetisms"/> und die eigene Liste des Nutzers.</param>
    public static SwissCorrectionResult Helvetisms(string? text, IEnumerable<string>? lines)
    {
        if (string.IsNullOrEmpty(text)) return new SwissCorrectionResult(string.Empty, 0);

        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (VocabularyEntry entry in VocabularyEntry.ParseAll(lines))
        {
            if (!IsSingleWord(entry.Term)) continue;
            foreach (string alias in entry.Aliases)
            {
                if (IsSingleWord(alias)) map.TryAdd(alias, entry.Term);
            }
        }

        if (map.Count == 0) return new SwissCorrectionResult(text, 0);

        int count = 0;
        string result = WordPattern().Replace(text, match =>
        {
            if (!map.TryGetValue(match.Value, out string? swiss)) return match.Value;
            count++;
            return MatchCase(match.Value, swiss);
        });

        return new SwissCorrectionResult(result, count);
    }

    /// <summary>True, wenn die Sprache Schweizer Regeln bekommt: Schweizerdeutsch oder Mundart, auch mit Dialekt ("gsw-be").</summary>
    /// <param name="language">Sprachkürzel.</param>
    public static bool Applies(string? language) => LanguageCodes.IsSwiss(language);

    private static bool IsSingleWord(string value) => value.Length > 0 && WordPattern().Match(value) is { Success: true } m && m.Length == value.Length;

    private static string MatchCase(string original, string replacement)
    {
        if (original.Length > 1 && original.All(c => !char.IsLetter(c) || char.IsUpper(c))) return replacement.ToUpperInvariant();
        if (char.IsUpper(original[0]) && char.IsLower(replacement[0])) return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        return replacement;
    }

    // Ein Wort aus Buchstaben und Akzentzeichen.
    [GeneratedRegex(@"[\p{L}\p{M}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
