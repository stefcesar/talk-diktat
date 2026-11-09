using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft die Ergänzung des Filters gegen erfundene Sätze ausschließlich gegen PLAYBOOK.md, Abschnitt 22
/// ("Phantomfilter, Ergänzung aus Blitztext"). Die Tests kennen die Umsetzung nicht.
/// Die Regelnummer aus Abschnitt 22 steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class HallucinationFilterErgaenzungTests
{
    private const string Zusatztext = "das ist ein testphantom";

    private static readonly HallucinationFilterOptions MitZusatztext = new(ExtraPhrases: new[] { Zusatztext });

    private static readonly string[] NeueSaetze =
    {
        "Abonniert den Kanal",
        "Bitte abonnieren",
        "Abonniere meinen Kanal",
        "Please subscribe",
        "Like and subscribe",
        "Please like and subscribe",
    };

    private static readonly string[] NeueAnfaenge =
    {
        "Copyright SWR",
        "Untertitel des ZDF",
        "Untertitel von Amara.org",
        "Untertitel von der Amara.org-Community",
        "Subtitles by Amara.org",
    };

    private static readonly string[] ZusaetzlicheVermerke =
    {
        "Untertitel im Auftrag des ZDF für funk",
        "Untertitel von Stephanie Geiges",
        "www.mooji.org",
    };

    public static TheoryData<string> NeueSaetzeDaten => new(NeueSaetze);

    public static TheoryData<string> NeueAnfaengeDaten => new(NeueAnfaenge);

    public static TheoryData<string> ZusaetzlicheVermerkeDaten => new(ZusaetzlicheVermerke);

    private static TranscriptionSegment Abschnitt(
        string text,
        double? wortsicherheit = null,
        double? keineSprache = null,
        double? kompression = null,
        double anfang = 0,
        double ende = 1) =>
        new(anfang, ende, text, wortsicherheit, keineSprache, kompression);

    private static IReadOnlyList<TranscriptionSegment> Liste(params TranscriptionSegment[] abschnitte) => abschnitte;

    private static bool EnthaeltVereinheitlicht(IReadOnlyList<string> liste, string eintrag) =>
        liste.Any(e => HallucinationFilter.Normalize(e) == HallucinationFilter.Normalize(eintrag));

    // ---------- Regel 1: neue ganze Sätze ----------

    // Regel 1: Die neuen Sätze stehen in KnownPhrases.
    [Theory]
    [MemberData(nameof(NeueSaetzeDaten))]
    public void Regel1_neuer_Satz_steht_in_KnownPhrases(string satz)
    {
        Assert.True(EnthaeltVereinheitlicht(HallucinationFilter.KnownPhrases, satz));
    }

    // Regel 1: Jeder neue Satz wird als ganzer Text verworfen, auch in anderer Groß und Kleinschreibung mit Satzzeichen.
    [Theory]
    [MemberData(nameof(NeueSaetzeDaten))]
    public void Regel1_neuer_Satz_wird_als_ganzer_Text_verworfen(string satz)
    {
        Assert.True(HallucinationFilter.IsKnownPhrase(satz));
        Assert.True(HallucinationFilter.IsKnownPhrase("  " + satz.ToUpperInvariant() + "!  "));
        Assert.True(HallucinationFilter.IsKnownPhrase(satz.ToLowerInvariant() + "."));
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(" " + satz + ". "));
    }

    // Regel 1: Jeder neue Satz wird als ganzer Abschnitt verworfen und gezählt.
    [Theory]
    [MemberData(nameof(NeueSaetzeDaten))]
    public void Regel1_neuer_Satz_wird_als_ganzer_Abschnitt_verworfen(string satz)
    {
        var abschnitt = Abschnitt(" " + satz + "!", wortsicherheit: -0.1, keineSprache: 0.01, kompression: 1.0);

        Assert.True(HallucinationFilter.ShouldDrop(abschnitt));

        var ergebnis = HallucinationFilter.Apply(Liste(abschnitt));
        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Empty(ergebnis.Kept);
        Assert.Equal(1, ergebnis.FilteredCount);
    }

    // Regel 1: Als Teil eines längeren Satzes wird ein neuer Satz nicht verworfen.
    [Theory]
    [MemberData(nameof(NeueSaetzeDaten))]
    public void Regel1_neuer_Satz_im_laengeren_Satz_bleibt(string satz)
    {
        var davor = "Er sagte " + satz;
        var danach = satz + " und dann gehen wir essen";

        Assert.False(HallucinationFilter.IsKnownPhrase(davor));
        Assert.False(HallucinationFilter.IsKnownPhrase(danach));
        Assert.Equal(davor, HallucinationFilter.FilterText(davor));
        Assert.Equal(danach, HallucinationFilter.FilterText(danach));
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt(danach)));
    }

    // ---------- Regel 2: neue Anfänge ----------

    // Regel 2: Die neuen Anfänge stehen in KnownPrefixes.
    [Theory]
    [MemberData(nameof(NeueAnfaengeDaten))]
    public void Regel2_neuer_Anfang_steht_in_KnownPrefixes(string anfang)
    {
        Assert.True(EnthaeltVereinheitlicht(HallucinationFilter.KnownPrefixes, anfang));
    }

    // Regel 2: Jeder neue Anfang wird allein und mit Zusatz nach einem Leerzeichen verworfen, auch mit Jahreszahl.
    [Theory]
    [MemberData(nameof(NeueAnfaengeDaten))]
    public void Regel2_neuer_Anfang_wird_auch_mit_Zusatz_verworfen(string anfang)
    {
        Assert.True(HallucinationFilter.IsKnownPhrase(anfang));
        Assert.True(HallucinationFilter.IsKnownPhrase(anfang + " 2021"));
        Assert.True(HallucinationFilter.IsKnownPhrase(anfang + ", 2019."));
        Assert.True(HallucinationFilter.IsKnownPhrase(anfang.ToUpperInvariant() + " und noch etwas"));
        Assert.True(HallucinationFilter.ShouldDrop(Abschnitt(anfang + " 2021")));
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(" " + anfang + " 2021 "));
    }

    // Regel 2 (mit Abschnitt 15 Regel 3): Ein Anfang, der ohne Leerzeichen weitergeht, ist nicht bekannt.
    [Fact]
    public void Regel2_Anfang_ohne_Leerzeichen_danach_zaehlt_nicht()
    {
        Assert.False(HallucinationFilter.IsKnownPhrase("Copyright SWRneo"));
    }

    // ---------- Regel 3: Vermerk am Ende ----------

    // Regel 3: Die Vermerkliste enthält alle Anfänge aus KnownPrefixes.
    [Fact]
    public void Regel3_KnownCredits_enthaelt_alle_Anfaenge()
    {
        Assert.NotNull(HallucinationFilter.KnownCredits);

        foreach (var anfang in HallucinationFilter.KnownPrefixes)
        {
            Assert.True(EnthaeltVereinheitlicht(HallucinationFilter.KnownCredits, anfang), anfang);
        }
    }

    // Regel 3: Die Vermerkliste enthält die drei zusätzlichen Vermerke.
    [Theory]
    [MemberData(nameof(ZusaetzlicheVermerkeDaten))]
    public void Regel3_KnownCredits_enthaelt_zusaetzliche_Vermerke(string vermerk)
    {
        Assert.True(EnthaeltVereinheitlicht(HallucinationFilter.KnownCredits, vermerk));
    }

    // Regel 3: Abspann Sätze sind keine Vermerke.
    [Theory]
    [InlineData("Vielen Dank fürs Zuschauen")]
    [InlineData("Bitte abonnieren")]
    public void Regel3_Abspann_Saetze_stehen_nicht_in_KnownCredits(string satz)
    {
        Assert.False(EnthaeltVereinheitlicht(HallucinationFilter.KnownCredits, satz));
    }

    // Regel 3: alle Beispiele aus dem Playbook wörtlich.
    [Theory]
    [InlineData("Das Rezept ist fertig. Untertitel im Auftrag des ZDF, 2021.", "Das Rezept ist fertig.")]
    [InlineData("Gut. Untertitel von Stephanie Geiges. Copyright WDR 2021", "Gut.")]
    [InlineData("Das Rezept ist fertig. Vielen Dank fürs Zuschauen.", "Das Rezept ist fertig. Vielen Dank fürs Zuschauen.")]
    [InlineData("Ich habe Copyright WDR gelesen.", "Ich habe Copyright WDR gelesen.")]
    [InlineData("Mehr unter www.mooji.org", "Mehr unter www.mooji.org")]
    [InlineData("Untertitel der Amara.org-Community", "")]
    public void Regel3_Beispiele_aus_dem_Playbook(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Jeder Eintrag der Vermerkliste wird am Ende abgeschnitten, auch mit Jahreszahl und Satzzeichen.
    [Fact]
    public void Regel3_jeder_Vermerk_am_Ende_wird_abgeschnitten()
    {
        foreach (var vermerk in HallucinationFilter.KnownCredits)
        {
            Assert.Equal("Das Rezept ist fertig.", HallucinationFilter.RemoveTrailingCredits("Das Rezept ist fertig. " + vermerk));
            Assert.Equal("Das Rezept ist fertig.", HallucinationFilter.RemoveTrailingCredits("Das Rezept ist fertig. " + vermerk + " 2021."));
            Assert.Equal("Das Rezept ist fertig.", HallucinationFilter.RemoveTrailingCredits("Das Rezept ist fertig. " + vermerk.ToUpperInvariant() + ", 2017!"));
        }
    }

    // Regel 3: Wörter nur aus Ziffern entfallen im Schlüssel, auch mehrere und an beliebiger Stelle.
    [Theory]
    [InlineData("Alles klar. Copyright SWR 2020 2021", "Alles klar.")]
    [InlineData("Alles klar. Copyright 2021 SWR", "Alles klar.")]
    [InlineData("Alles klar. Untertitel des ZDF (2019)", "Alles klar.")]
    [InlineData("Alles klar. Untertitel im Auftrag des ZDF für funk, 2017", "Alles klar.")]
    public void Regel3_Vermerk_mit_Jahreszahl_wird_abgeschnitten(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ein Abspann Satz am Ende bleibt.
    [Theory]
    [InlineData("Ich mache jetzt Schluss. Vielen Dank fürs Zuschauen!")]
    [InlineData("Ich mache jetzt Schluss. Bitte abonnieren.")]
    [InlineData("Ich mache jetzt Schluss. Please subscribe")]
    public void Regel3_Abspann_Satz_am_Ende_bleibt(string eingabe)
    {
        Assert.Equal(eingabe, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ein Anfang mit weiteren Wörtern danach ist kein Vermerk.
    [Theory]
    [InlineData("Untertitel des ZDF sind gut.")]
    [InlineData("Das Rezept ist fertig. Untertitel des ZDF sind gut.")]
    [InlineData("Das Rezept ist fertig. Copyright SWR gilt auch hier.")]
    [InlineData("Das Rezept ist fertig. Untertitel des ZDFneo")]
    public void Regel3_Anfang_mit_weiteren_Woertern_bleibt(string eingabe)
    {
        Assert.Equal(eingabe, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ein Vermerk innerhalb der letzten drei Satzanfänge wird abgeschnitten
    // (hier ist er der drittletzte Satzanfang; die Sätze danach bestehen nur aus Ziffern).
    [Fact]
    public void Regel3_Vermerk_als_drittletzter_Satzanfang_wird_abgeschnitten()
    {
        Assert.Equal("Das bleibt.", HallucinationFilter.RemoveTrailingCredits("Das bleibt. Untertitel des ZDF. 2021. 2022."));
    }

    // Regel 3: Nur die letzten drei Satzanfänge zählen. Ein Vermerk als viertletzter Satzanfang bleibt.
    [Fact]
    public void Regel3_Vermerk_als_viertletzter_Satzanfang_bleibt()
    {
        const string eingabe = "Das bleibt. Untertitel des ZDF. 2021. 2022. 2023.";

        Assert.Equal(eingabe, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Nur die letzten drei Satzanfänge zählen. Ein Vermerk vor vier weiteren Sätzen bleibt.
    [Fact]
    public void Regel3_Vermerk_vor_vier_weiteren_Saetzen_bleibt()
    {
        const string eingabe = "Das bleibt. Untertitel des ZDF. 2021. 2022. 2023. 2024.";

        Assert.Equal(eingabe, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ein Vermerk, nach dem noch ein gewöhnlicher Satz folgt, steht nicht am Ende und bleibt.
    [Fact]
    public void Regel3_Vermerk_mitten_im_Text_bleibt()
    {
        const string eingabe = "Das Rezept ist fertig. Untertitel des ZDF. Dann essen wir.";

        Assert.Equal(eingabe, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Das Abschneiden wiederholt sich bei zwei und drei Vermerken hintereinander.
    [Theory]
    [InlineData("Das Rezept ist fertig. Copyright SWR 2020. Untertitel des ZDF.", "Das Rezept ist fertig.")]
    [InlineData("Das Rezept ist fertig. Untertitel des ZDF. Copyright SWR 2020", "Das Rezept ist fertig.")]
    [InlineData("Das Rezept ist fertig. www.mooji.org. Untertitel von Stephanie Geiges. Copyright SWR 2020", "Das Rezept ist fertig.")]
    public void Regel3_mehrere_Vermerke_hintereinander_werden_abgeschnitten(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Nach einem Zeilenumbruch beginnt ein Satz, auch ohne Satzzeichen davor.
    [Theory]
    [InlineData("Das Rezept ist fertig\nUntertitel des ZDF", "Das Rezept ist fertig")]
    [InlineData("Das Rezept ist fertig\r\nUntertitel des ZDF", "Das Rezept ist fertig")]
    [InlineData("Das Rezept ist fertig\n   Copyright SWR 2021\n", "Das Rezept ist fertig")]
    public void Regel3_Zeilenumbruch_ist_Satzgrenze(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Nach Punkt, Ausrufezeichen, Fragezeichen oder Auslassungszeichen mit Leerraum beginnt ein Satz.
    [Theory]
    [InlineData("Und dann… Untertitel des ZDF", "Und dann…")]
    [InlineData("Wirklich? Untertitel des ZDF", "Wirklich?")]
    [InlineData("Toll! Copyright SWR 2020", "Toll!")]
    [InlineData("Fertig.\tUntertitel des ZDF", "Fertig.")]
    [InlineData("Fertig.   Untertitel des ZDF   ", "Fertig.")]
    public void Regel3_Satzzeichen_mit_Leerraum_ist_Satzgrenze(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ein Punkt ohne Leerraum danach trennt nicht.
    [Theory]
    [InlineData("Fertig.Untertitel des ZDF")]
    [InlineData("Siehe Seite.Copyright SWR 2021")]
    public void Regel3_Punkt_ohne_Leerraum_trennt_nicht(string eingabe)
    {
        Assert.Equal(eingabe, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ein Vermerk mit Punkt im Inneren ("Amara.org") wird als Ganzes erkannt.
    [Theory]
    [InlineData("Gut. Untertitel von Amara.org", "Gut.")]
    [InlineData("Gut. Subtitles by Amara.org 2015", "Gut.")]
    [InlineData("Gut. Untertitel von der Amara.org-Community", "Gut.")]
    public void Regel3_Vermerk_mit_Punkt_im_Inneren_wird_abgeschnitten(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Ist der ganze Text ein Vermerk, ergibt er leer (der Textanfang ist ein Satzanfang).
    [Theory]
    [InlineData("Untertitel von Stephanie Geiges")]
    [InlineData("  www.mooji.org  ")]
    [InlineData("Copyright SWR 2021. Untertitel des ZDF.")]
    public void Regel3_ganzer_Text_als_Vermerk_ergibt_leer(string eingabe)
    {
        Assert.Equal(string.Empty, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: null ergibt leer.
    [Fact]
    public void Regel3_null_ergibt_leer()
    {
        Assert.Equal(string.Empty, HallucinationFilter.RemoveTrailingCredits(null));
    }

    // Regel 3: Leerer Text und reiner Leerraum ergeben leer.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t ")]
    public void Regel3_ohne_Inhalt_ergibt_leer(string eingabe)
    {
        Assert.Equal(string.Empty, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Leerraum am Ende entfällt auch ohne Abschneiden, Leerraum am Anfang bleibt.
    [Theory]
    [InlineData("Hallo Welt.  ", "Hallo Welt.")]
    [InlineData("  Hallo Welt.", "  Hallo Welt.")]
    [InlineData("\t Hallo Welt. \r\n", "\t Hallo Welt.")]
    [InlineData("Hallo  Welt.", "Hallo  Welt.")]
    public void Regel3_Leerraum_am_Ende_entfaellt_am_Anfang_bleibt(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.RemoveTrailingCredits(eingabe));
    }

    // Regel 3: Nach dem Abschneiden bleibt Leerraum am Anfang erhalten.
    [Fact]
    public void Regel3_Leerraum_am_Anfang_bleibt_nach_Abschneiden()
    {
        Assert.Equal("  Gut.", HallucinationFilter.RemoveTrailingCredits("  Gut. Untertitel des ZDF  "));
    }

    // Regel 3: Zusatztexte aus den Optionen sind keine Vermerke (geprüft über FilterText und Apply mit Optionen).
    [Fact]
    public void Regel3_Zusatztexte_aus_Optionen_sind_keine_Vermerke()
    {
        const string text = "Das Rezept ist fertig. Das ist ein Testphantom.";

        Assert.Equal(text, HallucinationFilter.FilterText(text, MitZusatztext));

        var ergebnis = HallucinationFilter.Apply(Liste(Abschnitt(text)), MitZusatztext);
        Assert.Equal(text, ergebnis.Text);
        Assert.Single(ergebnis.Kept);
        Assert.Equal(text, ergebnis.Kept[0].Text);
        Assert.Equal(0, ergebnis.FilteredCount);
    }

    // ---------- Regel 4: Einbau in Apply ----------

    // Regel 4: Ein Abschnitt mit Vermerk am Ende wird gekürzt, alle anderen Werte bleiben gleich.
    [Fact]
    public void Regel4_Apply_kuerzt_Abschnitt_mit_Vermerk_am_Ende()
    {
        var mitVermerk = Abschnitt(" Das Rezept ist fertig. Untertitel des ZDF, 2021.", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.1, anfang: 0, ende: 2.5);
        var danach = Abschnitt(" Nächster Punkt.", wortsicherheit: -0.3, keineSprache: 0.02, kompression: 1.2, anfang: 2.5, ende: 4);

        var ergebnis = HallucinationFilter.Apply(Liste(mitVermerk, danach));

        Assert.Equal(2, ergebnis.Kept.Count);
        Assert.Equal(0, ergebnis.FilteredCount);
        Assert.Equal("Das Rezept ist fertig. Nächster Punkt.", ergebnis.Text);

        var gekuerzt = ergebnis.Kept[0];
        Assert.Equal(HallucinationFilter.RemoveTrailingCredits(mitVermerk.Text), gekuerzt.Text);
        Assert.Equal(" Das Rezept ist fertig.", gekuerzt.Text);
        Assert.Equal(0, gekuerzt.StartSeconds);
        Assert.Equal(2.5, gekuerzt.EndSeconds);
        Assert.Equal(-0.2, gekuerzt.AvgLogProb);
        Assert.Equal(0.01, gekuerzt.NoSpeechProb);
        Assert.Equal(1.1, gekuerzt.CompressionRatio);
        Assert.Equal(mitVermerk with { Text = " Das Rezept ist fertig." }, gekuerzt);
    }

    // Regel 4: Ein Abschnitt ohne Vermerk bleibt unverändert in Kept.
    [Fact]
    public void Regel4_Apply_laesst_Abschnitt_ohne_Vermerk_unveraendert()
    {
        var ohneVermerk = Abschnitt(" Das Rezept ist fertig.", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.1, anfang: 1, ende: 3);
        var abspann = Abschnitt(" Vielen Dank fürs Zuschauen und bis bald.", wortsicherheit: -0.4, keineSprache: 0.03, kompression: 1.3, anfang: 3, ende: 5);

        var ergebnis = HallucinationFilter.Apply(Liste(ohneVermerk, abspann));

        Assert.Equal(2, ergebnis.Kept.Count);
        Assert.Equal(ohneVermerk, ergebnis.Kept[0]);
        Assert.Equal(abspann, ergebnis.Kept[1]);
        Assert.Equal(0, ergebnis.FilteredCount);
    }

    // Regel 4: Wird nichts abgeschnitten, bleibt der Abschnitt unverändert, auch wenn er Leerraum am Ende hat.
    // (Auslegung: "nichts abgeschnitten" meint keinen Vermerk; der Leerraum am Ende gehört nicht dazu.)
    [Fact]
    public void Regel4_Apply_laesst_Abschnitt_ohne_Vermerk_mit_Leerraum_am_Ende_unveraendert()
    {
        var abschnitt = Abschnitt(" Das Rezept ist fertig.  ", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.1);

        var ergebnis = HallucinationFilter.Apply(Liste(abschnitt));

        Assert.Single(ergebnis.Kept);
        Assert.Equal(abschnitt, ergebnis.Kept[0]);
        Assert.Equal("Das Rezept ist fertig.", ergebnis.Text);
    }

    // Regel 4: Bleibt nach dem Kürzen nichts übrig, fällt der Abschnitt weg und zählt in FilteredCount.
    [Fact]
    public void Regel4_Apply_verwirft_Abschnitt_der_durch_Kuerzen_leer_wird()
    {
        // Der Text ist als Ganzes kein bekannter Phantomtext (Regel D greift nicht),
        // besteht aber nur aus zwei Vermerken hintereinander.
        const string nurVermerke = "www.mooji.org. Untertitel von Stephanie Geiges.";
        Assert.False(HallucinationFilter.IsKnownPhrase(nurVermerke));

        var ergebnis = HallucinationFilter.Apply(Liste(
            Abschnitt("Erster Satz.", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.0, anfang: 0, ende: 1),
            Abschnitt(nurVermerke, wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.0, anfang: 1, ende: 2),
            Abschnitt("Zweiter Satz.", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.0, anfang: 2, ende: 3)));

        Assert.Equal("Erster Satz. Zweiter Satz.", ergebnis.Text);
        Assert.Equal(1, ergebnis.FilteredCount);
        Assert.Equal(new[] { 0.0, 2.0 }, ergebnis.Kept.Select(a => a.StartSeconds));
    }

    // Regel 4: Abschnitte, die schon nach den Regeln A bis D wegfallen, zählen weiter genau einmal;
    // Kürzen und Verwerfen zusammen ergeben die richtige Zahl.
    [Fact]
    public void Regel4_Apply_zaehlt_Kuerzung_und_Regeln_A_bis_D_zusammen()
    {
        var ergebnis = HallucinationFilter.Apply(Liste(
            Abschnitt("Rauschen", keineSprache: 0.95, anfang: 0, ende: 1),                          // Regel B
            Abschnitt("Untertitel des ZDF 2021", anfang: 1, ende: 2),                               // Regel D
            Abschnitt("Untertitel von Stephanie Geiges. Copyright SWR", anfang: 2, ende: 3),        // durch Kürzen leer
            Abschnitt("Alles klar. Copyright SWR 2019", anfang: 3, ende: 4)));                       // gekürzt

        Assert.Equal("Alles klar.", ergebnis.Text);
        Assert.Equal(3, ergebnis.FilteredCount);
        Assert.Single(ergebnis.Kept);
        Assert.Equal("Alles klar.", ergebnis.Kept[0].Text);
        Assert.Equal(3, ergebnis.Kept[0].StartSeconds);
    }

    // ---------- Regel 5: Einbau in FilterText ----------

    // Regel 5: Ist der ganze Text ein bekannter Phantomtext, kommt leer zurück wie bisher.
    [Theory]
    [InlineData("Untertitel der Amara.org-Community")]
    [InlineData(" Please like and subscribe! ")]
    [InlineData("Copyright SWR 2021")]
    public void Regel5_FilterText_ganzer_Phantomtext_ergibt_leer(string eingabe)
    {
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(eingabe));
    }

    // Regel 5: Sonst kommt der Text nach RemoveTrailingCredits zurück, ohne Leerraum an den Rändern.
    [Theory]
    [InlineData("  Das Rezept ist fertig. Untertitel des ZDF  ", "Das Rezept ist fertig.")]
    [InlineData("Gut. Untertitel von Stephanie Geiges. Copyright WDR 2021", "Gut.")]
    [InlineData("\tDas Rezept ist fertig\nwww.mooji.org\n", "Das Rezept ist fertig")]
    [InlineData("  Das Rezept ist fertig. Vielen Dank fürs Zuschauen.  ", "Das Rezept ist fertig. Vielen Dank fürs Zuschauen.")]
    [InlineData(" Ich habe Copyright WDR gelesen. ", "Ich habe Copyright WDR gelesen.")]
    [InlineData("Mehr unter www.mooji.org", "Mehr unter www.mooji.org")]
    public void Regel5_FilterText_schneidet_Vermerk_ab_und_bereinigt_Raender(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.FilterText(eingabe));
    }

    // Regel 5: Ein Text, der nur aus Vermerken besteht, aber kein bekannter Phantomtext ist, ergibt leer.
    [Fact]
    public void Regel5_FilterText_nur_Vermerke_ergibt_leer()
    {
        const string nurVermerke = " www.mooji.org. Untertitel von Stephanie Geiges. ";
        Assert.False(HallucinationFilter.IsKnownPhrase(nurVermerke));

        Assert.Equal(string.Empty, HallucinationFilter.FilterText(nurVermerke));
    }

    // Regel 5: null oder nur Leerraum ergibt weiterhin leer.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public void Regel5_FilterText_ohne_Inhalt_ergibt_leer(string? eingabe)
    {
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(eingabe));
    }
}
