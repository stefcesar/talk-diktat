// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Localization;

/// <summary>
/// Die Sprachen der Oberfläche (Playbook Abschnitt 29, Regeln 1, 2 und 10). Unabhängig von der Diktiersprache.
/// </summary>
public static class UiLanguages
{
    /// <summary>Die Oberflächensprachen in fester Reihenfolge: de, en, it.</summary>
    public static IReadOnlyList<string> All { get; } = ["de", "en", "it"];

    /// <summary>Sprache, wenn weder Einstellung noch Systemsprache passt.</summary>
    public const string Fallback = "en";

    /// <summary>
    /// Die gültige Oberflächensprache (Regel 2): zuerst die Einstellung, dann die Systemsprache (Teil vor dem ersten
    /// Minus oder Unterstrich), sonst Englisch. Wirft nie.
    /// </summary>
    public static string Resolve(string? setting, string? systemCulture)
    {
        string? chosen = Normalize(setting);
        if (chosen is not null) return chosen;

        string culture = (systemCulture ?? string.Empty).Trim();
        int cut = culture.IndexOfAny(['-', '_']);
        string primary = cut >= 0 ? culture[..cut] : culture;
        return Normalize(primary) ?? Fallback;
    }

    /// <summary>Der Wert getrimmt und klein, wenn er eine der drei Sprachen ist, sonst null.</summary>
    public static string? Normalize(string? code)
    {
        string value = (code ?? string.Empty).Trim().ToLowerInvariant();
        return All.Contains(value) ? value : null;
    }

    /// <summary>Der Name der Sprache in ihrer eigenen Sprache (Regel 10); für Unbekanntes English.</summary>
    public static string NativeName(string? code) => Normalize(code) switch
    {
        "de" => "Deutsch",
        "it" => "Italiano",
        _ => "English",
    };
}
