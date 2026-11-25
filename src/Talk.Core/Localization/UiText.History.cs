// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich history (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] HistoryEntries() =>
    [
        ("history.count.newest", "Die neuesten {0} Einträge, neuester zuerst", "The newest {0} entries, newest first",
            "Le {0} voci più recenti, prima la più recente"),
        ("history.count.zero", "0 Einträge", "0 entries", "0 voci"),
        ("history.count.one", "1 Eintrag", "1 entry", "1 voce"),
        ("history.count.many", "{0} Einträge, neuester zuerst", "{0} entries, newest first", "{0} voci, prima la più recente"),
        ("history.summary.error", "Anzeige gestört", "Display failed", "Visualizzazione non riuscita"),

        // Fenster
        ("history.title", "Talk Verlauf", "Talk history", "Cronologia di Talk"),
        ("history.heading", "Verlauf", "History", "Cronologia"),
        ("history.clear", "Alles löschen", "Delete all", "Elimina tutto"),
        ("history.reload", "Neu laden", "Reload", "Ricarica"),
        ("history.copy", "Kopieren", "Copy", "Copia"),
        ("history.copied", "Kopiert", "Copied", "Copiato"),
        ("history.not_copied", "Nicht kopiert", "Not copied", "Non copiato"),
        ("history.fix_word", "Wort korrigieren", "Fix word", "Correggi parola"),

        // Leer und Fehler
        ("history.empty.title", "Noch keine Einträge.", "No entries yet.", "Ancora nessuna voce."),
        ("history.empty.hint", "Tippe zweimal auf ⇧ Caps, sprich, und tippe dann einmal auf ⇧ Caps. Jeder Text landet hier, auch wenn das Einfügen einmal nicht klappt.",
            "Tap Caps Lock twice, speak, and then tap Caps Lock once. Every text ends up here, even if inserting it fails at some point.",
            "Tocca due volte Bloc Maiusc, parla e poi tocca una volta Bloc Maiusc. Ogni testo finisce qui, anche se una volta l'inserimento non riesce."),
        ("history.error.title", "Der Verlauf ließ sich gerade nicht anzeigen.", "The history could not be shown just now.",
            "Al momento non è stato possibile mostrare la cronologia."),
        ("history.retry", "Noch einmal versuchen", "Try again", "Riprova"),

        // Meldungen und Fußtext
        ("history.clear_failed", "Eine Datei des Verlaufs ließ sich nicht löschen.", "A history file could not be deleted.",
            "Non è stato possibile eliminare un file della cronologia."),
        ("history.clear.confirm", "Alle Einträge im Verlauf löschen? Das lässt sich nicht rückgängig machen.",
            "Delete all entries in the history? This cannot be undone.",
            "Eliminare tutte le voci della cronologia? L'operazione non può essere annullata."),
        ("history.footer.saved",
            "Der Verlauf liegt verschlüsselt auf diesem Rechner und ist nur mit deinem Windows Konto lesbar. Einträge verschwinden nach 30 Tagen von selbst.",
            "The history is stored encrypted on this computer and can only be read with your Windows account. Entries disappear by themselves after 30 days.",
            "La cronologia è salvata in forma crittografata su questo computer ed è leggibile solo con il tuo account Windows. Le voci scompaiono da sole dopo 30 giorni."),
        ("history.footer.write_failed", "Achtung: Zuletzt ist ein Schreiben oder Löschen auf der Festplatte gescheitert.",
            "Warning: the last write or delete on the disk failed.",
            "Attenzione: l'ultima scrittura o eliminazione sul disco non è riuscita."),
        ("history.footer.memory_only",
            "Das Speichern ist abgestellt. Der Verlauf steht nur im Arbeitsspeicher und ist nach dem Beenden von Talk weg.",
            "Saving is turned off. The history is kept only in memory and is gone once you quit Talk.",
            "Il salvataggio è disattivato. La cronologia resta solo in memoria e sparisce quando chiudi Talk."),
    ];
}
