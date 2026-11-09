using Talk.Core.Audio;
using Talk.Core.Transcription;

using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft die Sprachcodes für Schweizerdeutsch (Playbook Abschnitt 26, Schritt 1, Regeln R5 bis R7):
/// LanguageCodes, TranscriptionRequest.WhisperLanguage und das Feld language beim Groq Erkenner.
/// Der Groq Teil nutzt den nachgebauten Dienst aus GroqTranscriberTests, es geht nie etwas ins Netz.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class LanguageCodesTests
{
    private const string Schluessel = "test-schluessel-123";
    private const string EinfacheAntwort = "{\"text\":\" Hallo Welt. \",\"language\":\"German\"}";

    private static CancellationToken Abbruch => TestContext.Current.CancellationToken;

    /// <summary>Clip mit 1600 Abtastwerten (100 ms).</summary>
    private static AudioClip Clip()
    {
        var werte = new short[1600];
        for (var i = 0; i < werte.Length; i++)
        {
            werte[i] = (short)((i * 37 % 2000) - 1000);
        }

        return new AudioClip(werte);
    }

    private static GroqTranscriber Erkenner(NachgebauterDienst dienst) =>
        new(new HttpClient(dienst), () => Schluessel, null, null);

    // R5: Die Konstanten.
    [Fact]
    public void R5_Konstanten()
    {
        Assert.Equal("de-ch", LanguageCodes.SwissStandard);
        Assert.Equal("gsw", LanguageCodes.SwissDialect);
        Assert.Equal("de", LanguageCodes.WhisperGerman);
    }

    // R5: IsSwiss erkennt de-ch und gsw in jeder Schreibweise und mit Randleerzeichen.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData("DE-CH")]
    [InlineData("GSW")]
    [InlineData("  De-Ch ")]
    [InlineData(" gSw  ")]
    public void R5_IsSwiss_wahr(string sprache)
    {
        Assert.True(LanguageCodes.IsSwiss(sprache));
    }

    // R5: IsSwiss ist falsch für andere Sprachen, leer und null.
    [Theory]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("auto")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("de-at")]
    [InlineData("de_ch")]
    public void R5_IsSwiss_falsch(string? sprache)
    {
        Assert.False(LanguageCodes.IsSwiss(sprache));
    }

    // R5: ToWhisper macht de-ch und gsw zu de, alles andere gekürzt und klein, null zu leer.
    [Theory]
    [InlineData("de-ch", "de")]
    [InlineData("gsw", "de")]
    [InlineData("  DE-CH ", "de")]
    [InlineData("GSW", "de")]
    [InlineData("de", "de")]
    [InlineData("  IT ", "it")]
    [InlineData("En", "en")]
    [InlineData("auto", "auto")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void R5_ToWhisper(string? sprache, string erwartet)
    {
        Assert.Equal(erwartet, LanguageCodes.ToWhisper(sprache));
    }

    // R6: WhisperLanguage entspricht ToWhisper(Language).
    [Theory]
    [InlineData("de-ch", "de")]
    [InlineData("gsw", "de")]
    [InlineData(" GSW ", "de")]
    [InlineData("de", "de")]
    [InlineData("IT", "it")]
    [InlineData("auto", "auto")]
    public void R6_WhisperLanguage(string sprache, string erwartet)
    {
        var anfrage = new TranscriptionRequest(sprache);

        Assert.Equal(erwartet, anfrage.WhisperLanguage);
        Assert.Equal(LanguageCodes.ToWhisper(sprache), anfrage.WhisperLanguage);
    }

    // R6: Vorgabe der Anfrage bleibt de.
    [Fact]
    public void R6_Vorgabe_bleibt_de()
    {
        Assert.Equal("de", new TranscriptionRequest().WhisperLanguage);
        Assert.False(new TranscriptionRequest().AutoDetect);
    }

    // R6: AutoDetect unverändert, auto und leer sind wahr.
    [Theory]
    [InlineData("auto")]
    [InlineData("")]
    public void R6_AutoDetect_wahr(string sprache)
    {
        Assert.True(new TranscriptionRequest(sprache).AutoDetect);
    }

    // R6: AutoDetect ist falsch für die Schweizer Sprachen.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData("de")]
    public void R6_AutoDetect_falsch(string sprache)
    {
        Assert.False(new TranscriptionRequest(sprache).AutoDetect);
    }

    // R7: Groq bekommt bei de-ch und gsw das Feld language=de, nie den Schweizer Code.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData(" DE-CH ")]
    [InlineData("GSW")]
    public async Task R7_Groq_sendet_de_fuer_Schweizerdeutsch(string sprache)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(sprache), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Assert.Equal("de", Formular.Feld(anfrage, "language").Text);
        // Nur die Textfelder prüfen; die WAV Bytes im Feld file könnten zufällig Buchstabenfolgen enthalten.
        foreach (var teil in Formular.Zerlegen(anfrage).Where(t => !t.Kopf.Contains("name=\"file\"", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("de-ch", teil.Kopf + teil.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gsw", teil.Kopf + teil.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // R7: Groq bekommt bei it das Feld language=it.
    [Fact]
    public async Task R7_Groq_sendet_it()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest("it"), Abbruch);

        Assert.Equal("it", Formular.Feld(Assert.Single(dienst.Anfragen), "language").Text);
    }

    // R7: Bei auto fehlt das Feld language.
    [Fact]
    public async Task R7_Groq_ohne_language_bei_auto()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest("auto"), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Formular.KeinFeld(anfrage, "language");
        Assert.Single(Formular.Felder(anfrage, "model"));
    }
}
