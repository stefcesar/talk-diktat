// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Hotkey;

/// <summary>
/// Antwort der Steuerung an den Hook: ob die Taste geschluckt wird (Windows sieht sie dann nicht) und was die App tun soll.
/// </summary>
/// <param name="Swallow">True, wenn der Hook das Ereignis nicht an Windows weitergeben darf.</param>
/// <param name="Action">Was die App tun soll. Die App führt das außerhalb des Hook Threads aus.</param>
public readonly record struct HotkeyDecision(bool Swallow, HotkeyAction Action)
{
    /// <summary>Taste durchlassen, nichts tun.</summary>
    public static HotkeyDecision Pass => new(false, HotkeyAction.None);

    /// <summary>Taste schlucken, nichts tun.</summary>
    public static HotkeyDecision Swallowed => new(true, HotkeyAction.None);
}
