using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft den Ablauf der Nachbearbeitung ausschließlich gegen PLAYBOOK.md, Abschnitt 16, "Regeln der reinen Logik",
/// Punkt 8 (Ablauf) und "Umsetzung am 11.10.2026", Punkt 1 (Ergebnis mit Zahlen). Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class PostProcessorTests
{
    private static readonly string[] Woerterbuch =
    [
        "LUM\u00CDORA",
        "Cesaretti",
        "Stefanos Kulinarische Welt",
        "beKEY = Bikey",
        "Lüneburg",
        "Hannover",
    ];

    private static string Bearbeite(string text, PostProcessingOptions optionen) =>
        new PostProcessor().Process(text, optionen);

    // ---------- Vertrag ----------

    // Vertrag: PostProcessor ist die Umsetzung von IPostProcessor.
    [Fact]
    public void PostProcessor_setzt_IPostProcessor_um()
    {
        IPostProcessor nachbearbeitung = new PostProcessor();

        Assert.Equal("Ich wollte fragen.", nachbearbeitung.Process("Ähm, ich wollte fragen.", new PostProcessingOptions()));
    }

    // ---------- Regel 8: Ablauf ----------

    // Regel 8: null Optionen wirft ArgumentNullException.
    [Theory]
    [InlineData("Ähm, ich wollte fragen.")]
    [InlineData("")]
    public void Process_ohne_Optionen_wirft(string text)
    {
        PostProcessor nachbearbeitung = new();

        Assert.Throws<ArgumentNullException>(() => nachbearbeitung.Process(text, null!));
    }

    // Regel 8 mit Umsetzung Punkt 1: ProcessDetailed ist derselbe Ablauf, also wirft es ebenso.
    [Fact]
    public void ProcessDetailed_ohne_Optionen_wirft()
    {
        PostProcessor nachbearbeitung = new();

        Assert.Throws<ArgumentNullException>(() => nachbearbeitung.ProcessDetailed("Ein Text.", null!));
    }

    // Regel 8: leerer Text ergibt leer.
    [Fact]
    public void Leerer_Text_ergibt_leer()
    {
        Assert.Equal(string.Empty, Bearbeite(string.Empty, new PostProcessingOptions()));
        Assert.Equal(string.Empty, Bearbeite(string.Empty, new PostProcessingOptions(RemoveFillers: false, Vocabulary: Woerterbuch)));
    }

    // Regel 8 mit Regel 7 und Regel 3 (8): Ein Text nur aus Leerraum oder nur aus Füllwörtern ergibt leer.
    [Theory]
    [InlineData("   ")]
    [InlineData("\r\n")]
    [InlineData("Ähm.")]
    [InlineData("Ähm, äh.")]
    [InlineData("...")]
    public void Text_ohne_Inhalt_ergibt_leer(string text)
    {
        Assert.Equal(string.Empty, Bearbeite(text, new PostProcessingOptions()));
    }

    // Regel 8: Zusammengesetzte Zeichen werden vereinheitlicht, ein Umlaut aus Buchstabe plus Punkten wird ein Zeichen.
    [Fact]
    public void Zusammengesetzter_Umlaut_wird_ein_Zeichen()
    {
        string zusammengesetzt = "Scho" + (char)0x0308 + "n ist es.";
        string einfach = "Sch" + (char)0x00F6 + "n ist es.";

        string ergebnis = Bearbeite(zusammengesetzt, new PostProcessingOptions(RemoveFillers: false));

        Assert.Equal(einfach, ergebnis);
        Assert.Equal(einfach.Length, ergebnis.Length);
    }

    // Regel 8: Das Vereinheitlichen kommt vor den Füllwörtern. Ein Zögerlaut mit zusammengesetztem Umlaut wird erkannt.
    [Fact]
    public void Vereinheitlichen_kommt_vor_den_Fuellwoertern()
    {
        string text = "A" + (char)0x0308 + "hm, ich wollte fragen.";

        Assert.Equal("Ich wollte fragen.", Bearbeite(text, new PostProcessingOptions()));
    }

    // Regel 8: Das Vereinheitlichen kommt vor dem Wörterbuch. Die richtige Schreibweise steht danach schon da.
    [Fact]
    public void Vereinheitlichen_kommt_vor_dem_Woerterbuch()
    {
        string zusammengesetzt = "In Lu" + (char)0x0308 + "neburg.";
        string einfach = "In L" + (char)0x00FC + "neburg.";

        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(
            zusammengesetzt,
            new PostProcessingOptions(RemoveFillers: false, Vocabulary: Woerterbuch));

        Assert.Equal(einfach, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel 8: Füllwörter entfallen, wenn eingeschaltet. Das ist die Vorgabe.
    [Theory]
    [InlineData("Ähm, ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Also ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Ich, ähm, wollte sagen, dass es klappt.", "Ich wollte sagen, dass es klappt.")]
    [InlineData("Ich habe also gewartet.", "Ich habe also gewartet.")]
    public void Fuellwoerter_entfallen_in_der_Vorgabe(string text, string erwartet)
    {
        Assert.Equal(erwartet, Bearbeite(text, new PostProcessingOptions()));
    }

    // Regel 8: Sind die Füllwörter ausgeschaltet, bleiben sie stehen.
    [Theory]
    [InlineData("Ähm, ich wollte fragen.")]
    [InlineData("Also ich wollte fragen.")]
    [InlineData("Ich, ähm, wollte sagen, dass es klappt.")]
    public void Fuellwoerter_bleiben_wenn_ausgeschaltet(string text)
    {
        Assert.Equal(text, Bearbeite(text, new PostProcessingOptions(RemoveFillers: false)));
    }

    // Regel 8: Die Füllwörter laufen mit der Sprache aus den Optionen.
    [Theory]
    [InlineData("en", "Um, I think, er, yes.", "I think yes.")]
    [InlineData("en", "Well, that works.", "That works.")]
    [InlineData("it", "Allora, andiamo.", "Andiamo.")]
    [InlineData("de", "Um zu testen, kommt er.", "Um zu testen, kommt er.")]
    [InlineData("de", "Allora, andiamo.", "Allora, andiamo.")]
    [InlineData("", "Ähm, um zu testen.", "Um zu testen.")]
    [InlineData("auto", "Also, ich wollte fragen.", "Also, ich wollte fragen.")]
    public void Fuellwoerter_laufen_mit_der_Sprache(string sprache, string text, string erwartet)
    {
        Assert.Equal(erwartet, Bearbeite(text, new PostProcessingOptions(Language: sprache)));
    }

    // Regel 8: Die Füllwörter laufen mit den Zusatzwörtern aus den Optionen.
    [Fact]
    public void Fuellwoerter_laufen_mit_den_Zusatzwoertern()
    {
        PostProcessingOptions mit = new(ExtraFillers: ["sozusagen", "sag ich mal"]);
        PostProcessingOptions ohne = new();

        Assert.Equal("Das ist fertig.", Bearbeite("Das ist sozusagen fertig, sag ich mal.", mit));
        Assert.Equal("Das ist sozusagen fertig.", Bearbeite("Das ist sozusagen fertig.", ohne));
    }

    // Regel 8: Sind die Füllwörter ausgeschaltet, bleiben auch die Zusatzwörter stehen.
    [Fact]
    public void Zusatzwoerter_bleiben_wenn_Fuellwoerter_ausgeschaltet()
    {
        PostProcessingOptions optionen = new(RemoveFillers: false, ExtraFillers: ["sozusagen"]);

        Assert.Equal("Das ist sozusagen fertig.", Bearbeite("Das ist sozusagen fertig.", optionen));
    }

    // Regel 8: Das Wörterbuch läuft, wenn Einträge da sind. Die Beispiele aus Regel 6.
    [Theory]
    [InlineData("ich arbeite bei lumiora.", "ich arbeite bei LUM\u00CDORA.")]
    [InlineData("Mit Herrn Cesareti und herrn cesareti.", "Mit Herrn Cesaretti und herrn cesareti.")]
    [InlineData("Lumi Ora ist toll.", "LUM\u00CDORA ist toll.")]
    [InlineData("Das ist stefanos kulinarische welt.", "Das ist Stefanos Kulinarische Welt.")]
    [InlineData("cesarettis Buch", "Cesarettis Buch")]
    [InlineData("Ich nutze Bikey und be key.", "Ich nutze beKEY und beKEY.")]
    [InlineData("In Luneburg und Hannovers Altstadt.", "In Lüneburg und Hannovers Altstadt.")]
    [InlineData("Hannoveraner sind nett.", "Hannoveraner sind nett.")]
    public void Woerterbuch_laeuft_mit_den_Eintraegen_aus_den_Optionen(string text, string erwartet)
    {
        Assert.Equal(erwartet, Bearbeite(text, new PostProcessingOptions(Vocabulary: Woerterbuch)));
    }

    // Regel 8: Ohne Einträge läuft das Wörterbuch nicht.
    [Fact]
    public void Ohne_Eintraege_bleibt_die_Schreibung()
    {
        Assert.Equal("ich arbeite bei lumiora.", Bearbeite("ich arbeite bei lumiora.", new PostProcessingOptions()));
        Assert.Equal("ich arbeite bei lumiora.", Bearbeite("ich arbeite bei lumiora.", new PostProcessingOptions(Vocabulary: [])));
    }

    // Regel 8: Das Wörterbuch läuft mit dem Schalter für Regel 6 (4).
    [Fact]
    public void Woerterbuch_laeuft_mit_dem_Schalter_fuer_aehnliche_Woerter()
    {
        PostProcessingOptions an = new(Vocabulary: Woerterbuch);
        PostProcessingOptions aus = new(Vocabulary: Woerterbuch, FuzzyVocabulary: false);

        Assert.Equal("Mit Herrn Cesaretti.", Bearbeite("Mit Herrn Cesareti.", an));
        Assert.Equal("Mit Herrn Cesareti.", Bearbeite("Mit Herrn Cesareti.", aus));
        Assert.Equal("Bei LUM\u00CDORA.", Bearbeite("Bei lumiora.", aus));
    }

    // Regel 6, Satz zur Sprache: PostProcessor gibt die Sprache aus den Optionen an das Wörterbuch weiter.
    [Theory]
    [InlineData("it", "Voglio cucinare il soffritto con lumiora.", "Voglio cucinare il soffritto con LUM\u00CDORA.")]
    [InlineData("en", "I like cucinare and soffritto at lumiora.", "I like cucinare and soffritto at LUM\u00CDORA.")]
    [InlineData("de", "Ich mag cucinare und soffritto bei lumiora.", "Ich mag Cucinare und Soffritto bei LUM\u00CDORA.")]
    [InlineData("it", "Cucinate bene.", "Cucinate bene.")]
    [InlineData("de", "Cucinate bene.", "Cucinare bene.")]
    public void Woerterbuch_laeuft_mit_der_Sprache(string sprache, string text, string erwartet)
    {
        PostProcessingOptions optionen = new(Vocabulary: ["Cucinare", "Soffritto", "LUM\u00CDORA"], Language: sprache);

        Assert.Equal(erwartet, Bearbeite(text, optionen));
    }

    // Regel 8: Am Ende wird der Leerraum aufgeräumt, auch wenn Füllwörter aus sind und kein Wörterbuch da ist.
    [Theory]
    [InlineData("Hallo  ,  wie geht es ?", "Hallo, wie geht es?")]
    [InlineData("  das .NET SDK  ", "das .NET SDK")]
    [InlineData("Zeile eins.\r\nZeile zwei.", "Zeile eins. Zeile zwei.")]
    public void Leerraum_wird_am_Ende_aufgeraeumt(string text, string erwartet)
    {
        Assert.Equal(erwartet, Bearbeite(text, new PostProcessingOptions(RemoveFillers: false)));
        Assert.Equal(erwartet, Bearbeite(text, new PostProcessingOptions()));
    }

    // Regel 8: Der Leerraum kommt nach dem Wörterbuch. Das Wörterbuch selbst lässt Leerraum stehen, das Ergebnis ist trotzdem sauber.
    [Fact]
    public void Leerraum_kommt_nach_dem_Woerterbuch()
    {
        PostProcessingOptions optionen = new(RemoveFillers: false, Vocabulary: Woerterbuch);

        Assert.Equal("ich arbeite bei LUM\u00CDORA.", Bearbeite("  ich   arbeite bei  lumiora .  ", optionen));
    }

    // Regel 8: alle Stufen zusammen.
    [Theory]
    [InlineData("Ähm, ich arbeite bei  lumiora .", "Ich arbeite bei LUM\u00CDORA.")]
    [InlineData("Also, ich nutze Bikey und be key.", "Ich nutze beKEY und beKEY.")]
    [InlineData("Ich, ähm, wohne in Luneburg , also bei Herrn Cesareti.", "Ich wohne in Lüneburg, also bei Herrn Cesaretti.")]
    public void Alle_Stufen_zusammen(string text, string erwartet)
    {
        Assert.Equal(erwartet, Bearbeite(text, new PostProcessingOptions(Vocabulary: Woerterbuch)));
    }

    // Regel 8: Die Füllwörter kommen vor dem Wörterbuch. Ein Zusatzwort entfällt, bevor das Wörterbuch es zu einem Namen zusammensetzt.
    [Fact]
    public void Fuellwoerter_kommen_vor_dem_Woerterbuch()
    {
        PostProcessingOptions optionen = new(Vocabulary: Woerterbuch, ExtraFillers: ["lumi"]);

        Assert.Equal("Ora ist toll.", Bearbeite("Lumi Ora ist toll.", optionen));
    }

    // Regel 8: Die Glättung läuft hier nicht. Jede Stufe liefert dasselbe wie "aus".
    [Fact]
    public void Glaettung_laeuft_nicht()
    {
        const string text = "Ähm, ich arbeite bei  lumiora , also echt gern .";
        string ohne = Bearbeite(text, new PostProcessingOptions(Vocabulary: Woerterbuch));

        foreach (SmoothingLevel stufe in Enum.GetValues<SmoothingLevel>())
        {
            string mit = Bearbeite(text, new PostProcessingOptions(Smoothing: stufe, Vocabulary: Woerterbuch));

            Assert.Equal(ohne, mit);
        }

        Assert.Equal("Ich arbeite bei LUM\u00CDORA, also echt gern.", ohne);
    }

    // ---------- Umsetzung am 11.10.2026, Punkt 1: Ergebnis mit Zahlen ----------

    // Punkt 1: Process ist gleichbedeutend mit ProcessDetailed(...).Text.
    [Theory]
    [InlineData("")]
    [InlineData("Ähm.")]
    [InlineData("Ähm, ich arbeite bei  lumiora .")]
    [InlineData("Ich nutze Bikey und be key.")]
    [InlineData("Ein Satz ohne Besonderheiten.")]
    public void Process_ist_gleich_ProcessDetailed_Text(string text)
    {
        PostProcessor nachbearbeitung = new();
        PostProcessingOptions optionen = new(Vocabulary: Woerterbuch);

        Assert.Equal(nachbearbeitung.ProcessDetailed(text, optionen).Text, nachbearbeitung.Process(text, optionen));
    }

    // Punkt 1: alle fünf Zahlen an einem Beispiel mit Füllwort und zwei Stellen aus dem Wörterbuch.
    [Fact]
    public void ProcessDetailed_liefert_Text_und_Zahlen()
    {
        const string text = "Ähm, ich nutze Bikey und be key.";

        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(text, new PostProcessingOptions(Vocabulary: Woerterbuch));

        Assert.Equal("Ich nutze beKEY und beKEY.", ergebnis.Text);
        Assert.True(ergebnis.FillersChanged);
        Assert.Equal(2, ergebnis.Replacements);
        Assert.Equal(text.Length, ergebnis.CharsIn);
        Assert.Equal("Ich nutze beKEY und beKEY.".Length, ergebnis.CharsOut);
    }

    // Punkt 1: FillersChanged ist wahr, wenn die Stufe Füllwörter den Text verändert hat.
    [Theory]
    [InlineData("Ähm, ich wollte fragen.")]
    [InlineData("Also ich wollte fragen.")]
    [InlineData("Ich, ähm, wollte sagen, dass es klappt.")]
    [InlineData("Ähm.")]
    public void FillersChanged_ist_wahr_wenn_ein_Fuellwort_entfaellt(string text)
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(text, new PostProcessingOptions());

        Assert.True(ergebnis.FillersChanged);
    }

    // Punkt 1: Ändert die Stufe Füllwörter nichts, ist FillersChanged falsch, auch wenn das Wörterbuch etwas ändert.
    [Theory]
    [InlineData("Ich wollte fragen.")]
    [InlineData("Ich habe also gewartet.")]
    [InlineData("Also.")]
    [InlineData("ich arbeite bei lumiora.")]
    public void FillersChanged_ist_falsch_wenn_kein_Fuellwort_entfaellt(string text)
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(text, new PostProcessingOptions(Vocabulary: Woerterbuch));

        Assert.False(ergebnis.FillersChanged);
    }

    // Punkt 1: Ist die Stufe Füllwörter ausgeschaltet, hat sie nichts verändert.
    [Fact]
    public void FillersChanged_ist_falsch_wenn_die_Stufe_aus_ist()
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(
            "Ähm, ich wollte fragen.",
            new PostProcessingOptions(RemoveFillers: false));

        Assert.False(ergebnis.FillersChanged);
        Assert.Equal("Ähm, ich wollte fragen.", ergebnis.Text);
    }

    // Punkt 1: Replacements ist die Zahl der Stellen, die das Wörterbuch geändert hat.
    [Theory]
    [InlineData("Ein Satz ohne Besonderheiten.", 0)]
    [InlineData("LUM\u00CDORA und Cesaretti.", 0)]
    [InlineData("ich arbeite bei lumiora.", 1)]
    [InlineData("Ich nutze Bikey und be key.", 2)]
    [InlineData("lumiora, cesaretti und hannover", 3)]
    public void Replacements_zaehlt_die_Stellen_des_Woerterbuchs(string text, int erwartet)
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(text, new PostProcessingOptions(Vocabulary: Woerterbuch));

        Assert.Equal(erwartet, ergebnis.Replacements);
    }

    // Punkt 1: Ohne Wörterbuch gibt es keine geänderte Stelle.
    [Fact]
    public void Replacements_ist_0_ohne_Woerterbuch()
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed("ich arbeite bei lumiora.", new PostProcessingOptions());

        Assert.Equal(0, ergebnis.Replacements);
    }

    // Punkt 1: CharsIn und CharsOut sind die Längen vor und nach der Nachbearbeitung.
    [Theory]
    [InlineData("Ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Ähm, ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Ähm.", "")]
    [InlineData("  Hallo  ,  Welt  ", "Hallo, Welt")]
    [InlineData("Lumi Ora ist toll.", "LUM\u00CDORA ist toll.")]
    public void CharsIn_und_CharsOut_sind_die_Laengen(string text, string erwartet)
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(text, new PostProcessingOptions(Vocabulary: Woerterbuch));

        Assert.Equal(erwartet, ergebnis.Text);
        Assert.Equal(text.Length, ergebnis.CharsIn);
        Assert.Equal(erwartet.Length, ergebnis.CharsOut);
    }

    // Punkt 1 mit Regel 8: Ein leerer Text ergibt leer mit lauter Nullen.
    [Fact]
    public void ProcessDetailed_leerer_Text()
    {
        PostProcessingResult ergebnis = new PostProcessor().ProcessDetailed(string.Empty, new PostProcessingOptions(Vocabulary: Woerterbuch));

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.False(ergebnis.FillersChanged);
        Assert.Equal(0, ergebnis.Replacements);
        Assert.Equal(0, ergebnis.CharsIn);
        Assert.Equal(0, ergebnis.CharsOut);
    }
}
