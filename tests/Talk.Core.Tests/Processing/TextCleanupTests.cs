// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft das Aufräumen des Leerraums ausschließlich gegen PLAYBOOK.md, Abschnitt 16, "Regeln der reinen Logik",
/// Punkt 7 (Leerraum) mit den dort genannten Beispielen. Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class TextCleanupTests
{
    // Die beiden Beispiele aus dem Playbook, wörtlich.
    [Fact]
    public void Beispiel_Leerzeichen_vor_Komma_und_Fragezeichen_entfallen()
    {
        Assert.Equal("Hallo, wie geht es?", TextCleanup.Clean("Hallo  ,  wie geht es ?"));
    }

    [Fact]
    public void Beispiel_Punkt_vor_Buchstabe_bleibt_mit_seinem_Leerzeichen()
    {
        Assert.Equal("das .NET SDK", TextCleanup.Clean("das .NET SDK"));
    }

    // Regel 7: Jede Folge von Leerraum wird ein Leerzeichen.
    [Theory]
    [InlineData("Hallo  Welt", "Hallo Welt")]
    [InlineData("Hallo     Welt", "Hallo Welt")]
    [InlineData("Hallo\tWelt", "Hallo Welt")]
    [InlineData("Hallo\nWelt", "Hallo Welt")]
    [InlineData("Hallo\r\nWelt", "Hallo Welt")]
    [InlineData("Hallo \t\r\n Welt", "Hallo Welt")]
    [InlineData("eins  zwei\t\tdrei\n\nvier", "eins zwei drei vier")]
    [InlineData("Zeile eins.\r\n\r\nZeile zwei.", "Zeile eins. Zeile zwei.")]
    public void Jede_Folge_von_Leerraum_wird_ein_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, TextCleanup.Clean(text));
    }

    // Regel 7: Ränder ohne Leerraum.
    [Theory]
    [InlineData(" Hallo", "Hallo")]
    [InlineData("Hallo ", "Hallo")]
    [InlineData("   Hallo Welt.   ", "Hallo Welt.")]
    [InlineData("\tHallo Welt.\r\n", "Hallo Welt.")]
    [InlineData("\r\n\r\nHallo\r\n\r\n", "Hallo")]
    public void Raender_ohne_Leerraum(string text, string erwartet)
    {
        Assert.Equal(erwartet, TextCleanup.Clean(text));
    }

    // Regel 7: Ein Leerzeichen vor Komma, Punkt, Semikolon, Doppelpunkt, Ausrufezeichen, Fragezeichen entfällt, wenn Leerraum folgt.
    [Theory]
    [InlineData("Hallo , Welt", "Hallo, Welt")]
    [InlineData("Hallo . Welt", "Hallo. Welt")]
    [InlineData("Hallo ; Welt", "Hallo; Welt")]
    [InlineData("Hallo : Welt", "Hallo: Welt")]
    [InlineData("Hallo ! Welt", "Hallo! Welt")]
    [InlineData("Hallo ? Welt", "Hallo? Welt")]
    [InlineData("Hallo   ,   Welt", "Hallo, Welt")]
    [InlineData("Hallo\t,\tWelt", "Hallo, Welt")]
    public void Leerzeichen_vor_Satzzeichen_entfaellt_wenn_Leerraum_folgt(string text, string erwartet)
    {
        Assert.Equal(erwartet, TextCleanup.Clean(text));
    }

    // Regel 7: Dasselbe gilt, wenn das Zeichen am Ende steht.
    [Theory]
    [InlineData("Hallo Welt ,", "Hallo Welt,")]
    [InlineData("Hallo Welt .", "Hallo Welt.")]
    [InlineData("Hallo Welt ;", "Hallo Welt;")]
    [InlineData("Hallo Welt :", "Hallo Welt:")]
    [InlineData("Hallo Welt !", "Hallo Welt!")]
    [InlineData("Hallo Welt ?", "Hallo Welt?")]
    [InlineData("Hallo Welt   .", "Hallo Welt.")]
    [InlineData("Hallo Welt .   ", "Hallo Welt.")]
    public void Leerzeichen_vor_Satzzeichen_entfaellt_am_Ende(string text, string erwartet)
    {
        Assert.Equal(erwartet, TextCleanup.Clean(text));
    }

    // Regel 7: Folgt auf das Zeichen kein Leerraum und steht es nicht am Ende, bleibt das Leerzeichen davor stehen.
    [Theory]
    [InlineData("das .NET SDK ist da")]
    [InlineData("die Datei .gitignore fehlt")]
    [InlineData("um 12 :30 Uhr")]
    [InlineData("eins ,zwei")]
    [InlineData("der Schalter !wichtig bleibt")]
    [InlineData("die Frage ?was bleibt")]
    public void Leerzeichen_vor_Satzzeichen_bleibt_wenn_kein_Leerraum_folgt(string text)
    {
        Assert.Equal(text, TextCleanup.Clean(text));
    }

    // Regel 7 nennt sechs Zeichen. Ein Leerzeichen vor anderen Zeichen bleibt stehen.
    [Theory]
    [InlineData("eins ( zwei ) drei")]
    [InlineData("eins \" zwei \" drei")]
    [InlineData("eins / zwei")]
    [InlineData("eins + zwei = drei")]
    public void Leerzeichen_vor_anderen_Zeichen_bleibt(string text)
    {
        Assert.Equal(text, TextCleanup.Clean(text));
    }

    // Regel 7: Mehrere Stellen in einem Text.
    [Theory]
    [InlineData("  Erstens , zweitens ; drittens : fertig !  ", "Erstens, zweitens; drittens: fertig!")]
    [InlineData("Ja . Nein . Doch ?", "Ja. Nein. Doch?")]
    [InlineData("Das .NET SDK , Version 10 .", "Das .NET SDK, Version 10.")]
    public void Mehrere_Stellen_werden_aufgeraeumt(string text, string erwartet)
    {
        Assert.Equal(erwartet, TextCleanup.Clean(text));
    }

    // Regel 7: Der Wortlaut ändert sich nicht. Ein schon sauberer Text bleibt, wie er ist.
    [Theory]
    [InlineData("Hallo, wie geht es?")]
    [InlineData("Größe, Maß und Übung: ÄÖÜ äöü ß.")]
    [InlineData("Version 1.5, um 12:30 Uhr; fertig!")]
    [InlineData("a")]
    [InlineData("7")]
    public void Sauberer_Text_bleibt(string text)
    {
        Assert.Equal(text, TextCleanup.Clean(text));
    }

    // Regel 7: ohne Buchstabe und Ziffer leer.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData(".")]
    [InlineData("...")]
    [InlineData("\u2026")]
    [InlineData(" , . ! ")]
    [InlineData("?!")]
    [InlineData("( )")]
    [InlineData("\"\"")]
    public void Ohne_Buchstabe_und_Ziffer_leer(string text)
    {
        Assert.Equal(string.Empty, TextCleanup.Clean(text));
    }

    // Regel 7: null leer.
    [Fact]
    public void Null_ergibt_leer()
    {
        Assert.Equal(string.Empty, TextCleanup.Clean(null));
    }

    // Regel 7: Ein Buchstabe oder eine Ziffer reicht, damit der Text bleibt.
    [Theory]
    [InlineData(" 3 . ", "3.")]
    [InlineData(" a ! ", "a!")]
    [InlineData("  ... ä  ", "... ä")]
    public void Ein_Buchstabe_oder_eine_Ziffer_reicht(string text, string erwartet)
    {
        Assert.Equal(erwartet, TextCleanup.Clean(text));
    }
}
