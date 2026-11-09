namespace Talk.Core.Audio;

/// <summary>
/// Erkennt Sprache in einem Audioabschnitt und schneidet Stille weg, bevor irgendetwas an einen Erkenner geht.
/// Die Erkennung selbst (Silero) liefert Abschnitte; Zusammenführen, Mindestlängen und Zuschneiden rechnet
/// <see cref="SilenceTrimmer"/> als reine Logik.
/// </summary>
public interface IVoiceActivityDetector
{
    /// <summary>Die verwendeten Einstellungen.</summary>
    VadOptions Options { get; }

    /// <summary>
    /// Findet die Sprachabschnitte im Clip (16 kHz, mono). Bereits zusammengeführt und nach Mindestlängen gefiltert,
    /// aufsteigend und ohne Überlappung. Leer, wenn keine Sprache gefunden wurde.
    /// </summary>
    /// <param name="clip">Zu prüfender Abschnitt.</param>
    IReadOnlyList<SpeechSegment> DetectSegments(AudioClip clip);

    /// <summary>
    /// Prüft, ob der Abschnitt überhaupt Sprache enthält.
    /// </summary>
    /// <param name="clip">Zu prüfender Abschnitt.</param>
    /// <returns>True, wenn Sprache erkannt wurde, sonst false.</returns>
    bool ContainsSpeech(AudioClip clip) => DetectSegments(clip).Count > 0;

    /// <summary>
    /// Entfernt Stille am Anfang und am Ende. Pausen zwischen den Sprachabschnitten bleiben erhalten.
    /// </summary>
    /// <param name="clip">Ursprünglicher Abschnitt.</param>
    /// <returns>Ein neuer, gekürzter Abschnitt; leer, wenn keine Sprache gefunden wurde.</returns>
    AudioClip TrimSilence(AudioClip clip) => SilenceTrimmer.Trim(clip, DetectSegments(clip), Options);
}
