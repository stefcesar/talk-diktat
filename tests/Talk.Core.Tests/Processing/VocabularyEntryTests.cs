using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft den Wörterbucheintrag ausschließlich gegen PLAYBOOK.md, Abschnitt 16, "Regeln der reinen Logik",
/// Punkt 4 (Parse, ParseAll, Terms) und den Satz zu ReadLines in Punkt 6. Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class VocabularyEntryTests
{
    private static void GleicheListe(string[] erwartet, IReadOnlyList<string> erhalten)
    {
        Assert.Equal(erwartet, erhalten.ToArray());
    }

    // ---------- Parse ----------

    // Regel 4: Form "Schreibweise", ohne Varianten.
    [Theory]
    [InlineData("LUM\u00CDORA")]
    [InlineData("Cesaretti")]
    [InlineData("Stefanos Kulinarische Welt")]
    [InlineData("Lüneburg")]
    [InlineData("Next.js")]
    public void Parse_nur_Schreibweise(string zeile)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        Assert.Equal(zeile, eintrag.Term);
        Assert.Empty(eintrag.Aliases);
    }

    // Regel 4: Form "Schreibweise = Variante 1 | Variante 2".
    [Fact]
    public void Parse_Schreibweise_mit_zwei_Varianten()
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse("Schreibweise = Variante 1 | Variante 2");

        Assert.NotNull(eintrag);
        Assert.Equal("Schreibweise", eintrag.Term);
        GleicheListe(["Variante 1", "Variante 2"], eintrag.Aliases);
    }

    // Regel 4, der Eintrag aus den Beispielen: "beKEY = Bikey".
    [Fact]
    public void Parse_Schreibweise_mit_einer_Variante()
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse("beKEY = Bikey");

        Assert.NotNull(eintrag);
        Assert.Equal("beKEY", eintrag.Term);
        GleicheListe(["Bikey"], eintrag.Aliases);
    }

    // Regel 4: Ränder ohne Leerraum, bei der Schreibweise und bei jeder Variante.
    [Theory]
    [InlineData("  beKEY  =  Bikey  |  be key  ")]
    [InlineData("beKEY=Bikey|be key")]
    [InlineData("\tbeKEY\t=\tBikey\t|\tbe key\t")]
    public void Parse_entfernt_Leerraum_an_den_Raendern(string zeile)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        Assert.Equal("beKEY", eintrag.Term);
        GleicheListe(["Bikey", "be key"], eintrag.Aliases);
    }

    // Regel 4: Ränder ohne Leerraum auch ohne Varianten.
    [Fact]
    public void Parse_entfernt_Leerraum_um_die_Schreibweise()
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse("   Cesaretti   ");

        Assert.NotNull(eintrag);
        Assert.Equal("Cesaretti", eintrag.Term);
        Assert.Empty(eintrag.Aliases);
    }

    // Regel 4: Leere Varianten fallen weg.
    [Theory]
    [InlineData("beKEY = | Bikey")]
    [InlineData("beKEY = Bikey |")]
    [InlineData("beKEY = Bikey |   | ")]
    [InlineData("beKEY = || Bikey ||")]
    public void Parse_laesst_leere_Varianten_weg(string zeile)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        Assert.Equal("beKEY", eintrag.Term);
        GleicheListe(["Bikey"], eintrag.Aliases);
    }

    // Regel 4: Sind alle Varianten leer, bleibt die Schreibweise ohne Varianten.
    [Theory]
    [InlineData("beKEY =")]
    [InlineData("beKEY = ")]
    [InlineData("beKEY = | | ")]
    public void Parse_nur_leere_Varianten_ergibt_Eintrag_ohne_Varianten(string zeile)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        Assert.Equal("beKEY", eintrag.Term);
        Assert.Empty(eintrag.Aliases);
    }

    // Regel 4: Doppelte Varianten fallen weg, ohne Groß und Klein.
    [Theory]
    [InlineData("beKEY = Bikey | Bikey")]
    [InlineData("beKEY = Bikey | bikey")]
    [InlineData("beKEY = Bikey | BIKEY | bikey | Bikey")]
    public void Parse_laesst_doppelte_Varianten_weg(string zeile)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        string variante = Assert.Single(eintrag.Aliases);
        Assert.Equal("Bikey", variante, ignoreCase: true);
    }

    // Regel 4: Doppelte fallen weg, die übrigen bleiben in ihrer Reihenfolge.
    [Fact]
    public void Parse_behaelt_die_Reihenfolge_der_Varianten()
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse("beKEY = Bikey | be key | bikey | Beekey");

        Assert.NotNull(eintrag);
        GleicheListe(["Bikey", "be key", "Beekey"], eintrag.Aliases);
    }

    // Regel 4: Varianten gleich der Schreibweise fallen weg.
    [Theory]
    [InlineData("beKEY = beKEY | Bikey")]
    [InlineData("beKEY = Bikey | beKEY")]
    [InlineData("beKEY =  beKEY  | Bikey")]
    public void Parse_laesst_Variante_gleich_der_Schreibweise_weg(string zeile)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        Assert.Equal("beKEY", eintrag.Term);
        GleicheListe(["Bikey"], eintrag.Aliases);
    }

    // Regel 4: Ohne Schreibweise (null, leer, nichts vor dem Gleichheitszeichen) kommt null.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("=")]
    [InlineData(" = ")]
    [InlineData("= Bikey")]
    [InlineData("  = Bikey | be key")]
    public void Parse_ohne_Schreibweise_ergibt_null(string? zeile)
    {
        Assert.Null(VocabularyEntry.Parse(zeile));
    }

    // Regel 4: Die Schreibweise bleibt Zeichen für Zeichen, wie sie eingetragen ist.
    [Theory]
    [InlineData("LUM\u00CDORA = Lumi Ora", "LUM\u00CDORA")]
    [InlineData("Za'atar = Satar", "Za'atar")]
    [InlineData("Well-Aging = Wellaging", "Well-Aging")]
    [InlineData("Muster-Beispiel-Klinik", "Muster-Beispiel-Klinik")]
    public void Parse_laesst_die_Schreibweise_unveraendert(string zeile, string schreibweise)
    {
        VocabularyEntry? eintrag = VocabularyEntry.Parse(zeile);

        Assert.NotNull(eintrag);
        Assert.Equal(schreibweise, eintrag.Term);
    }

    // ---------- ParseAll ----------

    // Regel 4: Die Reihenfolge bleibt.
    [Fact]
    public void ParseAll_behaelt_die_Reihenfolge()
    {
        string[] zeilen = ["LUM\u00CDORA", "Cesaretti", "Stefanos Kulinarische Welt", "beKEY = Bikey", "Lüneburg", "Hannover"];

        IReadOnlyList<VocabularyEntry> eintraege = VocabularyEntry.ParseAll(zeilen);

        Assert.Equal(
            new[] { "LUM\u00CDORA", "Cesaretti", "Stefanos Kulinarische Welt", "beKEY", "Lüneburg", "Hannover" },
            eintraege.Select(e => e.Term).ToArray());
        GleicheListe(["Bikey"], eintraege[3].Aliases);
        Assert.Empty(eintraege[0].Aliases);
    }

    // Regel 4: Unlesbare Zeilen fallen weg.
    [Fact]
    public void ParseAll_laesst_unlesbare_Zeilen_weg()
    {
        string[] zeilen = ["", "Cesaretti", "   ", "= Bikey", "Hannover", "="];

        IReadOnlyList<VocabularyEntry> eintraege = VocabularyEntry.ParseAll(zeilen);

        Assert.Equal(new[] { "Cesaretti", "Hannover" }, eintraege.Select(e => e.Term).ToArray());
    }

    // Regel 4: Bei gleichem Schlüssel der Schreibweise gewinnt der erste Eintrag.
    [Fact]
    public void ParseAll_bei_gleicher_Schreibweise_gewinnt_der_erste()
    {
        string[] zeilen = ["beKEY = Bikey", "beKEY = Beekey", "Cesaretti"];

        IReadOnlyList<VocabularyEntry> eintraege = VocabularyEntry.ParseAll(zeilen);

        Assert.Equal(2, eintraege.Count);
        Assert.Equal("beKEY", eintraege[0].Term);
        GleicheListe(["Bikey"], eintraege[0].Aliases);
        Assert.Equal("Cesaretti", eintraege[1].Term);
    }

    // Regel 4 mit Regel 5: Gleich ist, was denselben Schlüssel hat, also auch andere Großschreibung und fehlende Akzente.
    [Theory]
    [InlineData("LUM\u00CDORA", "lumiora = Lumi Ora")]
    [InlineData("LUM\u00CDORA", "Lumiora")]
    [InlineData("Lüneburg", "Luneburg")]
    [InlineData("beKEY", "BEKEY")]
    public void ParseAll_vergleicht_die_Schreibweise_ueber_den_Schluessel(string erste, string zweite)
    {
        string[] zeilen = [erste, zweite];

        IReadOnlyList<VocabularyEntry> eintraege = VocabularyEntry.ParseAll(zeilen);

        VocabularyEntry eintrag = Assert.Single(eintraege);
        Assert.Equal(erste, eintrag.Term);
        Assert.Empty(eintrag.Aliases);
    }

    // Regel 4: Null ergibt eine leere Liste.
    [Fact]
    public void ParseAll_null_ergibt_leere_Liste()
    {
        IReadOnlyList<VocabularyEntry> eintraege = VocabularyEntry.ParseAll(null);

        Assert.NotNull(eintraege);
        Assert.Empty(eintraege);
    }

    // Regel 4: Ohne Zeilen und mit nur unlesbaren Zeilen ist die Liste leer.
    [Fact]
    public void ParseAll_ohne_lesbare_Zeilen_ergibt_leere_Liste()
    {
        Assert.Empty(VocabularyEntry.ParseAll(Array.Empty<string>()));
        Assert.Empty(VocabularyEntry.ParseAll(["", "  ", "= Bikey"]));
    }

    // ---------- Terms ----------

    // Regel 4: Terms liefert nur die Schreibweisen, in Reihenfolge.
    [Fact]
    public void Terms_liefert_nur_die_Schreibweisen()
    {
        string[] zeilen = ["LUM\u00CDORA", "beKEY = Bikey | be key", "  Cesaretti  ", "Stefanos Kulinarische Welt = Stefanos Welt"];

        IReadOnlyList<string> schreibweisen = VocabularyEntry.Terms(zeilen);

        GleicheListe(["LUM\u00CDORA", "beKEY", "Cesaretti", "Stefanos Kulinarische Welt"], schreibweisen);
    }

    // Regel 4: Unlesbare Zeilen liefern keine Schreibweise.
    [Fact]
    public void Terms_laesst_unlesbare_Zeilen_weg()
    {
        string[] zeilen = ["", "Cesaretti", "= Bikey", "Hannover"];

        GleicheListe(["Cesaretti", "Hannover"], VocabularyEntry.Terms(zeilen));
    }

    // Regel 4: Null ergibt eine leere Liste.
    [Fact]
    public void Terms_null_ergibt_leere_Liste()
    {
        IReadOnlyList<string> schreibweisen = VocabularyEntry.Terms(null);

        Assert.NotNull(schreibweisen);
        Assert.Empty(schreibweisen);
    }

    // ---------- ReadLines ----------

    // Regel 6, Satz zu ReadLines: eine Zeile je Eintrag, Leerzeilen und Zeilen mit "#" am Anfang fallen weg.
    [Theory]
    [InlineData("LUM\u00CDORA\r\n\r\n# Marken\r\nbeKEY = Bikey\r\n")]
    [InlineData("LUM\u00CDORA\n\n# Marken\nbeKEY = Bikey\n")]
    [InlineData("# Kopfzeile\r\nLUM\u00CDORA\r\n   \r\nbeKEY = Bikey")]
    [InlineData("\r\n\r\nLUM\u00CDORA\r\n#\r\nbeKEY = Bikey\r\n\r\n")]
    public void ReadLines_liefert_nur_die_Zeilen_mit_Eintrag(string inhalt)
    {
        IReadOnlyList<string> zeilen = VocabularyEntry.ReadLines(inhalt);

        IReadOnlyList<VocabularyEntry> eintraege = VocabularyEntry.ParseAll(zeilen);

        Assert.Equal(2, zeilen.Count);
        Assert.Equal(new[] { "LUM\u00CDORA", "beKEY" }, eintraege.Select(e => e.Term).ToArray());
        GleicheListe(["Bikey"], eintraege[1].Aliases);
    }

    // Regel 6, Satz zu ReadLines: Ein Inhalt ohne Einträge ergibt keine Zeile.
    [Theory]
    [InlineData("")]
    [InlineData("\r\n\r\n")]
    [InlineData("# nur ein Kommentar")]
    [InlineData("# eins\r\n# zwei\r\n\r\n")]
    public void ReadLines_ohne_Eintraege_ist_leer(string inhalt)
    {
        Assert.Empty(VocabularyEntry.ReadLines(inhalt));
    }
}
