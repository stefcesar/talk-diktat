using System.Globalization;
using System.Text;

using Talk.Core.Transcription;

namespace Talk.Core.Processing;

/// <summary>Ergebnis der Wörterbuchprüfung.</summary>
/// <param name="Text">Der Text mit den richtigen Schreibweisen.</param>
/// <param name="Replacements">Anzahl der Stellen, an denen sich der Text geändert hat.</param>
public sealed record VocabularyCorrectionResult(string Text, int Replacements);

/// <summary>
/// Prüft den erkannten Text gegen das Wörterbuch und setzt die richtige Schreibweise ein. Alles außerhalb der Treffer
/// bleibt Zeichen für Zeichen erhalten.
/// </summary>
/// <remarks>
/// Verglichen wird über einen Schlüssel je Wort (<see cref="Key"/>): Kleinbuchstaben, ohne Akzente und Umlautpunkte, "ß" als "ss".
/// Regeln je Wort im Text, in dieser Reihenfolge, der erste Treffer gilt:
/// 1. Wortfolge: Die Schlüssel der Wörter ab dieser Stelle entsprechen den Schlüsseln einer Schreibweise oder einer Variante.
///    Zwischen den Wörtern steht Leerraum, ein Bindestrich oder ein Punkt ohne Leerraum. Längere Folgen gewinnen vor kürzeren.
///    Für einzelne Wörter heißt das: gleiche Buchstaben, aber andere Großschreibung oder fehlende Akzente ("lumiora" wird "LUMÍORA").
/// 2. Getrennt geschrieben: Zwei oder drei Wörter (getrennt durch Leerraum oder Bindestrich), deren Schlüssel zusammengefügt
///    dem Schlüssel einer einzelnen Schreibweise mit mindestens 5 Zeichen entsprechen ("Lumi Ora" wird "LUMÍORA").
/// 3. Genitiv: Der Schlüssel ist der einer einzelnen Schreibweise (mindestens 4 Zeichen) plus "s"; eingesetzt wird die Schreibweise plus "s".
/// 4. Ähnlich (abschaltbar): Das Wort beginnt mit einem Großbuchstaben, sein Schlüssel hat mindestens 6 Zeichen und weicht von
///    einer einzelnen Schreibweise mit mindestens 7 Zeichen um genau ein Zeichen ab (ersetzt, zu viel oder zu wenig),
///    bei gleichem ersten und gleichem letzten Zeichen ("Cesareti" wird "Cesaretti", "Adriana" bleibt neben "Adriano" stehen).
/// 5. Sprache: In Englisch und Italienisch wird ein Treffer nicht eingesetzt, wenn er sich vom Text nur durch den großen
///    Anfangsbuchstaben unterscheidet ("il soffritto" bleibt klein, "lumiora" wird trotzdem "LUMÍORA").
/// 6. Im Italienischen und in Mundart ("gsw") ist Regel 4 aus.
/// Schreibweisen aus mehreren Teilen ohne Leerraum ("Za'atar", "Well-Aging", "Next.js") gelten auch zusammengeschrieben ("Zaatar").
/// Steht die richtige Schreibweise schon da, ändert sich nichts und es wird nichts gezählt.
/// Hinweis: Marken, die zugleich gewöhnliche Wörter sind, gehören nicht ins Wörterbuch, weil Regel 1 sie überall groß schreibt.
/// </remarks>
public static class VocabularyCorrector
{
    private const int SplitMinKeyLength = 5;
    private const int GenitiveMinKeyLength = 4;
    private const int FuzzyMinTermKeyLength = 7;
    private const int FuzzyMinWordKeyLength = 6;
    private const string InnerSeparators = "-.'’";

    /// <summary>
    /// Vergleichsschlüssel: Kleinbuchstaben, Akzente und Umlautpunkte entfernt ("Í" wird "i", "ä" wird "a"), "ß" wird "ss",
    /// nur Buchstaben und Ziffern bleiben. Null ergibt leer.
    /// </summary>
    public static string Key(string? word)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;

        string decomposed = word.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            if (c is 'ß' or 'ẞ')
            {
                sb.Append("ss");
                continue;
            }

            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>Wendet das Wörterbuch auf den Text an.</summary>
    /// <param name="text">Der Text. Null ergibt leer.</param>
    /// <param name="entries">Die Einträge; null oder leer lässt den Text unverändert.</param>
    /// <param name="fuzzy">Regel 4 (ähnliche Wörter) ein oder aus.</param>
    /// <param name="language">Sprache des Texts ("de", "en", "it" oder ein Name); steuert die Regeln 5 und 6. Null oder unbekannt gilt wie Deutsch.</param>
    public static VocabularyCorrectionResult Apply(string? text, IReadOnlyList<VocabularyEntry>? entries, bool fuzzy = true, string? language = null)
    {
        if (string.IsNullOrEmpty(text)) return new VocabularyCorrectionResult(string.Empty, 0);
        if (entries is null || entries.Count == 0) return new VocabularyCorrectionResult(text, 0);

        (List<Pattern> patterns, List<SingleTerm> singles) = BuildPatterns(entries);
        if (patterns.Count == 0) return new VocabularyCorrectionResult(text, 0);

        // Regel 5: In Englisch und Italienisch bleibt die Kleinschreibung des Texts stehen, wenn nur der Anfangsbuchstabe abweicht.
        // Regel 6: Im Italienischen ist Regel 4 (ähnliche Wörter) aus, weil dort viele Einträge gewöhnliche Wörter sind.
        string lang = FillerWords.NormalizeLanguage(language);
        bool keepForeignCase = lang is "en" or "it";
        fuzzy = fuzzy && lang != "it";

        // Mundart hat keine feste Schreibung: Ähnliche Wörter wären oft Mundartwörter, keine Fehler (Playbook Abschnitt 26).
        fuzzy = fuzzy && !LanguageCodes.IsDialect(language);

        List<TextToken> tokens = TextTokenizer.Tokenize(text);
        StringBuilder output = new(text.Length + 16);
        int replacements = 0;
        int i = 0;

        while (i < tokens.Count)
        {
            TextToken token = tokens[i];

            if (!token.IsWord)
            {
                output.Append(token.Text);
                i++;
                continue;
            }

            string key = Key(token.Text);
            string? replacement = null;
            int end = i + 1;

            if (TryMatchPattern(tokens, i, key, patterns, out int patternEnd, out string? term))
            {
                replacement = term;
                end = patternEnd;
            }
            else if (TryMatchSplit(tokens, i, key, singles, out int splitEnd, out term))
            {
                replacement = term;
                end = splitEnd;
            }
            else if (TryMatchGenitive(key, singles, out term))
            {
                replacement = term + "s";
            }
            else if (fuzzy && char.IsUpper(token.Text[0]) && TryMatchFuzzy(key, singles, out term))
            {
                replacement = term;
            }

            if (replacement is null)
            {
                output.Append(token.Text);
                i++;
                continue;
            }

            string original = TextTokenizer.Join(tokens, i, end);

            // Schreibweisen mit Sonderzeichen am Rand ("C#", ".NET", "C++"): Stehen diese Zeichen schon im Text, gehören sie zum
            // Treffer, sonst würden sie verdoppelt. Ein einzelner Buchstabe ohne sein Zeichen ("Vitamin C") ist kein Treffer.
            if (ReferenceEquals(replacement, term) && term is not null)
            {
                string prefix = EdgePrefix(term);
                string suffix = EdgeSuffix(term);
                bool hasPrefix = prefix.Length > 0 && EndsWith(output, prefix);
                bool hasSuffix = suffix.Length > 0 && FollowsWith(tokens, end, suffix);

                if ((prefix.Length > 0 || suffix.Length > 0) && key.Length == 1 && end == i + 1 && !hasPrefix && !hasSuffix)
                {
                    output.Append(token.Text);
                    i++;
                    continue;
                }

                if (hasPrefix)
                {
                    output.Length -= prefix.Length;
                    original = prefix + original;
                }

                if (hasSuffix)
                {
                    end += suffix.Length;
                    original += suffix;
                }
            }

            if (keepForeignCase && DiffersOnlyInFirstLetterCase(original, replacement))
            {
                output.Append(original);
                i = end;
                continue;
            }

            if (!string.Equals(original, replacement, StringComparison.Ordinal)) replacements++;

            output.Append(replacement);
            i = end;
        }

        return new VocabularyCorrectionResult(output.ToString(), replacements);
    }

    // Die Zeichen am Anfang der Schreibweise, die weder Buchstabe noch Ziffer noch Leerraum sind.
    private static string EdgePrefix(string term)
    {
        int n = 0;
        while (n < term.Length && !char.IsLetterOrDigit(term[n]) && !char.IsWhiteSpace(term[n])) n++;
        return n == term.Length ? string.Empty : term[..n];
    }

    // Die Zeichen am Ende der Schreibweise, die weder Buchstabe noch Ziffer noch Leerraum sind.
    private static string EdgeSuffix(string term)
    {
        int n = term.Length;
        while (n > 0 && !char.IsLetterOrDigit(term[n - 1]) && !char.IsWhiteSpace(term[n - 1])) n--;
        return n == 0 ? string.Empty : term[n..];
    }

    private static bool EndsWith(StringBuilder output, string value)
    {
        if (output.Length < value.Length) return false;
        for (int k = 0; k < value.Length; k++)
        {
            if (output[output.Length - value.Length + k] != value[k]) return false;
        }

        return true;
    }

    // True, wenn ab der Stelle genau diese Zeichen als einzelne Satzzeichen folgen.
    private static bool FollowsWith(List<TextToken> tokens, int index, string value)
    {
        for (int k = 0; k < value.Length; k++)
        {
            if (index + k >= tokens.Count || !tokens[index + k].IsPunct(value[k])) return false;
        }

        return true;
    }

    private static (List<Pattern> Patterns, List<SingleTerm> Singles) BuildPatterns(IReadOnlyList<VocabularyEntry> entries)
    {
        List<Pattern> patterns = [];
        List<SingleTerm> singles = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (VocabularyEntry entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Term)) continue;
            string term = entry.Term.Trim();

            string[] termKeys = WordKeys(term);
            if (termKeys.Length == 0) continue;

            if (seen.Add(string.Join(' ', termKeys)))
            {
                patterns.Add(new Pattern(termKeys, term));
                if (termKeys.Length == 1) singles.Add(new SingleTerm(termKeys[0], term));
            }

            // Schreibweisen aus mehreren Teilen ohne Leerraum ("Za'atar", "Well-Aging", "Next.js") gelten auch zusammengeschrieben.
            if (termKeys.Length > 1 && !term.Any(char.IsWhiteSpace))
            {
                string joined = string.Concat(termKeys);
                if (seen.Add(joined))
                {
                    patterns.Add(new Pattern([joined], term));
                    singles.Add(new SingleTerm(joined, term));
                }
            }

            foreach (string alias in entry.Aliases ?? [])
            {
                string[] aliasKeys = WordKeys(alias);
                if (aliasKeys.Length > 0 && seen.Add(string.Join(' ', aliasKeys))) patterns.Add(new Pattern(aliasKeys, term));
            }
        }

        // Längere Wortfolgen zuerst, bei gleicher Länge die mit mehr Zeichen.
        patterns.Sort((a, b) =>
        {
            int byWords = b.Keys.Length.CompareTo(a.Keys.Length);
            return byWords != 0 ? byWords : b.TotalLength.CompareTo(a.TotalLength);
        });

        return (patterns, singles);
    }

    private static string[] WordKeys(string text) =>
        TextTokenizer.Words(text).Select(Key).Where(k => k.Length > 0).ToArray();

    private static bool TryMatchPattern(List<TextToken> tokens, int i, string firstKey, List<Pattern> patterns, out int end, out string? term)
    {
        foreach (Pattern pattern in patterns)
        {
            if (!string.Equals(pattern.Keys[0], firstKey, StringComparison.Ordinal)) continue;

            int pos = i + 1;
            bool matched = true;

            for (int k = 1; k < pattern.Keys.Length; k++)
            {
                if (pos + 1 >= tokens.Count || !IsInnerSeparator(tokens[pos]) || !tokens[pos + 1].IsWord
                    || !string.Equals(Key(tokens[pos + 1].Text), pattern.Keys[k], StringComparison.Ordinal))
                {
                    matched = false;
                    break;
                }

                pos += 2;
            }

            if (matched)
            {
                end = pos;
                term = pattern.Term;
                return true;
            }
        }

        end = i + 1;
        term = null;
        return false;
    }

    private static bool TryMatchSplit(List<TextToken> tokens, int i, string firstKey, List<SingleTerm> singles, out int end, out string? term)
    {
        for (int words = 3; words >= 2; words--)
        {
            StringBuilder joined = new(firstKey);
            int pos = i + 1;
            bool complete = true;

            for (int k = 1; k < words; k++)
            {
                if (pos + 1 >= tokens.Count || !(tokens[pos].IsSpace || tokens[pos].IsPunct('-')) || !tokens[pos + 1].IsWord)
                {
                    complete = false;
                    break;
                }

                joined.Append(Key(tokens[pos + 1].Text));
                pos += 2;
            }

            if (!complete) continue;

            string candidate = joined.ToString();
            if (candidate.Length < SplitMinKeyLength) continue;

            foreach (SingleTerm single in singles)
            {
                if (string.Equals(single.Key, candidate, StringComparison.Ordinal))
                {
                    end = pos;
                    term = single.Term;
                    return true;
                }
            }
        }

        end = i + 1;
        term = null;
        return false;
    }

    private static bool TryMatchGenitive(string key, List<SingleTerm> singles, out string? term)
    {
        if (key.Length > GenitiveMinKeyLength && key[^1] == 's')
        {
            foreach (SingleTerm single in singles)
            {
                if (single.Key.Length >= GenitiveMinKeyLength
                    && key.Length == single.Key.Length + 1
                    && key.StartsWith(single.Key, StringComparison.Ordinal))
                {
                    term = single.Term;
                    return true;
                }
            }
        }

        term = null;
        return false;
    }

    private static bool TryMatchFuzzy(string key, List<SingleTerm> singles, out string? term)
    {
        if (key.Length >= FuzzyMinWordKeyLength)
        {
            foreach (SingleTerm single in singles)
            {
                // Anfang und Ende müssen gleich sein: Am Wortende sitzt die Grammatik ("Adriana" ist nicht "Adriano").
                if (single.Key.Length >= FuzzyMinTermKeyLength
                    && key[0] == single.Key[0]
                    && key[^1] == single.Key[^1]
                    && IsOneEdit(key, single.Key))
                {
                    term = single.Term;
                    return true;
                }
            }
        }

        term = null;
        return false;
    }

    // True, wenn sich die beiden Zeichenketten um genau ein Zeichen unterscheiden: eines ersetzt, eines zu viel oder eines zu wenig.
    private static bool IsOneEdit(string a, string b)
    {
        if (a.Length == b.Length)
        {
            int differences = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i] && ++differences > 1) return false;
            }

            return differences == 1;
        }

        string shorter = a.Length < b.Length ? a : b;
        string longer = a.Length < b.Length ? b : a;
        if (longer.Length - shorter.Length != 1) return false;

        int s = 0;
        int l = 0;
        bool skipped = false;

        while (s < shorter.Length && l < longer.Length)
        {
            if (shorter[s] == longer[l])
            {
                s++;
                l++;
                continue;
            }

            if (skipped) return false;
            skipped = true;
            l++;
        }

        return true;
    }

    private static bool IsInnerSeparator(TextToken token) => token.IsSpace || token.IsPunctIn(InnerSeparators);

    // True, wenn der Text klein beginnt, die Schreibweise groß, und sonst alles gleich ist ("soffritto" gegen "Soffritto").
    private static bool DiffersOnlyInFirstLetterCase(string original, string replacement)
    {
        return original.Length == replacement.Length
            && original.Length > 0
            && original[0] != replacement[0]
            && char.ToUpperInvariant(original[0]) == replacement[0]
            && original.AsSpan(1).SequenceEqual(replacement.AsSpan(1));
    }

    private sealed record Pattern(string[] Keys, string Term)
    {
        public int TotalLength { get; } = Keys.Sum(k => k.Length);
    }

    private readonly record struct SingleTerm(string Key, string Term);
}
