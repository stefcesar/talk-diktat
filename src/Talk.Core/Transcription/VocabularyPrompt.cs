// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Transcription;

/// <summary>
/// Baut aus dem Wörterbuch den Hinweistext für den Erkenner. Der Erkenner nimmt nur einen kurzen Hinweis an
/// (bei Whisper 224 Tokens); deshalb wird nach Zeichen vorsichtig begrenzt und in der Reihenfolge der Begriffe gekürzt.
/// </summary>
public static class VocabularyPrompt
{
    /// <summary>Standardgrenze in Zeichen. Vorsichtig gewählt, weil Namen viele Tokens je Zeichen brauchen.</summary>
    public const int DefaultMaxChars = 400;

    private const string Separator = ", ";

    /// <summary>
    /// Verbindet die Begriffe mit Komma und Leerzeichen. Begriffe werden an den Rändern von Leerzeichen befreit, leere und
    /// doppelte (ohne Groß und Klein) fallen weg. Die Reihenfolge ist die Rangfolge: Beim ersten Begriff, der nicht mehr
    /// in die Grenze passt, endet der Hinweis. Ohne Begriffe kommt null zurück.
    /// </summary>
    public static string? Build(IReadOnlyList<string>? terms, int maxChars = DefaultMaxChars)
    {
        if (maxChars <= 0) throw new ArgumentOutOfRangeException(nameof(maxChars), "Die Grenze muss größer als 0 sein.");
        if (terms is null || terms.Count == 0) return null;

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        System.Text.StringBuilder prompt = new();

        foreach (string? raw in terms)
        {
            string term = raw?.Trim() ?? string.Empty;
            if (term.Length == 0 || !seen.Add(term)) continue;

            int needed = prompt.Length == 0 ? term.Length : Separator.Length + term.Length;
            if (prompt.Length + needed > maxChars) break;

            if (prompt.Length > 0) prompt.Append(Separator);
            prompt.Append(term);
        }

        return prompt.Length == 0 ? null : prompt.ToString();
    }

    /// <summary>
    /// Zerlegt eine Eingabe in Begriffe. Trennzeichen sind Komma, Semikolon und Zeilenumbruch. Ränder werden von Leerzeichen
    /// befreit, leere und doppelte Begriffe (ohne Groß und Klein) fallen weg, die Reihenfolge bleibt.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> terms = [];

        foreach (string part in input.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(part)) terms.Add(part);
        }

        return terms;
    }
}
