namespace Talk.Core;

/// <summary>
/// Was das Tray Symbol zeigt. Jeder Wert hat ein eigenes Symbol mit eigener Form.
/// </summary>
public enum TrayStatus
{
    /// <summary>
    /// Ruhe. Talk wartet auf den Doppeltipp.
    /// </summary>
    Idle,

    /// <summary>
    /// Aufnahme läuft.
    /// </summary>
    Recording,

    /// <summary>
    /// Die Aufnahme wird erkannt.
    /// </summary>
    Processing,

    /// <summary>
    /// Talk ruht, aber etwas stimmt nicht: Der Hook sitzt nicht oder das letzte Diktat ist gescheitert.
    /// </summary>
    Error
}

/// <summary>
/// Leitet aus Zustand der Steuerung und Fehlermerker ab, was das Tray Symbol zeigt. Reine Logik ohne Windows Aufrufe.
/// </summary>
public static class TrayStatusRule
{
    /// <summary>
    /// Aufnahme und Verarbeitung gehen immer vor, damit ein alter Fehler ein laufendes Diktat nie verdeckt.
    /// Der Fehler zeigt sich nur in Ruhe.
    /// </summary>
    public static TrayStatus Select(DictationState state, bool hasError) => state switch
    {
        DictationState.Recording => TrayStatus.Recording,
        DictationState.Processing => TrayStatus.Processing,
        _ => hasError ? TrayStatus.Error : TrayStatus.Idle,
    };

    /// <summary>
    /// Festes Wort fürs Protokoll und für den Namen der Symboldatei.
    /// </summary>
    public static string ToWord(this TrayStatus status) => status switch
    {
        TrayStatus.Recording => "aufnahme",
        TrayStatus.Processing => "verarbeitung",
        TrayStatus.Error => "fehler",
        _ => "ruhe",
    };
}
