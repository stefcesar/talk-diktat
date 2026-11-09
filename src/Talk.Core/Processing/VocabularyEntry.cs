// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Processing;

/// <summary>
/// Ein Eintrag des Wörterbuchs: die richtige Schreibweise und optional gesprochene oder falsch erkannte Varianten.
/// </summary>
/// <param name="Term">Die richtige Schreibweise, zum Beispiel "LUMÍORA".</param>
/// <param name="Aliases">Varianten, die durch die richtige Schreibweise ersetzt werden, zum Beispiel "Lumi Ora".</param>
public sealed record VocabularyEntry(string Term, IReadOnlyList<string> Aliases)
{
    /// <summary>Trennt die Schreibweise von den Varianten.</summary>
    public const char AliasSeparator = '=';

    /// <summary>Trennt die Varianten voneinander.</summary>
    public const char AliasDelimiter = '|';

    /// <summary>
    /// Liest einen Eintrag der Form "Schreibweise" oder "Schreibweise = Variante 1 | Variante 2". Ränder werden von Leerraum
    /// befreit; leere Varianten, Doppelte (ohne Groß und Klein) und Varianten, die der Schreibweise gleichen, fallen weg.
    /// Ohne Schreibweise (null, leer, nur Leerraum, oder nichts vor dem Gleichheitszeichen) kommt null zurück.
    /// </summary>
    public static VocabularyEntry? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        int separator = line.IndexOf(AliasSeparator);
        string term = (separator < 0 ? line : line[..separator]).Trim();
        if (term.Length == 0) return null;

        List<string> aliases = [];
        if (separator >= 0)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { term };
            foreach (string part in line[(separator + 1)..].Split(AliasDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (seen.Add(part)) aliases.Add(part);
            }
        }

        return new VocabularyEntry(term, aliases);
    }

    /// <summary>
    /// Liest alle Einträge in Reihenfolge. Unlesbare Zeilen fallen weg; bei gleicher Schreibweise (verglichen über
    /// <see cref="VocabularyCorrector.Key"/>) gewinnt der erste Eintrag. Null ergibt eine leere Liste.
    /// </summary>
    public static IReadOnlyList<VocabularyEntry> ParseAll(IEnumerable<string>? lines)
    {
        if (lines is null) return [];

        List<VocabularyEntry> entries = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string line in lines)
        {
            VocabularyEntry? entry = Parse(line);
            if (entry is null) continue;

            string key = VocabularyCorrector.Key(entry.Term);
            if (key.Length == 0 || !seen.Add(key)) continue;

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Liest den Inhalt einer Wörterbuchdatei: eine Zeile je Eintrag, Leerzeilen und Zeilen, die mit "#" beginnen, fallen weg.
    /// Null oder leer ergibt eine leere Liste.
    /// </summary>
    public static IReadOnlyList<string> ReadLines(string? fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent)) return [];

        List<string> lines = [];
        foreach (string raw in fileContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!raw.StartsWith('#')) lines.Add(raw);
        }

        return lines;
    }

    /// <summary>Nur die richtigen Schreibweisen in Reihenfolge, zum Beispiel für den Hinweistext an den Erkenner.</summary>
    public static IReadOnlyList<string> Terms(IEnumerable<string>? lines) =>
        ParseAll(lines).Select(e => e.Term).ToArray();
}
