// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.App.Input;

/// <summary>
/// Diagnosemeldung des Tastaturhooks ohne Aktion für die App. Der Hook legt sie in eine eigene Warteschlange,
/// der Verbraucher meldet sie außerhalb des Hook Threads, die App schreibt sie ins Protokoll (nur das Ereignis, keine Inhalte).
/// </summary>
internal enum HookNotice
{
    /// <summary>Umschalt plus Caps Lock wurde durchgelassen (echte Großschreibung), Protokollzeile "hotkey | shift-caps-passthrough".</summary>
    ShiftCapsPassthrough,
}
