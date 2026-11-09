namespace Talk.Core.Hotkey;

/// <summary>
/// Einstellungen der Hotkey Steuerung. Die Werte kommen später aus den Einstellungen der App.
/// </summary>
/// <param name="DoubleTapWindowMs">Höchster Abstand zwischen dem ersten und dem zweiten Drücken von Caps Lock, damit es als Doppeltipp zählt. Standard 350 ms.</param>
/// <param name="MinTapGapMs">Kleinster Abstand zwischen zwei Drücken. Alles darunter gilt als Prellen der Taste und wird ignoriert. Standard 30 ms.</param>
/// <param name="PushToTalkEnabled">Ob Caps Lock gedrückt halten die Aufnahme startet und Loslassen sie beendet. Standard aus.</param>
/// <param name="PushToTalkHoldMs">Wie lange Caps Lock gehalten werden muss, bis Push to Talk die Aufnahme startet. Standard 300 ms.</param>
public sealed record HotkeyOptions(
    int DoubleTapWindowMs = 350,
    int MinTapGapMs = 30,
    bool PushToTalkEnabled = false,
    int PushToTalkHoldMs = 300);
