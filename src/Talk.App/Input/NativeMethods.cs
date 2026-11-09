// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace Talk.App.Input;

/// <summary>
/// Die wenigen Windows Aufrufe, die der Tastaturhook braucht. Nichts darüber hinaus.
/// Alle Deklarationen sind absichtlich eng gehalten, damit nachprüfbar bleibt,
/// was die App vom System liest: Hook setzen und lösen, Nachrichtenschleife, Tastenzustand.
/// </summary>
internal static partial class NativeMethods
{
    // Hook Art und Hook Code
    internal const int WH_KEYBOARD_LL = 13;
    internal const int HC_ACTION = 0;

    // Fensternachrichten
    internal const uint WM_KEYDOWN = 0x0100;
    internal const uint WM_KEYUP = 0x0101;
    internal const uint WM_SYSKEYDOWN = 0x0104;
    internal const uint WM_SYSKEYUP = 0x0105;
    internal const uint WM_QUIT = 0x0012;
    internal const uint WM_APP = 0x8000;

    // Tastencodes: die einzigen drei, die Talk kennt
    internal const uint VK_SHIFT = 0x10;
    internal const uint VK_CAPITAL = 0x14;
    internal const uint VK_ESCAPE = 0x1B;

    /// <summary>
    /// Eigene Kennung in <c>dwExtraInfo</c> ("TALK" als Zahl). Ereignisse mit dieser Kennung stammen von Talk selbst
    /// und werden vom Hook ungeprüft weitergereicht. Fremde eingespeiste Ereignisse gelten wie echte Tasten.
    /// </summary>
    internal const nuint TalkInjectedMarker = 0x54414C4B;

    /// <summary>Signatur der Hook Prozedur für WH_KEYBOARD_LL.</summary>
    internal delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    /// <summary>Daten eines Tastenereignisses, wie Windows sie dem Hook übergibt.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>Eine Fensternachricht für die Nachrichtenschleife des Hook Threads.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    internal static extern nint DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessageW(uint idThread, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();
}
