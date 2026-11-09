using System.Text;

namespace Talk.Core.Transcription;

/// <summary>
/// Schwellen des Filters gegen erfundene Sätze.
/// </summary>
/// <param name="NoSpeechProbThreshold">Ab dieser Wahrscheinlichkeit für "keine Sprache" gilt Regel A zusammen mit der Wortsicherheit. Standard 0,6.</param>
/// <param name="AvgLogProbThreshold">Unter dieser Wortsicherheit gilt Regel A zusammen mit "keine Sprache". Standard minus 1,0.</param>
/// <param name="HardNoSpeechProbThreshold">Ab dieser Wahrscheinlichkeit für "keine Sprache" fällt der Abschnitt immer weg (Regel B). Standard 0,9.</param>
/// <param name="CompressionRatioThreshold">Über diesem Kompressionsverhältnis gilt der Abschnitt als Wiederholungsschleife (Regel C). Standard 2,4.</param>
/// <param name="ExtraPhrases">Zusätzliche bekannte Phantomtexte, die als ganzer Abschnitt verworfen werden, oder null.</param>
public sealed record HallucinationFilterOptions(
    double NoSpeechProbThreshold = 0.6,
    double AvgLogProbThreshold = -1.0,
    double HardNoSpeechProbThreshold = 0.9,
    double CompressionRatioThreshold = 2.4,
    IReadOnlyList<string>? ExtraPhrases = null);

/// <summary>
/// Ergebnis des Filters.
/// </summary>
/// <param name="Text">Text der behaltenen Abschnitte, je an den Rändern bereinigt und mit einem Leerzeichen verbunden.</param>
/// <param name="Kept">Die behaltenen Abschnitte in ihrer Reihenfolge.</param>
/// <param name="FilteredCount">Anzahl der als erfunden verworfenen Abschnitte (leere Abschnitte zählen nicht).</param>
public sealed record HallucinationFilterResult(string Text, IReadOnlyList<TranscriptionSegment> Kept, int FilteredCount);

/// <summary>
/// Verwirft Abschnitte, die der Erkenner bei Stille oder Rauschen erfindet. Reine Rechnung auf den Kennzahlen der Abschnitte
/// und einer kurzen Liste bekannter Phantomtexte.
/// </summary>
/// <remarks>
/// Regeln je Abschnitt, in dieser Reihenfolge:
/// Leer: Ein Abschnitt ohne Text (leer oder nur Leerzeichen) fällt weg und zählt nicht als gefiltert.
/// A: "keine Sprache" ist mindestens <see cref="HallucinationFilterOptions.NoSpeechProbThreshold"/> UND die Wortsicherheit liegt unter
///    <see cref="HallucinationFilterOptions.AvgLogProbThreshold"/> (beide Werte müssen vorhanden sein).
/// B: "keine Sprache" ist mindestens <see cref="HallucinationFilterOptions.HardNoSpeechProbThreshold"/>.
/// C: Das Kompressionsverhältnis liegt über <see cref="HallucinationFilterOptions.CompressionRatioThreshold"/>.
/// D: Der ganze Abschnitt ist ein bekannter Phantomtext (siehe <see cref="IsKnownPhrase"/>).
/// Fehlende Kennzahlen lösen ihre Regel nicht aus. Die Liste enthält bewusst nur Texte, die beim Diktieren kaum vorkommen
/// (Untertitel Vermerke und Abspann Sätze aus Videos), keine Alltagssätze wie "Vielen Dank".
/// Bei behaltenen Abschnitten schneidet <see cref="RemoveTrailingCredits"/> zusätzlich einen Untertitel Vermerk am Ende ab
/// (PLAYBOOK.md, Abschnitt 22).
/// </remarks>
public static class HallucinationFilter
{
    // Ganze Abschnitte, die nach dem Vereinheitlichen genau so lauten.
    // Aus den Primärquellen (Rechercheprotokoll 29): "Thank you for watching", "Thanks for watching",
    // "Untertitel von Stephanie Geiges", "SWR 2021", "www.mooji.org". Alle übrigen Einträge dieser Liste (die deutschen und
    // italienischen Abspann Sätze, "SWR 2020", "Thank you so much for watching") sind Erfahrungswerte ohne Primärquelle,
    // beim Diktieren aber kaum zu erwarten.
    private static readonly string[] ExactSource =
    [
        "Vielen Dank fürs Zuschauen",
        "Vielen Dank für das Zuschauen",
        "Danke fürs Zuschauen",
        "Untertitel von Stephanie Geiges",
        "SWR 2020",
        "SWR 2021",
        "Thank you for watching",
        "Thanks for watching",
        "Thank you so much for watching",
        "www.mooji.org",
        "Grazie per la visione",
        "Grazie per aver guardato il video",
        "Grazie per aver guardato",
        // Aus der Liste von Blitztext (Rechercheprotokoll 31), nur als ganzer Abschnitt.
        "Abonniert den Kanal",
        "Bitte abonnieren",
        "Abonniere meinen Kanal",
        "Please subscribe",
        "Like and subscribe",
        "Please like and subscribe",
    ];

    // Anfänge von Abschnitten: Der Abschnitt beginnt mit diesem Text und endet dort oder geht nach einem Leerzeichen weiter
    // (zum Beispiel mit einer Jahreszahl). Untertitel Vermerke aus den Primärquellen; "Untertitelung des ZDF" ist eine
    // naheliegende Abwandlung ohne eigene Quelle.
    private static readonly string[] PrefixSource =
    [
        "Untertitel der Amara.org-Community",
        "Untertitelung aufgrund der Amara.org-Community",
        "Untertitel im Auftrag des ZDF",
        "Untertitelung des ZDF",
        "Untertitelung im Auftrag des ZDF",
        "Copyright WDR",
        "Subtitles by the Amara.org community",
        "Transcribed by https://otter.ai",
        "Sottotitoli creati dalla comunità Amara.org",
        "Sottotitoli e revisione a cura di QTSS",
        "Sottotitoli a cura di QTSS",
        "Sottotitoli e revisione a cura di Amara.org",
        "Sottotitoli e revisione al canale di Amara.org",
        "Sottotitoli di Sottotitoli di Amara.org",
        // Aus der Liste von Blitztext (Rechercheprotokoll 31).
        "Copyright SWR",
        "Untertitel des ZDF",
        "Untertitel von Amara.org",
        "Untertitel von der Amara.org-Community",
        "Subtitles by Amara.org",
    ];

    // Untertitel Vermerke, die auch am Ende eines längeren Texts abgeschnitten werden (Abschnitt 22, Regel 3).
    // Abspann Sätze wie "Vielen Dank fürs Zuschauen" fehlen hier bewusst: Sie können ein echtes Diktat beenden.
    private static readonly string[] CreditSource =
    [
        .. PrefixSource,
        "Untertitel im Auftrag des ZDF für funk",
        "Untertitel von Stephanie Geiges",
        "www.mooji.org",
    ];

    private static readonly HashSet<string> Exact = new(ExactSource.Select(Normalize), StringComparer.Ordinal);
    private static readonly string[] Prefixes = PrefixSource.Select(Normalize).ToArray();
    private static readonly HashSet<string> Credits = new(CreditSource.Select(CreditKey).Where(k => k.Length > 0), StringComparer.Ordinal);

    // Höchstens so viele Satzanfänge vom Ende her werden auf einen Vermerk geprüft.
    private const int TrailingSentencesChecked = 3;

    /// <summary>Die bekannten Phantomtexte, die als ganzer Abschnitt verworfen werden.</summary>
    public static IReadOnlyList<string> KnownPhrases => ExactSource;

    /// <summary>Die bekannten Anfänge von Phantomtexten (Untertitel Vermerke), die auch mit Zusatz wie einer Jahreszahl verworfen werden.</summary>
    public static IReadOnlyList<string> KnownPrefixes => PrefixSource;

    /// <summary>Die Untertitel Vermerke, die <see cref="RemoveTrailingCredits"/> auch am Ende eines längeren Texts abschneidet.</summary>
    public static IReadOnlyList<string> KnownCredits => CreditSource;

    /// <summary>
    /// Vereinheitlicht einen Text für den Vergleich: Kleinbuchstaben, Apostrophe entfallen, jedes andere Zeichen außer
    /// Buchstaben und Ziffern wird zum Leerzeichen, mehrere Leerzeichen werden eines, Ränder ohne Leerzeichen.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        StringBuilder sb = new(text.Length);
        bool pendingSpace = false;

        foreach (char c in text)
        {
            if (c is '\'' or '’' or '‘' or '`' or '´') continue;

            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingSpace = true;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// True, wenn der vereinheitlichte Text genau einem bekannten Phantomtext entspricht (auch aus <paramref name="extraPhrases"/>)
    /// oder mit einem bekannten Anfang beginnt und dort endet oder nach einem Leerzeichen weitergeht.
    /// </summary>
    public static bool IsKnownPhrase(string? text, IReadOnlyList<string>? extraPhrases = null)
    {
        string normalized = Normalize(text);
        if (normalized.Length == 0) return false;

        if (Exact.Contains(normalized)) return true;

        foreach (string prefix in Prefixes)
        {
            if (normalized.Length == prefix.Length)
            {
                if (string.Equals(normalized, prefix, StringComparison.Ordinal)) return true;
            }
            else if (normalized.Length > prefix.Length
                && normalized.StartsWith(prefix, StringComparison.Ordinal)
                && normalized[prefix.Length] == ' ')
            {
                return true;
            }
        }

        if (extraPhrases is not null)
        {
            foreach (string extra in extraPhrases)
            {
                string n = Normalize(extra);
                if (n.Length > 0 && string.Equals(normalized, n, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    /// <summary>True, wenn der Abschnitt nach den Regeln A bis D als erfunden gilt. Leere Abschnitte ergeben false.</summary>
    public static bool ShouldDrop(TranscriptionSegment segment, HallucinationFilterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(segment);
        HallucinationFilterOptions o = options ?? new HallucinationFilterOptions();

        if (string.IsNullOrWhiteSpace(segment.Text)) return false;

        if (segment.NoSpeechProb is double noSpeech)
        {
            if (segment.AvgLogProb is double logProb
                && noSpeech >= o.NoSpeechProbThreshold
                && logProb < o.AvgLogProbThreshold)
            {
                return true;
            }

            if (noSpeech >= o.HardNoSpeechProbThreshold) return true;
        }

        if (segment.CompressionRatio is double ratio && ratio > o.CompressionRatioThreshold) return true;

        return IsKnownPhrase(segment.Text, o.ExtraPhrases);
    }

    /// <summary>Wendet die Regeln auf alle Abschnitte an und baut den Text aus den behaltenen.</summary>
    public static HallucinationFilterResult Apply(IReadOnlyList<TranscriptionSegment> segments, HallucinationFilterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(segments);
        HallucinationFilterOptions o = options ?? new HallucinationFilterOptions();

        List<TranscriptionSegment> kept = [];
        int filtered = 0;
        StringBuilder text = new();

        foreach (TranscriptionSegment segment in segments)
        {
            if (segment is null || string.IsNullOrWhiteSpace(segment.Text)) continue;

            if (ShouldDrop(segment, o))
            {
                filtered++;
                continue;
            }

            TranscriptionSegment keptSegment = segment;
            string trimmedEnd = segment.Text.TrimEnd();
            string withoutCredits = RemoveTrailingCredits(trimmedEnd);
            if (withoutCredits.Length != trimmedEnd.Length)
            {
                if (string.IsNullOrWhiteSpace(withoutCredits))
                {
                    filtered++;
                    continue;
                }

                keptSegment = segment with { Text = withoutCredits };
            }

            kept.Add(keptSegment);
            if (text.Length > 0) text.Append(' ');
            text.Append(keptSegment.Text.Trim());
        }

        return new HallucinationFilterResult(text.ToString(), kept, filtered);
    }

    /// <summary>
    /// Schneidet Untertitel Vermerke am Textende ab (Abschnitt 22, Regel 3): Leitet einer der letzten drei Satzanfänge einen
    /// Vermerk aus <see cref="KnownCredits"/> ein, der bis zum Textende reicht, endet der Text davor; das wiederholt sich.
    /// Leerraum am Ende entfällt, am Anfang bleibt er. Null ergibt einen leeren Text.
    /// </summary>
    public static string RemoveTrailingCredits(string? text)
    {
        if (text is null) return string.Empty;

        string current = text.TrimEnd();
        bool cut = true;

        while (cut && current.Length > 0)
        {
            cut = false;
            List<int> starts = SentenceStarts(current);

            for (int k = starts.Count - 1; k >= Math.Max(0, starts.Count - TrailingSentencesChecked); k--)
            {
                if (Credits.Contains(CreditKey(current[starts[k]..])))
                {
                    current = current[..starts[k]].TrimEnd();
                    cut = true;
                    break;
                }
            }
        }

        return current;
    }

    // Schlüssel eines Vermerks: vereinheitlicht, Wörter nur aus Ziffern (Jahreszahlen) entfallen.
    private static string CreditKey(string? text) =>
        string.Join(' ', Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !w.All(char.IsAsciiDigit)));

    // Satzanfänge in aufsteigender Reihenfolge: das erste Zeichen ohne Leerraum am Textanfang, nach Satzende mit folgendem
    // Leerraum und nach jedem Zeilenumbruch.
    private static List<int> SentenceStarts(string text)
    {
        List<int> starts = [];
        int first = NextNonWhiteSpace(text, 0);
        if (first < text.Length) starts.Add(first);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool boundary = c is '\n' or '\r'
                || (c is '.' or '!' or '?' or '…' && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]));
            if (!boundary) continue;

            int next = NextNonWhiteSpace(text, i + 1);
            if (next < text.Length && next > starts[^1]) starts.Add(next);
        }

        return starts;
    }

    private static int NextNonWhiteSpace(string text, int from)
    {
        while (from < text.Length && char.IsWhiteSpace(text[from])) from++;
        return from;
    }

    /// <summary>
    /// Für Antworten ohne Abschnitte: Ist der ganze Text ein bekannter Phantomtext, kommt ein leerer Text zurück,
    /// sonst der Text nach <see cref="RemoveTrailingCredits"/> ohne Leerzeichen an den Rändern. Null ergibt einen leeren Text.
    /// </summary>
    public static string FilterText(string? text, HallucinationFilterOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return IsKnownPhrase(text, options?.ExtraPhrases) ? string.Empty : RemoveTrailingCredits(text).Trim();
    }
}
