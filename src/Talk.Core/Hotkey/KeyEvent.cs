namespace Talk.Core.Hotkey;

/// <summary>
/// Ein gefiltertes Tastenereignis aus dem Hook. Nur Caps Lock und Esc kommen hier an.
/// </summary>
/// <param name="Key">Welche der beiden Tasten.</param>
/// <param name="Direction">Gedrückt oder losgelassen.</param>
/// <param name="ShiftHeld">Ob Umschalt in diesem Moment gehalten wird (der Hook liest den Zustand, er verfolgt keine Umschalt Ereignisse).</param>
/// <param name="TimestampMs">Monotoner Zeitstempel in Millisekunden (in der App <c>Environment.TickCount64</c>, in Tests beliebige Zahlen).</param>
public readonly record struct KeyEvent(HotkeyKey Key, KeyDirection Direction, bool ShiftHeld, long TimestampMs);
