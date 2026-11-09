// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Insertion;
using Xunit;

namespace Talk.Core.Tests.Insertion;

/// <summary>
/// Prüft die Textregel fürs Terminal ausschließlich gegen PLAYBOOK.md, Abschnitt 17, "Regeln für den Text", Punkt 5,
/// zusammen mit den Punkten 1, 2 und 4, die danach gelten. Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class InsertTextTerminalTests
{
    // ---------- Die Beispiele aus dem Playbook, wörtlich ----------

    [Theory]
    [InlineData("Echo hallo.", "echo hallo ")]
    [InlineData("Git status!", "git status ")]
    [InlineData("LUM\u00CDORA starten.", "LUM\u00CDORA starten ")]
    [InlineData("Dotnet build ...", "dotnet build ")]
    public void Beispiel_aus_dem_Playbook(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // ---------- Satzzeichen am Textende ----------

    // Regel 5: Punkt, Ausrufezeichen, Fragezeichen und Auslassungszeichen am Textende entfallen.
    [Theory]
    [InlineData("git status.")]
    [InlineData("git status!")]
    [InlineData("git status?")]
    [InlineData("git status\u2026")]
    public void Ein_Satzzeichen_am_Textende_entfaellt(string text)
    {
        Assert.Equal("git status ", InsertText.ForTerminal(text));
    }

    // Regel 5: auch mehrere Satzzeichen am Textende entfallen.
    [Theory]
    [InlineData("git status...")]
    [InlineData("git status!!!")]
    [InlineData("git status?!")]
    [InlineData("git status!?.")]
    [InlineData("git status.\u2026")]
    [InlineData("git status\u2026\u2026")]
    public void Mehrere_Satzzeichen_am_Textende_entfallen(string text)
    {
        Assert.Equal("git status ", InsertText.ForTerminal(text));
    }

    // Regel 5: auch mit Leerraum dazwischen.
    [Theory]
    [InlineData("git status .")]
    [InlineData("git status ...")]
    [InlineData("git status . . .")]
    [InlineData("git status ! ?")]
    [InlineData("git status. \u2026")]
    [InlineData("git status  .  !")]
    public void Satzzeichen_mit_Leerraum_dazwischen_entfallen(string text)
    {
        Assert.Equal("git status ", InsertText.ForTerminal(text));
    }

    // Regel 5 mit Regel 1: Steht hinter den Satzzeichen noch Leerraum, entfallen sie ebenso, und es bleibt bei einem Leerzeichen am Ende.
    [Theory]
    [InlineData("git status. ")]
    [InlineData("git status.  ")]
    [InlineData("git status ... ")]
    [InlineData("git status!\n")]
    [InlineData("git status.\r\n")]
    public void Satzzeichen_vor_Leerraum_am_Textende_entfallen(string text)
    {
        Assert.Equal("git status ", InsertText.ForTerminal(text));
    }

    // Regel 5 nennt vier Zeichen. Andere Zeichen am Textende bleiben stehen.
    [Theory]
    [InlineData("echo hallo,", "echo hallo, ")]
    [InlineData("echo hallo;", "echo hallo; ")]
    [InlineData("echo hallo:", "echo hallo: ")]
    [InlineData("echo \"hallo.\"", "echo \"hallo.\" ")]
    [InlineData("echo (hallo!)", "echo (hallo!) ")]
    [InlineData("cd /tmp/", "cd /tmp/ ")]
    public void Andere_Zeichen_am_Textende_bleiben(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // Regel 5: Satzzeichen mitten im Text bleiben.
    [Theory]
    [InlineData("echo hallo. welt.", "echo hallo. welt ")]
    [InlineData("echo hallo! und tschüss?", "echo hallo! und tschüss ")]
    [InlineData("ping 127.0.0.1.", "ping 127.0.0.1 ")]
    [InlineData("dotnet build Talk.slnx", "dotnet build Talk.slnx ")]
    [InlineData("echo was? wie... so.", "echo was? wie... so ")]
    [InlineData("git commit, dann push!", "git commit, dann push ")]
    public void Satzzeichen_mitten_im_Text_bleiben(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // ---------- Das erste Wort ----------

    // Regel 5: Das erste Wort beginnt klein, wenn nur sein erster Buchstabe groß ist.
    [Theory]
    [InlineData("Echo hallo", "echo hallo ")]
    [InlineData("Git status", "git status ")]
    [InlineData("Ls", "ls ")]
    [InlineData("Dotnet", "dotnet ")]
    [InlineData("Öffne die Datei", "öffne die Datei ")]
    [InlineData("Ändere das", "ändere das ")]
    [InlineData("A b c", "a b c ")]
    public void Erstes_Wort_beginnt_klein(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // Regel 5: Ein Wort mit weiteren Großbuchstaben bleibt, wie es ist. Die Beispiele aus dem Playbook: LUMIORA mit Akzent und beKEY.
    [Theory]
    [InlineData("LUM\u00CDORA starten", "LUM\u00CDORA starten ")]
    [InlineData("beKEY starten", "beKEY starten ")]
    [InlineData("DotNet build", "DotNet build ")]
    [InlineData("GIT status", "GIT status ")]
    [InlineData("PowerShell starten.", "PowerShell starten ")]
    [InlineData("LS", "LS ")]
    public void Erstes_Wort_mit_weiteren_Grossbuchstaben_bleibt(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // Regel 5: Ein schon klein geschriebenes erstes Wort bleibt.
    [Theory]
    [InlineData("echo hallo", "echo hallo ")]
    [InlineData("git status", "git status ")]
    [InlineData("öffne die Datei", "öffne die Datei ")]
    public void Kleines_erstes_Wort_bleibt(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // Regel 5 spricht nur vom ersten Wort. Alle weiteren Wörter behalten ihre Großschreibung.
    [Theory]
    [InlineData("Git Status Jetzt.", "git Status Jetzt ")]
    [InlineData("Echo Hallo Welt!", "echo Hallo Welt ")]
    [InlineData("Öffne Datei Bericht.txt", "öffne Datei Bericht.txt ")]
    [InlineData("Echo hallo. Welt.", "echo hallo. Welt ")]
    public void Nur_das_erste_Wort_wird_klein(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // ---------- Bleibt nichts übrig ----------

    // Regel 5: Bleibt danach nichts übrig, wird nichts eingefügt.
    [Theory]
    [InlineData(".")]
    [InlineData("!")]
    [InlineData("?")]
    [InlineData("\u2026")]
    [InlineData("...")]
    [InlineData("?!")]
    [InlineData(". . .")]
    [InlineData(" ... ")]
    [InlineData("  !  ?  ")]
    public void Nur_Satzzeichen_ergibt_leer(string text)
    {
        string ergebnis = InsertText.ForTerminal(text);

        Assert.Equal(string.Empty, ergebnis);
        Assert.False(InsertText.IsInsertable(ergebnis));
    }

    // Regel 4: Ein leerer Text oder einer nur aus Leerraum wird nie eingefügt.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n\r\n")]
    public void Ohne_Inhalt_ergibt_leer(string? text)
    {
        Assert.Equal(string.Empty, InsertText.ForTerminal(text));
    }

    // ---------- Danach gelten Regel 1 und Regel 2 ----------

    // Regel 1: An den Text wird ein Leerzeichen angehängt. Endet er schon mit Leerraum, kommt keines dazu.
    [Theory]
    [InlineData("git status", "git status ")]
    [InlineData("git status ", "git status ")]
    [InlineData("ls", "ls ")]
    public void Leerzeichen_am_Ende(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // Regel 1: Zwei Diktate hintereinander kleben nicht zusammen.
    [Fact]
    public void Zwei_Diktate_kleben_nicht_zusammen()
    {
        string zusammen = InsertText.ForTerminal("Git add.") + InsertText.ForTerminal("Git commit.");

        Assert.Equal("git add git commit ", zusammen);
    }

    // Regel 2 gilt nach Regel 5: Zeilenumbrüche und andere Steuerzeichen werden zu einem Leerzeichen.
    [Theory]
    [InlineData("Echo eins\nzwei.", "echo eins zwei ")]
    [InlineData("Echo eins\r\nzwei.", "echo eins zwei ")]
    [InlineData("Echo eins\r\n\r\nzwei.", "echo eins zwei ")]
    [InlineData("Echo\thallo.", "echo hallo ")]
    [InlineData("Echo eins.\nEcho zwei.", "echo eins. Echo zwei ")]
    public void Umbrueche_und_Steuerzeichen_werden_zu_einem_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }

    // Regel 2, der Zweck: Im Terminal steht nie ein Umbruch und kein anderes Steuerzeichen, denn das würde einen Befehl abschicken.
    [Theory]
    [InlineData("Rm alles.\n")]
    [InlineData("Rm alles\r\n")]
    [InlineData("\nRm alles.")]
    [InlineData("Rm\nalles\n.\n")]
    [InlineData("Echo a\tb.")]
    [InlineData("Echo a\u0003b.")]
    [InlineData("Echo a\u001Bb.")]
    public void Ergebnis_enthaelt_nie_ein_Steuerzeichen(string text)
    {
        string ergebnis = InsertText.ForTerminal(text);

        foreach (char zeichen in ergebnis)
        {
            Assert.False(char.IsControl(zeichen));
        }

        Assert.DoesNotContain(".", ergebnis);
        Assert.EndsWith(" ", ergebnis);
    }

    // Regel 5 im Vergleich mit Regel 2 allein: Tippen behält den Satz, das Terminal bekommt den Befehl.
    [Fact]
    public void Terminal_schreibt_Befehl_Tippen_schreibt_Satz()
    {
        const string text = "Echo hallo.";

        Assert.Equal("Echo hallo. ", InsertText.ForTyping(text));
        Assert.Equal("echo hallo ", InsertText.ForTerminal(text));
    }
}
