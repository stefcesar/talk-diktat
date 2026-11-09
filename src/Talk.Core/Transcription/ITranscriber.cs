using Talk.Core.Audio;

namespace Talk.Core.Transcription;

/// <summary>
/// Wandelt einen Audioabschnitt in Text um. Es gibt später zwei Umsetzungen (Groq und lokal),
/// eine Weiche wählt nach Einstellung, Netz und Limit. Die Umsetzung kommt in Schritt 4.
/// </summary>
public interface ITranscriber
{
    /// <summary>
    /// Anzeigename des Erkenners, zum Beispiel für Overlay und Verlauf.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Erkennt den gesprochenen Text im Abschnitt.
    /// </summary>
    /// <param name="clip">Audioabschnitt mit 16 kHz mono.</param>
    /// <param name="request">Sprache und Wörterbuch.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Erkannter Text samt Kennzahlen.</returns>
    Task<TranscriptionResult> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken ct);
}
