// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Text;

namespace Talk.Core.Localization;

/// <summary>
/// Die Übersetzungstabelle der Oberfläche (Playbook Abschnitt 29, Regeln 4 bis 7). Je Schlüssel drei Fassungen: de, en, it.
/// Die deutsche Fassung ist der heutige Text Wort für Wort (Regel 13).
/// </summary>
public static partial class UiText
{
    // Je Eintrag: Schlüssel, Deutsch, Englisch, Italienisch. Jeder Bereich hat seine eigene Datei UiText.<Bereich>.cs mit einer
    // Methode; Methoden statt Felder, damit die Reihenfolge der Initialisierung über mehrere Dateien keine Rolle spielt.
    private static (string Key, string De, string En, string It)[] CommonEntries() =>
    [
        ("common.close", "Schließen", "Close", "Chiudi"),
        ("common.device.unnamed", "Aufnahmegerät", "Recording device", "Dispositivo di registrazione"),
        ("common.save", "Speichern", "Save", "Salva"),
        ("common.delete", "Löschen", "Delete", "Elimina"),
        ("common.name_with_detail", "{0} ({1})", "{0} ({1})", "{0} ({1})"),
    ];

    private static readonly (string Key, string De, string En, string It)[] Entries =
    [
        .. CommonEntries(), .. TrayEntries(), .. OverlayEntries(), .. KeyEntries(), .. HistoryEntries(), .. WordFixEntries(),
        .. SettingsEntries(), .. LangEntries(), .. DiagEntries(), .. MsgEntries(),
    ];

    private static readonly Dictionary<string, (string De, string En, string It)> Table = BuildTable();

    /// <summary>Alle Schlüssel in der Reihenfolge der Tabelle.</summary>
    public static IReadOnlyList<string> Keys { get; } = Entries.Select(e => e.Key).ToArray();

    /// <summary>True, wenn es den Schlüssel gibt (ordinal, Groß und Klein zählt). Null und leer ergeben false.</summary>
    public static bool Has(string? key) => !string.IsNullOrEmpty(key) && Table.ContainsKey(key);

    /// <summary>
    /// Der Text in der Sprache (Regel 7). Unbekannte Sprache gilt als Englisch, unbekannter Schlüssel kommt selbst zurück,
    /// null oder leerer Schlüssel ergibt "?". Wirft nie.
    /// </summary>
    public static string Get(string? key, string? language)
    {
        if (string.IsNullOrEmpty(key)) return "?";
        if (!Table.TryGetValue(key, out (string De, string En, string It) entry)) return key;

        return (UiLanguages.Normalize(language) ?? UiLanguages.Fallback) switch
        {
            "de" => entry.De,
            "it" => entry.It,
            _ => entry.En,
        };
    }

    /// <summary>
    /// Wie <see cref="Get"/>, dann {0}, {1} usw. durch die Argumente ersetzt (Regel 7). Zu wenige Argumente oder args null
    /// ergeben die unveränderte Fassung; ein null Argument wird leerer Text. Wirft nie.
    /// </summary>
    public static string Format(string? key, string? language, params string?[]? args)
    {
        string template = Get(key, language);
        if (args is null) return template;

        StringBuilder result = new(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            if (template[i] == '{' && TryReadPlaceholder(template, i, out int number, out int end))
            {
                if (number >= args.Length) return template;
                result.Append(args[number] ?? string.Empty);
                i = end;
                continue;
            }

            result.Append(template[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Die Nummern der Platzhalter in einem Text, ohne Doppel. Null, wenn der Text gegen Regel 6 verstößt
    /// (Format oder Breite, geschweifte Klammern außerhalb von {n}). Für die Prüfung der Tabelle.
    /// </summary>
    public static IReadOnlySet<int>? PlaceholderNumbers(string? text)
    {
        HashSet<int> numbers = [];
        string value = text ?? string.Empty;
        int i = 0;
        while (i < value.Length)
        {
            char c = value[i];
            if (c == '{')
            {
                if (!TryReadPlaceholder(value, i, out int number, out int end)) return null;
                numbers.Add(number);
                i = end;
                continue;
            }

            if (c == '}') return null;
            i++;
        }

        return numbers;
    }

    // {n} mit n aus Ziffern, sonst nichts dazwischen.
    private static bool TryReadPlaceholder(string text, int start, out int number, out int end)
    {
        number = 0;
        end = start;
        int i = start + 1;
        int digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]) && digits < 6)
        {
            number = number * 10 + (text[i] - '0');
            i++;
            digits++;
        }

        if (digits == 0 || i >= text.Length || text[i] != '}') return false;
        end = i + 1;
        return true;
    }

    private static Dictionary<string, (string De, string En, string It)> BuildTable()
    {
        Dictionary<string, (string De, string En, string It)> table = new(StringComparer.Ordinal);
        foreach ((string key, string de, string en, string it) in Entries) table[key] = (de, en, it);
        return table;
    }
}
