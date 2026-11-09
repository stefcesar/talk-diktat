namespace Talk.Core;

/// <summary>
/// Zustand der Steuerung. Overlay, Töne und Tray richten sich allein nach diesem Zustand.
/// </summary>
public enum DictationState
{
    /// <summary>
    /// Ruhe. Es läuft keine Aufnahme, ein einzelner Druck auf Caps Lock macht nichts.
    /// </summary>
    Idle,

    /// <summary>
    /// Aufnahme. Das Mikrofon ist offen, der Pegel wird gemessen.
    /// </summary>
    Recording,

    /// <summary>
    /// Verarbeitung. Die Aufnahme wird erkannt, nachbearbeitet und eingefügt.
    /// </summary>
    Processing
}
