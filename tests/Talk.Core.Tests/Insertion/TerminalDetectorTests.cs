// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Insertion;
using Xunit;

namespace Talk.Core.Tests.Insertion;

/// <summary>
/// Prüft die Terminalerkennung ausschließlich gegen PLAYBOOK.md, Abschnitt 17, "Erkennung der Sonderfälle", Punkt 3.
/// Die Tests kennen die Umsetzung nicht. Die Namen stehen hier so, wie das Playbook sie aufzählt.
/// </summary>
public sealed class TerminalDetectorTests
{
    private const string FremdeKlasse = "Notepad";
    private const string FremderProzess = "notepad";

    // Punkt 3: jede der fünf Fensterklassen, bei einem Prozess, der selbst kein Terminal ist.
    [Theory]
    [InlineData("ConsoleWindowClass")]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS")]
    [InlineData("mintty")]
    [InlineData("PuTTY")]
    [InlineData("VirtualConsoleClass")]
    public void Erkennt_jede_Fensterklasse(string klasse)
    {
        Assert.True(TerminalDetector.IsTerminal(FremderProzess, klasse));
    }

    // Punkt 3: jeder der zehn Prozessnamen, bei einer Fensterklasse, die selbst kein Terminal ist.
    [Theory]
    [InlineData("WindowsTerminal")]
    [InlineData("conhost")]
    [InlineData("cmd")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("mintty")]
    [InlineData("alacritty")]
    [InlineData("wezterm-gui")]
    [InlineData("putty")]
    [InlineData("ConEmu64")]
    public void Erkennt_jeden_Prozessnamen(string prozess)
    {
        Assert.True(TerminalDetector.IsTerminal(prozess, FremdeKlasse));
    }

    // Punkt 3: Groß und Kleinschreibung zählt nicht, bei den Fensterklassen.
    [Theory]
    [InlineData("consolewindowclass")]
    [InlineData("CONSOLEWINDOWCLASS")]
    [InlineData("cascadia_hosting_window_class")]
    [InlineData("Cascadia_Hosting_Window_Class")]
    [InlineData("MINTTY")]
    [InlineData("Mintty")]
    [InlineData("putty")]
    [InlineData("PUTTY")]
    [InlineData("virtualconsoleclass")]
    [InlineData("VIRTUALCONSOLECLASS")]
    public void Fensterklasse_ohne_Ruecksicht_auf_Schreibung(string klasse)
    {
        Assert.True(TerminalDetector.IsTerminal(FremderProzess, klasse));
    }

    // Punkt 3: Groß und Kleinschreibung zählt nicht, bei den Prozessnamen.
    [Theory]
    [InlineData("windowsterminal")]
    [InlineData("WINDOWSTERMINAL")]
    [InlineData("Conhost")]
    [InlineData("CONHOST")]
    [InlineData("CMD")]
    [InlineData("Cmd")]
    [InlineData("PowerShell")]
    [InlineData("POWERSHELL")]
    [InlineData("PWSH")]
    [InlineData("Mintty")]
    [InlineData("Alacritty")]
    [InlineData("ALACRITTY")]
    [InlineData("WezTerm-GUI")]
    [InlineData("WEZTERM-GUI")]
    [InlineData("PuTTY")]
    [InlineData("PUTTY")]
    [InlineData("conemu64")]
    [InlineData("CONEMU64")]
    public void Prozessname_ohne_Ruecksicht_auf_Schreibung(string prozess)
    {
        Assert.True(TerminalDetector.IsTerminal(prozess, FremdeKlasse));
    }

    // Punkt 3: Fensterklasse ODER Prozessname genügt. Eines davon darf fehlen.
    [Fact]
    public void Eines_von_beiden_genuegt()
    {
        Assert.True(TerminalDetector.IsTerminal(null, "ConsoleWindowClass"));
        Assert.True(TerminalDetector.IsTerminal("", "ConsoleWindowClass"));
        Assert.True(TerminalDetector.IsTerminal("cmd", null));
        Assert.True(TerminalDetector.IsTerminal("cmd", ""));
        Assert.True(TerminalDetector.IsTerminal("WindowsTerminal", "CASCADIA_HOSTING_WINDOW_CLASS"));
    }

    // Punkt 3, letzter Satz: Ein Terminal in einem anderen Programm (zum Beispiel im Editor) wird nicht erkannt.
    [Theory]
    [InlineData("Code", "Chrome_WidgetWin_1")]
    [InlineData("devenv", "HwndWrapper")]
    [InlineData("rider64", "SunAwtFrame")]
    public void Terminal_in_einem_anderen_Programm_wird_nicht_erkannt(string prozess, string klasse)
    {
        Assert.False(TerminalDetector.IsTerminal(prozess, klasse));
    }

    // Punkt 3: Gewöhnliche Programme sind kein Terminal.
    [Theory]
    [InlineData("notepad", "Notepad")]
    [InlineData("chrome", "Chrome_WidgetWin_1")]
    [InlineData("WINWORD", "OpusApp")]
    [InlineData("explorer", "CabinetWClass")]
    [InlineData("WhatsApp", "ApplicationFrameWindow")]
    [InlineData("Talk", "HwndWrapper")]
    public void Gewoehnliche_Programme_sind_kein_Terminal(string prozess, string klasse)
    {
        Assert.False(TerminalDetector.IsTerminal(prozess, klasse));
    }

    // Punkt 3: Die beiden Listen sind getrennt. Ein Name, der nur als Prozessname genannt ist, zählt nicht als Fensterklasse.
    [Theory]
    [InlineData("WindowsTerminal")]
    [InlineData("conhost")]
    [InlineData("cmd")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("alacritty")]
    [InlineData("wezterm-gui")]
    [InlineData("ConEmu64")]
    public void Prozessname_als_Fensterklasse_zaehlt_nicht(string name)
    {
        Assert.False(TerminalDetector.IsTerminal(FremderProzess, name));
    }

    // Punkt 3: Die beiden Listen sind getrennt. Ein Name, der nur als Fensterklasse genannt ist, zählt nicht als Prozessname.
    [Theory]
    [InlineData("ConsoleWindowClass")]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS")]
    [InlineData("VirtualConsoleClass")]
    public void Fensterklasse_als_Prozessname_zaehlt_nicht(string name)
    {
        Assert.False(TerminalDetector.IsTerminal(name, FremdeKlasse));
    }

    // Punkt 3: Nur die genannten Namen zählen, kein Teilwort und kein längerer Name.
    [Theory]
    [InlineData("cmder", "Notepad")]
    [InlineData("mycmd", "Notepad")]
    [InlineData("powershell_ise", "Notepad")]
    [InlineData("ConEmu", "Notepad")]
    [InlineData("wezterm", "Notepad")]
    [InlineData("notepad", "ConsoleWindowClass2")]
    [InlineData("notepad", "ConsoleWindow")]
    [InlineData("notepad", "PuTTYConfigBox")]
    public void Aehnliche_Namen_zaehlen_nicht(string prozess, string klasse)
    {
        Assert.False(TerminalDetector.IsTerminal(prozess, klasse));
    }

    // Punkt 3: Ohne Fensterklasse und ohne Prozessname gibt es nichts, woran ein Terminal erkannt würde.
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("", null)]
    [InlineData("   ", "   ")]
    public void Ohne_Angaben_kein_Terminal(string? prozess, string? klasse)
    {
        Assert.False(TerminalDetector.IsTerminal(prozess, klasse));
    }
}
