namespace Talk.Core.Processing;

/// <summary>
/// Schreibt den ersten Buchstaben jedes Satzes groß (Playbook Abschnitt 28). Ob der diktierte Text mitten in einen Satz kommt,
/// entscheidet erst das Einfügen (<c>InsertText.AdaptToContext</c>, Abschnitt 24); dort wird ein passendes Wort wieder klein.
/// </summary>
/// <remarks>
/// Regeln:
/// 1. Ein Satz beginnt am Anfang des Texts und nach einem Satzende: Punkt, Ausrufezeichen, Fragezeichen oder Auslassungszeichen (…),
///    gefolgt von Leerraum. Zwischen Satzende und Leerraum dürfen schließende Zeichen stehen (<c>" ” “ » « ' ’ ) ]</c>), nach dem
///    Leerraum öffnende (<c>" „ “ « » ' ‚ ( [</c>).
/// 2. Groß wird nur der erste Buchstabe des ersten Worts, und nur wenn er klein ist und das Wort sonst keinen Großbuchstaben hat
///    ("iPhone", "eBay" bleiben). Beginnt der Satz mit einer Ziffer oder einem anderen Zeichen als einem Buchstaben, bleibt er.
///    Eine Adresse bleibt auch: Das Wort bis zum nächsten Leerraum enthält "@" oder "://" oder beginnt mit "www.".
/// 3. Kein Satzende ist ein Punkt nach einer Abkürzung aus der Liste (<see cref="Abbreviations"/>, ohne Groß und Klein, der Teil
///    vor dem Punkt), nach einem einzelnen Buchstaben ("z. B.", "d. h.") und nach einer Zahl ("3. Mai").
/// 4. Alles andere bleibt Zeichen für Zeichen. Null ergibt leer.
/// </remarks>
public static class SentenceCase
{
    /// <summary>Abkürzungen, nach deren Punkt kein neuer Satz beginnt (Teil vor dem Punkt, ohne Groß und Klein).</summary>
    public static IReadOnlyCollection<string> Abbreviations { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ca", "bzw", "usw", "etc", "vgl", "ggf", "evtl", "inkl", "zzgl", "exkl", "nr", "dr", "prof", "hr", "fr", "st", "str",
        "tel", "abs", "art", "bd", "kap", "s", "sog", "u", "o", "z", "d", "v", "j", "mio", "mrd", "min", "max", "std", "jh",
        "jhd", "chr", "geb", "gest", "verh", "bspw", "allg", "ehem", "mind", "rd", "tsd", "lt", "gem", "ff", "vs", "mr", "mrs",
        "ms", "e.g", "i.e", "sig", "dott", "ing", "avv", "pag",
    };

    private const string ClosingAfterEnd = "\"”“»«'’)]";
    private const string OpeningBeforeStart = "\"„“«»'‚([";

    /// <summary>Wendet die Regeln 1 bis 4 an.</summary>
    /// <param name="text">Der Text.</param>
    public static string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        char[] chars = text.ToCharArray();
        CapitalizeWordAt(chars, 0);

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is not ('.' or '!' or '?' or '…')) continue;
            if (chars[i] == '.' && !EndsSentence(chars, i)) continue;

            int j = i + 1;
            while (j < chars.Length && ClosingAfterEnd.Contains(chars[j])) j++;
            if (j >= chars.Length || !char.IsWhiteSpace(chars[j])) continue;

            while (j < chars.Length && char.IsWhiteSpace(chars[j])) j++;
            CapitalizeWordAt(chars, j);
        }

        return new string(chars);
    }

    // Ab start: öffnende Zeichen überspringen, dann den ersten Buchstaben groß, wenn Regel 2 es erlaubt.
    private static void CapitalizeWordAt(char[] chars, int start)
    {
        int i = start;
        while (i < chars.Length && (char.IsWhiteSpace(chars[i]) || OpeningBeforeStart.Contains(chars[i]))) i++;
        if (i >= chars.Length || !char.IsLetter(chars[i]) || !char.IsLower(chars[i])) return;

        for (int k = i + 1; k < chars.Length && char.IsLetter(chars[k]); k++)
        {
            if (char.IsUpper(chars[k])) return;
        }

        int end = i;
        while (end < chars.Length && !char.IsWhiteSpace(chars[end])) end++;
        string token = new(chars, i, end - i);
        if (token.Contains('@') || token.Contains("://", StringComparison.Ordinal) || token.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return;

        chars[i] = char.ToUpperInvariant(chars[i]);
    }

    /// <summary>
    /// True, wenn das erste Wort von <paramref name="final"/> groß beginnt, in <paramref name="original"/> aber an seiner ersten Stelle
    /// klein steht: Die Großschreibung am Anfang kommt dann erst von Talk. Gesucht wird das ganze Wort ohne Groß und Klein, weil
    /// die Nachbearbeitung davor Füllwörter streichen kann ("äh gestern" wird "Gestern"). Öffnende Zeichen und Leerraum zählen nicht.
    /// Das Einfügen macht das Wort mitten im Satz dann wieder klein (<c>InsertText.AdaptToContext</c>).
    /// </summary>
    /// <param name="original">Der Text der Erkennung.</param>
    /// <param name="final">Der fertige Text.</param>
    public static bool RaisedStart(string? original, string? final)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(final)) return false;

        int i = 0;
        while (i < final.Length && (char.IsWhiteSpace(final[i]) || OpeningBeforeStart.Contains(final[i]))) i++;
        int end = i;
        while (end < final.Length && char.IsLetter(final[end])) end++;
        if (end == i || !char.IsUpper(final[i])) return false;
        string word = final[i..end];

        for (int at = original.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = original.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            bool whole = (at == 0 || !char.IsLetter(original[at - 1])) && (at + word.Length == original.Length || !char.IsLetter(original[at + word.Length]));
            if (whole) return char.IsLower(original[at]);
        }

        return false;
    }

    // Regel 3: Ein Punkt beendet den Satz nicht nach einer Abkürzung, einem einzelnen Buchstaben oder einer Zahl.
    private static bool EndsSentence(char[] chars, int dot)
    {
        int k = dot - 1;
        while (k >= 0 && (char.IsLetterOrDigit(chars[k]) || chars[k] == '.')) k--;
        string before = new string(chars, k + 1, dot - k - 1);
        if (before.Length == 0) return true;
        if (before.All(char.IsDigit)) return false;

        string lastWord = before.Contains('.') ? before[(before.LastIndexOf('.') + 1)..] : before;
        if (lastWord.Length == 1 && char.IsLetter(lastWord[0])) return false;
        return !Abbreviations.Contains(before) && !Abbreviations.Contains(lastWord);
    }
}
