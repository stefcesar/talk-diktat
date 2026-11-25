// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich key (Schlüsseldialog) (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] KeyEntries() =>
    [
        // Fenster
        ("key.title", "Groq Schlüssel", "Groq key", "Chiave Groq"),
        ("key.intro",
            "Der Schlüssel liegt nur in der Windows Anmeldeinformationsverwaltung. Er wird nie angezeigt, nie protokolliert und in keine Datei geschrieben. Die Zwischenablage wird nur auf Knopfdruck gelesen und nie beschrieben.",
            "The key is stored only in Windows Credential Manager. It is never shown, never logged and never written to a file. The clipboard is read only when you press the button and is never written to.",
            "La chiave è salvata solo in Gestione credenziali di Windows. Non viene mai mostrata, mai registrata e mai scritta in un file. Gli Appunti vengono letti solo premendo il pulsante e non vengono mai modificati."),
        ("key.stored.yes", "Schlüssel hinterlegt: ja", "Key stored: yes", "Chiave salvata: sì"),
        ("key.stored.no", "Schlüssel hinterlegt: nein", "Key stored: no", "Chiave salvata: no"),
        ("key.new_label", "Neuer Schlüssel", "New key", "Nuova chiave"),
        ("key.paste", "Aus Zwischenablage einfügen", "Paste from clipboard", "Incolla dagli Appunti"),
        ("key.check", "Prüfen", "Check", "Verifica"),
        ("key.status.ready", "Bereit", "Ready", "Pronto"),
        ("key.local_only_hint", "Nur lokal ist eingeschaltet: Prüfen ist aus, weil dann nichts ins Internet geht.",
            "Local only is on: Check is off, because then nothing goes to the internet.",
            "Solo locale è attivo: Verifica è disattivata, perché così nulla va su internet."),

        // Einfügen
        ("key.paste.unreadable", "Die Zwischenablage ließ sich nicht lesen", "The clipboard could not be read",
            "Impossibile leggere gli Appunti"),
        ("key.paste.no_text", "Die Zwischenablage enthält keinen Text", "The clipboard contains no text",
            "Gli Appunti non contengono testo"),
        ("key.paste.too_long", "Der Text in der Zwischenablage ist zu lang ({0} Zeichen), nichts eingefügt",
            "The text in the clipboard is too long ({0} characters), nothing pasted",
            "Il testo negli Appunti è troppo lungo ({0} caratteri), niente incollato"),
        ("key.paste.not_a_key",
            "Das sieht nicht wie ein Schlüssel aus (Leerzeichen, Zeilenumbruch oder Sonderzeichen), nichts eingefügt",
            "That does not look like a key (space, line break or special character), nothing pasted",
            "Non sembra una chiave (spazio, a capo o carattere speciale), niente incollato"),
        ("key.pasted", "Eingefügt: {0} Zeichen", "Pasted: {0} characters", "Incollato: {0} caratteri"),

        // Speichern
        ("key.save.empty", "Das Feld ist leer, nichts gespeichert", "The field is empty, nothing saved",
            "Il campo è vuoto, niente salvato"),
        ("key.save.not_a_key",
            "Das sieht nicht wie ein Schlüssel aus (Leerzeichen, Zeilenumbruch oder Sonderzeichen), nichts gespeichert",
            "That does not look like a key (space, line break or special character), nothing saved",
            "Non sembra una chiave (spazio, a capo o carattere speciale), niente salvato"),
        ("key.save.failed", "Speichern fehlgeschlagen", "Saving failed", "Salvataggio non riuscito"),
        ("key.saved", "Gespeichert", "Saved", "Salvata"),

        // Prüfen
        ("key.check.not_a_key",
            "Das sieht nicht wie ein Schlüssel aus (Leerzeichen, Zeilenumbruch oder Sonderzeichen), nichts geprüft",
            "That does not look like a key (space, line break or special character), nothing checked",
            "Non sembra una chiave (spazio, a capo o carattere speciale), niente verificato"),
        ("key.check.running", "Prüfung läuft", "Checking", "Verifica in corso"),
        ("key.check.local_only", "Nur lokal ist eingeschaltet, es wurde nichts gesendet", "Local only is on, nothing was sent",
            "Solo locale è attivo, non è stato inviato nulla"),
        ("key.check.missing", "Kein Schlüssel", "No key", "Nessuna chiave"),
        ("key.check.valid.field", "Schlüssel gültig (Inhalt des Felds, noch nicht gespeichert)",
            "Key valid (field content, not saved yet)", "Chiave valida (contenuto del campo, non ancora salvato)"),
        ("key.check.valid.stored", "Schlüssel gültig (hinterlegter Schlüssel)", "Key valid (stored key)",
            "Chiave valida (chiave salvata)"),
        ("key.check.invalid.field", "Schlüssel abgelehnt (Inhalt des Felds, noch nicht gespeichert)",
            "Key rejected (field content, not saved yet)", "Chiave rifiutata (contenuto del campo, non ancora salvato)"),
        ("key.check.invalid.stored", "Schlüssel abgelehnt (hinterlegter Schlüssel)", "Key rejected (stored key)",
            "Chiave rifiutata (chiave salvata)"),
        ("key.check.unreachable.field", "Keine Bestätigung von Groq (Inhalt des Felds, noch nicht gespeichert)",
            "No confirmation from Groq (field content, not saved yet)",
            "Nessuna conferma da Groq (contenuto del campo, non ancora salvato)"),
        ("key.check.unreachable.stored", "Keine Bestätigung von Groq (hinterlegter Schlüssel)",
            "No confirmation from Groq (stored key)", "Nessuna conferma da Groq (chiave salvata)"),
        ("key.check.failed", "Prüfung fehlgeschlagen", "Check failed", "Verifica non riuscita"),

        // Rückfragen und Löschen
        ("key.unusual.confirm", "Der Text sieht nicht wie ein Groq Schlüssel aus. Trotzdem verwenden?",
            "The text does not look like a Groq key. Use it anyway?", "Il testo non sembra una chiave Groq. Usarlo comunque?"),
        ("key.delete.confirm", "Den hinterlegten Schlüssel wirklich löschen?", "Really delete the stored key?",
            "Eliminare davvero la chiave salvata?"),
        ("key.delete.failed", "Löschen fehlgeschlagen", "Deleting failed", "Eliminazione non riuscita"),
        ("key.deleted", "Gelöscht", "Deleted", "Eliminata"),
    ];
}
