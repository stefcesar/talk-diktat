// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich diag (Diagnosefenster) (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
// Tastennamen nach Regel 12: en Caps Lock, Shift, Esc; it Bloc Maiusc, Maiusc, Esc.
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] DiagEntries() =>
    [
        // Fenster und Zustand
        ("diag.title", "Talk Diagnose", "Talk diagnostics", "Diagnostica di Talk"),
        ("diag.state", "Zustand", "State", "Stato"),
        ("diag.state.idle", "Ruhe", "Idle", "In attesa"),
        ("diag.state.recording", "Aufnahme", "Recording", "Registrazione"),
        ("diag.state.processing", "Verarbeitung", "Processing", "Elaborazione"),
        ("diag.state.error", "Fehler", "Error", "Errore"),

        // Zähler
        ("diag.count.start", "Start", "Start", "Avvio"),
        ("diag.count.stop", "Stopp", "Stop", "Stop"),
        ("diag.count.cancel", "Abbruch", "Cancel", "Annullamento"),

        // Taste und Hook
        ("diag.caps.on", "Feststelltaste: an", "Caps Lock: on", "Bloc Maiusc: attivo"),
        ("diag.caps.off", "Feststelltaste: aus",
            "Caps Lock: off",
            "Bloc Maiusc: non attivo"),
        ("diag.hook.active", "Hook: aktiv", "Hook: active", "Hook: attivo"),
        ("diag.hook.inactive", "Hook: nicht aktiv (Windows Fehler {0})", "Hook: not active (Windows error {0})",
            "Hook: non attivo (errore di Windows {0})"),

        // Letzte Neusetzung des Hooks, je Grund ein ganzer Satz (Regel 9); {0} ist die Uhrzeit
        ("diag.reinstall.none", "Letzte Hook Neusetzung: noch keine", "Last hook reset: none yet",
            "Ultimo ripristino dell'hook: ancora nessuno"),
        ("diag.reinstall.resume", "Letzte Hook Neusetzung: {0} (aufwachen)", "Last hook reset: {0} (wake from sleep)",
            "Ultimo ripristino dell'hook: {0} (risveglio)"),
        ("diag.reinstall.resume.failed", "Letzte Hook Neusetzung: {0} (aufwachen, fehlgeschlagen)",
            "Last hook reset: {0} (wake from sleep, failed)", "Ultimo ripristino dell'hook: {0} (risveglio, non riuscito)"),
        ("diag.reinstall.unlock", "Letzte Hook Neusetzung: {0} (entsperren)", "Last hook reset: {0} (unlock)",
            "Ultimo ripristino dell'hook: {0} (sblocco)"),
        ("diag.reinstall.unlock.failed", "Letzte Hook Neusetzung: {0} (entsperren, fehlgeschlagen)",
            "Last hook reset: {0} (unlock, failed)", "Ultimo ripristino dell'hook: {0} (sblocco, non riuscito)"),
        ("diag.reinstall.logon", "Letzte Hook Neusetzung: {0} (anmelden)", "Last hook reset: {0} (sign in)",
            "Ultimo ripristino dell'hook: {0} (accesso)"),
        ("diag.reinstall.logon.failed", "Letzte Hook Neusetzung: {0} (anmelden, fehlgeschlagen)",
            "Last hook reset: {0} (sign in, failed)", "Ultimo ripristino dell'hook: {0} (accesso, non riuscito)"),
        ("diag.reinstall.interval", "Letzte Hook Neusetzung: {0} (intervall)", "Last hook reset: {0} (interval)",
            "Ultimo ripristino dell'hook: {0} (intervallo)"),
        ("diag.reinstall.interval.failed", "Letzte Hook Neusetzung: {0} (intervall, fehlgeschlagen)",
            "Last hook reset: {0} (interval, failed)", "Ultimo ripristino dell'hook: {0} (intervallo, non riuscito)"),
        ("diag.reinstall.unknown", "Letzte Hook Neusetzung: {0} (unbekannt)", "Last hook reset: {0} (unknown)",
            "Ultimo ripristino dell'hook: {0} (sconosciuto)"),
        ("diag.reinstall.unknown.failed", "Letzte Hook Neusetzung: {0} (unbekannt, fehlgeschlagen)",
            "Last hook reset: {0} (unknown, failed)", "Ultimo ripristino dell'hook: {0} (sconosciuto, non riuscito)"),

        // Aufnahme und Erkennung
        ("diag.level", "Pegel", "Level", "Livello"),
        ("diag.result", "Ergebnis", "Result", "Risultato"),
        ("diag.recognition", "Erkennung", "Recognition", "Riconoscimento"),
        ("diag.language", "Sprache:", "Dictation language:", "Lingua di dettatura:"),
        ("diag.vocabulary", "Zusätzliche Begriffe für diesen Lauf (mit Komma getrennt, gilt bis zum Beenden)",
            "Extra terms for this run (separated by commas, valid until Talk quits)",
            "Termini aggiuntivi per questa sessione (separati da virgole, validi fino alla chiusura di Talk)"),
        ("diag.transcript", "Erkannter Text", "Recognized text", "Testo riconosciuto"),
        ("diag.raw", "Roh: {0}", "Raw: {0}", "Grezzo: {0}"),
        ("diag.unsmoothed", "Vor der Glättung: {0}", "Before smoothing: {0}", "Prima della rifinitura: {0}"),
        ("diag.retry", "Erneut versuchen", "Try again", "Riprova"),

        // Bedienung
        ("diag.push_to_talk", "Push to Talk (Caps Lock gedrückt halten)", "Push to Talk (hold down Caps Lock)",
            "Push to Talk (tieni premuto Bloc Maiusc)"),
        ("diag.double_tap", "Doppeltipp Fenster in ms:", "Double-tap window in ms:", "Intervallo del doppio tocco in ms:"),
        ("diag.apply", "Übernehmen", "Apply", "Applica"),
        ("diag.double_tap.invalid", "Bitte eine ganze Zahl größer als 0 eingeben.", "Please enter a whole number greater than 0.",
            "Inserisci un numero intero maggiore di 0."),
        ("diag.double_tap.invalid.title", "Doppeltipp Fenster", "Double-tap window", "Intervallo del doppio tocco"),
        ("diag.help", "Caps Lock doppelt tippen startet die Aufnahme, einmal tippen stoppt sie, Esc bricht ab. Umschalt plus Caps Lock schaltet die Großschreibung wie gewohnt. Nach dem Stopp läuft die Stilleerkennung. Ohne Sprache wird nichts gesendet, mit Sprache geht der zugeschnittene Ausschnitt an die Erkennung. Audio bleibt im Arbeitsspeicher, es wird nie eine Datei geschrieben.",
            "Double-tap Caps Lock to start recording, tap once to stop, Esc cancels. Shift plus Caps Lock turns capitals on and off as usual. After stopping, silence detection runs. Without speech nothing is sent; with speech the trimmed part goes to recognition. Audio stays in memory, no file is ever written.",
            "Tocca due volte Bloc Maiusc per avviare la registrazione, una volta per fermarla, Esc annulla. Maiusc più Bloc Maiusc attiva e disattiva le maiuscole come di consueto. Dopo lo stop parte il rilevamento del silenzio. Senza parlato non viene inviato nulla; con il parlato la parte ritagliata va al riconoscimento. L'audio resta in memoria, non viene mai scritto alcun file."),
    ];
}
