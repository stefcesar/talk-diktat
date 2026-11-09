namespace Talk.Core.Hotkey;

/// <summary>
/// Was die App nach einem Tastenereignis tun soll. Die Steuerung entscheidet, die App führt aus.
/// </summary>
public enum HotkeyAction
{
    /// <summary>Nichts tun.</summary>
    None,

    /// <summary>Aufnahme starten. Grund: Doppeltipp erkannt oder Caps Lock lange genug gehalten (Push to Talk).</summary>
    StartRecording,

    /// <summary>Aufnahme beenden und Text erzeugen. Grund: Einzeldruck während der Aufnahme oder Push to Talk losgelassen.</summary>
    StopRecording,

    /// <summary>Aufnahme abbrechen, nichts erzeugen. Grund: Esc während der Aufnahme.</summary>
    Cancel,
}
