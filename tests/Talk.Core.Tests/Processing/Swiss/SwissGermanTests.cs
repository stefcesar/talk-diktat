// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Processing;
using Talk.Core.Processing.Swiss;
using Talk.Core.Transcription;

using Xunit;

namespace Talk.Core.Tests.Processing.Swiss;

/// <summary>
/// Prüft Schweizerdeutsch und Mundart ausschließlich gegen PLAYBOOK.md, Abschnitt 26, Regeln S1 bis S7:
/// Schreibweise, Helvetismen, eingebaute Listen, Nachbearbeitung, Füllwörter, Wörterbuch und Sprachcodes.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test. Alle Texte sind erfunden.
/// </summary>
public sealed class SwissGermanTests
{
    private static SwissCorrectionResult Helvetismen(string? text, params string[] zeilen) => SwissText.Helvetisms(text, zeilen);

    private static PostProcessingResult Bearbeite(string text, PostProcessingOptions optionen) =>
        new PostProcessor().ProcessDetailed(text, optionen);

    // ---------- S1: Schreibweise ----------

    // S1: Jedes ß wird ss, ẞ wird SS, der Rest bleibt.
    [Theory]
    [InlineData("Straße", "Strasse")]
    [InlineData("Die große Straße, gleich daneben.", "Die grosse Strasse, gleich daneben.")]
    [InlineData("STRAẞE", "STRASSE")]
    [InlineData("Maß und ẞ", "Mass und SS")]
    [InlineData("Ohne Sonderzeichen, aber mit Ümläuten.", "Ohne Sonderzeichen, aber mit Ümläuten.")]
    [InlineData("  ß  ", "  ss  ")]
    public void Spelling_ersetzt_nur_das_scharfe_s(string text, string erwartet)
    {
        Assert.Equal(erwartet, SwissText.Spelling(text));
    }

    // S1: null und leer ergeben "".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Spelling_ohne_Text_ist_leer(string? text)
    {
        Assert.Equal("", SwissText.Spelling(text));
    }

    // ---------- S2: Helvetismen ----------

    // S2: Ein ganzes Wort wird durch das Schweizer Wort ersetzt, Vergleich ohne Groß und Klein.
    [Fact]
    public void Helvetisms_ersetzt_ganzes_Wort()
    {
        SwissCorrectionResult ergebnis = Helvetismen("Ich fahre mit dem Fahrrad.", "Velo = Fahrrad");

        Assert.Equal("Ich fahre mit dem Velo.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // S2: Schreibung des Ersatzes folgt dem Original: groß beginnend, ganz groß, klein (dann wie geschrieben).
    [Theory]
    [InlineData("Wir parken hier.", "parkieren = parken", "Wir parkieren hier.")]
    [InlineData("Parken ist teuer.", "parkieren = parken", "Parkieren ist teuer.")]
    [InlineData("PARKEN VERBOTEN", "parkieren = parken", "PARKIEREN VERBOTEN")]
    [InlineData("mein fahrrad", "Velo = Fahrrad", "mein Velo")]
    [InlineData("Mein Fahrrad", "Velo = Fahrrad", "Mein Velo")]
    [InlineData("MEIN FAHRRAD", "Velo = Fahrrad", "MEIN VELO")]
    public void Helvetisms_uebernimmt_die_Schreibung_des_Originals(string text, string zeile, string erwartet)
    {
        Assert.Equal(erwartet, Helvetismen(text, zeile).Text);
    }

    // S2: Umlaute zählen: "Fahrrad" trifft nicht "Fahrräder".
    [Fact]
    public void Helvetisms_unterscheidet_Umlaute()
    {
        SwissCorrectionResult ergebnis = Helvetismen("Zwei Fahrräder stehen da.", "Velo = Fahrrad");

        Assert.Equal("Zwei Fahrräder stehen da.", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // S2: Nie ein Teil eines längeren Worts.
    [Theory]
    [InlineData("Der Fahrradweg ist neu.")]
    [InlineData("Ein Elektrofahrrad.")]
    [InlineData("Fahrrads Kette.")]
    public void Helvetisms_ersetzt_keine_Wortteile(string text)
    {
        SwissCorrectionResult ergebnis = Helvetismen(text, "Velo = Fahrrad");

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // S2: Ist das längere Wort selbst Variante, wird es ersetzt.
    [Fact]
    public void Helvetisms_ersetzt_laengeres_Wort_wenn_es_selbst_Variante_ist()
    {
        SwissCorrectionResult ergebnis = Helvetismen("Der Fahrradweg und das Fahrrad.", "Velo = Fahrrad", "Veloweg = Fahrradweg");

        Assert.Equal("Der Veloweg und das Velo.", ergebnis.Text);
        Assert.Equal(2, ergebnis.Replacements);
    }

    // S2: Wörter mit Bindestrich zählen als eigene Wörter.
    [Fact]
    public void Helvetisms_Bindestrich_trennt_Woerter()
    {
        SwissCorrectionResult ergebnis = Helvetismen("Die Fahrrad-Tour und die Tour-Fahrrad-Aktion.", "Velo = Fahrrad");

        Assert.Equal("Die Velo-Tour und die Tour-Velo-Aktion.", ergebnis.Text);
        Assert.Equal(2, ergebnis.Replacements);
    }

    // S2: Varianten oder Schweizer Wörter aus mehreren Wörtern werden nicht beachtet.
    [Theory]
    [InlineData("Velo = Fahr rad", "Das Fahr rad steht.")]
    [InlineData("Velo Weg = Fahrrad", "Das Fahrrad steht.")]
    public void Helvetisms_ignoriert_mehrere_Woerter(string zeile, string text)
    {
        SwissCorrectionResult ergebnis = Helvetismen(text, zeile);

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // S2: Dieselbe Variante in zwei Zeilen: die erste Zeile gewinnt.
    [Fact]
    public void Helvetisms_erste_Zeile_gewinnt()
    {
        Assert.Equal("Das Velo.", Helvetismen("Das Fahrrad.", "Velo = Fahrrad", "Göppel = Fahrrad").Text);
        Assert.Equal("Das Göppel.", Helvetismen("Das Fahrrad.", "Göppel = Fahrrad", "Velo = Fahrrad").Text);
    }

    // S2: Replacements zählt die ersetzten Wörter.
    [Fact]
    public void Helvetisms_zaehlt_jedes_ersetzte_Wort()
    {
        SwissCorrectionResult ergebnis = Helvetismen(
            "Fahrrad, Fahrrad und parken im Krankenhaus.", "Velo = Fahrrad", "parkieren = parken", "Spital = Krankenhaus");

        Assert.Equal("Velo, Velo und parkieren im Spital.", ergebnis.Text);
        Assert.Equal(4, ergebnis.Replacements);
    }

    // S2: Text außerhalb der Treffer bleibt Zeichen für Zeichen gleich.
    [Fact]
    public void Helvetisms_laesst_Umgebung_unveraendert()
    {
        SwissCorrectionResult ergebnis = Helvetismen("  Das  Fahrrad,\tja?!  (Fahrrad)…\n", "Velo = Fahrrad");

        Assert.Equal("  Das  Velo,\tja?!  (Velo)…\n", ergebnis.Text);
        Assert.Equal(2, ergebnis.Replacements);
    }

    // S2: null Text ergibt "" und 0.
    [Fact]
    public void Helvetisms_ohne_Text()
    {
        SwissCorrectionResult ergebnis = SwissText.Helvetisms(null, ["Velo = Fahrrad"]);

        Assert.Equal("", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // S2: null oder leere Zeilen lassen den Text, wie er ist.
    [Fact]
    public void Helvetisms_ohne_Zeilen()
    {
        const string text = "Ich fahre mit dem Fahrrad.";

        SwissCorrectionResult ohne = SwissText.Helvetisms(text, null);
        SwissCorrectionResult leer = SwissText.Helvetisms(text, []);

        Assert.Equal(text, ohne.Text);
        Assert.Equal(0, ohne.Replacements);
        Assert.Equal(text, leer.Text);
        Assert.Equal(0, leer.Replacements);
    }

    // ---------- S3: eingebaute Listen ----------

    private static IReadOnlyList<VocabularyEntry> Eintraege(IReadOnlyList<string> zeilen) =>
        zeilen.Select(z => VocabularyEntry.Parse(z)!).ToArray();

    // S3: Helvetismen sind nicht leer und enthalten die Kernwörter.
    [Theory]
    [InlineData("Velo", "Fahrrad")]
    [InlineData("parkieren", "parken")]
    [InlineData("Spital", "Krankenhaus")]
    public void Helvetismen_enthalten_Kernwoerter(string schweiz, string variante)
    {
        Assert.NotEmpty(SwissWordLists.Helvetisms);
        Assert.Contains(Eintraege(SwissWordLists.Helvetisms),
            e => e.Term == schweiz && e.Aliases.Contains(variante, StringComparer.OrdinalIgnoreCase));
    }

    // S3: Jede Zeile lässt sich lesen und hat mindestens eine Variante.
    [Fact]
    public void Helvetismen_jede_Zeile_ist_gueltig()
    {
        foreach (string zeile in SwissWordLists.Helvetisms)
        {
            VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);
            Assert.NotNull(eintrag);
            Assert.NotEmpty(eintrag.Aliases);
        }
    }

    // S3: Keine Variante steht in zwei verschiedenen Zeilen (ohne Groß und Klein).
    [Fact]
    public void Helvetismen_keine_doppelte_Variante()
    {
        string[] doppelt = SwissWordLists.Helvetisms
            .SelectMany((z, i) => VocabularyEntry.Parse(z)!.Aliases.Distinct(StringComparer.OrdinalIgnoreCase).Select(a => (a, i)))
            .GroupBy(x => x.a, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.i).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(doppelt);
    }

    // S3: Mundart je Dialekt nicht leer.
    [Theory]
    [InlineData("zh")]
    [InlineData("be")]
    [InlineData("bs")]
    [InlineData("mix")]
    public void Mundart_je_Dialekt_nicht_leer(string dialekt)
    {
        Assert.NotEmpty(SwissWordLists.Mundart(dialekt));
    }

    // S3: Unbekannt und null ergeben die gemischte Liste.
    [Theory]
    [InlineData("xx")]
    [InlineData(null)]
    public void Mundart_unbekannt_ist_gemischt(string? dialekt)
    {
        Assert.Equal(SwissWordLists.Mundart("mix"), SwissWordLists.Mundart(dialekt));
    }

    // S3: Typische Formen: Bern "nid", Zürich "nöd", Basel "nit".
    [Theory]
    [InlineData("be", "nid")]
    [InlineData("zh", "nöd")]
    [InlineData("bs", "nit")]
    public void Mundart_enthaelt_typische_Form(string dialekt, string form)
    {
        Assert.Contains(Eintraege(SwissWordLists.Mundart(dialekt)), e => e.Term == form);
    }

    // S3: Namen der Dialekte.
    [Theory]
    [InlineData("zh", "Zürichdeutsch")]
    [InlineData("be", "Berndeutsch")]
    [InlineData("bs", "Baseldeutsch")]
    public void DialectName_bekannte_Dialekte(string dialekt, string name)
    {
        Assert.Equal(name, SwissWordLists.DialectName(dialekt));
    }

    // S3: Unbekannt oder null: ein Name mit "Mundart".
    [Theory]
    [InlineData("xx")]
    [InlineData(null)]
    [InlineData("mix")]
    public void DialectName_unbekannt_nennt_Mundart(string? dialekt)
    {
        Assert.Contains("Mundart", SwissWordLists.DialectName(dialekt));
    }

    // S3: Hinweiswörter nicht leer, mit "Velo", ohne Gruß.
    [Fact]
    public void HintTerms_mit_Velo_ohne_Gruss()
    {
        Assert.NotEmpty(SwissWordLists.HintTerms);
        Assert.Contains("Velo", SwissWordLists.HintTerms);
        Assert.DoesNotContain(SwissWordLists.HintTerms, w => w.Contains("Grüezi", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- S4: Nachbearbeitung ----------

    // S4: Bei "de-ch" und "gsw" Helvetismen und ss.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData("gsw-be")]
    public void PostProcessor_schweiz_ersetzt_und_schreibt_ss(string sprache)
    {
        PostProcessingResult ergebnis = Bearbeite("Ich fahre mit dem Fahrrad zur Straße.", new PostProcessingOptions(Language: sprache));

        Assert.Contains("Velo", ergebnis.Text);
        Assert.Contains("Strasse", ergebnis.Text);
        Assert.DoesNotContain("ß", ergebnis.Text);
        Assert.DoesNotContain("Fahrrad", ergebnis.Text);
    }

    // S4: Wörterbuchbegriffe mit ß kommen mit ss heraus.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    public void PostProcessor_schweiz_Woerterbuch_mit_ss(string sprache)
    {
        PostProcessingOptions optionen = new(Language: sprache, Vocabulary: ["Großmünster = Kathedrale"]);

        PostProcessingResult ergebnis = Bearbeite("Wir besuchen die Kathedrale heute.", optionen);

        Assert.Contains("Grossmünster", ergebnis.Text);
        Assert.DoesNotContain("ß", ergebnis.Text);
    }

    // S4: Eigene Helvetismen gelten und gewinnen gegen die eingebauten.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    public void PostProcessor_eigene_Helvetismen_gewinnen(string sprache)
    {
        PostProcessingOptions optionen = new(Language: sprache, SwissVocabulary: ["Göppel = Fahrrad"]);

        PostProcessingResult ergebnis = Bearbeite("Ich fahre mit dem Fahrrad.", optionen);

        Assert.Contains("Göppel", ergebnis.Text);
        Assert.DoesNotContain("Velo", ergebnis.Text);
    }

    // S4: Eigene Helvetismen für ein Wort, das nicht eingebaut ist, werden angewandt.
    [Fact]
    public void PostProcessor_eigene_Helvetismen_werden_angewandt()
    {
        PostProcessingOptions optionen = new(Language: "de-ch", SwissVocabulary: ["Natel = Mobiltelefon"]);

        PostProcessingResult ergebnis = Bearbeite("Mein Mobiltelefon ist leer.", optionen);

        Assert.Contains("Natel", ergebnis.Text);
        Assert.DoesNotContain("Mobiltelefon", ergebnis.Text);
    }

    // S4: Bei "de" keine Schweizer Ersetzung, ß bleibt, auch mit eigenen Helvetismen.
    [Fact]
    public void PostProcessor_deutsch_bleibt_deutsch()
    {
        PostProcessingOptions optionen = new(Language: "de", SwissVocabulary: ["Göppel = Fahrrad"]);

        PostProcessingResult ergebnis = Bearbeite("Ich fahre mit dem Fahrrad zur Straße.", optionen);

        Assert.Contains("Fahrrad", ergebnis.Text);
        Assert.Contains("Straße", ergebnis.Text);
        Assert.DoesNotContain("Velo", ergebnis.Text);
        Assert.DoesNotContain("Göppel", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // S4: Die Zahl der Ersetzungen enthält die Schweizer Ersetzungen.
    [Fact]
    public void PostProcessor_zaehlt_Schweizer_Ersetzungen()
    {
        PostProcessingResult nur = Bearbeite("Ich fahre mit dem Fahrrad ins Krankenhaus.", new PostProcessingOptions(Language: "de-ch"));
        PostProcessingResult mit = Bearbeite(
            "Herr Cesareti fährt mit dem Fahrrad.", new PostProcessingOptions(Language: "de-ch", Vocabulary: ["Cesaretti"]));

        Assert.Equal(2, nur.Replacements);
        Assert.Equal(2, mit.Replacements);
        Assert.Contains("Cesaretti", mit.Text);
    }

    // ---------- S5: Füllwörter ----------

    // S5: Schweizer Codes zählen für Füllwörter als Deutsch.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData("gsw-be")]
    [InlineData("GSW")]
    public void NormalizeLanguage_schweiz_ist_deutsch(string sprache)
    {
        Assert.Equal("de", FillerWords.NormalizeLanguage(sprache));
    }

    // ---------- S6: unscharfe Wörterbuchregel ----------

    private static readonly VocabularyEntry[] Name = [new("Cesaretti", [])];

    // S6: In Mundart ist die unscharfe Regel aus, auch wenn fuzzy an ist.
    [Theory]
    [InlineData("gsw")]
    [InlineData("gsw-be")]
    public void Vocabulary_Mundart_ohne_unscharfe_Regel(string sprache)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Mit Herrn Cesareti.", Name, true, sprache);

        Assert.Equal("Mit Herrn Cesareti.", ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    // S6: Bei "de-ch" und "de" bleibt sie an.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("de")]
    public void Vocabulary_Hochdeutsch_mit_unscharfer_Regel(string sprache)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("Mit Herrn Cesareti.", Name, true, sprache);

        Assert.Equal("Mit Herrn Cesaretti.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    // ---------- S7: Sprachcodes ----------

    // S7: IsDialect.
    [Theory]
    [InlineData("gsw", true)]
    [InlineData("gsw-be", true)]
    [InlineData(" GSW-ZH ", true)]
    [InlineData("de-ch", false)]
    [InlineData("de", false)]
    [InlineData("gswx", false)]
    [InlineData(null, false)]
    public void IsDialect(string? sprache, bool erwartet)
    {
        Assert.Equal(erwartet, LanguageCodes.IsDialect(sprache));
    }

    // S7: IsSwiss gilt auch für Dialekt mit Ort; Applies folgt IsSwiss.
    [Theory]
    [InlineData("gsw-be", true)]
    [InlineData("gsw", true)]
    [InlineData("de-ch", true)]
    [InlineData("de", false)]
    [InlineData(null, false)]
    public void IsSwiss_und_Applies(string? sprache, bool erwartet)
    {
        Assert.Equal(erwartet, LanguageCodes.IsSwiss(sprache));
        Assert.Equal(erwartet, SwissText.Applies(sprache));
    }

    // S7: Whisper bekommt für Mundart mit Ort "de".
    [Fact]
    public void ToWhisper_Mundart_mit_Ort_ist_deutsch()
    {
        Assert.Equal("de", LanguageCodes.ToWhisper("gsw-be"));
    }

    // S7: DialectTag.
    [Theory]
    [InlineData("be", "gsw-be")]
    [InlineData(null, "gsw-mix")]
    [InlineData("", "gsw-mix")]
    [InlineData(" BS ", "gsw-bs")]
    public void DialectTag(string? dialekt, string erwartet)
    {
        Assert.Equal(erwartet, LanguageCodes.DialectTag(dialekt));
    }

    // S7: DialectOf.
    [Theory]
    [InlineData("gsw-be", "be")]
    [InlineData("gsw", "mix")]
    [InlineData("de", "mix")]
    public void DialectOf(string? sprache, string erwartet)
    {
        Assert.Equal(erwartet, LanguageCodes.DialectOf(sprache));
    }

    // S7: Feste Codes.
    [Fact]
    public void Feste_Codes()
    {
        Assert.Equal("de-ch", LanguageCodes.SwissStandard);
        Assert.Equal("gsw", LanguageCodes.SwissDialect);
    }
}
