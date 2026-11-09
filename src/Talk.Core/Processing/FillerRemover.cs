// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Processing;

/// <summary>
/// Entfernt Füllwörter aus diktiertem Text und räumt die Satzzeichen auf, die nur wegen des Füllworts dastanden.
/// Reine Rechnung auf dem Text, ohne Netz.
/// </summary>
/// <remarks>
/// Regeln:
/// 1. Zögerlaute (<see cref="FillerWords.Hesitations"/>) und die Zusatzwörter des Nutzers entfallen an jeder Stelle, als ganze
///    Wörter, ohne Groß und Klein.
/// 2. Einstiege aus <see cref="FillerWords.StartFillersAlways"/> entfallen am Satzanfang: mit folgendem Komma immer; ohne Komma
///    nur, wenn das Wort groß geschrieben ist oder ganz am Anfang des Texts steht.
/// 3. Einstiege aus beiden Listen entfallen mit folgendem Komma am Satzanfang und als Einschub zwischen zwei Kommas.
/// 4. Ein Einstieg, auf den nur noch das Satzende folgt ("Also."), bleibt stehen.
/// 5. Satzanfang ist der Anfang des Texts und die Stelle nach Punkt, Ausrufezeichen, Fragezeichen oder Auslassungszeichen;
///    öffnende Anführungszeichen und Klammern dazwischen zählen nicht.
/// 6. Satzzeichen: Steht das Füllwort zwischen zwei Kommas, entfallen beide Kommas. Steht davor ein Komma und danach das
///    Satzende, entfällt das Komma davor. Folgt nur ein Komma, entfällt dieses. Hängen Auslassungspunkte direkt am Füllwort,
///    entfallen sie am Satzanfang mit; mitten im Satz bleiben sie und rücken an das Wort davor.
/// 7. Nach einem entfernten Füllwort am Satzanfang beginnt das nächste Wort groß.
/// 8. Am Ende wird der Leerraum aufgeräumt (<see cref="TextCleanup.Clean"/>); bleibt kein Buchstabe und keine Ziffer, ist das Ergebnis leer.
/// </remarks>
public static class FillerRemover
{
    private const string SentenceEnders = ".!?…";
    private const string OpeningMarks = "\"„“«‹(‚'";

    /// <summary>Entfernt die Füllwörter der Sprache und die Zusatzwörter aus dem Text.</summary>
    /// <param name="text">Der Text. Null oder nur Leerraum ergibt leer.</param>
    /// <param name="language">Sprache ("de", "en", "it" oder ein Name); unbekannt entfernt nur die gemeinsamen Zögerlaute.</param>
    /// <param name="extraFillers">Zusatzwörter oder Wortfolgen des Nutzers, die an jeder Stelle entfallen, oder null.</param>
    public static string Remove(string? text, string? language = "de", IReadOnlyList<string>? extraFillers = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        List<string[]> anywhere = ToPhrases(FillerWords.Hesitations(language), extraFillers);
        List<string[]> startAlways = ToPhrases(FillerWords.StartFillersAlways(language), null);
        List<string[]> startWithComma = ToPhrases(FillerWords.StartFillersWithComma(language), null);

        List<TextToken> input = TextTokenizer.Tokenize(text);
        List<TextToken> output = new(input.Count);
        bool capitalizeNext = false;
        int i = 0;

        while (i < input.Count)
        {
            TextToken token = input[i];

            if (!token.IsWord)
            {
                output.Add(token);
                i++;
                continue;
            }

            bool sentenceStart = IsSentenceStart(output);
            int end = MatchFiller(input, i, output, sentenceStart, anywhere, startAlways, startWithComma);

            if (end < 0)
            {
                output.Add(capitalizeNext ? Capitalize(token) : token);
                capitalizeNext = false;
                i++;
                continue;
            }

            i = RemoveSpan(input, end, output, sentenceStart, ref capitalizeNext);
        }

        return TextCleanup.Clean(TextTokenizer.Join(output));
    }

    // Liefert den Index hinter dem Füllwort (oder der Wortfolge) ab Position i, sonst minus 1.
    private static int MatchFiller(
        List<TextToken> input,
        int i,
        List<TextToken> output,
        bool sentenceStart,
        List<string[]> anywhere,
        List<string[]> startAlways,
        List<string[]> startWithComma)
    {
        foreach (string[] phrase in anywhere)
        {
            int end = MatchPhrase(input, i, phrase);
            if (end > 0 && !IsProtected(input, i, end, output, sentenceStart)) return end;
        }

        int prev = LastNonSpaceIndex(output);
        bool prevIsComma = prev >= 0 && output[prev].IsPunct(',');
        bool textStart = prev < 0;

        foreach (string[] phrase in startAlways)
        {
            int end = MatchPhrase(input, i, phrase);
            if (end > 0 && Qualifies(input, i, end, sentenceStart, textStart, prevIsComma, always: true)) return end;
        }

        foreach (string[] phrase in startWithComma)
        {
            int end = MatchPhrase(input, i, phrase);
            if (end > 0 && Qualifies(input, i, end, sentenceStart, textStart, prevIsComma, always: false)) return end;
        }

        return -1;
    }

    // Ein Zögerlaut bleibt stehen, wenn er erkennbar etwas anderes ist: Teil einer Doppelform mit Bindestrich ("Uh-huh")
    // oder "hm" als Einheit hinter einer Zahl ("800 hm").
    private static bool IsProtected(List<TextToken> input, int start, int end, List<TextToken> output, bool sentenceStart)
    {
        if (start > 0 && input[start - 1].IsPunct('-')) return true;
        if (end < input.Count && input[end].IsPunct('-')) return true;
        if (end - start != 1) return false;

        // Nur "hm" ist auch eine Einheit (Höhenmeter). Alle anderen Zögerlaute entfallen auch hinter einer Zahl.
        if (!string.Equals(input[start].Text, "hm", StringComparison.OrdinalIgnoreCase)) return false;

        int prev = LastNonSpaceIndex(output);
        return prev >= 0 && output[prev].IsWord && output[prev].Text.All(char.IsDigit);
    }

    private static bool Qualifies(List<TextToken> input, int start, int end, bool sentenceStart, bool textStart, bool prevIsComma, bool always)
    {
        int next = SkipSpaces(input, end);

        // Regel 4: Folgt nur noch das Satzende, bleibt der Einstieg stehen.
        if (next >= input.Count || input[next].IsPunctIn(SentenceEnders)) return false;

        bool nextIsComma = input[next].IsPunct(',');
        if (nextIsComma) return sentenceStart || prevIsComma;

        return always && sentenceStart && (textStart || char.IsUpper(input[start].Text[0]));
    }

    // Entfernt das Füllwort und die Satzzeichen, die nur seinetwegen dastanden. Liefert den Index, an dem es weitergeht.
    private static int RemoveSpan(List<TextToken> input, int end, List<TextToken> output, bool sentenceStart, ref bool capitalizeNext)
    {
        int prev = LastNonSpaceIndex(output);
        bool prevIsComma = prev >= 0 && output[prev].IsPunct(',');

        int dotsEnd = EllipsisEnd(input, end);
        if (dotsEnd > 0)
        {
            if (sentenceStart)
            {
                capitalizeNext = true;
                return SkipSpaces(input, dotsEnd);
            }

            // Mitten im Satz bleiben die Punkte und rücken an das Wort davor.
            if (prevIsComma) output.RemoveRange(prev, output.Count - prev);
            else TrimTrailingSpaces(output);
            return end;
        }

        int next = SkipSpaces(input, end);
        bool nextIsComma = next < input.Count && input[next].IsPunct(',');
        bool nextIsEnd = next >= input.Count || input[next].IsPunctIn(SentenceEnders);

        if (prevIsComma && nextIsComma)
        {
            output.RemoveRange(prev, output.Count - prev);
            int resume = SkipSpaces(input, next + 1);
            if (output.Count > 0 && resume < input.Count) output.Add(new TextToken(TextTokenKind.Space, " "));
            return resume;
        }

        if (prevIsComma && nextIsEnd)
        {
            output.RemoveRange(prev, output.Count - prev);
            return next;
        }

        if (nextIsComma)
        {
            if (sentenceStart) capitalizeNext = true;
            return SkipSpaces(input, next + 1);
        }

        if (sentenceStart) capitalizeNext = true;

        // Stand das Füllwort als eigener Satz da ("Ähm. Ich komme."), geht sein Satzzeichen mit, sonst bliebe es verwaist stehen.
        // Das gilt nur, wenn hinter dem Satzzeichen Leerraum oder das Textende folgt. Ein Punkt direkt vor einem Wort gehört zu
        // diesem Wort ("Ähm .NET ist gut.").
        if (sentenceStart && nextIsEnd)
        {
            int after = next;
            while (after < input.Count && input[after].IsPunctIn(SentenceEnders)) after++;
            if (after >= input.Count || input[after].IsSpace) return SkipSpaces(input, after);
        }

        return next;
    }

    // Vergleicht die Wörter ab Position i mit der Wortfolge; zwischen den Wörtern steht genau ein Stück Leerraum.
    private static int MatchPhrase(List<TextToken> input, int i, string[] phrase)
    {
        int pos = i;

        for (int k = 0; k < phrase.Length; k++)
        {
            if (k > 0)
            {
                if (pos >= input.Count || !input[pos].IsSpace) return -1;
                pos++;
            }

            if (pos >= input.Count || !input[pos].IsWord) return -1;
            if (!string.Equals(input[pos].Text, phrase[k], StringComparison.OrdinalIgnoreCase)) return -1;
            pos++;
        }

        return pos;
    }

    private static List<string[]> ToPhrases(IReadOnlyList<string> words, IReadOnlyList<string>? extra)
    {
        List<string[]> phrases = [];

        foreach (string entry in extra is null ? words : [.. words, .. extra])
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            string[] parts = TextTokenizer.Words(entry);
            if (parts.Length > 0) phrases.Add(parts);
        }

        // Längere Wortfolgen zuerst, damit "na ja" vor einem einzelnen "na" greift.
        phrases.Sort((a, b) => b.Length.CompareTo(a.Length));
        return phrases;
    }

    private static bool IsSentenceStart(List<TextToken> output)
    {
        for (int i = output.Count - 1; i >= 0; i--)
        {
            TextToken token = output[i];
            if (token.IsSpace || token.IsPunctIn(OpeningMarks)) continue;
            return token.IsPunctIn(SentenceEnders);
        }

        return true;
    }

    private static int EllipsisEnd(List<TextToken> input, int index)
    {
        if (index < input.Count && input[index].IsPunct('…')) return index + 1;

        if (index + 2 < input.Count && input[index].IsPunct('.') && input[index + 1].IsPunct('.') && input[index + 2].IsPunct('.'))
        {
            int j = index + 3;
            while (j < input.Count && input[j].IsPunct('.')) j++;
            return j;
        }

        return -1;
    }

    private static int SkipSpaces(List<TextToken> tokens, int index)
    {
        while (index < tokens.Count && tokens[index].IsSpace) index++;
        return index;
    }

    private static int LastNonSpaceIndex(List<TextToken> tokens)
    {
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (!tokens[i].IsSpace) return i;
        }

        return -1;
    }

    private static void TrimTrailingSpaces(List<TextToken> tokens)
    {
        while (tokens.Count > 0 && tokens[^1].IsSpace) tokens.RemoveAt(tokens.Count - 1);
    }

    private static TextToken Capitalize(TextToken token)
    {
        if (token.Text.Length == 0 || !char.IsLower(token.Text[0])) return token;
        return token with { Text = char.ToUpperInvariant(token.Text[0]) + token.Text[1..] };
    }
}
