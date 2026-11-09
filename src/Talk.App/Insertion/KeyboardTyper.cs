using System.Diagnostics;
using System.Runtime.InteropServices;

using Talk.App.Input;

namespace Talk.App.Insertion;

/// <summary>Wie ein Tippversuch ausging.</summary>
internal enum TypeResult
{
    /// <summary>Der ganze Text wurde getippt.</summary>
    Done,

    /// <summary>Windows hat ein Paket nicht ganz angenommen.</summary>
    Failed,

    /// <summary>Das Zielfenster war nicht mehr vorn. Was bis dahin getippt wurde, steht im Ziel.</summary>
    LostFocus
}

/// <summary>
/// Speist Tasten ein: Strg+V für das Einfügen und Unicode Zeichen für das Tippen. Jedes Ereignis trägt die Talk Kennung,
/// damit der eigene Hook es ungeprüft durchlässt. Der Rückgabewert von SendInput wird immer geprüft (Playbook Lehre 20).
/// </summary>
internal static class KeyboardTyper
{
    private const int CharsPerPacket = 16;
    private static readonly TimeSpan PacketPause = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan ModifierTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ModifierPoll = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Wartet kurz, bis Strg, Alt und die Windows Taste losgelassen sind. Mit einer dieser Tasten würde aus der Eingabe ein Befehl.
    /// </summary>
    public static async Task<bool> WaitForModifiersReleasedAsync()
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (IsDown(InsertNative.VK_CONTROL) || IsDown(InsertNative.VK_MENU) || IsDown(InsertNative.VK_LWIN) || IsDown(InsertNative.VK_RWIN))
        {
            if (waited.Elapsed >= ModifierTimeout) return false;
            await Task.Delay(ModifierPoll);
        }

        return true;
    }

    /// <summary>
    /// True, solange Umschalt gedrückt oder eingerastet ist. Dann würde aus Strg+V ein anderer Befehl, deshalb wird getippt.
    /// </summary>
    public static bool IsShiftDown => IsDown(InsertNative.VK_SHIFT);

    /// <summary>
    /// Sendet Strg+V an das Fenster, das vorn ist.
    /// </summary>
    public static bool SendPaste()
    {
        InsertNative.INPUT[] inputs =
        [
            Key(InsertNative.VK_CONTROL, 0, 0),
            Key(InsertNative.VK_V, 0, 0),
            Key(InsertNative.VK_V, 0, InsertNative.KEYEVENTF_KEYUP),
            Key(InsertNative.VK_CONTROL, 0, InsertNative.KEYEVENTF_KEYUP),
        ];
        return Send(inputs);
    }

    /// <summary>
    /// Tippt den Text Zeichen für Zeichen als Unicode, in kleinen Paketen. Vor jedem Paket wird geprüft, ob das Zielfenster noch vorn ist.
    /// Ein Surrogatpaar wird nie auf zwei Pakete verteilt. Einmal begonnen, lässt sich das Tippen nicht abbrechen, damit kein halber Text stehen bleibt.
    /// </summary>
    public static async Task<TypeResult> TypeAsync(string text, nint target)
    {
        int offset = 0;
        while (offset < text.Length)
        {
            if (InsertNative.GetForegroundWindow() != target) return TypeResult.LostFocus;

            int count = Math.Min(CharsPerPacket, text.Length - offset);
            if (offset + count < text.Length && char.IsHighSurrogate(text[offset + count - 1])) count++;

            InsertNative.INPUT[] inputs = new InsertNative.INPUT[count * 2];
            for (int i = 0; i < count; i++)
            {
                char c = text[offset + i];
                inputs[2 * i] = Key(0, c, InsertNative.KEYEVENTF_UNICODE);
                inputs[(2 * i) + 1] = Key(0, c, InsertNative.KEYEVENTF_UNICODE | InsertNative.KEYEVENTF_KEYUP);
            }

            if (!Send(inputs)) return TypeResult.Failed;

            offset += count;
            if (offset < text.Length) await Task.Delay(PacketPause);
        }

        return TypeResult.Done;
    }

    private static bool Send(InsertNative.INPUT[] inputs)
    {
        int size = Marshal.SizeOf<InsertNative.INPUT>();
        if (Environment.Is64BitProcess && size != InsertNative.ExpectedInputSize) return false;

        return InsertNative.SendInput((uint)inputs.Length, inputs, size) == (uint)inputs.Length;
    }

    private static InsertNative.INPUT Key(ushort virtualKey, ushort scan, uint flags) => new()
    {
        type = InsertNative.INPUT_KEYBOARD,
        u = new InsertNative.InputUnion
        {
            ki = new InsertNative.KEYBDINPUT
            {
                wVk = virtualKey,
                wScan = scan,
                dwFlags = flags,
                dwExtraInfo = NativeMethods.TalkInjectedMarker,
            },
        },
    };

    private static bool IsDown(int virtualKey) => (InsertNative.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
