using System.Text;

using Talk.Core.Processing.Swiss;

namespace Talk.Core.Processing;

/// <summary>
/// Die Nachbearbeitung ohne Netz: Füllwörter entfernen, Wörterbuch prüfen, Leerraum aufräumen, in dieser Reihenfolge.
/// Der Filter gegen erfundene Sätze läuft schon in den Erkennern; die Glättung per Sprachmodell ist eine eigene Stufe,
/// weil sie ins Netz geht.
/// </summary>
public sealed class PostProcessor : IPostProcessor
{
    /// <inheritdoc />
    public string Process(string text, PostProcessingOptions options) => ProcessDetailed(text, options).Text;

    /// <summary>
    /// Wie <see cref="Process"/>, dazu die Zahlen fürs Protokoll.
    /// </summary>
    public PostProcessingResult ProcessDetailed(string text, PostProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(text)) return new PostProcessingResult(string.Empty, false, 0, text?.Length ?? 0, 0);

        // Zusammengesetzte Zeichen vereinheitlichen, damit ein Umlaut immer ein Zeichen ist.
        string result = text.Normalize(NormalizationForm.FormC);

        bool fillersChanged = false;
        if (options.RemoveFillers)
        {
            string withoutFillers = FillerRemover.Remove(result, options.Language, options.ExtraFillers);
            // Die Stufe räumt am Ende auch Leerraum auf. Als Änderung zählt nur, was über dieses Aufräumen hinausgeht.
            fillersChanged = !string.Equals(withoutFillers, TextCleanup.Clean(result), StringComparison.Ordinal);
            result = withoutFillers;
        }

        // Schweizerdeutsch und Mundart (Playbook Abschnitt 26): zuerst die Schweizer Wörter, eigene vor den festen, danach das Wörterbuch
        // des Nutzers, damit es das letzte Wort hat. Ganz am Ende gilt die Schweizer Schreibung, auch für Schreibweisen aus dem Wörterbuch.
        bool swiss = SwissText.Applies(options.Language);
        int replacements = 0;
        if (swiss)
        {
            IEnumerable<string> swissLines = (options.SwissVocabulary ?? []).Concat(SwissWordLists.Helvetisms);
            SwissCorrectionResult helvetisms = SwissText.Helvetisms(result, swissLines);
            replacements += helvetisms.Replacements;
            result = helvetisms.Text;
        }

        if (options.Vocabulary is { Count: > 0 })
        {
            VocabularyCorrectionResult corrected = VocabularyCorrector.Apply(
                result,
                VocabularyEntry.ParseAll(options.Vocabulary),
                options.FuzzyVocabulary,
                options.Language);
            replacements += corrected.Replacements;
            result = corrected.Text;
        }

        string cleaned = TextCleanup.Clean(swiss ? SwissText.Spelling(result) : result);
        if (options.CapitalizeSentences) cleaned = SentenceCase.Apply(cleaned);
        return new PostProcessingResult(cleaned, fillersChanged, replacements, text.Length, cleaned.Length);
    }
}

/// <summary>
/// Ergebnis der Nachbearbeitung mit den Zahlen fürs Protokoll.
/// </summary>
/// <param name="Text">Der nachbearbeitete Text, leer, wenn nichts übrig bleibt.</param>
/// <param name="FillersChanged">True, wenn die Stufe Füllwörter mehr geändert hat als Leerraum.</param>
/// <param name="Replacements">Zahl der Stellen, die das Wörterbuch geändert hat.</param>
/// <param name="CharsIn">Länge des Texts vor der Nachbearbeitung.</param>
/// <param name="CharsOut">Länge des Texts danach.</param>
public sealed record PostProcessingResult(string Text, bool FillersChanged, int Replacements, int CharsIn, int CharsOut);
