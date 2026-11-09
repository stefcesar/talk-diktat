using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft die Sprachangabe und die Wortlisten ausschließlich gegen PLAYBOOK.md, Abschnitt 16,
/// "Regeln der reinen Logik", Punkt 1 (Sprache) und Punkt 2 (Listen). Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class FillerWordsTests
{
    // Regel 2: Die gemeinsamen Zögerlaute, in keiner der drei Sprachen ein Wort.
    private static readonly string[] Gemeinsam =
    [
        "äh", "ähh", "ähm", "ähhm", "öh", "öhm", "hm", "hmm", "hmmm", "mhm", "ehm", "uhm", "erm",
    ];

    private static string[] Sortiert(IEnumerable<string> woerter) =>
        woerter.OrderBy(w => w, StringComparer.Ordinal).ToArray();

    private static void GleicheMenge(IEnumerable<string> erwartet, IEnumerable<string> erhalten)
    {
        Assert.Equal(Sortiert(erwartet), Sortiert(erhalten));
    }

    // ---------- NormalizeLanguage ----------

    // Regel 1: "de" für "german" und alles, was mit "de" beginnt. Groß und Klein und Randleerzeichen sind egal.
    [Theory]
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("deutsch")]
    [InlineData("german")]
    [InlineData("DE")]
    [InlineData("De-de")]
    [InlineData("Deutsch")]
    [InlineData("DEUTSCH")]
    [InlineData("German")]
    [InlineData("GERMAN")]
    [InlineData(" de ")]
    [InlineData("  german  ")]
    [InlineData("\tde-AT\t")]
    [InlineData("de_CH")]
    [InlineData("deu")]
    public void NormalizeLanguage_erkennt_Deutsch(string sprache)
    {
        Assert.Equal("de", FillerWords.NormalizeLanguage(sprache));
    }

    // Regel 1: "en" für alles mit "en" am Anfang.
    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("english")]
    [InlineData("EN")]
    [InlineData("English")]
    [InlineData("ENGLISH")]
    [InlineData(" en ")]
    [InlineData("eng")]
    public void NormalizeLanguage_erkennt_Englisch(string sprache)
    {
        Assert.Equal("en", FillerWords.NormalizeLanguage(sprache));
    }

    // Regel 1: "it" für alles mit "it" am Anfang.
    [Theory]
    [InlineData("it")]
    [InlineData("it-IT")]
    [InlineData("italian")]
    [InlineData("italiano")]
    [InlineData("IT")]
    [InlineData("Italian")]
    [InlineData("ITALIANO")]
    [InlineData(" it ")]
    [InlineData("ita")]
    public void NormalizeLanguage_erkennt_Italienisch(string sprache)
    {
        Assert.Equal("it", FillerWords.NormalizeLanguage(sprache));
    }

    // Regel 1: sonst leer, auch null, leer, "auto" und "fr".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData("fr")]
    [InlineData("fr-FR")]
    [InlineData("es")]
    [InlineData("nl")]
    [InlineData("x")]
    public void NormalizeLanguage_unbekannt_ergibt_leer(string? sprache)
    {
        Assert.Equal(string.Empty, FillerWords.NormalizeLanguage(sprache));
    }

    // ---------- Hesitations ----------

    // Regel 2: Deutsch hat nur die gemeinsamen Zögerlaute.
    [Theory]
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("german")]
    [InlineData("Deutsch")]
    public void Hesitations_Deutsch_sind_genau_die_gemeinsamen(string sprache)
    {
        GleicheMenge(Gemeinsam, FillerWords.Hesitations(sprache));
    }

    // Regel 2: Unbekannte Sprache hat nur die gemeinsamen Zögerlaute.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("fr")]
    public void Hesitations_unbekannte_Sprache_sind_genau_die_gemeinsamen(string? sprache)
    {
        GleicheMenge(Gemeinsam, FillerWords.Hesitations(sprache));
    }

    // Regel 2: Englisch zusätzlich uh, uhh, um, umm, er.
    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("English")]
    public void Hesitations_Englisch_sind_die_gemeinsamen_und_fuenf_weitere(string sprache)
    {
        string[] erwartet = [.. Gemeinsam, "uh", "uhh", "um", "umm", "er"];

        GleicheMenge(erwartet, FillerWords.Hesitations(sprache));
    }

    // Regel 2: Italienisch zusätzlich mh.
    [Theory]
    [InlineData("it")]
    [InlineData("it-IT")]
    [InlineData("italiano")]
    public void Hesitations_Italienisch_sind_die_gemeinsamen_und_mh(string sprache)
    {
        string[] erwartet = [.. Gemeinsam, "mh"];

        GleicheMenge(erwartet, FillerWords.Hesitations(sprache));
    }

    // Regel 2, der Zweck der Trennung: "um" und "er" sind im Deutschen Wörter und stehen dort nicht in der Liste.
    [Theory]
    [InlineData("um")]
    [InlineData("er")]
    [InlineData("uh")]
    [InlineData("mh")]
    public void Hesitations_Deutsch_enthaelt_keine_Laute_anderer_Sprachen(string wort)
    {
        Assert.DoesNotContain(wort, FillerWords.Hesitations("de"), StringComparer.OrdinalIgnoreCase);
    }

    // ---------- StartFillersAlways ----------

    // Regel 2: Einstiege, die am Satzanfang immer entfallen, gibt es nur im Deutschen: also, naja, na ja, tja.
    [Theory]
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("german")]
    public void StartFillersAlways_Deutsch_sind_also_naja_na_ja_tja(string sprache)
    {
        GleicheMenge(["also", "naja", "na ja", "tja"], FillerWords.StartFillersAlways(sprache));
    }

    // Regel 2: nur Deutsch, alle anderen Sprachen haben keine solchen Einstiege.
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("fr")]
    public void StartFillersAlways_andere_Sprachen_sind_leer(string? sprache)
    {
        Assert.Empty(FillerWords.StartFillersAlways(sprache));
    }

    // ---------- StartFillersWithComma ----------

    // Regel 2: Einstiege nur mit folgendem Komma, Deutsch: so.
    [Fact]
    public void StartFillersWithComma_Deutsch_ist_so()
    {
        GleicheMenge(["so"], FillerWords.StartFillersWithComma("de"));
    }

    // Regel 2: Englisch: well, so.
    [Fact]
    public void StartFillersWithComma_Englisch_sind_well_und_so()
    {
        GleicheMenge(["well", "so"], FillerWords.StartFillersWithComma("en"));
    }

    // Regel 2: Italienisch: allora, dunque.
    [Fact]
    public void StartFillersWithComma_Italienisch_sind_allora_und_dunque()
    {
        GleicheMenge(["allora", "dunque"], FillerWords.StartFillersWithComma("it"));
    }

    // Regel 2: unbekannte Sprache keine.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("fr")]
    public void StartFillersWithComma_unbekannte_Sprache_ist_leer(string? sprache)
    {
        Assert.Empty(FillerWords.StartFillersWithComma(sprache));
    }

    // Regel 1 und Regel 2 zusammen: Die Listen hängen an der vereinheitlichten Sprache, nicht an der Schreibung der Angabe.
    [Fact]
    public void Listen_gelten_fuer_jede_Schreibung_der_Sprache()
    {
        GleicheMenge(FillerWords.Hesitations("en"), FillerWords.Hesitations(" EN-us "));
        GleicheMenge(FillerWords.StartFillersWithComma("it"), FillerWords.StartFillersWithComma("Italiano"));
        GleicheMenge(FillerWords.StartFillersAlways("de"), FillerWords.StartFillersAlways(" Deutsch "));
    }
}
