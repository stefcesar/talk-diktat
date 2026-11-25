// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich overlay (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] OverlayEntries() =>
    [
        ("overlay.title", "Talk Overlay", "Talk Overlay", "Talk Overlay"),
        ("overlay.state.recording", "Aufnahme", "Recording", "Registrazione"),
        ("overlay.state.processing", "Verarbeitung", "Processing", "Elaborazione"),
        ("overlay.state.error", "Fehler, siehe Diagnose", "Error, see Diagnostics", "Errore, vedi Diagnostica"),
    ];
}
