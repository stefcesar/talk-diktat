// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Microsoft.Win32;

using Talk.App.Diagnostics;

namespace Talk.App.Settings;

/// <summary>
/// Autostart mit Windows über den Benutzereintrag in der Registrierung (Playbook Abschnitt 4 Punkt 11): ein Wert "Talk" unter
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> mit dem Pfad der Programmdatei in Anführungszeichen.
/// Braucht keine Administratorrechte und gilt nur für dieses Windows Konto. Geschrieben wird nur, wenn der Nutzer den Haken
/// in den Einstellungen setzt oder entfernt, oder wenn er es im Installer wählt.
/// </summary>
internal static class Autostart
{
    /// <summary>Der Schlüssel, aus dem Windows beim Anmelden Programme startet.</summary>
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Der Name des Werts.</summary>
    public const string ValueName = "Talk";

    /// <summary>Der Pfad der laufenden Programmdatei, oder null, wenn er sich nicht bestimmen lässt.</summary>
    public static string? CurrentExePath => Environment.ProcessPath;

    /// <summary>
    /// True, wenn ein Eintrag für Talk vorhanden ist. Wirft nie.
    /// </summary>
    public static bool IsEnabled(string keyPath = RunKeyPath, string valueName = ValueName)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string value && value.Trim().Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// True, wenn der Eintrag auf genau diese Programmdatei zeigt. Zeigt er woandershin (alte Kopie, anderer Ordner), startet Windows
    /// nicht diese Fassung.
    /// </summary>
    public static bool PointsTo(string exePath, string keyPath = RunKeyPath, string valueName = ValueName)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string value
                && string.Equals(value.Trim(), Command(exePath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Setzt oder entfernt den Eintrag. Liefert false, wenn es nicht gelungen ist. Wirft nie. Im Protokoll steht nur an oder aus,
    /// nie der Pfad.
    /// </summary>
    public static bool Set(bool enabled, string? exePath, string keyPath = RunKeyPath, string valueName = ValueName)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            if (enabled)
            {
                if (string.IsNullOrWhiteSpace(exePath)) return false;
                key.SetValue(valueName, Command(exePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }

            AppLog.Write("autostart", "set", enabled ? "zustand=an" : "zustand=aus");
            return true;
        }
        catch (Exception)
        {
            AppLog.Write("autostart", "error", "grund=schreiben");
            return false;
        }
    }

    // Der Pfad in Anführungszeichen, damit Leerzeichen im Pfad Windows nicht verwirren.
    private static string Command(string exePath) => $"\"{exePath}\"";
}