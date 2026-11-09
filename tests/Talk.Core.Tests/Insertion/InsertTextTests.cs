using Talk.Core.Insertion;
using Xunit;

namespace Talk.Core.Tests.Insertion;

/// <summary>
/// Prüft die Regeln für den Text ausschließlich gegen PLAYBOOK.md, Abschnitt 17, "Regeln für den Text".
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class InsertTextTests
{
    // ---------- IsInsertable ----------

    // Regel 4: Ein leerer Text oder einer nur aus Leerraum wird nie eingefügt.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \t\r\n ")]
    public void IsInsertable_leer_oder_nur_Leerraum_ergibt_falsch(string? text)
    {
        Assert.False(InsertText.IsInsertable(text));
    }

    // Regel 4, Umkehrung: Jeder Text mit Inhalt wird eingefügt, auch mit Leerraum außen herum.
    [Theory]
    [InlineData("a")]
    [InlineData("Hallo Welt")]
    [InlineData("  Hallo  ")]
    [InlineData("\nHallo\n")]
    [InlineData(".")]
    [InlineData("1")]
    public void IsInsertable_Text_mit_Inhalt_ergibt_wahr(string text)
    {
        Assert.True(InsertText.IsInsertable(text));
    }

    // ---------- ForClipboard ----------

    // Regel 1: An den Text wird ein Leerzeichen angehängt.
    [Theory]
    [InlineData("Hallo", "Hallo ")]
    [InlineData("Hallo Welt.", "Hallo Welt. ")]
    [InlineData("a", "a ")]
    public void ForClipboard_haengt_ein_Leerzeichen_an(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForClipboard(text));
    }

    // Regel 1: Endet der Text schon mit Leerraum, kommt keines dazu.
    // Regel 3: Der Text bleibt dabei unverändert, auch ein Umbruch am Ende.
    [Theory]
    [InlineData("Hallo ")]
    [InlineData("Hallo  ")]
    [InlineData("Hallo\t")]
    [InlineData("Hallo\n")]
    [InlineData("Hallo\r\n")]
    public void ForClipboard_haengt_nach_Leerraum_nichts_an(string text)
    {
        Assert.Equal(text, InsertText.ForClipboard(text));
    }

    // Regel 3: Beim Einfügen über die Zwischenablage bleibt der Text unverändert, Zeilenumbrüche bleiben stehen.
    [Theory]
    [InlineData("Zeile eins\nZeile zwei")]
    [InlineData("Zeile eins\r\nZeile zwei")]
    [InlineData("Absatz eins\r\n\r\nAbsatz zwei")]
    [InlineData("Zwei  Leerzeichen\tund ein Tabulator")]
    [InlineData("  Leerraum am Anfang")]
    [InlineData("\nUmbruch am Anfang")]
    [InlineData("Größe, Maß und Übung: ÄÖÜ äöü ß")]
    public void ForClipboard_laesst_den_Text_unveraendert(string text)
    {
        Assert.Equal(text + " ", InsertText.ForClipboard(text));
    }

    // Regel 1: Zwei Diktate hintereinander kleben nicht zusammen.
    [Fact]
    public void ForClipboard_zwei_Diktate_kleben_nicht_zusammen()
    {
        var zusammen = InsertText.ForClipboard("Hallo") + InsertText.ForClipboard("Welt");

        Assert.Equal("Hallo Welt ", zusammen);
    }

    // Regel 4: Ein leerer Text oder einer nur aus Leerraum wird nie eingefügt, es bleibt also nichts zum Einfügen übrig.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void ForClipboard_ohne_Inhalt_ergibt_leer(string? text)
    {
        Assert.Equal(string.Empty, InsertText.ForClipboard(text));
    }

    // ---------- ForTyping ----------

    // Regel 2: Beim Tippen werden Zeilenumbrüche zu einem Leerzeichen. Regel 1: Am Ende kommt ein Leerzeichen dazu.
    [Theory]
    [InlineData("eins\nzwei", "eins zwei ")]
    [InlineData("eins\r\nzwei", "eins zwei ")]
    [InlineData("eins\rzwei", "eins zwei ")]
    [InlineData("eins\nzwei\ndrei", "eins zwei drei ")]
    [InlineData("eins\r\nzwei\r\ndrei", "eins zwei drei ")]
    public void ForTyping_macht_aus_einem_Umbruch_ein_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    // Regel 2: Mehrere Umbrüche hintereinander ergeben ein einziges Leerzeichen.
    [Theory]
    [InlineData("eins\n\nzwei", "eins zwei ")]
    [InlineData("eins\n\n\n\nzwei", "eins zwei ")]
    [InlineData("eins\r\n\r\nzwei", "eins zwei ")]
    [InlineData("eins\r\n\r\n\r\nzwei", "eins zwei ")]
    [InlineData("eins\n\r\n\rzwei", "eins zwei ")]
    [InlineData("eins\n\nzwei\r\n\r\ndrei", "eins zwei drei ")]
    public void ForTyping_macht_aus_mehreren_Umbruechen_ein_einziges_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    // Regel 2 und Regel 1: Ein Umbruch am Ende wird zu einem Leerzeichen, danach endet der Text schon mit Leerraum.
    [Theory]
    [InlineData("Hallo\n", "Hallo ")]
    [InlineData("Hallo\r\n", "Hallo ")]
    [InlineData("Hallo\n\n\n", "Hallo ")]
    [InlineData("Hallo\r\n\r\n", "Hallo ")]
    public void ForTyping_Umbruch_am_Ende_ergibt_genau_ein_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    // Regel 2: Ein Umbruch am Anfang wird ebenfalls zu einem Leerzeichen und schickt nichts ab.
    [Theory]
    [InlineData("\nHallo", " Hallo ")]
    [InlineData("\r\n\r\nHallo", " Hallo ")]
    public void ForTyping_Umbruch_am_Anfang_wird_zu_einem_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    // Regel 1: Ohne Umbruch gilt nur die Regel für das Leerzeichen am Ende.
    [Theory]
    [InlineData("Hallo", "Hallo ")]
    [InlineData("Hallo Welt.", "Hallo Welt. ")]
    [InlineData("Hallo ", "Hallo ")]
    public void ForTyping_ohne_Umbruch_gilt_nur_das_Leerzeichen_am_Ende(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    // Regel 2 nennt nur Zeilenumbrüche: Leerzeichen, Tabulatoren und Zeichen im Inneren bleiben, wie sie sind.
    [Theory]
    [InlineData("Zwei  Leerzeichen", "Zwei  Leerzeichen ")]
    [InlineData("  Leerraum am Anfang", "  Leerraum am Anfang ")]
    [InlineData("git commit, dann push!", "git commit, dann push! ")]
    [InlineData("Größe, Maß und Übung: ÄÖÜ äöü ß", "Größe, Maß und Übung: ÄÖÜ äöü ß ")]
    public void ForTyping_laesst_alles_andere_unveraendert(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    // Regel 2, der Zweck: Im getippten Text steht nie ein Umbruch, denn der würde im Terminal einen Befehl abschicken.
    [Theory]
    [InlineData("eins\nzwei")]
    [InlineData("eins\r\nzwei")]
    [InlineData("eins\rzwei")]
    [InlineData("\neins\n\nzwei\n")]
    [InlineData("\r\neins\r\n\r\nzwei\r\n")]
    [InlineData("eins \n zwei")]
    [InlineData("eins\n \nzwei")]
    [InlineData("rm alles\n")]
    public void ForTyping_enthaelt_nie_einen_Umbruch(string text)
    {
        var getippt = InsertText.ForTyping(text);

        Assert.DoesNotContain("\n", getippt);
        Assert.DoesNotContain("\r", getippt);
    }

    // Regel 1: Zwei Diktate hintereinander kleben auch beim Tippen nicht zusammen.
    [Fact]
    public void ForTyping_zwei_Diktate_kleben_nicht_zusammen()
    {
        var zusammen = InsertText.ForTyping("Hallo") + InsertText.ForTyping("Welt\n");

        Assert.Equal("Hallo Welt ", zusammen);
    }

    // Regel 4: Ein leerer Text oder einer nur aus Leerraum wird nie eingefügt, es bleibt also nichts zum Tippen übrig.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n\r\n")]
    public void ForTyping_ohne_Inhalt_ergibt_leer(string? text)
    {
        Assert.Equal(string.Empty, InsertText.ForTyping(text));
    }

    // Regel 2 und Regel 3 im Vergleich: Derselbe Text behält über die Zwischenablage seinen Umbruch und verliert ihn beim Tippen.
    [Fact]
    public void Zwischenablage_behaelt_den_Umbruch_Tippen_nicht()
    {
        const string text = "Erste Zeile\r\nZweite Zeile";

        Assert.Equal("Erste Zeile\r\nZweite Zeile ", InsertText.ForClipboard(text));
        Assert.Equal("Erste Zeile Zweite Zeile ", InsertText.ForTyping(text));
    }

    /// <summary>
    /// Regel für den Text, Punkt 2 (Fassung nach der Prüfung vom 11.10.2026): Beim Tippen wird auch jedes andere Steuerzeichen
    /// zu einem Leerzeichen, weil es im Zielprogramm etwas auslösen kann. Eine Folge ergibt ein einziges Leerzeichen.
    /// </summary>
    [Theory]
    [InlineData("Ein\tTabulator", "Ein Tabulator ")]
    [InlineData("Hallo\t", "Hallo ")]
    [InlineData("a\u0003b", "a b ")]
    [InlineData("a\u0004b", "a b ")]
    [InlineData("a\u0008b", "a b ")]
    [InlineData("a\u001Bb", "a b ")]
    [InlineData("a\u007Fb", "a b ")]
    [InlineData("a\t\r\n\u001Bb", "a b ")]
    public void ForTyping_macht_Steuerzeichen_zu_einem_Leerzeichen(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTyping(text));
    }

    /// <summary>
    /// Über die Zwischenablage bleibt der Text bis auf das Leerzeichen am Ende unverändert, auch mit Tabulator.
    /// </summary>
    [Fact]
    public void ForClipboard_laesst_Tabulator_stehen()
    {
        Assert.Equal("Ein\tTabulator ", InsertText.ForClipboard("Ein\tTabulator"));
    }}
