// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.App.Input;

/// <summary>
/// Liest den Zustand der Feststelltaste für Anzeige und Protokoll.
/// In Schritt 2 wird nur beobachtet, nichts korrigiert.
/// </summary>
internal static class CapsLockState
{
    /// <summary>True, wenn die Feststelltaste eingeschaltet ist (unterstes Bit von GetKeyState).</summary>
    public static bool IsOn => (NativeMethods.GetKeyState((int)NativeMethods.VK_CAPITAL) & 1) != 0;

    /// <summary>Festes Wort für das Protokoll: "on" oder "off".</summary>
    public static string LogDetail => IsOn ? "on" : "off";
}
