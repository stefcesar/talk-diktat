// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Windows.Markup;

using Talk.Core.Localization;

namespace Talk.App.Ui;

/// <summary>
/// Die aktuelle Sprache der Oberfläche (Playbook Abschnitt 29). Fenster holen ihre Texte beim Öffnen; das Tray Menü baut sich
/// bei <see cref="Changed"/> neu (Regel 11).
/// </summary>
internal static class Lang
{
    /// <summary>Die gültige Oberflächensprache: de, en oder it.</summary>
    public static string Current { get; private set; } = UiLanguages.Resolve(null, CultureInfo.CurrentUICulture.Name);

    /// <summary>Kommt, wenn sich <see cref="Current"/> geändert hat.</summary>
    public static event Action? Changed;

    /// <summary>
    /// Setzt die Sprache aus der Einstellung (null heißt: Anzeigesprache von Windows, Regel 2). True, wenn sie sich geändert hat.
    /// </summary>
    public static bool Apply(string? setting)
    {
        string resolved = UiLanguages.Resolve(setting, CultureInfo.CurrentUICulture.Name);
        if (resolved == Current) return false;
        Current = resolved;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Der Text zum Schlüssel in der aktuellen Sprache.</summary>
    public static string T(string key) => UiText.Get(key, Current);

    /// <summary>Der Text mit eingesetzten Werten. Zahlen und Daten vorher mit <see cref="Culture"/> formatieren (Regel 8).</summary>
    public static string F(string key, params string?[] args) => UiText.Format(key, Current, args);

    /// <summary>Die Kultur für Zahlen, Uhrzeiten und Daten in der aktuellen Sprache (Regel 8).</summary>
    public static CultureInfo Culture => Current switch
    {
        "de" => CultureInfo.GetCultureInfo("de-DE"),
        "it" => CultureInfo.GetCultureInfo("it-IT"),
        _ => CultureInfo.GetCultureInfo("en-GB"),
    };
}

/// <summary>
/// XAML: <c>Text="{ui:T settings.title}"</c> setzt den Text zum Schlüssel in der aktuellen Sprache, einmal beim Laden des Fensters.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension()
    {
    }

    public TExtension(string key)
    {
        Key = key;
    }

    /// <summary>Der Schlüssel aus der Übersetzungstabelle.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Lang.T(Key);
}
