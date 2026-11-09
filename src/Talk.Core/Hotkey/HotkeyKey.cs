// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Hotkey;

/// <summary>
/// Die einzigen Tasten, die der Hook an die Steuerung weitergibt. Alle anderen Tasten sieht die Steuerung nie.
/// </summary>
public enum HotkeyKey
{
    /// <summary>Caps Lock (Feststelltaste).</summary>
    CapsLock,

    /// <summary>Esc.</summary>
    Escape,
}
