namespace Talk.Core.Transcription;

/// <summary>
/// Vorgaben für eine Erkennung.
/// </summary>
/// <param name="Language">Sprachkürzel, zum Beispiel "de", "it", "en", "de-ch" oder "gsw". Standard ist Deutsch. Leer oder "auto" überlässt die Wahl dem Erkenner.</param>
/// <param name="Vocabulary">Wörterbuch mit Begriffen, die der Erkenner bevorzugen soll, oder null.</param>
public sealed record TranscriptionRequest(
    string Language = "de",
    IReadOnlyList<string>? Vocabulary = null)
{
    /// <summary>Kürzel für die automatische Spracherkennung.</summary>
    public const string AutoLanguage = "auto";

    /// <summary>True, wenn keine feste Sprache vorgegeben ist (leer oder "auto", Groß und Klein egal).</summary>
    public bool AutoDetect =>
        string.IsNullOrWhiteSpace(Language)
        || string.Equals(Language.Trim(), AutoLanguage, StringComparison.OrdinalIgnoreCase);

    /// <summary>Das Kürzel, das an Whisper geht: Schweizerdeutsch und Mundart als "de" (<see cref="LanguageCodes.ToWhisper"/>).</summary>
    public string WhisperLanguage => LanguageCodes.ToWhisper(Language);
}
