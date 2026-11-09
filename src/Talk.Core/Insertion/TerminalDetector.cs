// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Insertion;

/// <summary>
/// Erkennt Terminals an Fensterklasse oder Prozessname (Playbook Abschnitt 17). Groß und Kleinschreibung zählt nicht.
/// Ein Terminal, das in einem anderen Programm steckt, wird nicht erkannt.
/// </summary>
public static class TerminalDetector
{
    private static readonly HashSet<string> ClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConsoleWindowClass",
        "CASCADIA_HOSTING_WINDOW_CLASS",
        "mintty",
        "PuTTY",
        "VirtualConsoleClass",
    };

    private static readonly HashSet<string> ProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsTerminal",
        "conhost",
        "cmd",
        "powershell",
        "pwsh",
        "mintty",
        "alacritty",
        "wezterm-gui",
        "putty",
        "ConEmu64",
    };

    /// <summary>
    /// True, wenn Fensterklasse oder Prozessname zu einem Terminal gehören. Ein angehängtes ".exe" am Prozessnamen stört nicht.
    /// </summary>
    public static bool IsTerminal(string? processName, string? className)
    {
        if (!string.IsNullOrWhiteSpace(className) && ClassNames.Contains(className.Trim())) return true;
        if (string.IsNullOrWhiteSpace(processName)) return false;

        string name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return ProcessNames.Contains(name);
    }
}
