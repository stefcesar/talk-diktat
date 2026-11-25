// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

// Bereich lang (Namen der Diktiersprachen und Dialekte) (Playbook Abschnitt 29). Deutsch Wort für Wort wie heute (Regel 13).
// Schlüssel je Code aus settings.json, der Bindestrich wird zum Unterstrich (Regel 5): de-ch ergibt lang.de_ch, Dialekt zh ergibt lang.zh.
public static partial class UiText
{
    private static (string Key, string De, string En, string It)[] LangEntries() =>
    [
        // Diktiersprachen
        ("lang.de", "Deutsch", "German", "Tedesco"),
        ("lang.it", "Italienisch", "Italian", "Italiano"),
        ("lang.en", "Englisch", "English", "Inglese"),
        ("lang.de_ch", "Schweizerdeutsch", "Swiss German", "Svizzero tedesco"),
        ("lang.gsw", "Mundart", "Swiss dialect", "Dialetto svizzero"),

        // Dialekte bei Mundart
        ("lang.zh", "Zürich", "Zurich", "Zurigo"),
        ("lang.be", "Bern", "Bern", "Berna"),
        ("lang.bs", "Basel", "Basel", "Basilea"),
        ("lang.mix", "gemischt", "mixed", "misto"),
    ];
}
