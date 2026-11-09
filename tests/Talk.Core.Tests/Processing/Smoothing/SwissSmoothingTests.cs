// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;

using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Processing.Swiss;

using Xunit;

namespace Talk.Core.Tests.Processing.Smoothing;

/// <summary>
/// Prüft die Glättung für Schweizerdeutsch und Mundart ausschließlich gegen PLAYBOOK.md, Abschnitt 26, Regeln S8 bis S10:
/// Anweisung an das Sprachmodell, Prüfung der Antwort und Wahl des Modells. Der Dienst ist nachgebaut, es geht nie etwas ins Netz.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test. Alle Texte sind erfunden.
/// </summary>
public sealed class SwissSmoothingTests
{
    private const string Eingang = "Hi, guten Morgen, ich habe heute vor um indisch zu kochen und wollte jemanden einladen.";
    private const string Hochdeutsch = "In Container können auch keine Treiber geladen werden.";
    private const string Mundart = "I Container chönd au kei Triiber glade wärde.";

    private static readonly SmoothingLevel[] Stufen = [SmoothingLevel.Correct, SmoothingLevel.Smooth, SmoothingLevel.Message];

    /// <summary>Antwortet mit festem Status und Text und merkt sich, was ankam.</summary>
    private sealed class NachgebauterDienst(HttpStatusCode status, string antwort) : HttpMessageHandler
    {
        public int Aufrufe { get; private set; }

        public string? Anfrage { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Aufrufe++;
            Anfrage = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(antwort, Encoding.UTF8, "application/json") };
        }
    }

    private static string Antwort(string inhalt) =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = inhalt }, finish_reason = "stop" } } });

    private static GroqSmoother Glaetter(HttpMessageHandler dienst, GroqSmoothingOptions? optionen = null) =>
        new(new HttpClient(dienst), () => "pruefschluessel", optionen ?? new GroqSmoothingOptions());

    private static string Modell(NachgebauterDienst dienst)
    {
        using JsonDocument anfrage = JsonDocument.Parse(dienst.Anfrage!);
        return anfrage.RootElement.GetProperty("model").GetString()!;
    }

    // ---------- S8: Anweisung ----------

    // S8: Stufe aus ergibt für jede Sprache eine leere Anweisung.
    [Theory]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData("gsw-be")]
    [InlineData(null)]
    public void Aus_ergibt_leere_Anweisung(string? sprache)
    {
        Assert.Equal("", SmoothingPrompt.Build(SmoothingLevel.Off, sprache));
    }

    // S8: Deutsch und Italienisch.
    [Fact]
    public void Deutsch_und_Italienisch()
    {
        foreach (SmoothingLevel stufe in Stufen)
        {
            Assert.Contains("Der Text ist auf Deutsch", SmoothingPrompt.Build(stufe, "de"));
            Assert.Contains("Italienisch", SmoothingPrompt.Build(stufe, "it"));
        }
    }

    // S8: Schweizer Hochdeutsch: eigener Satz, nie ß, nicht der deutsche Satz.
    [Fact]
    public void Schweizer_Hochdeutsch()
    {
        foreach (SmoothingLevel stufe in Stufen)
        {
            string anweisung = SmoothingPrompt.Build(stufe, "de-ch");
            Assert.Contains("Schweizer Hochdeutsch", anweisung);
            Assert.Contains("nie ß", anweisung);
            Assert.DoesNotContain("Der Text ist auf Deutsch", anweisung);
        }
    }

    // S8: Mundart nennt den Dialekt mit Beispiel, sagt "Mundart" und nicht "übersetze nichts".
    [Theory]
    [InlineData("gsw-be", "Berndeutsch", "nid")]
    [InlineData("gsw-zh", "Zürichdeutsch", "nöd")]
    public void Mundart_mit_Dialekt(string sprache, string name, string beispiel)
    {
        foreach (SmoothingLevel stufe in Stufen)
        {
            string anweisung = SmoothingPrompt.Build(stufe, sprache);
            Assert.Contains(name, anweisung);
            Assert.Contains(beispiel, anweisung);
            Assert.Contains("Mundart", anweisung);
            Assert.DoesNotContain("übersetze nichts", anweisung, StringComparison.OrdinalIgnoreCase);
        }
    }

    // S8: Mundart ohne Dialekt nennt den Namen der gemischten Mundart.
    [Fact]
    public void Mundart_ohne_Dialekt()
    {
        foreach (SmoothingLevel stufe in Stufen)
        {
            string anweisung = SmoothingPrompt.Build(stufe, "gsw");
            Assert.Contains(SwissWordLists.DialectName("mix"), anweisung);
            Assert.Contains("Mundart", anweisung);
            Assert.DoesNotContain("übersetze nichts", anweisung, StringComparison.OrdinalIgnoreCase);
        }
    }

    // S8: Jede nicht leere Anweisung verbietet, Fragen zu beantworten.
    [Theory]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    [InlineData("gsw-be")]
    [InlineData("gsw-zh")]
    [InlineData("gsw-bs")]
    public void Jede_Anweisung_beantwortet_keine_Fragen(string sprache)
    {
        foreach (SmoothingLevel stufe in Stufen)
        {
            Assert.Contains("Beantworte keine Fragen", SmoothingPrompt.Build(stufe, sprache));
        }
    }

    // ---------- S9: Prüfung der Antwort ----------

    // S9: Mundart darf weit vom Hochdeutsch weg sein, aber nur im Mundart Modus.
    [Fact]
    public void Mundart_Antwort_nur_im_Mundart_Modus()
    {
        Assert.NotNull(SmoothingGuard.Accept(Hochdeutsch, Mundart, dialect: true));
        Assert.Null(SmoothingGuard.Accept(Hochdeutsch, Mundart, dialect: false));
    }

    // S9: Auch im Mundart Modus verworfen: leer, Rahmen, fremde Antwort, viel zu lang.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Gerne! I Container chönd au kei Triiber glade wärde.")]
    [InlineData("Das Wetter ist heute schön und sonnig in Bern.")]
    public void Mundart_Modus_verwirft_Unbrauchbares(string antwort)
    {
        Assert.Null(SmoothingGuard.Accept(Hochdeutsch, antwort, dialect: true));
    }

    // S9: Mehr als etwa doppelt so lang plus 20 Zeichen wird verworfen.
    [Fact]
    public void Mundart_Modus_verwirft_viel_zu_lange_Antwort()
    {
        string lang = string.Join(" ", Enumerable.Repeat(Mundart, 4));

        Assert.True(lang.Length > (2 * Hochdeutsch.Length) + 20);
        Assert.Null(SmoothingGuard.Accept(Hochdeutsch, lang, dialect: true));
    }

    // S9: Mindestlänge in Wörtern.
    [Fact]
    public void IsLongEnough_mit_Mindestzahl()
    {
        Assert.True(SmoothingGuard.IsLongEnough("Ja gern", 2));
        Assert.False(SmoothingGuard.IsLongEnough("Ja", 2));
        Assert.False(SmoothingGuard.IsLongEnough("Ja gern", SmoothingGuard.MinWords));
    }

    // S9: Feste Grenzen.
    [Fact]
    public void Feste_Mindestzahlen()
    {
        Assert.Equal(4, SmoothingGuard.MinWords);
        Assert.Equal(2, SmoothingGuard.MinDialectWords);
    }

    // ---------- S10: Modell und Mindestlänge beim Dienst ----------

    // S10: Feste Modellnamen.
    [Fact]
    public void Feste_Modellnamen()
    {
        Assert.Equal("openai/gpt-oss-20b", GroqSmoothingOptions.DefaultModel);
        Assert.Equal("openai/gpt-oss-120b", GroqSmoothingOptions.DefaultSwissModel);
        Assert.Equal(GroqSmoothingOptions.DefaultSwissModel, new GroqSmoothingOptions().SwissModel);
    }

    // S10: Schweizerdeutsch und Mundart nutzen das große Modell, Deutsch das kleine.
    [Theory]
    [InlineData("de-ch", "openai/gpt-oss-120b")]
    [InlineData("gsw-be", "openai/gpt-oss-120b")]
    [InlineData("de", "openai/gpt-oss-20b")]
    public async Task Modell_je_Sprache(string sprache, string modell)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Eingang));

        await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, sprache, CancellationToken.None);

        Assert.Equal(1, dienst.Aufrufe);
        Assert.Equal(modell, Modell(dienst));
    }

    // S10: Ein eigenes Schweizer Modell wird genutzt.
    [Theory]
    [InlineData("de-ch")]
    [InlineData("gsw-zh")]
    public async Task Eigenes_Schweizer_Modell(string sprache)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Eingang));

        await Glaetter(dienst, new GroqSmoothingOptions(SwissModel: "pruef/modell-ch"))
            .SmoothAsync(Eingang, SmoothingLevel.Smooth, sprache, CancellationToken.None);

        Assert.Equal("pruef/modell-ch", Modell(dienst));
    }

    // S10: In Mundart gehen schon zwei Wörter an den Dienst.
    [Fact]
    public async Task Mundart_sendet_zwei_Woerter()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort("Jo gärn"));

        await Glaetter(dienst).SmoothAsync("Ja gern", SmoothingLevel.Smooth, "gsw-zh", CancellationToken.None);

        Assert.Equal(1, dienst.Aufrufe);
    }

    // S10: In Deutsch sind drei Wörter zu kurz, es geht nichts hinaus.
    [Fact]
    public async Task Deutsch_drei_Woerter_zu_kurz()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort("Ja gern doch"));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync("Ja gern doch", SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.TooShort, ergebnis.Outcome);
        Assert.Equal(0, dienst.Aufrufe);
    }
}
