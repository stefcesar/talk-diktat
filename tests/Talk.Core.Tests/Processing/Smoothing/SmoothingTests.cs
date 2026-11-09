using System.Net;
using System.Text;
using System.Text.Json;

using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Settings;

using Xunit;

namespace Talk.Core.Tests.Processing.Smoothing;

/// <summary>
/// Prüft die Glättung per Sprachmodell (Playbook Abschnitt 21) gegen einen nachgebauten Dienst. Es geht nie etwas ins Netz.
/// Alle Texte sind erfunden.
/// </summary>
public sealed class SmoothingTests
{
    private const string Eingang = "Hi, guten Morgen, ich habe heute vor um indisch zu kochen und wollte jemanden einladen.";
    private const string Geglaettet = "Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.";

    /// <summary>Antwortet mit festem Status und Text und merkt sich, was ankam.</summary>
    private sealed class NachgebauterDienst(HttpStatusCode status, string antwort, TimeSpan? verzoegerung = null) : HttpMessageHandler
    {
        public int Aufrufe { get; private set; }

        public string? Anfrage { get; private set; }

        public string? Schluessel { get; private set; }

        public Uri? Adresse { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Aufrufe++;
            Adresse = request.RequestUri;
            Schluessel = request.Headers.Authorization?.Parameter;
            Anfrage = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (verzoegerung is { } warten) await Task.Delay(warten, cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(antwort, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>Wirft bei jedem Aufruf einen Netzfehler.</summary>
    private sealed class KeinNetz : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("kein Netz");
    }

    private static string Antwort(string inhalt, string ende = "stop") =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = inhalt }, finish_reason = ende } } });

    private static GroqSmoother Glaetter(HttpMessageHandler dienst, string? schluessel = "pruefschluessel", int zeitlimitMs = 4000) =>
        new(new HttpClient(dienst), () => schluessel, new GroqSmoothingOptions(TimeoutMs: zeitlimitMs));

    [Fact]
    public async Task Erfolg_liefert_den_geglaetteten_Text()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.True(ergebnis.Applied);
        Assert.Equal(Geglaettet, ergebnis.Text);
        Assert.Equal(1, dienst.Aufrufe);
    }

    [Fact]
    public async Task Anfrage_enthaelt_nur_Anweisung_und_Text_und_geht_an_die_Chat_Adresse()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));

        await Glaetter(dienst, "  pruefschluessel  ").SmoothAsync(Eingang, SmoothingLevel.Correct, "de", CancellationToken.None);

        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", dienst.Adresse!.ToString());
        Assert.Equal("pruefschluessel", dienst.Schluessel);

        using JsonDocument anfrage = JsonDocument.Parse(dienst.Anfrage!);
        JsonElement wurzel = anfrage.RootElement;
        Assert.Equal("openai/gpt-oss-20b", wurzel.GetProperty("model").GetString());
        Assert.False(wurzel.GetProperty("stream").GetBoolean());
        Assert.False(wurzel.GetProperty("include_reasoning").GetBoolean());
        Assert.Equal("low", wurzel.GetProperty("reasoning_effort").GetString());

        JsonElement nachrichten = wurzel.GetProperty("messages");
        Assert.Equal(2, nachrichten.GetArrayLength());
        Assert.Equal("system", nachrichten[0].GetProperty("role").GetString());
        Assert.Equal(SmoothingPrompt.Build(SmoothingLevel.Correct, "de"), nachrichten[0].GetProperty("content").GetString());
        Assert.Equal("user", nachrichten[1].GetProperty("role").GetString());
        Assert.Equal(Eingang, nachrichten[1].GetProperty("content").GetString());

        // Außer diesen Feldern steht nichts in der Anfrage: kein Wörterbuch, kein Verlauf.
        string[] felder = wurzel.EnumerateObject().Select(f => f.Name).Order().ToArray();
        Assert.Equal(["include_reasoning", "max_completion_tokens", "messages", "model", "reasoning_effort", "stream", "temperature"], felder);
    }

    [Theory]
    [InlineData(SmoothingLevel.Off, SmoothingOutcome.Off)]
    public async Task Stufe_aus_sendet_nichts(SmoothingLevel stufe, SmoothingOutcome erwartet)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, stufe, "de", CancellationToken.None);

        Assert.Equal(erwartet, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
        Assert.Equal(0, dienst.Aufrufe);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hallo")]
    [InlineData("Nur drei Wörter")]
    public async Task Zu_kurzer_Text_sendet_nichts(string text)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(text, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.TooShort, ergebnis.Outcome);
        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, dienst.Aufrufe);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ohne_Schluessel_wird_nichts_gesendet(string? schluessel)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));

        SmoothingResult ergebnis = await Glaetter(dienst, schluessel).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.NoKey, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
        Assert.Equal(0, dienst.Aufrufe);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SmoothingOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, SmoothingOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, SmoothingOutcome.RateLimited)]
    [InlineData(HttpStatusCode.RequestTimeout, SmoothingOutcome.Timeout)]
    [InlineData(HttpStatusCode.InternalServerError, SmoothingOutcome.ServiceError)]
    [InlineData(HttpStatusCode.BadRequest, SmoothingOutcome.ServiceError)]
    public async Task Fehler_des_Dienstes_laesst_den_Text_wie_er_ist(HttpStatusCode status, SmoothingOutcome erwartet)
    {
        NachgebauterDienst dienst = new(status, "{\"error\":{\"message\":\"geheim\"}}");

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(erwartet, ergebnis.Outcome);
        Assert.False(ergebnis.Applied);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":null}}]}")]
    public async Task Unlesbare_Antwort_laesst_den_Text_wie_er_ist(string antwort)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, antwort);

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.ServiceError, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hier ist der überarbeitete Text: Guten Morgen, ich koche heute indisch und lade jemanden ein.")]
    [InlineData("Gerne! Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData("Ja.")]
    [InlineData("```\nGuten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.\n```")]
    public async Task Antwort_die_die_Schutzregeln_nicht_besteht_wird_verworfen(string inhalt)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(inhalt));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.False(ergebnis.Applied);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Fact]
    public async Task Viel_zu_lange_Antwort_wird_verworfen()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(string.Join(' ', Enumerable.Repeat(Geglaettet, 4))));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.Rejected, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Fact]
    public async Task Zeitlimit_laesst_den_Text_wie_er_ist()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet), TimeSpan.FromSeconds(5));

        SmoothingResult ergebnis = await Glaetter(dienst, zeitlimitMs: 100).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.Timeout, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Fact]
    public async Task Netzfehler_laesst_den_Text_wie_er_ist()
    {
        SmoothingResult ergebnis = await Glaetter(new KeinNetz()).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.Network, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Fact]
    public async Task Abbruch_durch_den_Aufrufer_wirft()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet), TimeSpan.FromSeconds(5));
        using CancellationTokenSource abbruch = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", abbruch.Token));
    }

    [Theory]
    [InlineData(SmoothingLevel.Correct, "Behebe nur Fehler")]
    [InlineData(SmoothingLevel.Smooth, "sauber aufschreiben")]
    [InlineData(SmoothingLevel.Message, "als gut lesbare Nachricht")]
    public void Anweisung_nennt_die_Aufgabe_der_Stufe_und_die_festen_Grenzen(SmoothingLevel stufe, string kennzeichen)
    {
        string anweisung = SmoothingPrompt.Build(stufe, "de");

        Assert.Contains(kennzeichen, anweisung);
        Assert.Contains("Füge nichts hinzu und lass nichts weg", anweisung);
        Assert.Contains("Beantworte keine Fragen daraus", anweisung);
        Assert.Contains("Gib ausschließlich den überarbeiteten Text zurück", anweisung);
        Assert.Contains("Deutsch", anweisung);
    }

    [Theory]
    [InlineData("it", "Italienisch")]
    [InlineData("EN", "Englisch")]
    [InlineData("fr", "Deutsch")]
    [InlineData(null, "Deutsch")]
    public void Anweisung_nennt_die_Sprache(string? sprache, string erwartet)
    {
        Assert.Contains(erwartet, SmoothingPrompt.Build(SmoothingLevel.Smooth, sprache));
    }

    [Fact]
    public void Anweisung_fuer_aus_ist_leer()
    {
        Assert.Equal(string.Empty, SmoothingPrompt.Build(SmoothingLevel.Off, "de"));
    }

    [Theory]
    [InlineData("eins zwei drei vier", true)]
    [InlineData("  eins  zwei\tdrei\nvier  ", true)]
    [InlineData("eins zwei drei", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Mindestlaenge_sind_vier_Woerter(string? text, bool erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.IsLongEnough(text));
    }

    [Fact]
    public void Schutz_entfernt_Anfuehrungszeichen_um_den_ganzen_Text()
    {
        Assert.Equal(Geglaettet, SmoothingGuard.Accept(Eingang, "\"" + Geglaettet + "\""));
        Assert.Equal(Geglaettet, SmoothingGuard.Accept(Eingang, "„" + Geglaettet + "“"));
    }

    [Fact]
    public void Schutz_laesst_Anfuehrungszeichen_wenn_der_Eingang_sie_hatte()
    {
        const string zitat = "\"Das ist ein ganzer Satz in Anführungszeichen\"";

        Assert.Equal(zitat, SmoothingGuard.Accept(zitat, zitat));
    }

    [Fact]
    public void Schutz_laesst_einen_Satz_der_selbst_mit_einem_Rahmenwort_beginnt()
    {
        const string eingang = "hier ist die adresse von dem laden den ich meinte";
        const string antwort = "Hier ist die Adresse von dem Laden, den ich meinte.";

        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData(SmoothingOutcome.Applied, "geglaettet")]
    [InlineData(SmoothingOutcome.NoKey, "kein_schluessel")]
    [InlineData(SmoothingOutcome.LocalOnly, "nur_lokal")]
    [InlineData(SmoothingOutcome.Terminal, "terminal")]
    [InlineData(SmoothingOutcome.Rejected, "antwort_verworfen")]
    public void Feste_Woerter_fuers_Protokoll(SmoothingOutcome ausgang, string erwartet)
    {
        Assert.Equal(erwartet, ausgang.ToWord());
    }

    [Fact]
    public void Jeder_Ausgang_hat_ein_eigenes_Wort_das_ins_Protokoll_passt()
    {
        string[] woerter = Enum.GetValues<SmoothingOutcome>().Select(a => a.ToWord()).ToArray();

        Assert.Equal(woerter.Length, woerter.Distinct().Count());
        Assert.All(woerter, w => Assert.Matches("^[a-z_]{1,24}$", w));
    }

    [Fact]
    public void Einstellungen_Glaettung_ist_aus_und_steht_auf_Saetze()
    {
        DictationSettings s = new();

        Assert.False(s.SmoothingEnabled);
        Assert.Equal(SmoothingLevel.Smooth, s.Smoothing);
    }

    [Theory]
    [InlineData("fehler", SmoothingLevel.Correct)]
    [InlineData("saetze", SmoothingLevel.Smooth)]
    [InlineData("nachricht", SmoothingLevel.Message)]
    [InlineData("unsinn", SmoothingLevel.Smooth)]
    public void Einstellungen_lesen_die_Stufe_als_Wort(string wort, SmoothingLevel erwartet)
    {
        DictationSettings s = DictationSettingsJson.Read($$"""{"smoothingEnabled":true,"smoothingLevel":"{{wort}}"}""");

        Assert.True(s.SmoothingEnabled);
        Assert.Equal(erwartet, s.Smoothing);
    }

    [Fact]
    public void Einstellungen_schreiben_und_lesen_die_Glaettung()
    {
        DictationSettings s = new(SmoothingEnabled: true, Smoothing: SmoothingLevel.Message);

        Assert.Equal(s, DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s)));
    }

    [Fact]
    public void Einstellungen_die_Stufe_ist_nie_aus()
    {
        Assert.Equal(SmoothingLevel.Smooth, new DictationSettings(Smoothing: SmoothingLevel.Off).Normalize().Smoothing);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("content_filter")]
    public async Task Abgeschnittene_Antwort_wird_nie_uebernommen(string ende)
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet, ende));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.Rejected, ergebnis.Outcome);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Fact]
    public async Task Antwort_ohne_Angabe_zum_Ende_wird_nicht_uebernommen()
    {
        string antwort = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = Geglaettet } } } });
        NachgebauterDienst dienst = new(HttpStatusCode.OK, antwort);

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.False(ergebnis.Applied);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task Fehler_des_Dienstes_nennt_den_Statuscode_als_Zahl(HttpStatusCode status, int erwartet)
    {
        NachgebauterDienst dienst = new(status, "{\"error\":{\"message\":\"geheim\"}}");

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(erwartet, ergebnis.HttpStatus);
        Assert.Equal(Eingang, ergebnis.Text);
    }

    [Fact]
    public async Task Sehr_langer_Text_wird_nicht_gesendet()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));
        string lang = string.Join(' ', Enumerable.Repeat("wort", 1300));

        SmoothingResult ergebnis = await Glaetter(dienst).SmoothAsync(lang, SmoothingLevel.Smooth, "de", CancellationToken.None);

        Assert.Equal(SmoothingOutcome.TooLong, ergebnis.Outcome);
        Assert.Equal(0, dienst.Aufrufe);
    }

    [Fact]
    public async Task Tokengrenze_laesst_Platz_fuer_die_doppelte_Laenge()
    {
        NachgebauterDienst dienst = new(HttpStatusCode.OK, Antwort(Geglaettet));

        await Glaetter(dienst).SmoothAsync(Eingang, SmoothingLevel.Message, "de", CancellationToken.None);

        using JsonDocument anfrage = JsonDocument.Parse(dienst.Anfrage!);
        Assert.Equal(1024, anfrage.RootElement.GetProperty("max_completion_tokens").GetInt32());
        Assert.Equal(0.2, anfrage.RootElement.GetProperty("temperature").GetDouble(), 3);

        // Langer Eingang: Die Grenze wächst mit, mindestens ein Token je Zeichen des Eingangs.
        string lang = string.Join(' ', Enumerable.Repeat("wort", 1000));
        NachgebauterDienst zweiter = new(HttpStatusCode.OK, Antwort(lang));
        await Glaetter(zweiter).SmoothAsync(lang, SmoothingLevel.Smooth, "de", CancellationToken.None);
        using JsonDocument lange = JsonDocument.Parse(zweiter.Anfrage!);
        Assert.True(lange.RootElement.GetProperty("max_completion_tokens").GetInt32() >= lang.Length);
    }

    [Theory]
    [InlineData("also hier ist der link den du wolltest und die adresse", "Hier ist der Link, den du wolltest, und die Adresse.")]
    [InlineData("ja klar das machen wir morgen zusammen im büro", "Klar, das machen wir morgen zusammen im Büro.")]
    [InlineData("ja gerne komme ich am dienstag zum essen vorbei", "Gerne komme ich am Dienstag zum Essen vorbei.")]
    [InlineData("also natürlich geht das auch am mittwoch nachmittag", "Natürlich geht das auch am Mittwoch Nachmittag.")]
    public void Schutz_laesst_gute_Antworten_die_wie_ein_Rahmen_beginnen(string eingang, string antwort)
    {
        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData("Überarbeitet: Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData("Korrigierte Fassung: Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData("Good morning, I am planning to cook Indian food today and wanted to invite someone over.")]
    [InlineData("Ich kann dabei leider nicht helfen, weil mir dazu wichtige Angaben und der Zusammenhang fehlen.")]
    [InlineData("**Guten Morgen**, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData("Guten Morgen, ich habe heute vor, indisch zu kochen,\u001B und wollte jemanden einladen.")]
    public void Schutz_verwirft_Ueberschrift_Uebersetzung_Ablehnung_Fettschrift_und_Steuerzeichen(string antwort)
    {
        Assert.Null(SmoothingGuard.Accept(Eingang, antwort));
    }

    [Fact]
    public void Schutz_entfernt_unsichtbare_Zeichen()
    {
        Assert.Equal(Geglaettet, SmoothingGuard.Accept(Eingang, "\u200B" + Geglaettet + "\u202E"));
    }

    [Fact]
    public void Schutz_laesst_Anfuehrungszeichen_im_Inneren_in_Ruhe()
    {
        // Die Antwort beginnt und endet mit Anführungszeichen, es sind aber zwei wörtliche Reden: Nichts wird abgeschnitten.
        const string eingang = "ja das machen wir sagte er und sie sagte dann bis morgen";
        const string antwort = "\u201EJa, das machen wir\u201C, sagte er, und sie sagte: \u201EDann bis morgen\u201C";

        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData("ich bin um drei da", "I'll be there at three.")]
    [InlineData("wie spät ist es jetzt", "Das kann ich leider nicht sagen.")]
    [InlineData("kommst du heute noch", "Vieni ancora oggi?")]
    public void Schutz_verwirft_auch_bei_kurzem_Eingang_Uebersetzung_und_Antwort(string eingang, string antwort)
    {
        Assert.Null(SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData("ich bin um drei da", "Ich bin um drei da.")]
    [InlineData("wie spät ist es jetzt", "Wie spät ist es jetzt?")]
    public void Schutz_laesst_kurzen_Eingang_mit_gleichen_Woertern(string eingang, string antwort)
    {
        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData("Gerne. Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData("Hier ist der überarbeitete Text\n\nGuten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData("Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.\n\nSoll ich noch etwas ändern?")]
    [InlineData("Guten Morgen, ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.\nNur die Kommas wurden korrigiert.")]
    public void Schutz_verwirft_Rahmen_mit_Punkt_und_eigene_Zeilen_des_Modells(string antwort)
    {
        Assert.Null(SmoothingGuard.Accept(Eingang, antwort));
    }

    [Theory]
    [InlineData("ich wollte noch fragen ob du morgen zeit hast", "Ich wollte noch fragen, ob du morgen Zeit hast.\n\nSoll ich noch etwas ändern?")]
    [InlineData("ich bin hier im büro und warte auf dich", "Hier ist der überarbeitete Text\n\nIch bin hier im Büro und warte auf dich.")]
    public void Schutz_verwirft_eigene_Zeile_auch_wenn_sie_ein_haeufiges_Wort_teilt(string eingang, string antwort)
    {
        Assert.Null(SmoothingGuard.Accept(eingang, antwort));
    }

    [Fact]
    public void Schutz_verwirft_englischen_Rahmen_mit_typografischem_Apostroph()
    {
        Assert.Null(SmoothingGuard.Accept(
            "i will send the report tomorrow morning",
            "Here’s your message: I will send the report tomorrow morning."));
    }

    [Fact]
    public void Schutz_verwirft_Ueberschrift_auch_wenn_ihr_Wort_im_Eingang_nur_als_Teilwort_steckt()
    {
        const string eingang = "im kontext der besprechung wollte ich noch etwas sagen";
        const string gut = "Im Kontext der Besprechung wollte ich noch etwas sagen.";

        Assert.Null(SmoothingGuard.Accept(eingang, "Text: " + gut));
        Assert.Equal(gut, SmoothingGuard.Accept(eingang, gut));
    }

    [Fact]
    public void Schutz_verwirft_Rahmen_auch_wenn_der_Eingang_selbst_einen_Doppelpunkt_hat()
    {
        const string eingang = "Kurze Frage: Kommst du morgen zum Training?";

        Assert.Null(SmoothingGuard.Accept(eingang, "Hier ist der überarbeitete Text: Kurze Frage: Kommst du morgen zum Training?"));
        Assert.Equal(eingang, SmoothingGuard.Accept(eingang, eingang));
    }

    [Theory]
    [InlineData("kannst du mir das bitte noch einmal erklären", "Klar! Kannst du mir das bitte noch einmal erklären?")]
    [InlineData("die versicherung hat sich heute bei mir gemeldet", "Sicher! Die Versicherung hat sich heute bei mir gemeldet.")]
    public void Schutz_zaehlt_die_Wendung_im_Eingang_nur_als_ganzes_Wort(string eingang, string antwort)
    {
        Assert.Null(SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData("klar das machen wir morgen zusammen", "Klar! Das machen wir morgen zusammen.")]
    [InlineData("gerne komme ich morgen bei euch vorbei", "Gerne! Ich komme morgen bei euch vorbei.")]
    [InlineData("folgendes wir treffen uns morgen um acht", "Folgendes: Wir treffen uns morgen um acht.")]
    public void Schutz_laesst_die_Wendung_wenn_sie_diktiert_wurde(string eingang, string antwort)
    {
        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    [Theory]
    [InlineData(false, false, true, false, SmoothingOutcome.Off)]
    [InlineData(true, true, true, false, SmoothingOutcome.LocalOnly)]
    [InlineData(true, false, false, false, SmoothingOutcome.LocalOnly)]
    [InlineData(true, true, false, true, SmoothingOutcome.LocalOnly)]
    [InlineData(true, false, true, true, SmoothingOutcome.Terminal)]
    public void Weiche_sperrt_lokalen_Lauf_lokal_erkannten_Text_und_Terminal(
        bool an, bool lokalerLauf, bool ueberNetzErkannt, bool terminal, SmoothingOutcome erwartet)
    {
        Assert.Equal(erwartet, SmoothingGate.Check(an, lokalerLauf, ueberNetzErkannt, terminal));
    }

    [Fact]
    public void Weiche_laesst_nur_durch_wenn_alles_stimmt()
    {
        Assert.Null(SmoothingGate.Check(enabled: true, localRun: false, recognizedOnline: true, terminal: false));
    }

    [Theory]
    [InlineData("2026-10-11 09:00:00.000 | smooth | result | stufe=saetze ms=412 zeichen_ein=88 zeichen_aus=81")]
    [InlineData("2026-10-15 09:00:00.000 | smooth | result | stufe=nachricht quelle=programm ms=412 zeichen_ein=88 zeichen_aus=81")]
    [InlineData("2026-10-11 09:00:00.000 | smooth | skipped | grund=nur_lokal")]
    [InlineData("2026-10-15 09:00:00.000 | smooth | skipped | grund=aus_programm")]
    [InlineData("2026-10-11 09:00:00.000 | smooth | error | grund=dienst status=400")]
    [InlineData("2026-10-11 09:00:00.000 | smooth | error | grund=antwort_verworfen")]
    public void Protokollzeilen_der_Glaettung_bestehen_die_Protokollregel(string zeile)
    {
        Assert.True(Talk.Core.Diagnostics.LogLineRule.IsClean(zeile));
    }

    [Fact]
    public void Jedes_Wort_fuer_einen_Ausgang_besteht_die_Protokollregel()
    {
        foreach (SmoothingOutcome ausgang in Enum.GetValues<SmoothingOutcome>())
        {
            Assert.True(Talk.Core.Diagnostics.LogLineRule.IsClean($"2026-10-11 09:00:00.000 | smooth | error | grund={ausgang.ToWord()} status=503"), ausgang.ToString());
        }
    }

    [Fact]
    public void Schutz_laesst_Absaetze_der_Stufe_Nachricht()
    {
        const string antwort = "Guten Morgen,\n\nich habe heute vor, indisch zu kochen, und wollte jemanden einladen.";

        Assert.Equal(antwort, SmoothingGuard.Accept(Eingang, antwort));
    }
}
