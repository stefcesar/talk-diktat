// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft den Filter gegen erfundene Sätze ausschließlich gegen Regel 3 der reinen Logik in PLAYBOOK.md, Abschnitt 15.
/// Die Tests kennen die Umsetzung nicht. Wo ein eigener Phantomtext gebraucht wird, kommt er über die Zusatztexte
/// der Optionen (schon in vereinheitlichter Schreibweise), damit die Tests nicht von der eingebauten Liste abhängen.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class HallucinationFilterTests
{
    private const string Zusatztext = "das ist ein testphantom";

    private static readonly HallucinationFilterOptions MitZusatztext = new(ExtraPhrases: new[] { Zusatztext });

    private static TranscriptionSegment Abschnitt(
        string text,
        double? wortsicherheit = null,
        double? keineSprache = null,
        double? kompression = null,
        double anfang = 0,
        double ende = 1) =>
        new(anfang, ende, text, wortsicherheit, keineSprache, kompression);

    private static IReadOnlyList<TranscriptionSegment> Liste(params TranscriptionSegment[] abschnitte) => abschnitte;

    // ---------- Normalize ----------

    // Regel 3, Normalize: die beiden Beispiele aus der Regel.
    [Theory]
    [InlineData("Untertitel der Amara.org-Community!", "untertitel der amara org community")]
    [InlineData("Vielen Dank für's Zuschauen.", "vielen dank fürs zuschauen")]
    public void Normalize_Beispiele_aus_der_Regel(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Kleinbuchstaben, auch bei Umlauten und Akzenten.
    [Theory]
    [InlineData("HALLO Welt", "hallo welt")]
    [InlineData("ÄÖÜ Straße", "äöü straße")]
    [InlineData("LUMÍORA", "lumíora")]
    public void Normalize_macht_Kleinbuchstaben(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Apostrophe (gerade, typografisch, Akzentzeichen) entfallen ersatzlos.
    [Theory]
    [InlineData("für's")] // gerade
    [InlineData("für’s")] // typografisch
    [InlineData("für`s")] // Akzentzeichen Gravis
    [InlineData("für´s")] // Akzentzeichen Akut
    public void Normalize_entfernt_Apostrophe_ersatzlos(string eingabe)
    {
        Assert.Equal("fürs", HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Jedes andere Zeichen, das kein Buchstabe und keine Ziffer ist, wird zu einem Leerzeichen.
    [Theory]
    [InlineData("a.b", "a b")]
    [InlineData("a-b", "a b")]
    [InlineData("a,b", "a b")]
    [InlineData("a!b", "a b")]
    [InlineData("a?b", "a b")]
    [InlineData("a:b", "a b")]
    [InlineData("a/b", "a b")]
    [InlineData("a_b", "a b")]
    [InlineData("a\"b", "a b")]
    [InlineData("a(b)c", "a b c")]
    [InlineData("a\tb", "a b")]
    [InlineData("a\nb", "a b")]
    [InlineData("a\u2013b", "a b")]
    public void Normalize_ersetzt_andere_Zeichen_durch_Leerzeichen(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Buchstaben und Ziffern bleiben erhalten.
    [Theory]
    [InlineData("SWR 2021", "swr 2021")]
    [InlineData("Copyright WDR 2021", "copyright wdr 2021")]
    [InlineData("abc123", "abc123")]
    public void Normalize_behaelt_Buchstaben_und_Ziffern(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Mehrere Leerzeichen werden eines.
    [Theory]
    [InlineData("a    b", "a b")]
    [InlineData("a . , ! b", "a b")]
    [InlineData("a...b", "a b")]
    [InlineData("a \t\r\n b", "a b")]
    public void Normalize_fasst_Leerzeichen_zusammen(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Keine Leerzeichen an den Rändern.
    [Theory]
    [InlineData("  hallo  ", "hallo")]
    [InlineData("...hallo!!!", "hallo")]
    [InlineData(" (Hallo Welt) ", "hallo welt")]
    public void Normalize_hat_keine_Leerzeichen_an_den_Raendern(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: null ergibt leer; Text ohne Buchstaben und Ziffern ergibt ebenfalls leer.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...!?")]
    [InlineData("'")]
    public void Normalize_ohne_Inhalt_ergibt_leer(string? eingabe)
    {
        Assert.Equal(string.Empty, HallucinationFilter.Normalize(eingabe));
    }

    // Regel 3, Normalize: Ein schon vereinheitlichter Text bleibt unverändert.
    [Fact]
    public void Normalize_ist_stabil_bei_wiederholter_Anwendung()
    {
        var einmal = HallucinationFilter.Normalize("Untertitel im Auftrag des ZDF für funk, 2017");

        Assert.Equal("untertitel im auftrag des zdf für funk 2017", einmal);
        Assert.Equal(einmal, HallucinationFilter.Normalize(einmal));
    }

    // ---------- IsKnownPhrase ----------

    // Regel 3, IsKnownPhrase: die Liste der bekannten Texte ist vorhanden und nicht leer.
    [Fact]
    public void KnownPhrases_ist_nicht_leer()
    {
        Assert.NotNull(HallucinationFilter.KnownPhrases);
        Assert.NotEmpty(HallucinationFilter.KnownPhrases);
        Assert.NotNull(HallucinationFilter.KnownPrefixes);
    }

    // Regel 3, IsKnownPhrase: wahr, wenn der vereinheitlichte Text genau einem Eintrag aus KnownPhrases entspricht.
    [Fact]
    public void IsKnownPhrase_erkennt_jeden_Eintrag_aus_KnownPhrases()
    {
        foreach (var eintrag in HallucinationFilter.KnownPhrases)
        {
            Assert.True(HallucinationFilter.IsKnownPhrase(eintrag));
        }
    }

    // Regel 3, IsKnownPhrase: verglichen wird der vereinheitlichte Text, Schreibweise und Satzzeichen sind egal.
    [Fact]
    public void IsKnownPhrase_erkennt_Eintraege_in_anderer_Schreibweise()
    {
        foreach (var eintrag in HallucinationFilter.KnownPhrases)
        {
            var andereSchreibweise = "  " + eintrag.ToUpperInvariant() + " !  ";

            Assert.True(HallucinationFilter.IsKnownPhrase(andereSchreibweise));
        }
    }

    // Regel 3, IsKnownPhrase: wahr, wenn der Text mit einem Eintrag aus KnownPrefixes beginnt und dort endet.
    [Fact]
    public void IsKnownPhrase_erkennt_Anfang_der_dort_endet()
    {
        foreach (var anfang in HallucinationFilter.KnownPrefixes)
        {
            Assert.True(HallucinationFilter.IsKnownPhrase(anfang));
        }
    }

    // Regel 3, IsKnownPhrase: wahr, wenn der Text mit einem Eintrag aus KnownPrefixes beginnt
    // und nach einem Leerzeichen weitergeht.
    [Fact]
    public void IsKnownPhrase_erkennt_Anfang_der_nach_Leerzeichen_weitergeht()
    {
        foreach (var anfang in HallucinationFilter.KnownPrefixes)
        {
            Assert.True(HallucinationFilter.IsKnownPhrase(anfang + " 2017"));
            Assert.True(HallucinationFilter.IsKnownPhrase(anfang + ", für irgendwen."));
        }
    }

    // Regel 3, IsKnownPhrase: Beispiel "Untertitel im Auftrag des ZDF für funk, 2017" ja.
    [Fact]
    public void IsKnownPhrase_Beispiel_ZDF_fuer_funk_ja()
    {
        Assert.True(HallucinationFilter.IsKnownPhrase("Untertitel im Auftrag des ZDF für funk, 2017"));
    }

    // Regel 3, IsKnownPhrase: Beispiel "Untertitel im Auftrag des ZDFneo" nein (geht ohne Leerzeichen weiter).
    [Fact]
    public void IsKnownPhrase_Beispiel_ZDFneo_nein()
    {
        Assert.False(HallucinationFilter.IsKnownPhrase("Untertitel im Auftrag des ZDFneo"));
    }

    // Regel 3, IsKnownPhrase: Das Beispielpaar belegt den Anfang "Untertitel im Auftrag des ZDF";
    // endet der Text genau dort, ist er bekannt.
    [Fact]
    public void IsKnownPhrase_Anfang_ZDF_der_dort_endet_ja()
    {
        Assert.True(HallucinationFilter.IsKnownPhrase("Untertitel im Auftrag des ZDF"));
    }

    // Regel 3, IsKnownPhrase: "Vielen Dank" ist kein Phantomtext.
    [Theory]
    [InlineData("Vielen Dank")]
    [InlineData("Vielen Dank.")]
    [InlineData("vielen dank!")]
    public void IsKnownPhrase_Vielen_Dank_ist_kein_Phantomtext(string text)
    {
        Assert.False(HallucinationFilter.IsKnownPhrase(text));
    }

    // Regel 3, IsKnownPhrase: "Vielen Dank fürs Zuschauen und bis bald" ist kein Phantomtext (nur ganzer Abschnitt).
    [Fact]
    public void IsKnownPhrase_Abspann_mit_Fortsetzung_ist_kein_Phantomtext()
    {
        Assert.False(HallucinationFilter.IsKnownPhrase("Vielen Dank fürs Zuschauen und bis bald"));
    }

    // Regel 3, IsKnownPhrase: Der ganze Abschnitt "Vielen Dank fürs Zuschauen" ist dagegen ein Phantomtext
    // (das folgt aus dem Zusatz "nur ganzer Abschnitt"), auch mit Apostroph und Punkt.
    [Theory]
    [InlineData("Vielen Dank fürs Zuschauen")]
    [InlineData("Vielen Dank für's Zuschauen.")]
    [InlineData(" vielen dank fürs zuschauen! ")]
    public void IsKnownPhrase_ganzer_Abspann_ist_Phantomtext(string text)
    {
        Assert.True(HallucinationFilter.IsKnownPhrase(text));
    }

    // Regel 3, IsKnownPhrase: Ein bekannter Text mitten in einem Satz zählt nicht (nur ganzer Abschnitt).
    [Fact]
    public void IsKnownPhrase_bekannter_Text_im_Satz_zaehlt_nicht()
    {
        foreach (var eintrag in HallucinationFilter.KnownPhrases)
        {
            Assert.False(HallucinationFilter.IsKnownPhrase("Ich diktiere jetzt " + eintrag + " und noch mehr"));
        }
    }

    // Regel 3, IsKnownPhrase: Leerer Text ergibt falsch.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void IsKnownPhrase_leerer_Text_ergibt_falsch(string? text)
    {
        Assert.False(HallucinationFilter.IsKnownPhrase(text));
        Assert.False(HallucinationFilter.IsKnownPhrase(text, new[] { Zusatztext }));
    }

    // Regel 3, IsKnownPhrase: Gewöhnliche Diktate sind kein Phantomtext.
    [Theory]
    [InlineData("Hallo Welt")]
    [InlineData("Bitte schick mir die Rechnung bis Freitag.")]
    [InlineData("Untertitel")]
    public void IsKnownPhrase_gewoehnlicher_Text_ergibt_falsch(string text)
    {
        Assert.False(HallucinationFilter.IsKnownPhrase(text));
    }

    // Regel 3, IsKnownPhrase: wahr, wenn der vereinheitlichte Text genau einem Zusatztext entspricht.
    [Fact]
    public void IsKnownPhrase_erkennt_Zusatztext()
    {
        var zusatz = new[] { Zusatztext };

        Assert.True(HallucinationFilter.IsKnownPhrase("Das ist ein Testphantom!", zusatz));
        Assert.True(HallucinationFilter.IsKnownPhrase(Zusatztext, zusatz));
    }

    // Regel 3, IsKnownPhrase: Ohne Zusatztexte ist derselbe Text nicht bekannt; Zusatztexte gelten nur genau.
    [Fact]
    public void IsKnownPhrase_Zusatztext_gilt_nur_genau_und_nur_wenn_angegeben()
    {
        var zusatz = new[] { Zusatztext };

        Assert.False(HallucinationFilter.IsKnownPhrase("Das ist ein Testphantom!"));
        Assert.False(HallucinationFilter.IsKnownPhrase("Das ist ein Testphantom!", Array.Empty<string>()));
        Assert.False(HallucinationFilter.IsKnownPhrase("Das ist ein Testphantom und mehr", zusatz));
        Assert.False(HallucinationFilter.IsKnownPhrase("Das ist ein", zusatz));
    }

    // ---------- ShouldDrop ----------

    // Regel 3 (Vertrag): Standardwerte der Optionen sind 0,6, minus 1,0, 0,9 und 2,4, ohne Zusatztexte.
    [Fact]
    public void Optionen_haben_die_Standardwerte()
    {
        var optionen = new HallucinationFilterOptions();

        Assert.Equal(0.6, optionen.NoSpeechProbThreshold);
        Assert.Equal(-1.0, optionen.AvgLogProbThreshold);
        Assert.Equal(0.9, optionen.HardNoSpeechProbThreshold);
        Assert.Equal(2.4, optionen.CompressionRatioThreshold);
        Assert.Null(optionen.ExtraPhrases);
    }

    // Regel 3, ShouldDrop: Ein leerer Abschnitt ergibt falsch, auch wenn die Kennzahlen jede Regel auslösen würden.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void ShouldDrop_leerer_Abschnitt_ergibt_falsch(string text)
    {
        var abschnitt = Abschnitt(text, wortsicherheit: -5.0, keineSprache: 0.99, kompression: 9.0);

        Assert.False(HallucinationFilter.ShouldDrop(abschnitt));
    }

    // Regel 3, ShouldDrop: Ein gewöhnlicher Abschnitt mit guten Kennzahlen bleibt.
    [Fact]
    public void ShouldDrop_guter_Abschnitt_bleibt()
    {
        var abschnitt = Abschnitt("Hallo Welt", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.2);

        Assert.False(HallucinationFilter.ShouldDrop(abschnitt));
    }

    // Regel 3, ShouldDrop Regel A: "keine Sprache" größer gleich 0,6 UND Wortsicherheit kleiner als minus 1,0.
    // Grenzen: genau 0,6 mit minus 1,01 fällt weg, minus 1,0 genau bleibt.
    [Theory]
    [InlineData(0.6, -1.01, true)]
    [InlineData(0.6, -1.0, false)]
    [InlineData(0.7, -1.5, true)]
    [InlineData(0.89, -3.0, true)]
    [InlineData(0.59, -1.01, false)]
    [InlineData(0.59, -5.0, false)]
    [InlineData(0.7, -0.99, false)]
    [InlineData(0.7, -0.2, false)]
    public void ShouldDrop_Regel_A_braucht_beide_Bedingungen(double keineSprache, double wortsicherheit, bool erwartet)
    {
        var abschnitt = Abschnitt("Hallo Welt", wortsicherheit: wortsicherheit, keineSprache: keineSprache);

        Assert.Equal(erwartet, HallucinationFilter.ShouldDrop(abschnitt));
    }

    // Regel 3, ShouldDrop Regel A: Fehlt einer der beiden Werte, löst die Regel nicht aus.
    [Fact]
    public void ShouldDrop_Regel_A_loest_ohne_beide_Werte_nicht_aus()
    {
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt", wortsicherheit: null, keineSprache: 0.7)));
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt", wortsicherheit: -3.0, keineSprache: null)));
    }

    // Regel 3, ShouldDrop Regel B: "keine Sprache" größer gleich 0,9.
    // Grenzen: 0,9 genau fällt weg, 0,89 bleibt (wenn A nicht greift).
    [Theory]
    [InlineData(0.9, true)]
    [InlineData(0.95, true)]
    [InlineData(1.0, true)]
    [InlineData(0.89, false)]
    [InlineData(0.5, false)]
    public void ShouldDrop_Regel_B_ab_0_9(double keineSprache, bool erwartet)
    {
        // Wortsicherheit minus 0,1 ist nicht kleiner als minus 1,0, also greift Regel A nicht.
        var abschnitt = Abschnitt("Hallo Welt", wortsicherheit: -0.1, keineSprache: keineSprache);

        Assert.Equal(erwartet, HallucinationFilter.ShouldDrop(abschnitt));
    }

    // Regel 3, ShouldDrop Regel B: greift auch ohne Wortsicherheit; bei 0,89 ohne Wortsicherheit bleibt der Abschnitt.
    [Fact]
    public void ShouldDrop_Regel_B_braucht_keine_Wortsicherheit()
    {
        Assert.True(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt", wortsicherheit: null, keineSprache: 0.9)));
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt", wortsicherheit: null, keineSprache: 0.89)));
    }

    // Regel 3, ShouldDrop Regel C: Kompressionsverhältnis größer als 2,4.
    // Grenzen: 2,4 genau bleibt, 2,5 fällt weg.
    [Theory]
    [InlineData(2.4, false)]
    [InlineData(2.5, true)]
    [InlineData(2.41, true)]
    [InlineData(10.0, true)]
    [InlineData(1.0, false)]
    public void ShouldDrop_Regel_C_ueber_2_4(double kompression, bool erwartet)
    {
        var abschnitt = Abschnitt("Hallo Welt", wortsicherheit: -0.1, keineSprache: 0.01, kompression: kompression);

        Assert.Equal(erwartet, HallucinationFilter.ShouldDrop(abschnitt));
    }

    // Regel 3, ShouldDrop Regel C: greift auch ohne die anderen Kennzahlen.
    [Fact]
    public void ShouldDrop_Regel_C_braucht_keine_anderen_Kennzahlen()
    {
        Assert.True(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt", kompression: 2.5)));
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt", kompression: 2.4)));
    }

    // Regel 3, ShouldDrop: Fehlende Kennzahlen lösen ihre Regel nicht aus.
    [Fact]
    public void ShouldDrop_ohne_Kennzahlen_bleibt_gewoehnlicher_Text()
    {
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt("Hallo Welt")));
    }

    // Regel 3, ShouldDrop Regel D: bekannter Phantomtext fällt weg, auch mit guten oder fehlenden Kennzahlen.
    [Fact]
    public void ShouldDrop_Regel_D_bekannter_Phantomtext()
    {
        foreach (var eintrag in HallucinationFilter.KnownPhrases)
        {
            Assert.True(HallucinationFilter.ShouldDrop(Abschnitt(eintrag)));
            Assert.True(HallucinationFilter.ShouldDrop(
                Abschnitt(" " + eintrag + " ", wortsicherheit: -0.1, keineSprache: 0.01, kompression: 1.0)));
        }
    }

    // Regel 3, ShouldDrop Regel D: gilt auch für die Zusatztexte aus den Optionen, ohne Optionen nicht.
    [Fact]
    public void ShouldDrop_Regel_D_mit_Zusatztext()
    {
        var abschnitt = Abschnitt(" Das ist ein Testphantom. ", wortsicherheit: -0.1, keineSprache: 0.01, kompression: 1.0);

        Assert.True(HallucinationFilter.ShouldDrop(abschnitt, MitZusatztext));
        Assert.False(HallucinationFilter.ShouldDrop(abschnitt));
        Assert.False(HallucinationFilter.ShouldDrop(abschnitt, new HallucinationFilterOptions()));
    }

    // Regel 3, ShouldDrop Regel D: "Vielen Dank" bleibt als Abschnitt erhalten.
    [Fact]
    public void ShouldDrop_Vielen_Dank_bleibt()
    {
        Assert.False(HallucinationFilter.ShouldDrop(Abschnitt("Vielen Dank.", wortsicherheit: -0.3, keineSprache: 0.1, kompression: 0.8)));
    }

    // Regel 3, ShouldDrop: Die Grenzen kommen aus den Optionen.
    [Fact]
    public void ShouldDrop_nimmt_die_Grenzen_aus_den_Optionen()
    {
        var streng = new HallucinationFilterOptions(
            NoSpeechProbThreshold: 0.3,
            AvgLogProbThreshold: -0.5,
            HardNoSpeechProbThreshold: 0.8,
            CompressionRatioThreshold: 2.0);

        // Regel A mit eigenen Grenzen: 0,3 und kleiner als minus 0,5.
        var a = Abschnitt("Hallo Welt", wortsicherheit: -0.6, keineSprache: 0.3);
        Assert.True(HallucinationFilter.ShouldDrop(a, streng));
        Assert.False(HallucinationFilter.ShouldDrop(a));

        // Regel B mit eigener Grenze 0,8.
        var b = Abschnitt("Hallo Welt", wortsicherheit: -0.1, keineSprache: 0.8);
        Assert.True(HallucinationFilter.ShouldDrop(b, streng));
        Assert.False(HallucinationFilter.ShouldDrop(b));

        // Regel C mit eigener Grenze 2,0.
        var c = Abschnitt("Hallo Welt", kompression: 2.2);
        Assert.True(HallucinationFilter.ShouldDrop(c, streng));
        Assert.False(HallucinationFilter.ShouldDrop(c));
    }

    // ---------- Apply ----------

    // Regel 3, Apply: Ohne Abschnitte kommt leerer Text, nichts behalten, nichts gezählt.
    [Fact]
    public void Apply_ohne_Abschnitte_ergibt_leeres_Ergebnis()
    {
        var ergebnis = HallucinationFilter.Apply(Liste());

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Empty(ergebnis.Kept);
        Assert.Equal(0, ergebnis.FilteredCount);
    }

    // Regel 3, Apply: Text sind die behaltenen Texte, je an den Rändern bereinigt und mit einem Leerzeichen verbunden.
    [Fact]
    public void Apply_verbindet_bereinigte_Texte_mit_einem_Leerzeichen()
    {
        var ergebnis = HallucinationFilter.Apply(Liste(
            Abschnitt("  Hallo Welt. ", anfang: 0, ende: 1),
            Abschnitt(" Wie geht es dir?  ", anfang: 1, ende: 2),
            Abschnitt("Gut.", anfang: 2, ende: 3)));

        Assert.Equal("Hallo Welt. Wie geht es dir? Gut.", ergebnis.Text);
        Assert.Equal(3, ergebnis.Kept.Count);
        Assert.Equal(0, ergebnis.FilteredCount);
    }

    // Regel 3, Apply: Leere Abschnitte fallen weg und zählen nicht.
    [Fact]
    public void Apply_laesst_leere_Abschnitte_weg_ohne_zu_zaehlen()
    {
        var ergebnis = HallucinationFilter.Apply(Liste(
            Abschnitt("", anfang: 0, ende: 1),
            Abschnitt("Hallo", anfang: 1, ende: 2),
            Abschnitt("   ", keineSprache: 0.99, anfang: 2, ende: 3),
            Abschnitt("Welt", anfang: 3, ende: 4),
            Abschnitt("\t", kompression: 9.0, anfang: 4, ende: 5)));

        Assert.Equal("Hallo Welt", ergebnis.Text);
        Assert.Equal(0, ergebnis.FilteredCount);
        Assert.Equal(new[] { 1.0, 3.0 }, ergebnis.Kept.Select(a => a.StartSeconds));
    }

    // Regel 3, Apply: Verworfene zählen in FilteredCount; Kept behält die Reihenfolge.
    [Fact]
    public void Apply_zaehlt_verworfene_und_behaelt_die_Reihenfolge()
    {
        var eingabe = Liste(
            Abschnitt(" Erster Satz.", wortsicherheit: -0.2, keineSprache: 0.01, kompression: 1.1, anfang: 0, ende: 1),
            Abschnitt(" Rauschen eins", wortsicherheit: -0.2, keineSprache: 0.95, kompression: 1.0, anfang: 1, ende: 2), // Regel B
            Abschnitt(" Zweiter Satz.", wortsicherheit: -0.3, keineSprache: 0.02, kompression: 1.2, anfang: 2, ende: 3),
            Abschnitt(" Rauschen zwei", wortsicherheit: -1.5, keineSprache: 0.7, kompression: 1.0, anfang: 3, ende: 4), // Regel A
            Abschnitt(" la la la la la", wortsicherheit: -0.2, keineSprache: 0.02, kompression: 3.0, anfang: 4, ende: 5), // Regel C
            Abschnitt(" Das ist ein Testphantom.", wortsicherheit: -0.2, keineSprache: 0.02, kompression: 1.0, anfang: 5, ende: 6), // Regel D
            Abschnitt(" Dritter Satz.", wortsicherheit: -0.1, keineSprache: 0.03, kompression: 1.3, anfang: 6, ende: 7));

        var ergebnis = HallucinationFilter.Apply(eingabe, MitZusatztext);

        Assert.Equal("Erster Satz. Zweiter Satz. Dritter Satz.", ergebnis.Text);
        Assert.Equal(4, ergebnis.FilteredCount);
        Assert.Equal(new[] { 0.0, 2.0, 6.0 }, ergebnis.Kept.Select(a => a.StartSeconds));
        Assert.Equal(new[] { 1.0, 3.0, 7.0 }, ergebnis.Kept.Select(a => a.EndSeconds));
        Assert.Equal(
            new[] { "Erster Satz.", "Zweiter Satz.", "Dritter Satz." },
            ergebnis.Kept.Select(a => a.Text.Trim()));
        Assert.Equal(new double?[] { -0.2, -0.3, -0.1 }, ergebnis.Kept.Select(a => a.AvgLogProb));
        Assert.Equal(new double?[] { 0.01, 0.02, 0.03 }, ergebnis.Kept.Select(a => a.NoSpeechProb));
        Assert.Equal(new double?[] { 1.1, 1.2, 1.3 }, ergebnis.Kept.Select(a => a.CompressionRatio));
    }

    // Regel 3, Apply: Ohne Optionen gelten die Standardgrenzen und keine Zusatztexte.
    [Fact]
    public void Apply_ohne_Optionen_nimmt_Standardgrenzen()
    {
        var eingabe = Liste(
            Abschnitt("Hallo", wortsicherheit: -1.0, keineSprache: 0.6, kompression: 2.4, anfang: 0, ende: 1),
            Abschnitt("Das ist ein Testphantom.", anfang: 1, ende: 2),
            Abschnitt("Welt", wortsicherheit: -1.01, keineSprache: 0.6, anfang: 2, ende: 3));

        var ergebnis = HallucinationFilter.Apply(eingabe);

        Assert.Equal("Hallo Das ist ein Testphantom.", ergebnis.Text);
        Assert.Equal(1, ergebnis.FilteredCount);
        Assert.Equal(2, ergebnis.Kept.Count);
    }

    // Regel 3, Apply: Alles verworfen ergibt leeren Text.
    [Fact]
    public void Apply_alles_verworfen_ergibt_leeren_Text()
    {
        var ergebnis = HallucinationFilter.Apply(
            Liste(
                Abschnitt("Rauschen", keineSprache: 0.95, anfang: 0, ende: 1),
                Abschnitt("noch mehr noch mehr", kompression: 4.0, anfang: 1, ende: 2),
                Abschnitt("Das ist ein Testphantom", anfang: 2, ende: 3)),
            MitZusatztext);

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Empty(ergebnis.Kept);
        Assert.Equal(3, ergebnis.FilteredCount);
    }

    // Regel 3, Apply: Nur leere Abschnitte ergeben leeren Text und zählen nicht.
    [Fact]
    public void Apply_nur_leere_Abschnitte_ergibt_leeren_Text_ohne_Zaehlung()
    {
        var ergebnis = HallucinationFilter.Apply(Liste(Abschnitt(""), Abschnitt("  "), Abschnitt("\n")));

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Empty(ergebnis.Kept);
        Assert.Equal(0, ergebnis.FilteredCount);
    }

    // Regel 3, Apply: Ein bekannter Phantomtext als einziger Abschnitt wird verworfen und gezählt.
    [Fact]
    public void Apply_verwirft_bekannten_Phantomtext_als_einzigen_Abschnitt()
    {
        var ergebnis = HallucinationFilter.Apply(Liste(Abschnitt(" Untertitel im Auftrag des ZDF für funk, 2017")));

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Empty(ergebnis.Kept);
        Assert.Equal(1, ergebnis.FilteredCount);
    }

    // ---------- FilterText ----------

    // Regel 3, FilterText: Ist der ganze Text ein Phantomtext, kommt leer zurück.
    [Fact]
    public void FilterText_ganzer_Phantomtext_ergibt_leer()
    {
        Assert.Equal(string.Empty, HallucinationFilter.FilterText("Untertitel im Auftrag des ZDF für funk, 2017"));

        foreach (var eintrag in HallucinationFilter.KnownPhrases)
        {
            Assert.Equal(string.Empty, HallucinationFilter.FilterText("  " + eintrag + "  "));
        }
    }

    // Regel 3, FilterText: Zusatztexte aus den Optionen gelten auch hier.
    [Fact]
    public void FilterText_mit_Zusatztext_ergibt_leer()
    {
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(" Das ist ein Testphantom. ", MitZusatztext));
        Assert.Equal("Das ist ein Testphantom.", HallucinationFilter.FilterText(" Das ist ein Testphantom. "));
    }

    // Regel 3, FilterText: Sonst kommt der Text ohne Randleerzeichen zurück, im Inneren unverändert.
    [Theory]
    [InlineData("  Hallo Welt.  ", "Hallo Welt.")]
    [InlineData("Hallo Welt.", "Hallo Welt.")]
    [InlineData("\tZwei  Leerzeichen bleiben, Satzzeichen auch!\r\n", "Zwei  Leerzeichen bleiben, Satzzeichen auch!")]
    [InlineData(" Vielen Dank ", "Vielen Dank")]
    [InlineData("Vielen Dank fürs Zuschauen und bis bald", "Vielen Dank fürs Zuschauen und bis bald")]
    [InlineData("Untertitel im Auftrag des ZDFneo", "Untertitel im Auftrag des ZDFneo")]
    public void FilterText_gibt_gewoehnlichen_Text_ohne_Randleerzeichen_zurueck(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, HallucinationFilter.FilterText(eingabe));
    }

    // Regel 3, FilterText: null oder nur Leerzeichen ergibt leer.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \t\r\n ")]
    public void FilterText_ohne_Inhalt_ergibt_leer(string? eingabe)
    {
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(eingabe));
        Assert.Equal(string.Empty, HallucinationFilter.FilterText(eingabe, MitZusatztext));
    }
}
