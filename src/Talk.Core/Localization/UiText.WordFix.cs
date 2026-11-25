// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich wordfix (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] WordFixEntries() =>
    [
        // Fenster
        ("wordfix.title", "Wort korrigieren", "Fix word", "Correggi parola"),
        ("wordfix.intro", "Klicke auf das Wort, das Talk falsch geschrieben hat. Gehört das Nachbarwort dazu (zum Beispiel \"Lumi Ora\"), klicke es auch an. Dann das richtige Wort eintragen und merken.",
            "Click the word that Talk misspelled. If the next word belongs to it (for example “Lumi Ora”), click it too. Then enter the correct word and remember it.",
            "Fai clic sulla parola che Talk ha scritto male. Se la parola vicina ne fa parte (per esempio «Lumi Ora»), fai clic anche su quella. Poi inserisci la parola giusta e memorizzala."),
        ("wordfix.correct_label", "Richtig geschrieben", "Correct spelling", "Grafia corretta"),
        ("wordfix.list.label", "Liste", "List", "Elenco"),
        ("wordfix.list.private", "Privat", "Private", "Privato"),
        ("wordfix.list.general", "Allgemein", "General", "Generale"),
        ("wordfix.list.hint",
            "Privat: Namen von Menschen und alles Persönliche, bleibt immer auf diesem Rechner, darum vorgewählt. Allgemein: Marken, Firmen, Orte, Fachwörter, geht auch als Hinweis an Groq.",
            "Private: names of people and anything personal, always stays on this computer, so it is preselected. General: brands, companies, places, technical terms, also goes to Groq as a hint.",
            "Privato: nomi di persone e tutto ciò che è personale, resta sempre su questo computer, per questo è preselezionato. Generale: marchi, aziende, luoghi, termini tecnici, va anche a Groq come suggerimento."),
        ("wordfix.remember", "Merken", "Remember", "Memorizza"),

        // Meldungen. {0} und {1} sind Wörter aus dem Text oder der Liste, sie werden nie übersetzt (Regel 12).
        ("wordfix.select_first", "Bitte zuerst oben das falsche Wort anklicken.", "Please click the wrong word above first.",
            "Prima fai clic sulla parola sbagliata qui sopra."),
        ("wordfix.added_same", "„{0}“ steht jetzt im Wörterbuch.", "“{0}” is now in the dictionary.", "«{0}» ora è nel dizionario."),
        ("wordfix.added", "Gemerkt: Aus „{0}“ wird ab dem nächsten Diktat „{1}“.",
            "Remembered: from the next dictation on, “{0}” becomes “{1}”.",
            "Memorizzato: dal prossimo dettato «{0}» diventa «{1}»."),
        ("wordfix.unchanged", "Steht schon als „{0}“ im Wörterbuch. Zum Ändern in den Einstellungen erst entfernen.",
            "Already in the dictionary as “{0}”. To change it, remove it in Settings first.",
            "È già nel dizionario come «{0}». Per modificarlo, rimuovilo prima nelle Impostazioni."),
        ("wordfix.failed", "Das ließ sich nicht merken.", "That could not be remembered.", "Non è stato possibile memorizzarlo."),
    ];
}
