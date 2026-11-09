namespace Talk.Core.Audio;

/// <summary>
/// Nimmt Ton vom Mikrofon auf und liefert ihn als Audioabschnitt im Arbeitsspeicher.
/// Die Umsetzung über WASAPI kommt in Schritt 3.
/// </summary>
public interface IAudioRecorder
{
    /// <summary>
    /// Wird ausgelöst, wenn sich der Eingangspegel ändert.
    /// Der Wert liegt zwischen 0 (Stille) und 1 (Vollausschlag) und dient dem Overlay.
    /// </summary>
    event EventHandler<float>? LevelChanged;

    /// <summary>
    /// Startet die Aufnahme.
    /// </summary>
    /// <param name="deviceId">Kennung des Aufnahmegeräts oder null für das Standardgerät.</param>
    /// <param name="ct">Abbruchsignal.</param>
    Task StartAsync(string? deviceId, CancellationToken ct);

    /// <summary>
    /// Beendet die Aufnahme und liefert das Aufgenommene als Abschnitt mit 16 kHz mono.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    Task<AudioClip> StopAsync(CancellationToken ct);

    /// <summary>
    /// Bricht die Aufnahme ab und verwirft alles Aufgenommene. Der Zwischenspeicher wird mit Nullen überschrieben.
    /// Ohne laufende Aufnahme passiert nichts.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    Task CancelAsync(CancellationToken ct);

    /// <summary>True, solange eine Aufnahme läuft.</summary>
    bool IsRecording { get; }
}
