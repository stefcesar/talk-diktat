using Talk.Core.Processing;
using Talk.Core.Processing.Swiss;

using Xunit;

namespace Talk.Core.Tests.Processing.Swiss;

/// <summary>
/// Prüft Bindestriche und die Artikelsicherheit der eingebauten Helvetismen ausschließlich gegen die Regeln T4 und T5.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test. Alle Texte sind erfunden.
/// </summary>
public sealed class SwissNachtragTests
{
    private static string Schweiz(string text) =>
        new PostProcessor().ProcessDetailed(text, new PostProcessingOptions(Language: "de-ch")).Text;

    // ---------- T4: Bindestriche ----------

    // T4: U+2010 und U+2011 werden zum einfachen Bindestrich.
    [Theory]
    [InlineData("Walsh‑Code", "Walsh-Code")]
    [InlineData("Walsh‐Code", "Walsh-Code")]
    [InlineData("A‐B‑C", "A-B-C")]
    public void Spelling_macht_Bindestriche_einfach(string text, string erwartet)
    {
        Assert.Equal(erwartet, SwissText.Spelling(text));
    }

    // T4: Andere Striche bleiben, auch der Halbgeviertstrich U+2013.
    [Theory]
    [InlineData("Seite 3–5")]
    [InlineData("Wort — Wort")]
    [InlineData("Walsh-Code")]
    [InlineData("minus − eins")]
    public void Spelling_laesst_andere_Striche(string text)
    {
        Assert.Equal(text, SwissText.Spelling(text));
    }

    // ---------- T5: Artikelsicherheit der eingebauten Liste ----------

    // T5: Einzahl und Mehrzahl mit passendem Artikel.
    [Theory]
    [InlineData("Das Fahrrad steht neben dem Krankenhaus am Bahnsteig.", "Das Velo steht neben dem Spital am Perron.")]
    [InlineData("Die Fahrräder und die Fahrkarten liegen beim Geldautomaten.", "Die Velos und die Billette liegen beim Bancomaten.")]
    public void Helvetismen_mit_passendem_Artikel(string text, string erwartet)
    {
        Assert.Equal(erwartet, Schweiz(text));
    }

    // T5: Führerschein wird Führerausweis, nie Fahrausweis.
    [Fact]
    public void Fuehrerschein_wird_Fuehrerausweis()
    {
        string ergebnis = Schweiz("Ich habe meinen Führerschein dabei.");

        Assert.Contains("Führerausweis", ergebnis);
        Assert.DoesNotContain("Fahrausweis", ergebnis);
    }

    // T5: Wörter mit gleicher Einzahl und Mehrzahl bleiben unverändert.
    [Theory]
    [InlineData("Die Hausmeister kommen.")]
    [InlineData("zwei Hähnchen")]
    public void Unsichere_Woerter_bleiben(string text)
    {
        Assert.Equal(text, Schweiz(text));
    }
}
