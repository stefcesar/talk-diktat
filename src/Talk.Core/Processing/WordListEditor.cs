namespace Talk.Core.Processing;

/// <summary>Was eine Änderung an einer Wörterliste bewirkt hat.</summary>
public enum WordListChange
{
    /// <summary>Eine neue Zeile steht am Ende der Liste.</summary>
    Added,

    /// <summary>Eine vorhandene Zeile hat neue Varianten bekommen.</summary>
    Extended,

    /// <summary>Wort und Varianten standen schon in der Liste, nichts geändert.</summary>
    Unchanged,

    /// <summary>Die Zeile des Worts ist entfernt.</summary>
    Removed,

    /// <summary>Das Wort steht nicht in der Liste, nichts geändert.</summary>
    NotFound,

    /// <summary>Die Eingabe ist ungültig, nichts geändert.</summary>
    Invalid
}

/// <summary>Ergebnis einer Änderung an einer Wörterliste.</summary>
/// <param name="Content">Der neue Inhalt der Datei; bei jeder Änderung ohne Wirkung der alte, Zeichen für Zeichen.</param>
/// <param name="Change">Was geschehen ist.</param>
/// <param name="Problem">Bei <see cref="WordListChange.Invalid"/> der Grund als kurzer deutscher Satz, sonst null.</param>
/// <param name="StoredTerm">
/// Bei <see cref="WordListChange.Extended"/> und <see cref="WordListChange.Unchanged"/> die Schreibweise, die schon in der Liste stand
/// (sie kann sich in Akzenten oder Groß und Klein von der Eingabe unterscheiden), sonst null.
/// </param>
public sealed record WordListEditResult(string Content, WordListChange Change, string? Problem = null, string? StoredTerm = null)
{
    /// <summary>True, wenn sich der Inhalt geändert hat und die Datei geschrieben werden muss.</summary>
    public bool Changed => Change is WordListChange.Added or WordListChange.Extended or WordListChange.Removed;
}

/// <summary>
/// Ändert den Inhalt einer Wörterliste (<c>woerterbuch.txt</c> und Geschwister) so, wie es die App aus den Einstellungen und dem
/// Verlauf tut (Playbook Abschnitt 27). Reine Funktionen auf dem Text; Lesen und Schreiben der Datei macht die App.
/// </summary>
/// <remarks>
/// Regeln:
/// 1. Alles, was die Änderung nicht betrifft (andere Einträge, Kommentare, Leerzeilen, Zeilenenden), bleibt Zeichen für Zeichen.
/// 2. Ob ein Wort schon in der Liste steht, entscheidet der Vergleichsschlüssel <see cref="VocabularyCorrector.Key"/> (ohne Groß und
///    Klein, ohne Akzente). Steht es schon da, bleibt seine Schreibweise; nur fehlende Varianten kommen an genau diese Zeile.
/// 3. Ein neues Wort kommt als eigene Zeile ans Ende, im Format "Wort" oder "Wort = Variante | Variante". Die Zeilenenden folgen der
///    Datei (CRLF, wenn sie CRLF hat, sonst LF; eine leere Datei bekommt CRLF). Fehlt am Ende der Datei der Zeilenumbruch, kommt er vorher dazu.
/// 4. Entfernen nimmt nur die Zeilen, deren Schreibweise denselben Schlüssel hat, samt ihrem Zeilenende.
/// 5. Eingaben werden an den Rändern von Leerraum befreit. Ungültig ist: leeres Wort; ein Wort oder eine Variante mit "=", "|" oder
///    Zeilenumbruch, mit "#" am Anfang oder länger als <see cref="MaxLength"/> Zeichen; ein Wort ohne Buchstaben oder Ziffern.
///    Varianten, die leer sind oder denselben Schlüssel wie das Wort haben, fallen still weg, doppelte ebenso.
/// </remarks>
public static class WordListEditor
{
    /// <summary>Höchstlänge eines Worts oder einer Variante.</summary>
    public const int MaxLength = 80;

    /// <summary>Kopfzeile einer Datei, die die App neu anlegt.</summary>
    public const string NewFileHeader = "# Talk Wörterbuch. Eine Zeile je Eintrag: Wort oder Wort = Variante | Variante";

    /// <summary>Die Einträge der Liste in Reihenfolge, ohne Kommentare und Leerzeilen. Null oder leer ergibt eine leere Liste.</summary>
    /// <param name="content">Der Inhalt der Datei.</param>
    public static IReadOnlyList<VocabularyEntry> Entries(string? content) => VocabularyEntry.ParseAll(VocabularyEntry.ReadLines(content));

    /// <summary>Trennt eine Eingabe mit mehreren Varianten an Kommas; Ränder ohne Leerraum, leere Teile fallen weg.</summary>
    /// <param name="text">Die Eingabe, zum Beispiel "Cesareti, Cäsaretti".</param>
    public static IReadOnlyList<string> SplitVariants(string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Prüft Wort und Varianten nach Regel 5. Null, wenn alles gültig ist, sonst der Grund als kurzer deutscher Satz.
    /// </summary>
    /// <param name="term">Das richtige Wort.</param>
    /// <param name="variants">Die Varianten oder null.</param>
    public static string? Validate(string? term, IEnumerable<string>? variants)
    {
        string t = (term ?? string.Empty).Trim();
        if (t.Length == 0) return "Bitte ein Wort eintragen.";

        string? problem = CheckPart(t, "Das Wort");
        if (problem is not null) return problem;
        if (VocabularyCorrector.Key(t).Length == 0) return "Das Wort braucht mindestens einen Buchstaben oder eine Ziffer.";

        foreach (string variant in variants ?? [])
        {
            string v = (variant ?? string.Empty).Trim();
            if (v.Length == 0) continue;
            problem = CheckPart(v, "Eine Variante");
            if (problem is not null) return problem;
        }

        return null;
    }

    /// <summary>
    /// Ein Hinweis, wenn das Wort ein gewöhnliches kleingeschriebenes Wort ist (nur Kleinbuchstaben, ein Wort): Das Wörterbuch setzt
    /// seine Schreibweise überall ein. Sonst null. Kein Verbot.
    /// </summary>
    /// <param name="term">Das Wort.</param>
    public static string? Hint(string? term)
    {
        string t = (term ?? string.Empty).Trim();
        bool plainLower = t.Length > 0 && t.All(char.IsLetter) && t.All(char.IsLower);
        return plainLower
            ? "Hinweis: Talk schreibt dieses Wort ab jetzt überall genau so. Für gewöhnliche Wörter ist das Wörterbuch nicht gedacht."
            : null;
    }

    /// <summary>Fügt ein Wort mit Varianten hinzu oder ergänzt seine Zeile (Regeln 1 bis 3 und 5).</summary>
    /// <param name="content">Der bisherige Inhalt der Datei; null gilt als leer.</param>
    /// <param name="term">Das richtige Wort.</param>
    /// <param name="variants">Die falschen Schreibweisen oder null.</param>
    public static WordListEditResult Add(string? content, string? term, IEnumerable<string>? variants)
    {
        string text = content ?? string.Empty;
        List<string> variantList = (variants ?? []).Select(v => (v ?? string.Empty).Trim()).ToList();
        string? problem = Validate(term, variantList);
        if (problem is not null) return new WordListEditResult(text, WordListChange.Invalid, problem);

        string t = term!.Trim();
        string key = VocabularyCorrector.Key(t);
        List<string> wanted = CleanVariants(variantList, key);

        List<(string Body, string Ending)> lines = SplitLines(text);
        for (int i = 0; i < lines.Count; i++)
        {
            VocabularyEntry? entry = ParseLine(lines[i].Body);
            if (entry is null || VocabularyCorrector.Key(entry.Term) != key) continue;

            HashSet<string> known = new(entry.Aliases.Select(VocabularyCorrector.Key), StringComparer.Ordinal);
            List<string> missing = wanted.Where(v => !known.Contains(VocabularyCorrector.Key(v))).ToList();
            if (missing.Count == 0) return new WordListEditResult(text, WordListChange.Unchanged, StoredTerm: entry.Term);

            string body = lines[i].Body.TrimEnd();
            string joined = string.Join(" | ", missing);
            body += body.Contains(VocabularyEntry.AliasSeparator) ? $" | {joined}" : $" = {joined}";
            lines[i] = (body, lines[i].Ending);
            return new WordListEditResult(Join(lines), WordListChange.Extended, StoredTerm: entry.Term);
        }

        string newline = text.Contains("\r\n", StringComparison.Ordinal) || !text.Contains('\n') ? "\r\n" : "\n";
        string line = wanted.Count == 0 ? t : $"{t} = {string.Join(" | ", wanted)}";
        string prefix = text.Length > 0 && !text.EndsWith('\n') ? newline : string.Empty;
        return new WordListEditResult(text + prefix + line + newline, WordListChange.Added);
    }

    /// <summary>Entfernt die Zeile(n) des Worts (Regeln 1 und 4).</summary>
    /// <param name="content">Der bisherige Inhalt der Datei; null gilt als leer.</param>
    /// <param name="term">Das Wort, verglichen über den Schlüssel.</param>
    public static WordListEditResult Remove(string? content, string? term)
    {
        string text = content ?? string.Empty;
        string key = VocabularyCorrector.Key((term ?? string.Empty).Trim());
        if (key.Length == 0) return new WordListEditResult(text, WordListChange.NotFound);

        List<(string Body, string Ending)> lines = SplitLines(text);
        int before = lines.Count;
        lines.RemoveAll(l => ParseLine(l.Body) is { } entry && VocabularyCorrector.Key(entry.Term) == key);

        return lines.Count == before
            ? new WordListEditResult(text, WordListChange.NotFound)
            : new WordListEditResult(Join(lines), WordListChange.Removed);
    }

    private static string? CheckPart(string value, string what)
    {
        if (value.Length > MaxLength) return $"{what} ist zu lang (höchstens {MaxLength} Zeichen).";
        if (value.StartsWith('#')) return $"{what} darf nicht mit # beginnen.";
        if (value.IndexOfAny(['=', '|', '\r', '\n']) >= 0) return $"{what} darf kein =, | und keinen Zeilenumbruch enthalten.";
        return null;
    }

    // Varianten ohne leere, ohne solche mit dem Schlüssel des Worts und ohne doppelte (nach Schlüssel), in Reihenfolge.
    private static List<string> CleanVariants(IEnumerable<string> variants, string termKey)
    {
        HashSet<string> seen = new(StringComparer.Ordinal) { termKey };
        List<string> result = [];
        foreach (string v in variants)
        {
            string key = VocabularyCorrector.Key(v);
            if (v.Length == 0 || key.Length == 0 || !seen.Add(key)) continue;
            result.Add(v);
        }

        return result;
    }

    // Eine Zeile als Eintrag lesen, wie ReadLines es tut: Kommentare und Leerzeilen sind kein Eintrag.
    private static VocabularyEntry? ParseLine(string body)
    {
        string trimmed = body.Trim();
        return trimmed.Length == 0 || trimmed.StartsWith('#') ? null : VocabularyEntry.Parse(trimmed);
    }

    // Zerlegt den Text in Zeilen samt ihrem Zeilenende ("\r\n", "\n", "\r" oder leer bei der letzten Zeile ohne Umbruch).
    private static List<(string Body, string Ending)> SplitLines(string text)
    {
        List<(string, string)> lines = [];
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n')) continue;
            int end = text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? i + 2 : i + 1;
            lines.Add((text[start..i], text[i..end]));
            start = end;
            i = end - 1;
        }

        if (start < text.Length) lines.Add((text[start..], string.Empty));
        return lines;
    }

    private static string Join(List<(string Body, string Ending)> lines) => string.Concat(lines.Select(l => l.Body + l.Ending));
}
