using System.Text;

namespace Talk.Core.Insertion;

/// <summary>
/// Die Regeln für den Text beim Einfügen (Playbook Abschnitt 17).
/// </summary>
public static class InsertText
{
    /// <summary>
    /// False bei null, leerem Text und Text nur aus Leerraum. Solcher Text wird nie eingefügt und kommt nicht in den Verlauf.
    /// </summary>
    public static bool IsInsertable(string? text) => !string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Text für den Weg über die Zwischenablage: unverändert, mit einem Leerzeichen am Ende, wenn er nicht schon mit Leerraum endet.
    /// Nicht einfügbarer Text ergibt einen leeren Text.
    /// </summary>
    public static string ForClipboard(string? text)
    {
        if (!IsInsertable(text)) return string.Empty;
        return WithTrailingSpace(text!);
    }

    /// <summary>
    /// Text fürs Tippen: Jede Folge von Zeilenumbrüchen und anderen Steuerzeichen (auch Tabulator, U+0085, U+2028, U+2029) wird zu einem einzigen Leerzeichen, danach gilt die Regel für das Leerzeichen am Ende.
    /// Nicht einfügbarer Text ergibt einen leeren Text.
    /// </summary>
    public static string ForTyping(string? text)
    {
        if (!IsInsertable(text)) return string.Empty;

        StringBuilder builder = new(text!.Length + 1);
        bool inBreak = false;
        foreach (char c in text)
        {
            if (char.IsControl(c) || c is '\u2028' or '\u2029')
            {
                if (!inBreak) builder.Append(' ');
                inBreak = true;
            }
            else
            {
                builder.Append(c);
                inBreak = false;
            }
        }

        return WithTrailingSpace(builder.ToString());
    }

    /// <summary>
    /// Text fürs Terminal: Befehl statt Satz. Satzzeichen am Textende entfallen, das erste Wort (bis zum ersten Leerraum) beginnt klein,
    /// wenn nur sein erster Buchstabe groß ist. Danach gelten die Regeln fürs Tippen. Bleibt nichts übrig, ist das Ergebnis leer.
    /// </summary>
    public static string ForTerminal(string? text)
    {
        if (!IsInsertable(text)) return string.Empty;

        // Satzzeichen und Leerraum am Textende entfallen, auch im Wechsel ("git status . !").
        string trimmed = text!.Trim().TrimEnd('.', '!', '?', '\u2026', ' ', '\t', '\r', '\n');
        if (trimmed.Length == 0) return string.Empty;

        // Das erste Wort reicht bis zum ersten Leerraum. Klein wird es nur, wenn außer dem ersten Buchstaben keiner groß ist.
        int wordEnd = 0;
        while (wordEnd < trimmed.Length && !char.IsWhiteSpace(trimmed[wordEnd])) wordEnd++;

        bool onlyFirstUpper = wordEnd > 0 && char.IsUpper(trimmed[0]);
        for (int i = 1; i < wordEnd && onlyFirstUpper; i++)
        {
            if (char.IsUpper(trimmed[i])) onlyFirstUpper = false;
        }

        if (onlyFirstUpper) trimmed = char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
        return ForTyping(trimmed);
    }

    /// <summary>
    /// Wörter, die mitten im Satz sicher klein stehen, aus Deutsch, Englisch und Italienisch (Playbook Abschnitt 24, Regel 5).
    /// Die Höflichkeitsform ("Sie", "Ihr", "Ihnen", "Ihre") und "I" fehlen bewusst.
    /// </summary>
    public static IReadOnlyCollection<string> MidSentenceWords => MidSentence;

    private static readonly HashSet<string> MidSentence = new(StringComparer.OrdinalIgnoreCase)
    {
        "aber", "als", "also", "am", "an", "auch", "auf", "aus", "bei", "beim", "bin", "bis", "bist", "da", "damit", "dann", "das",
        "dass", "dem", "den", "denn", "der", "des", "dich", "die", "dir", "doch", "du", "durch", "ein", "eine", "einem", "einen",
        "einer", "eines", "er", "es", "für", "gerade", "habe", "haben", "hat", "hatte", "hier", "ich", "im", "immer", "in", "ins",
        "ist", "ja", "jetzt", "kann", "kein", "keine", "man", "mal", "mehr", "mich", "mir", "mit", "muss", "nach", "nein", "nicht",
        "noch", "nur", "ob", "oder", "ohne", "schon", "sehr", "sein", "seine", "sind", "so", "sondern", "um", "und", "uns", "unter",
        "vom", "von", "vor", "war", "waren", "was", "weil", "wenn", "wer", "wie", "wir", "wird", "wo", "zu", "zum", "zur", "über",
        "a", "and", "are", "as", "at", "be", "because", "but", "by", "for", "from", "had", "has", "have", "he", "her", "his", "if",
        "is", "it", "its", "my", "not", "of", "on", "or", "our", "she", "that", "the", "their", "then", "there", "they", "this", "to",
        "was", "we", "were", "what", "when", "which", "who", "will", "with", "would", "you", "your",
        "al", "alla", "anche", "che", "come", "con", "del", "della", "di", "e", "è", "gli", "ho", "il", "la", "le", "lo", "ma", "mi",
        "nel", "nella", "non", "o", "per", "perché", "più", "se", "si", "sono", "su", "ti", "tra", "un", "una", "uno",
    };

    // Nach diesen Zeichen kommt vorn ein Leerzeichen dazu (Regel 3), zusätzlich nach Buchstaben und Ziffern.
    private const string SpaceAfter = ".,;:!?…)]}%”";

    /// <summary>
    /// Passt den Anfang des Texts an die Zeichen vor dem Cursor an (Playbook Abschnitt 24, Regeln 1 bis 6): ein Leerzeichen vorn,
    /// wenn der Text sonst am vorigen Wort klebte, und ein kleiner erster Buchstabe, wenn mitten im Satz ein Wort aus
    /// <see cref="MidSentenceWords"/> folgt. Ist <paramref name="before"/> null oder leer, bleibt der Text unverändert.
    /// Mit <paramref name="startRaised"/> wird mitten im Satz jedes erste Wort klein, bei dem nur der erste Buchstabe groß ist,
    /// auch außerhalb der Liste: Den Großbuchstaben hat dann erst die Satzregel gesetzt (Playbook Abschnitt 28).
    /// </summary>
    public static string AdaptToContext(string? text, string? before, bool startRaised = false)
    {
        if (!IsInsertable(text)) return text ?? string.Empty;
        if (string.IsNullOrEmpty(before)) return text!;

        int visible = before.Length - 1;
        while (visible >= 0 && char.IsWhiteSpace(before[visible])) visible--;
        if (visible < 0) return text!;

        // Steht zwischen dem letzten sichtbaren Zeichen und dem Cursor ein Zeilenumbruch, beginnt eine neue Zeile (Regel 2).
        for (int i = visible + 1; i < before.Length; i++)
        {
            if (before[i] is '\n' or '\r' or '\u0085' or (char)0x2028 or (char)0x2029) return text!;
        }

        string result = text!;
        char lastVisible = before[visible];
        if (char.IsLetterOrDigit(lastVisible) || lastVisible is ',' or ';') result = LowerFirstWord(result, anyWord: startRaised);

        char last = before[^1];
        if (!char.IsWhiteSpace(result[0]) && (char.IsLetterOrDigit(last) || SpaceAfter.Contains(last))) result = " " + result;

        return result;
    }

    // Regel 4: erstes Wort aus der Liste (mit anyWord jedes Wort), nur der erste Buchstabe groß, dann wird er klein.
    private static string LowerFirstWord(string text, bool anyWord)
    {
        int start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;

        int end = start;
        while (end < text.Length && char.IsLetter(text[end])) end++;
        if (end == start) return text;

        string word = text[start..end];
        if (!char.IsUpper(word[0]) || (!anyWord && !MidSentence.Contains(word))) return text;
        for (int i = 1; i < word.Length; i++)
        {
            if (char.IsUpper(word[i])) return text;
        }

        return string.Concat(text.AsSpan(0, start), char.ToLowerInvariant(word[0]).ToString(), text.AsSpan(start + 1));
    }

    private static string WithTrailingSpace(string text) => char.IsWhiteSpace(text[^1]) ? text : text + " ";
}
