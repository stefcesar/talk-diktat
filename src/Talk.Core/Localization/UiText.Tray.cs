// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich tray (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] TrayEntries() =>
    [
        ("tray.hint.title", "Talk läuft", "Talk is running", "Talk è attivo"),
        ("tray.hint.text", "Caps Lock doppelt tippen startet das Diktat. Ein Klick auf dieses Symbol öffnet das Menü.",
            "Double-tap Caps Lock to start dictating. One click on this icon opens the menu.",
            "Tocca due volte Bloc Maiusc per iniziare a dettare. Un clic su questa icona apre il menu."),

        // Menüpunkte
        ("tray.menu.copy_last", "Letzten Text kopieren", "Copy last text", "Copia ultimo testo"),
        ("tray.menu.history", "Verlauf", "History", "Cronologia"),
        ("tray.menu.local_only", "Nur lokal (nichts geht ins Internet)",
            "Local only (nothing goes to the internet)",
            "Solo locale (niente va su internet)"),
        ("tray.menu.smoothing", "Glätten (Text geht über Groq)", "Smoothing (text goes through Groq)",
            "Rifinitura (il testo passa da Groq)"),
        ("tray.menu.settings", "Einstellungen", "Settings", "Impostazioni"),
        ("tray.menu.diagnostics", "Diagnose", "Diagnostics", "Diagnostica"),
        ("tray.menu.quit", "Talk beenden", "Quit Talk", "Esci da Talk"),

        // Kurzinfo am Symbol je Zustand
        ("tray.tooltip.recording", "Talk: Aufnahme", "Talk: Recording", "Talk: Registrazione"),
        ("tray.tooltip.processing", "Talk: Verarbeitung", "Talk: Processing", "Talk: Elaborazione"),
        ("tray.tooltip.error", "Talk: Fehler", "Talk: Error", "Talk: Errore"),
        ("tray.tooltip.idle", "Talk: Ruhe", "Talk: Idle", "Talk: In attesa"),
    ];
}
