using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft Schlüssel und Wörterbuchprüfung ausschließlich gegen PLAYBOOK.md, Abschnitt 16, "Regeln der reinen Logik",
/// Punkt 5 (Schlüssel) und Punkt 6 (Wörterbuch anwenden) mit allen dort genannten Beispielen, dazu "Bekannte Grenzen"
/// Punkt 1 und 2. Die Tests kennen die Umsetzung nicht. Die Regelnummer in den Kommentaren meint die Teilregeln aus Punkt 6.
/// </summary>
public sealed class VocabularyCorrectorTests
{
    private static VocabularyEntry Eintrag(string schreibweise, params string[] varianten) =>
        new(schreibweise, varianten);

    private static IReadOnlyList<VocabularyEntry> Liste(params string[] schreibweisen) =>
        schreibweisen.Select(s => Eintrag(s)).ToArray();

    // Das Wörterbuch der Beispiele: LUMIORA mit Akzent, Cesaretti, Stefanos Kulinarische Welt, "beKEY = Bikey", Lüneburg, Hannover.
    private static IReadOnlyList<VocabularyEntry> Beispielwoerterbuch() =>
    [
        Eintrag("LUM\u00CDORA"),
        Eintrag("Cesaretti"),
        Eintrag("Stefanos Kulinarische Welt"),
        Eintrag("beKEY", "Bikey"),
        Eintrag("Lüneburg"),
        Eintrag("Hannover"),
    ];

    // ---------- Key ----------

    // Regel 5: Kleinbuchstaben, Akzente und Umlautpunkte entfernt. Die beiden Beispiele aus dem Playbook zuerst.
    [Theory]
    [InlineData("LUM\u00CDORA", "lumiora")]
    [InlineData("Lüneburg", "luneburg")]
    [InlineData("Cesaretti", "cesaretti")]
    [InlineData("beKEY", "bekey")]
    [InlineData("HANNOVER", "hannover")]
    [InlineData("Übung", "ubung")]
    [InlineData("Öl", "ol")]
    [InlineData("ÄÖÜ", "aou")]
    [InlineData("äöü", "aou")]
    [InlineData("\u00C8\u00E8", "ee")]
    public void Key_Kleinbuchstaben_ohne_Akzente_und_Umlautpunkte(string wort, string erwartet)
    {
        Assert.Equal(erwartet, VocabularyCorrector.Key(wort));
    }

    // Regel 5: "ß" wird "ss".
    [Theory]
    [InlineData("Straße", "strasse")]
    [InlineData("Maß", "mass")]
    [InlineData("ß", "ss")]
    public void Key_Eszett_wird_ss(string wort, string erwartet)
    {
        Assert.Equal(erwartet, VocabularyCorrector.Key(wort));
    }

    // Regel 5: Nur Buchstaben und Ziffern bleiben.
    [Theory]
    [InlineData("Za'atar", "zaatar")]
    [InlineData("Well-Aging", "wellaging")]
    [InlineData("Next.js", "nextjs")]
    [InlineData("be key", "bekey")]
    [InlineData(" Hannover ", "hannover")]
    [InlineData("Web3", "web3")]
    [InlineData("3D-Druck", "3ddruck")]
    [InlineData("(Hallo!)", "hallo")]
    public void Key_nur_Buchstaben_und_Ziffern_bleiben(string wort, string erwartet)
    {
        Assert.Equal(erwartet, VocabularyCorrector.Key(wort));
    }

    // Regel 5: Null ergibt leer. Ohne Buchstabe und Ziffer bleibt ebenfalls nichts.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("-")]
    public void Key_ohne_Buchstabe_und_Ziffer_ist_leer(string? wort)
    {
        Assert.Equal(string.Empty, VocabularyCorrector.Key(wort));
    }

    // Regel 5: Ein Umlaut aus Buchstabe plus Punkten hat denselben Schlüssel wie der Umlaut als ein Zeichen.
    [Fact]
    public void Key_zusammengesetzter_Umlaut_wie_einfacher_Umlaut()
    {
        string zusammengesetzt = "Lu" + (char)0x0308 + "neburg";

        Assert.Equal("luneburg", VocabularyCorrector.Key(zusammengesetzt));
    }

    // ---------- Apply: die Beispiele aus dem Playbook, wörtlich ----------

    [Theory]
    [InlineData("ich arbeite bei lumiora.", "ich arbeite bei LUM\u00CDORA.")]
    [InlineData("Mit Herrn Cesareti und herrn cesareti.", "Mit Herrn Cesaretti und herrn cesareti.")]
    [InlineData("Lumi Ora ist toll.", "LUM\u00CDORA ist toll.")]
    [InlineData("Das ist stefanos kulinarische welt.", "Das ist Stefanos Kulinarische Welt.")]
    [InlineData("cesarettis Buch", "Cesarettis Buch")]
    [InlineData("Ich nutze Bikey und be key.", "Ich nutze beKEY und beKEY.")]
    [InlineData("In Luneburg und Hannovers Altstadt.", "In Lüneburg und Hannovers Altstadt.")]
    [InlineData("Hannoveraner sind nett.", "Hannoveraner sind nett.")]
    public void Beispiel_aus_dem_Playbook(string text, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(erwartet, ergebnis.Text);
    }

    // Regel 6, letzter Satz: Replacements zählt die geänderten Stellen. Gezählt an den Beispielen aus dem Playbook.
    [Theory]
    [InlineData("ich arbeite bei lumiora.", 1)]
    [InlineData("Mit Herrn Cesareti und herrn cesareti.", 1)]
    [InlineData("Lumi Ora ist toll.", 1)]
    [InlineData("Das ist stefanos kulinarische welt.", 1)]
    [InlineData("cesarettis Buch", 1)]
    [InlineData("Ich nutze Bikey und be key.", 2)]
    [InlineData("In Luneburg und Hannovers Altstadt.", 1)]
    [InlineData("Hannoveraner sind nett.", 0)]
    public void Beispiel_aus_dem_Playbook_zaehlt_die_geaenderten_Stellen(string text, int erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(erwartet, ergebnis.Replacements);
    }

    // ---------- Apply: Grundverhalten ----------

    // Regel 6: Null ergibt leer mit 0.
    [Fact]
    public void Null_ergibt_leer_mit_0()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(null, Beispielwoerterbuch());

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel 6: Ohne Einträge bleibt der Text mit 0.
    [Theory]
    [InlineData("ich arbeite bei lumiora.")]
    [InlineData("  Leerraum   bleibt  ")]
    [InlineData("")]
    public void Ohne_Eintraege_bleibt_der_Text_mit_0(string text)
    {
        VocabularyCorrectionResult ohne = VocabularyCorrector.Apply(text, null);
        VocabularyCorrectionResult leer = VocabularyCorrector.Apply(text, Array.Empty<VocabularyEntry>());

        Assert.Equal(text, ohne.Text);
        Assert.Equal(0, ohne.Replacements);
        Assert.Equal(text, leer.Text);
        Assert.Equal(0, leer.Replacements);
    }

    // Regel 6: Steht die richtige Schreibweise schon da, ändert sich nichts und es wird nichts gezählt.
    [Theory]
    [InlineData("LUM\u00CDORA und Cesaretti nutzen beKEY.")]
    [InlineData("Stefanos Kulinarische Welt in Lüneburg bei Hannover.")]
    [InlineData("Cesarettis Buch und Hannovers Altstadt.")]
    [InlineData("Ein Satz ohne jedes Wort aus dem Wörterbuch.")]
    public void Richtige_Schreibweise_bleibt_und_zaehlt_nicht(string text)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel 6: Alles außerhalb der Treffer bleibt Zeichen für Zeichen erhalten, auch Leerraum und Satzzeichen.
    [Theory]
    [InlineData("  ich   arbeite\tbei lumiora ,  ok?", "  ich   arbeite\tbei LUM\u00CDORA ,  ok?")]
    [InlineData("Erste Zeile\r\nlumiora\r\n\r\nEnde  ", "Erste Zeile\r\nLUM\u00CDORA\r\n\r\nEnde  ")]
    [InlineData("(lumiora) \"cesaretti\"; hannover!", "(LUM\u00CDORA) \"Cesaretti\"; Hannover!")]
    public void Alles_ausserhalb_der_Treffer_bleibt_erhalten(string text, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(erwartet, ergebnis.Text);
    }

    // Regel 6: Jede geänderte Stelle zählt einmal.
    [Theory]
    [InlineData("lumiora, cesaretti!", "LUM\u00CDORA, Cesaretti!", 2)]
    [InlineData("lumiora lumiora lumiora", "LUM\u00CDORA LUM\u00CDORA LUM\u00CDORA", 3)]
    [InlineData("LUM\u00CDORA und lumiora", "LUM\u00CDORA und LUM\u00CDORA", 1)]
    public void Jede_geaenderte_Stelle_zaehlt_einmal(string text, string erwartet, int stellen)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(erwartet, ergebnis.Text);
        Assert.Equal(stellen, ergebnis.Replacements);
    }

    // ---------- Regel (1): Wortfolge ----------

    // Regel (1) für einzelne Wörter: gleiche Buchstaben, andere Großschreibung oder fehlende Akzente.
    [Theory]
    [InlineData("LUMIORA", "LUM\u00CDORA")]
    [InlineData("Lumiora", "LUM\u00CDORA")]
    [InlineData("lum\u00EDora", "LUM\u00CDORA")]
    [InlineData("CESARETTI", "Cesaretti")]
    [InlineData("bekey", "beKEY")]
    [InlineData("BEKEY", "beKEY")]
    [InlineData("lüneburg", "Lüneburg")]
    [InlineData("LUNEBURG", "Lüneburg")]
    public void Einzelnes_Wort_mit_gleichem_Schluessel_wird_ersetzt(string wort, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Das ist " + wort + ".", Beispielwoerterbuch());

        Assert.Equal("Das ist " + erwartet + ".", ergebnis.Text);
    }

    // Regel (1): Die Wortfolge kann auch eine Variante sein, ohne Groß und Klein.
    [Theory]
    [InlineData("Ich nutze bikey.", "Ich nutze beKEY.")]
    [InlineData("Ich nutze BIKEY.", "Ich nutze beKEY.")]
    [InlineData("Die lumi aura Kerze.", "Die LUM\u00CDORA Kerze.")]
    [InlineData("Die Lumi Aura Kerze.", "Die LUM\u00CDORA Kerze.")]
    public void Variante_wird_durch_die_Schreibweise_ersetzt(string text, string erwartet)
    {
        IReadOnlyList<VocabularyEntry> eintraege = [Eintrag("beKEY", "Bikey"), Eintrag("LUM\u00CDORA", "Lumi Aura")];

        Assert.Equal(erwartet, VocabularyCorrector.Apply(text, eintraege).Text);
    }

    // Regel (1): Zwischen den Wörtern einer Folge steht Leerraum, ein Bindestrich oder ein Punkt ohne Leerraum.
    [Theory]
    [InlineData("Das ist stefanos  kulinarische   welt.")]
    [InlineData("Das ist stefanos-kulinarische-welt.")]
    [InlineData("Das ist stefanos kulinarische-welt.")]
    [InlineData("Das ist stefanos.kulinarische.welt.")]
    [InlineData("Das ist STEFANOS KULINARISCHE WELT.")]
    public void Wortfolge_mit_erlaubten_Trennern_wird_ersetzt(string text)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal("Das ist Stefanos Kulinarische Welt.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Regel (1): Ein Komma zwischen den Wörtern trennt die Folge, es ist kein erlaubter Trenner.
    [Fact]
    public void Wortfolge_mit_Komma_dazwischen_ist_keine_Folge()
    {
        IReadOnlyList<VocabularyEntry> eintraege = [Eintrag("Kulinarische Welt")];

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("eine kulinarische, welt weite Reise", eintraege);

        Assert.Equal("eine kulinarische, welt weite Reise", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel (1): Längere Folgen gewinnen, unabhängig von der Reihenfolge der Einträge.
    [Fact]
    public void Laengere_Folge_gewinnt()
    {
        IReadOnlyList<VocabularyEntry> kurzZuerst = [Eintrag("STEFANOS"), Eintrag("Stefanos Kulinarische Welt")];
        IReadOnlyList<VocabularyEntry> langZuerst = [Eintrag("Stefanos Kulinarische Welt"), Eintrag("STEFANOS")];

        const string text = "das ist stefanos kulinarische welt und stefanos buch";
        const string erwartet = "das ist Stefanos Kulinarische Welt und STEFANOS buch";

        Assert.Equal(erwartet, VocabularyCorrector.Apply(text, kurzZuerst).Text);
        Assert.Equal(erwartet, VocabularyCorrector.Apply(text, langZuerst).Text);
    }

    // ---------- Regel (2): getrennt geschrieben ----------

    // Regel (2): zwei oder drei Wörter, getrennt durch Leerraum oder Bindestrich, zusammen der Schlüssel einer einzelnen Schreibweise.
    [Theory]
    [InlineData("Lumi Ora ist toll.")]
    [InlineData("lumi ora ist toll.")]
    [InlineData("Lumi-Ora ist toll.")]
    [InlineData("Lu mi ora ist toll.")]
    [InlineData("Lu-mi-ora ist toll.")]
    [InlineData("Lumi  Ora ist toll.")]
    public void Getrennt_geschriebenes_Wort_wird_zusammengesetzt(string text)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal("LUM\u00CDORA ist toll.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Regel (2): höchstens drei Wörter. Vier Teile werden nicht zusammengesetzt.
    [Fact]
    public void Vier_getrennte_Teile_bleiben()
    {
        const string text = "Lu mi o ra ist toll.";

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel (2): Die Schreibweise braucht mindestens 5 Zeichen. Mit genau 5 greift die Regel.
    [Fact]
    public void Getrennt_geschrieben_greift_ab_5_Zeichen()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Ta min kommt.", Liste("Tamin"));

        Assert.Equal("Tamin kommt.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Regel (2): Mit 4 Zeichen greift die Regel nicht.
    [Fact]
    public void Getrennt_geschrieben_greift_nicht_bei_4_Zeichen()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Le mo kommt.", Liste("Lemo"));

        Assert.Equal("Le mo kommt.", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // ---------- Regel (3): Genitiv ----------

    // Regel (3): Schlüssel gleich dem einer einzelnen Schreibweise plus "s", eingesetzt wird die Schreibweise plus "s".
    [Theory]
    [InlineData("cesarettis Buch", "Cesarettis Buch")]
    [InlineData("CESARETTIS Buch", "Cesarettis Buch")]
    [InlineData("hannovers Altstadt", "Hannovers Altstadt")]
    [InlineData("lumioras Kerzen", "LUM\u00CDORAs Kerzen")]
    [InlineData("luneburgs Mitte", "Lüneburgs Mitte")]
    public void Genitiv_wird_mit_s_eingesetzt(string text, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch());

        Assert.Equal(erwartet, ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Regel (3): Die Schreibweise braucht mindestens 4 Zeichen. Mit genau 4 greift die Regel.
    [Fact]
    public void Genitiv_greift_ab_4_Zeichen()
    {
        Assert.Equal("Lemos Buch", VocabularyCorrector.Apply("lemos Buch", Liste("Lemo")).Text);
    }

    // Regel (3): Mit 3 Zeichen greift die Regel nicht.
    [Fact]
    public void Genitiv_greift_nicht_bei_3_Zeichen()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("umas Buch", Liste("Uma"));

        Assert.Equal("umas Buch", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // ---------- Regel (4): ähnlich ----------

    // Regel (4): ein Zeichen ersetzt, zu viel oder zu wenig, bei großem Anfang und gleichem ersten und letzten Zeichen.
    [Theory]
    [InlineData("Cesareti", "Cesaretti")]
    [InlineData("Cesaratti", "Cesaretti")]
    [InlineData("Cesarettti", "Cesaretti")]
    [InlineData("Cessaretti", "Cesaretti")]
    [InlineData("Ceaaretti", "Cesaretti")]
    [InlineData("Hanover", "Hannover")]
    [InlineData("Hannower", "Hannover")]
    public void Aehnliches_Wort_wird_ersetzt(string wort, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Mit " + wort + " im Text.", Beispielwoerterbuch());

        Assert.Equal("Mit " + erwartet + " im Text.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Regel (4): nur wenn eingeschaltet.
    [Theory]
    [InlineData("Cesareti")]
    [InlineData("Cesaratti")]
    [InlineData("Cesarettti")]
    [InlineData("Hanover")]
    public void Aehnliches_Wort_bleibt_wenn_ausgeschaltet(string wort)
    {
        string text = "Mit " + wort + " im Text.";

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch(), fuzzy: false);

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Die übrigen Regeln hängen nicht am Schalter für Regel (4).
    [Fact]
    public void Ausgeschaltet_gelten_die_Regeln_1_bis_3_weiter()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(
            "lumiora, Lumi Ora und cesarettis Buch",
            Beispielwoerterbuch(),
            fuzzy: false);

        Assert.Equal("LUM\u00CDORA, LUM\u00CDORA und Cesarettis Buch", ergebnis.Text);
        Assert.Equal(3, ergebnis.Replacements);
    }

    // Regel (4): Das Wort muss groß beginnen.
    [Theory]
    [InlineData("cesareti")]
    [InlineData("cesaratti")]
    [InlineData("hanover")]
    public void Aehnliches_Wort_klein_geschrieben_bleibt(string wort)
    {
        string text = "Mit " + wort + " im Text.";

        Assert.Equal(text, VocabularyCorrector.Apply(text, Beispielwoerterbuch()).Text);
    }

    // Regel (4): gleiches erstes UND gleiches letztes Zeichen. Die Beispiele aus dem Playbook: Adriana und Ernesta bleiben.
    [Theory]
    [InlineData("Adriano", "Adriana")]
    [InlineData("Ernesto", "Ernesta")]
    [InlineData("Cesaretti", "Cesaretto")]
    [InlineData("Cesaretti", "Besaretti")]
    [InlineData("Hannover", "Hannovel")]
    [InlineData("Hannover", "Mannover")]
    public void Aehnliches_Wort_mit_anderem_ersten_oder_letzten_Zeichen_bleibt(string schreibweise, string wort)
    {
        string text = wort + " kommt morgen.";

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Liste(schreibweise));

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel (4): genau ein Zeichen. Zwei Abweichungen bleiben stehen.
    [Theory]
    [InlineData("Casaratti")]
    [InlineData("Cesarti")]
    [InlineData("Cesarettttti")]
    [InlineData("Hannoveraner")]
    public void Wort_mit_mehr_als_einer_Abweichung_bleibt(string wort)
    {
        string text = "Mit " + wort + " im Text.";

        Assert.Equal(text, VocabularyCorrector.Apply(text, Beispielwoerterbuch()).Text);
    }

    // Regel (4): Die Schreibweise braucht mindestens 7 Zeichen. Mit genau 7 greift die Regel, das Wort darf 6 haben.
    [Theory]
    [InlineData("Kanduru")]
    [InlineData("Kandru")]
    [InlineData("Kandorru")]
    public void Aehnlich_greift_ab_7_Zeichen_der_Schreibweise(string wort)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(wort + " kommt.", Liste("Kandoru"));

        Assert.Equal("Kandoru kommt.", ergebnis.Text);
    }

    // Regel (4): Mit 6 oder weniger Zeichen der Schreibweise greift die Regel nicht.
    [Theory]
    [InlineData("Tirone", "Tirune")]
    [InlineData("Tirone", "Tironne")]
    [InlineData("Tamin", "Tamon")]
    [InlineData("Belan", "Bellan")]
    public void Aehnlich_greift_nicht_bei_kurzer_Schreibweise(string schreibweise, string wort)
    {
        string text = wort + " kommt.";

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Liste(schreibweise));

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // ---------- Regel (5): Sprache ----------

    // Regel (5): In Englisch und Italienisch bleibt ein Treffer, der sich nur durch den großen Anfangsbuchstaben unterscheidet.
    // Die Beispiele aus dem Playbook: "il soffritto" und "cucinare" bleiben klein, "lumiora" wird trotzdem ersetzt.
    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public void Englisch_und_Italienisch_lassen_kleine_Woerter_klein(string sprache)
    {
        IReadOnlyList<VocabularyEntry> eintraege = Liste("Soffritto", "Cucinare", "LUM\u00CDORA");

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(
            "il soffritto e cucinare con lumiora",
            eintraege,
            fuzzy: true,
            language: sprache);

        Assert.Equal("il soffritto e cucinare con LUM\u00CDORA", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Regel (5): Deutsch und unbekannte Sprache setzen immer ein. Null gilt wie Deutsch.
    [Theory]
    [InlineData("de")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("auto")]
    public void Deutsch_und_unbekannte_Sprache_setzen_immer_ein(string? sprache)
    {
        IReadOnlyList<VocabularyEntry> eintraege = Liste("Soffritto", "Cucinare", "LUM\u00CDORA");

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(
            "il soffritto e cucinare con lumiora",
            eintraege,
            fuzzy: true,
            language: sprache);

        Assert.Equal("il Soffritto e Cucinare con LUM\u00CDORA", ergebnis.Text);
        Assert.Equal(3, ergebnis.Replacements);
    }

    // Regel (5): Unterscheidet sich der Treffer durch mehr als den großen Anfangsbuchstaben, wird er auch dort eingesetzt.
    [Theory]
    [InlineData("it", "uso bikey ogni giorno", "uso beKEY ogni giorno")]
    [InlineData("en", "i use bekey every day", "i use beKEY every day")]
    [InlineData("it", "a luneburg", "a Lüneburg")]
    [InlineData("en", "at lumi ora", "at LUM\u00CDORA")]
    public void Englisch_und_Italienisch_setzen_groessere_Unterschiede_ein(string sprache, string text, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Beispielwoerterbuch(), fuzzy: true, language: sprache);

        Assert.Equal(erwartet, ergebnis.Text);
    }

    // ---------- Regel (6): Italienisch ohne Regel (4) ----------

    // Regel (6): Im Italienischen ist Regel (4) aus. Das Beispiel aus dem Playbook: "Cucinate" bleibt.
    [Fact]
    public void Italienisch_laesst_aehnliche_Woerter_stehen()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(
            "Cucinate bene.",
            Liste("Cucinare"),
            fuzzy: true,
            language: "it");

        Assert.Equal("Cucinate bene.", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // Regel (6) gilt nur für Italienisch: In Deutsch, Englisch und unbekannter Sprache greift Regel (4) weiter.
    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData(null)]
    [InlineData("")]
    public void Andere_Sprachen_ersetzen_aehnliche_Woerter(string? sprache)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(
            "Cucinate bene.",
            Liste("Cucinare"),
            fuzzy: true,
            language: sprache);

        Assert.Equal("Cucinare bene.", ergebnis.Text);
    }

    // Regel (6): Im Italienischen bleibt auch ein ähnlich geschriebener Name stehen, die genaue Schreibung wird weiter ersetzt.
    [Fact]
    public void Italienisch_ersetzt_weiter_nach_den_Regeln_1_bis_3()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(
            "Con Cesareti e lumiora, il libro di CESARETTIS.",
            Beispielwoerterbuch(),
            fuzzy: true,
            language: "it");

        Assert.Equal("Con Cesareti e LUM\u00CDORA, il libro di Cesarettis.", ergebnis.Text);
        Assert.Equal(2, ergebnis.Replacements);
    }

    // ---------- Zusatz: Schreibweisen aus mehreren Teilen ohne Leerraum ----------

    // Zusatz: Schreibweisen aus mehreren Teilen gelten auch zusammengeschrieben. Die drei Beispiele aus dem Playbook.
    [Theory]
    [InlineData("Wir würzen mit Zaatar.", "Wir würzen mit Za'atar.")]
    [InlineData("Das ist Wellaging.", "Das ist Well-Aging.")]
    [InlineData("Gebaut mit Nextjs.", "Gebaut mit Next.js.")]
    [InlineData("zaatar, wellaging und nextjs", "Za'atar, Well-Aging und Next.js")]
    public void Mehrteilige_Schreibweise_gilt_auch_zusammengeschrieben(string text, string erwartet)
    {
        IReadOnlyList<VocabularyEntry> eintraege = Liste("Za'atar", "Well-Aging", "Next.js");

        Assert.Equal(erwartet, VocabularyCorrector.Apply(text, eintraege).Text);
    }

    // Zusatz: Zwischen den Wörtern einer Folge sind Leerraum, Bindestrich, Punkt und Apostroph erlaubt.
    [Theory]
    [InlineData("za'atar", "Za'atar")]
    [InlineData("za atar", "Za'atar")]
    [InlineData("za-atar", "Za'atar")]
    [InlineData("well-aging", "Well-Aging")]
    [InlineData("well aging", "Well-Aging")]
    [InlineData("Well Aging", "Well-Aging")]
    [InlineData("next.js", "Next.js")]
    [InlineData("next js", "Next.js")]
    [InlineData("Next-JS", "Next.js")]
    public void Mehrteilige_Schreibweise_mit_anderem_Trenner_wird_ersetzt(string geschrieben, string erwartet)
    {
        IReadOnlyList<VocabularyEntry> eintraege = Liste("Za'atar", "Well-Aging", "Next.js");

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Heute " + geschrieben + " morgen.", eintraege);

        Assert.Equal("Heute " + erwartet + " morgen.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // Zusatz: Steht die mehrteilige Schreibweise schon richtig da, ändert sich nichts.
    [Fact]
    public void Mehrteilige_Schreibweise_richtig_geschrieben_bleibt()
    {
        IReadOnlyList<VocabularyEntry> eintraege = Liste("Za'atar", "Well-Aging", "Next.js");
        const string text = "Za'atar, Well-Aging und Next.js.";

        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, eintraege);

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // ---------- Bekannte Grenzen ----------

    // Bekannte Grenze 1: Eine Marke, die zugleich ein gewöhnliches Wort ist, wird im Deutschen überall groß geschrieben.
    [Fact]
    public void Grenze_Marke_als_gewoehnliches_Wort_wird_ueberall_gross()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("ich will cucinare lernen", Liste("Cucinare"));

        Assert.Equal("ich will Cucinare lernen", ergebnis.Text);
    }

    // Bekannte Grenze 2: Ein Eintrag "Bär" macht aus "Bar" also "Bär".
    [Theory]
    [InlineData("Die Bar ist offen.", "Die Bär ist offen.")]
    [InlineData("Die bar ist offen.", "Die Bär ist offen.")]
    public void Grenze_Wort_ohne_Umlautpunkte_wird_auf_den_Umlaut_gebracht(string text, string erwartet)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, Liste("Bär"));

        Assert.Equal(erwartet, ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }
}
