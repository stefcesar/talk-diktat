// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft den Hinweistext aus dem Wörterbuch ausschließlich gegen Regel 2 der reinen Logik in PLAYBOOK.md, Abschnitt 15.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class VocabularyPromptTests
{
    private static IReadOnlyList<string> Liste(params string[] begriffe) => begriffe;

    // Regel 2: Begriffe werden mit Komma und Leerzeichen verbunden.
    [Fact]
    public void Build_verbindet_mit_Komma_und_Leerzeichen()
    {
        var hinweis = VocabularyPrompt.Build(Liste("Cesaretti", "LUMÍORA", "Stefanos Kulinarische Welt"));

        Assert.Equal("Cesaretti, LUMÍORA, Stefanos Kulinarische Welt", hinweis);
    }

    // Regel 2: Ein einzelner Begriff kommt ohne Trennzeichen zurück.
    [Fact]
    public void Build_mit_einem_Begriff_liefert_nur_den_Begriff()
    {
        Assert.Equal("Cesaretti", VocabularyPrompt.Build(Liste("Cesaretti")));
    }

    // Regel 2: Begriffe werden an den Rändern bereinigt.
    [Fact]
    public void Build_bereinigt_die_Raender()
    {
        var hinweis = VocabularyPrompt.Build(Liste("  Cesaretti ", "\tLUMÍORA\t", " Stefanos Kulinarische Welt  "));

        Assert.Equal("Cesaretti, LUMÍORA, Stefanos Kulinarische Welt", hinweis);
    }

    // Regel 2: Leere Einträge fallen weg.
    [Fact]
    public void Build_laesst_leere_Eintraege_weg()
    {
        var hinweis = VocabularyPrompt.Build(Liste("", "Alpha", "   ", "Beta", "\t"));

        Assert.Equal("Alpha, Beta", hinweis);
    }

    // Regel 2: Doppelte fallen weg, ohne Groß und Klein, der erste gewinnt.
    [Fact]
    public void Build_laesst_Doppelte_weg_der_erste_gewinnt()
    {
        var hinweis = VocabularyPrompt.Build(Liste("Alpha", "Beta", "ALPHA", "alpha", "beta", "Gamma"));

        Assert.Equal("Alpha, Beta, Gamma", hinweis);
    }

    // Regel 2: Doppelte werden nach dem Bereinigen der Ränder erkannt.
    [Fact]
    public void Build_erkennt_Doppelte_nach_dem_Bereinigen()
    {
        var hinweis = VocabularyPrompt.Build(Liste(" lumíora ", "LUMÍORA", "Lumíora  "));

        Assert.Equal("lumíora", hinweis);
    }

    // Regel 2: Die Reihenfolge der Begriffe bleibt erhalten (Reihenfolge ist Rangfolge).
    [Fact]
    public void Build_behaelt_die_Reihenfolge()
    {
        var hinweis = VocabularyPrompt.Build(Liste("Zeta", "Alpha", "Mitte"));

        Assert.Equal("Zeta, Alpha, Mitte", hinweis);
    }

    // Regel 2: Ein Begriff, der genau passt, kommt noch hinein.
    [Fact]
    public void Build_nimmt_Begriff_der_genau_passt()
    {
        // "abc, defgh" hat genau 10 Zeichen.
        var hinweis = VocabularyPrompt.Build(Liste("abc", "defgh", "ij"), 10);

        Assert.Equal("abc, defgh", hinweis);
    }

    // Regel 2: Ein einzelner Begriff, der genau die Grenze füllt, kommt hinein.
    [Fact]
    public void Build_nimmt_einzelnen_Begriff_mit_genau_der_Grenze()
    {
        Assert.Equal("abcde", VocabularyPrompt.Build(Liste("abcde"), 5));
    }

    // Regel 2: Beim ersten Begriff, der nicht mehr passt, endet der Hinweis; spätere kürzere kommen nicht mehr dazu.
    [Fact]
    public void Build_endet_beim_ersten_Begriff_der_nicht_passt()
    {
        // "abc, defgh" bräuchte 10 Zeichen, die Grenze ist 9. "abc, ij" hätte nur 7, darf aber nicht mehr dazu.
        var hinweis = VocabularyPrompt.Build(Liste("abc", "defgh", "ij"), 9);

        Assert.Equal("abc", hinweis);
    }

    // Regel 2: Auch mehrere spätere kurze Begriffe kommen nach dem Ende nicht mehr dazu.
    [Fact]
    public void Build_nimmt_nach_dem_Ende_keinen_weiteren_Begriff()
    {
        var hinweis = VocabularyPrompt.Build(Liste("eins", "zwei", "ein sehr langer Begriff", "a", "b"), 20);

        Assert.Equal("eins, zwei", hinweis);
    }

    // Regel 2: Passt schon der erste Begriff nicht, endet der Hinweis sofort und bleibt ohne Inhalt.
    [Fact]
    public void Build_ohne_passenden_ersten_Begriff_bleibt_ohne_Inhalt()
    {
        var hinweis = VocabularyPrompt.Build(Liste("abcdef", "ab"), 5);

        Assert.True(string.IsNullOrEmpty(hinweis));
    }

    // Regel 2: Das Ergebnis ist nie länger als die Grenze.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(100)]
    public void Build_ist_nie_laenger_als_die_Grenze(int grenze)
    {
        var hinweis = VocabularyPrompt.Build(Liste("Alpha", "Beta", "Gamma", "Delta", "Epsilon"), grenze);

        Assert.True((hinweis?.Length ?? 0) <= grenze);
    }

    // Regel 2: Doppelte und leere Einträge verbrauchen keinen Platz in der Grenze.
    [Fact]
    public void Build_zaehlt_weggelassene_Eintraege_nicht_zur_Grenze()
    {
        // "Alpha, Beta" hat genau 11 Zeichen.
        var hinweis = VocabularyPrompt.Build(Liste("Alpha", "", "alpha", "  ", "Beta"), 11);

        Assert.Equal("Alpha, Beta", hinweis);
    }

    // Regel 2: Standardgrenze 400.
    [Fact]
    public void Standardgrenze_ist_400()
    {
        Assert.Equal(400, VocabularyPrompt.DefaultMaxChars);
    }

    // Regel 2: Ohne Angabe gilt die Grenze 400; 40 Begriffe zu je 8 Zeichen plus Trenner sind genau 398 Zeichen,
    // der nächste Begriff passt nicht mehr.
    [Fact]
    public void Build_ohne_Angabe_begrenzt_auf_400_Zeichen()
    {
        var begriffe = Enumerable.Range(0, 60).Select(i => $"Wort{i:0000}").ToArray();

        var hinweis = VocabularyPrompt.Build(begriffe);

        var erwartet = string.Join(", ", begriffe.Take(40));
        Assert.Equal(398, erwartet.Length);
        Assert.Equal(erwartet, hinweis);
        Assert.Equal(VocabularyPrompt.Build(begriffe, 400), hinweis);
    }

    // Regel 2: Ein Hinweis mit genau 400 Zeichen passt in die Standardgrenze.
    [Fact]
    public void Build_ohne_Angabe_nimmt_genau_400_Zeichen()
    {
        // 39 Begriffe zu je 8 Zeichen plus Trenner sind 388 Zeichen, dazu Trenner und 10 Zeichen ergibt 400.
        var begriffe = Enumerable.Range(0, 39).Select(i => $"Wort{i:0000}").Append("ZehnZeiche").Append("x").ToArray();

        var hinweis = VocabularyPrompt.Build(begriffe);

        Assert.NotNull(hinweis);
        Assert.Equal(400, hinweis.Length);
        Assert.EndsWith(", ZehnZeiche", hinweis);
    }

    // Regel 2: Keine Begriffe (null, leer, nur leere) ergeben null.
    [Fact]
    public void Build_ohne_Begriffe_ergibt_null()
    {
        Assert.Null(VocabularyPrompt.Build(null));
        Assert.Null(VocabularyPrompt.Build(Liste()));
        Assert.Null(VocabularyPrompt.Build(Liste("", "   ", "\t")));
    }

    // Regel 2: Grenze kleiner gleich 0 wirft ArgumentOutOfRangeException.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-400)]
    public void Build_mit_Grenze_kleiner_gleich_0_wirft(int grenze)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VocabularyPrompt.Build(Liste("Alpha"), grenze));
    }

    // Regel 2: Parse trennt an Komma.
    [Fact]
    public void Parse_trennt_an_Komma()
    {
        Assert.Equal(Liste("Cesaretti", "LUMÍORA", "Stefanos Kulinarische Welt"),
            VocabularyPrompt.Parse("Cesaretti, LUMÍORA, Stefanos Kulinarische Welt"));
    }

    // Regel 2: Parse trennt an Semikolon.
    [Fact]
    public void Parse_trennt_an_Semikolon()
    {
        Assert.Equal(Liste("Alpha", "Beta", "Gamma"), VocabularyPrompt.Parse("Alpha;Beta; Gamma"));
    }

    // Regel 2: Parse trennt an Zeilenumbruch (beide Schreibweisen).
    [Fact]
    public void Parse_trennt_an_Zeilenumbruch()
    {
        Assert.Equal(Liste("Alpha", "Beta", "Gamma"), VocabularyPrompt.Parse("Alpha\nBeta\r\nGamma"));
    }

    // Regel 2: Parse trennt an allen drei Zeichen zugleich und bereinigt die Ränder.
    [Fact]
    public void Parse_trennt_gemischt_und_bereinigt_Raender()
    {
        Assert.Equal(Liste("Alpha", "Beta", "Gamma", "Delta Epsilon"),
            VocabularyPrompt.Parse("  Alpha , Beta ;\n  Gamma\r\n Delta Epsilon  "));
    }

    // Regel 2: Parse lässt leere Begriffe weg.
    [Fact]
    public void Parse_laesst_leere_Begriffe_weg()
    {
        Assert.Equal(Liste("Alpha", "Beta"), VocabularyPrompt.Parse(",, Alpha ;; \n\n , Beta,;"));
    }

    // Regel 2: Parse lässt doppelte Begriffe weg und behält die Reihenfolge.
    [Fact]
    public void Parse_laesst_Doppelte_weg_und_behaelt_die_Reihenfolge()
    {
        Assert.Equal(Liste("Zeta", "Alpha", "Mitte"), VocabularyPrompt.Parse("Zeta, Alpha, Zeta; Mitte\nAlpha"));
    }

    // Regel 2: Parse mit null oder nur Leerzeichen ergibt eine leere Liste.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \t \r\n ")]
    public void Parse_ohne_Inhalt_ergibt_leere_Liste(string? eingabe)
    {
        var begriffe = VocabularyPrompt.Parse(eingabe);

        Assert.NotNull(begriffe);
        Assert.Empty(begriffe);
    }

    // Regel 2: Nur Trennzeichen ergeben ebenfalls eine leere Liste (alle Begriffe sind leer).
    [Fact]
    public void Parse_nur_Trennzeichen_ergibt_leere_Liste()
    {
        Assert.Empty(VocabularyPrompt.Parse(" , ; \n ,"));
    }

    // Regel 2: Parse und Build zusammen: die Vorgabe aus dem Testfenster ergibt den Hinweis in derselben Reihenfolge.
    [Fact]
    public void Parse_und_Build_ergeben_zusammen_den_Hinweis()
    {
        var begriffe = VocabularyPrompt.Parse("Cesaretti; lumíora\nLUMÍORA, Stefanos Kulinarische Welt");

        var hinweis = VocabularyPrompt.Build(begriffe);

        Assert.Equal("Cesaretti, lumíora, Stefanos Kulinarische Welt", hinweis);
    }
}
