using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Talk.Core.Audio;
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>Eine vom nachgebauten Dienst aufgezeichnete Anfrage.</summary>
/// <param name="Methode">HTTP Methode.</param>
/// <param name="Adresse">Vollständige Adresse der Anfrage.</param>
/// <param name="Authorization">Rohwert der Kopfzeile Authorization oder null.</param>
/// <param name="Inhaltstyp">Inhaltstyp des Rumpfs samt Trennmarke oder null.</param>
/// <param name="Inhalt">Der Rumpf als Bytes, im Dienst gelesen.</param>
internal sealed record AufgezeichneteAnfrage(
    HttpMethod Methode,
    string Adresse,
    string? Authorization,
    string? Inhaltstyp,
    byte[] Inhalt);

/// <summary>
/// Nachgebauter Dienst für die Tests zu Regel 6 und 7: zeichnet jede Anfrage auf (der Inhalt wird hier gelesen,
/// weil er nach dem Senden freigegeben ist) und liefert eine vorgegebene Antwort, wirft oder wartet.
/// Es entsteht nie eine echte Netzverbindung.
/// </summary>
internal sealed class NachgebauterDienst : HttpMessageHandler
{
    private readonly Func<AufgezeichneteAnfrage, CancellationToken, Task<HttpResponseMessage>> _antwort;
    private readonly List<AufgezeichneteAnfrage> _anfragen = new();

    public NachgebauterDienst(Func<AufgezeichneteAnfrage, CancellationToken, Task<HttpResponseMessage>> antwort)
    {
        _antwort = antwort;
    }

    /// <summary>Alle bisher aufgezeichneten Anfragen in Reihenfolge.</summary>
    public IReadOnlyList<AufgezeichneteAnfrage> Anfragen
    {
        get
        {
            lock (_anfragen)
            {
                return _anfragen.ToArray();
            }
        }
    }

    /// <summary>Dienst, der immer mit demselben Status, Text und denselben Kopfzeilen antwortet.</summary>
    public static NachgebauterDienst MitAntwort(int status, string text, params (string Name, string Wert)[] kopfzeilen) =>
        new((_, _) => Task.FromResult(Antwort(status, text, kopfzeilen)));

    /// <summary>Dienst, der bei jeder Anfrage eine HttpRequestException wirft.</summary>
    public static NachgebauterDienst MitNetzfehler(string meldung = "Verbindung fehlgeschlagen") =>
        new((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException(meldung)));

    /// <summary>
    /// Dienst, der länger wartet als jede Wartezeit im Test. Er achtet auf das Abbruchsignal; ohne Abbruch
    /// antwortet er nach 20 Sekunden mit einer gültigen Antwort, damit ein fehlerhafter Erkenner den Test nicht aufhängt.
    /// </summary>
    public static NachgebauterDienst DerNichtAntwortet(Action? beimEintreffen = null) =>
        new(async (_, ct) =>
        {
            beimEintreffen?.Invoke();
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
            return Antwort(200, "{\"text\":\"zu spät\",\"data\":[]}");
        });

    /// <summary>Baut eine Antwort mit Status, JSON Text und Kopfzeilen.</summary>
    public static HttpResponseMessage Antwort(int status, string text, params (string Name, string Wert)[] kopfzeilen)
    {
        var antwort = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(text, Encoding.UTF8, "application/json"),
        };
        foreach (var (name, wert) in kopfzeilen)
        {
            Assert.True(antwort.Headers.TryAddWithoutValidation(name, wert));
        }

        return antwort;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inhalt = request.Content is null
            ? Array.Empty<byte>()
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var authorization = request.Headers.TryGetValues("Authorization", out var werte)
            ? string.Join(",", werte)
            : null;
        var anfrage = new AufgezeichneteAnfrage(
            request.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            authorization,
            request.Content?.Headers.ContentType?.ToString(),
            inhalt);
        lock (_anfragen)
        {
            _anfragen.Add(anfrage);
        }

        return await _antwort(anfrage, cancellationToken);
    }
}

/// <summary>Ein Teil der Formulardaten: Kopfzeilen als Text, Inhalt als Bytes.</summary>
internal sealed record Formularteil(string Kopf, byte[] Inhalt)
{
    /// <summary>Der Inhalt als Text (UTF8).</summary>
    public string Text => Encoding.UTF8.GetString(Inhalt);
}

/// <summary>Zerlegt aufgezeichnete Formulardaten (multipart/form-data) am aufgezeichneten Text.</summary>
internal static class Formular
{
    /// <summary>Liefert alle Teile der Formulardaten in Reihenfolge.</summary>
    public static IReadOnlyList<Formularteil> Zerlegen(AufgezeichneteAnfrage anfrage)
    {
        Assert.NotNull(anfrage.Inhaltstyp);
        Assert.StartsWith("multipart/form-data", anfrage.Inhaltstyp, StringComparison.OrdinalIgnoreCase);
        var marke = Regex.Match(anfrage.Inhaltstyp, "boundary=\"?([^\";]+)\"?", RegexOptions.IgnoreCase);
        Assert.True(marke.Success, "Der Inhaltstyp nennt keine Trennmarke.");
        var trenner = "--" + marke.Groups[1].Value;

        // Latin1 bildet jedes Byte auf genau ein Zeichen ab, so bleiben die Bytes der WAV Datei erhalten.
        var text = Encoding.Latin1.GetString(anfrage.Inhalt);
        var stuecke = text.Split(trenner);
        var teile = new List<Formularteil>();
        for (var i = 1; i < stuecke.Length; i++)
        {
            var stueck = stuecke[i];
            if (stueck.StartsWith("--", StringComparison.Ordinal))
            {
                break;
            }

            if (stueck.StartsWith("\r\n", StringComparison.Ordinal))
            {
                stueck = stueck[2..];
            }

            var kopfEnde = stueck.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            Assert.True(kopfEnde >= 0, "Ein Teil der Formulardaten hat keine Kopfzeilen.");
            var kopf = stueck[..kopfEnde];
            var rumpf = stueck[(kopfEnde + 4)..];
            if (rumpf.EndsWith("\r\n", StringComparison.Ordinal))
            {
                rumpf = rumpf[..^2];
            }

            teile.Add(new Formularteil(kopf, Encoding.Latin1.GetBytes(rumpf)));
        }

        return teile;
    }

    /// <summary>Liefert die Teile, deren Feldname in Anführungszeichen genau dem Namen entspricht.</summary>
    public static IReadOnlyList<Formularteil> Felder(AufgezeichneteAnfrage anfrage, string name) =>
        Zerlegen(anfrage)
            .Where(teil => Regex.IsMatch(teil.Kopf, "(?<![A-Za-z])name=\"" + Regex.Escape(name) + "\""))
            .ToArray();

    /// <summary>Liefert genau das eine Feld mit diesem Namen in Anführungszeichen.</summary>
    public static Formularteil Feld(AufgezeichneteAnfrage anfrage, string name) => Assert.Single(Felder(anfrage, name));

    /// <summary>Prüft, dass es kein Feld mit diesem Namen gibt, weder mit noch ohne Anführungszeichen.</summary>
    public static void KeinFeld(AufgezeichneteAnfrage anfrage, string name)
    {
        var muster = "(?<![A-Za-z])name=\"?" + Regex.Escape(name) + "\"?(?![A-Za-z_])";
        Assert.DoesNotContain(Zerlegen(anfrage), teil => Regex.IsMatch(teil.Kopf, muster));
        Assert.DoesNotMatch(muster, Encoding.Latin1.GetString(anfrage.Inhalt));
    }

    /// <summary>Sucht eine Bytefolge im Inhalt.</summary>
    public static int Suchen(byte[] inhalt, byte[] folge) => inhalt.AsSpan().IndexOf(folge);
}

/// <summary>
/// Prüft den Groq Erkenner ausschließlich gegen Regel 6 (Buchstaben a bis k) der reinen Logik in PLAYBOOK.md,
/// Abschnitt 15, mit einem nachgebauten Dienst als HttpMessageHandler. Es geht nie etwas ins Netz.
/// Der Schlüssel ist ein erfundener Testwert. Die Tests kennen die Umsetzung nicht.
/// Regelnummer und Buchstabe stehen jeweils im Kommentar über dem Test.
/// </summary>
public sealed class GroqTranscriberTests
{
    private const string Schluessel = "test-schluessel-123";
    private const string Endpunkt = "https://api.groq.com/openai/v1/audio/transcriptions";
    private const string EinfacheAntwort = "{\"text\":\" Hallo Welt. \",\"language\":\"German\"}";
    private const string Fehlerantwort =
        "{\"error\":{\"message\":\"Fehler vom Dienst\",\"type\":\"dienst_fehler\",\"code\":\"dienst_code\"}}";

    private static CancellationToken Abbruch => TestContext.Current.CancellationToken;

    /// <summary>Clip mit 1600 Abtastwerten (100 ms) und gut unterscheidbaren Werten.</summary>
    private static AudioClip Clip()
    {
        var werte = new short[1600];
        for (var i = 0; i < werte.Length; i++)
        {
            werte[i] = (short)((i * 37 % 20000) - 10000);
        }

        return new AudioClip(werte);
    }

    private static AudioClip LeererClip() => new(Array.Empty<short>());

    private static GroqTranscriber Erkenner(
        NachgebauterDienst dienst,
        string? schluessel = Schluessel,
        GroqOptions? optionen = null,
        HallucinationFilterOptions? filter = null) =>
        new(new HttpClient(dienst), () => schluessel, optionen, filter);

    private static async Task<TranscriptionException> Fehler(
        NachgebauterDienst dienst,
        string? schluessel = Schluessel,
        GroqOptions? optionen = null)
    {
        var erkenner = Erkenner(dienst, schluessel, optionen);
        return await Assert.ThrowsAsync<TranscriptionException>(
            () => erkenner.TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch));
    }

    /// <summary>Alle Meldungen einer Ausnahme samt ihrer inneren Ausnahmen.</summary>
    private static IEnumerable<string> Meldungen(Exception ausnahme)
    {
        for (Exception? aktuell = ausnahme; aktuell is not null; aktuell = aktuell.InnerException)
        {
            yield return aktuell.Message;
        }
    }

    // ---------- Vertrag ----------

    // Regel 6 (Vertrag): Name ist "groq".
    [Fact]
    public void Name_ist_groq()
    {
        var erkenner = Erkenner(NachgebauterDienst.MitAntwort(200, EinfacheAntwort));

        Assert.Equal("groq", erkenner.Name);
        Assert.Equal("groq", GroqTranscriber.EngineName);
    }

    // ---------- (a) ----------

    // Regel 6 (a): Leerer Clip ergibt ein leeres Ergebnis mit Engine "groq", ohne Anfrage.
    [Fact]
    public async Task a_Leerer_Clip_ergibt_leeres_Ergebnis_ohne_Anfrage()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var erkenner = Erkenner(dienst);

        var ergebnis = await erkenner.TranscribeAsync(LeererClip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Equal("groq", ergebnis.Engine);
        Assert.Empty(dienst.Anfragen);
    }

    // Regel 6 (a): Das gilt auch ohne Schlüssel (kein NoKey bei leerem Clip).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task a_Leerer_Clip_ergibt_leeres_Ergebnis_auch_ohne_Schluessel(string? schluessel)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var erkenner = Erkenner(dienst, schluessel);

        var ergebnis = await erkenner.TranscribeAsync(LeererClip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Equal("groq", ergebnis.Engine);
        Assert.Empty(dienst.Anfragen);
    }

    // ---------- (b) ----------

    // Regel 6 (b): Kein Schlüssel (null, leer, nur Leerzeichen) wirft NoKey, ohne Anfrage.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task b_Kein_Schluessel_wirft_NoKey_ohne_Anfrage(string? schluessel)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        var ausnahme = await Fehler(dienst, schluessel);

        Assert.Equal(TranscriptionError.NoKey, ausnahme.Error);
        Assert.Empty(dienst.Anfragen);
    }

    // ---------- (c) ----------

    // Regel 6 (c): Die Anfrage ist ein POST an den Endpunkt (Vorgabe aus den Optionen).
    [Fact]
    public async Task c_Anfrage_ist_ein_POST_an_den_Standardendpunkt()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Assert.Equal(HttpMethod.Post, anfrage.Methode);
        Assert.Equal(Endpunkt, anfrage.Adresse);
    }

    // Regel 6 (c): Der Endpunkt kommt aus den Optionen.
    [Fact]
    public async Task c_Anfrage_geht_an_den_Endpunkt_aus_den_Optionen()
    {
        const string eigenerEndpunkt = "https://api.groq.com/openai/v1/audio/eigener-weg";
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var optionen = new GroqOptions(Endpoint: eigenerEndpunkt);

        await Erkenner(dienst, optionen: optionen).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Assert.Equal(HttpMethod.Post, anfrage.Methode);
        Assert.Equal(eigenerEndpunkt, anfrage.Adresse);
    }

    // Regel 6 (c): Kopfzeile Authorization ist "Bearer" plus Schlüssel.
    [Fact]
    public async Task c_Authorization_ist_Bearer_mit_Schluessel()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("Bearer " + Schluessel, Assert.Single(dienst.Anfragen).Authorization);
    }

    // Regel 6 (c): Der Schlüssel geht ohne Randleerzeichen in die Kopfzeile.
    [Fact]
    public async Task c_Authorization_nimmt_Schluessel_ohne_Randleerzeichen()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst, "  " + Schluessel + " \r\n").TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("Bearer " + Schluessel, Assert.Single(dienst.Anfragen).Authorization);
    }

    // Regel 6 (c): Der Schlüssel wird bei jedem Aufruf neu über die Funktion gelesen.
    [Fact]
    public async Task c_Schluessel_wird_je_Aufruf_neu_gelesen()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var aktuell = "test-schluessel-eins";
        var erkenner = new GroqTranscriber(new HttpClient(dienst), () => aktuell);

        await erkenner.TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);
        aktuell = "test-schluessel-zwei";
        await erkenner.TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(
            new[] { "Bearer test-schluessel-eins", "Bearer test-schluessel-zwei" },
            dienst.Anfragen.Select(a => a.Authorization));
    }

    // Regel 6 (c): Feld file mit Dateiname "audio.wav", Inhaltstyp audio/wav und Inhalt genau WavEncoder.Encode(clip).
    [Fact]
    public async Task c_Feld_file_traegt_die_WAV_Datei()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var clip = Clip();

        await Erkenner(dienst).TranscribeAsync(clip, new TranscriptionRequest(), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        var wav = WavEncoder.Encode(clip);
        var feld = Formular.Feld(anfrage, "file");
        Assert.Matches("filename=\"?audio\\.wav\"?", feld.Kopf);
        Assert.Matches(new Regex("content-type:\\s*audio/wav", RegexOptions.IgnoreCase), feld.Kopf);
        Assert.Equal(wav, feld.Inhalt);
        Assert.True(Formular.Suchen(anfrage.Inhalt, wav) >= 0, "Die WAV Bytes stehen nicht als Bytefolge im Inhalt.");
    }

    // Regel 6 (c): Feld model kommt aus den Optionen (Vorgabe whisper-large-v3-turbo).
    [Fact]
    public async Task c_Feld_model_hat_die_Vorgabe()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("whisper-large-v3-turbo", Formular.Feld(Assert.Single(dienst.Anfragen), "model").Text);
    }

    // Regel 6 (c): Feld model kommt aus den Optionen (eigener Wert).
    [Fact]
    public async Task c_Feld_model_kommt_aus_den_Optionen()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var optionen = new GroqOptions(Model: "whisper-large-v3");

        await Erkenner(dienst, optionen: optionen).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("whisper-large-v3", Formular.Feld(Assert.Single(dienst.Anfragen), "model").Text);
    }

    // Regel 6 (c): response_format gleich verbose_json, temperature gleich 0.
    [Fact]
    public async Task c_Felder_response_format_und_temperature()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Assert.Equal("verbose_json", Formular.Feld(anfrage, "response_format").Text);
        Assert.Equal("0", Formular.Feld(anfrage, "temperature").Text);
    }

    // Regel 6 (c): Die Feldnamen stehen in Anführungszeichen (name="model"), gefolgt vom Wert.
    [Fact]
    public async Task c_Feldnamen_stehen_in_Anfuehrungszeichen()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var anfrageDaten = new TranscriptionRequest("de", new[] { "Cesaretti" });

        await Erkenner(dienst).TranscribeAsync(Clip(), anfrageDaten, Abbruch);

        var text = Encoding.Latin1.GetString(Assert.Single(dienst.Anfragen).Inhalt);
        Assert.Contains("name=\"file\"", text);
        Assert.Matches("name=\"model\"[^\\r\\n]*(\\r\\n[^\\r\\n]+)*\\r\\n\\r\\nwhisper-large-v3-turbo\\r\\n", text);
        Assert.Matches("name=\"response_format\"[^\\r\\n]*(\\r\\n[^\\r\\n]+)*\\r\\n\\r\\nverbose_json\\r\\n", text);
        Assert.Matches("name=\"temperature\"[^\\r\\n]*(\\r\\n[^\\r\\n]+)*\\r\\n\\r\\n0\\r\\n", text);
        Assert.Matches("name=\"language\"[^\\r\\n]*(\\r\\n[^\\r\\n]+)*\\r\\n\\r\\nde\\r\\n", text);
        Assert.Matches("name=\"prompt\"[^\\r\\n]*(\\r\\n[^\\r\\n]+)*\\r\\n\\r\\nCesaretti\\r\\n", text);
    }

    // Regel 6 (c): language klein und ohne Randleerzeichen.
    [Theory]
    [InlineData("de", "de")]
    [InlineData("it", "it")]
    [InlineData("en", "en")]
    [InlineData("DE", "de")]
    [InlineData("  It  ", "it")]
    public async Task c_Feld_language_ist_klein_und_ohne_Randleerzeichen(string sprache, string erwartet)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(sprache), Abbruch);

        Assert.Equal(erwartet, Formular.Feld(Assert.Single(dienst.Anfragen), "language").Text);
    }

    // Regel 6 (c): language fehlt bei AutoDetect.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData(" Auto ")]
    public async Task c_Feld_language_fehlt_bei_AutoDetect(string sprache)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(sprache), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Formular.KeinFeld(anfrage, "language");
        Assert.Single(Formular.Felder(anfrage, "model"));
    }

    // Regel 6 (c): prompt ist der Hinweistext aus den Begriffen.
    [Fact]
    public async Task c_Feld_prompt_ist_der_Hinweistext()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var begriffe = new[] { " Cesaretti ", "LUMÍORA", "", "cesaretti", "Stefanos Kulinarische Welt" };

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest("de", begriffe), Abbruch);

        var feld = Formular.Feld(Assert.Single(dienst.Anfragen), "prompt");
        Assert.Equal("Cesaretti, LUMÍORA, Stefanos Kulinarische Welt", feld.Text);
        Assert.Equal(VocabularyPrompt.Build(begriffe), feld.Text);
    }

    // Regel 6 (c): Der Hinweistext beachtet die Grenze MaxPromptChars aus den Optionen.
    [Fact]
    public async Task c_Feld_prompt_beachtet_MaxPromptChars()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var begriffe = new[] { "Cesaretti", "LUMÍORA", "Welt" };
        var optionen = new GroqOptions(MaxPromptChars: 12);

        await Erkenner(dienst, optionen: optionen).TranscribeAsync(Clip(), new TranscriptionRequest("de", begriffe), Abbruch);

        Assert.Equal("Cesaretti", Formular.Feld(Assert.Single(dienst.Anfragen), "prompt").Text);
    }

    // Regel 6 (c): prompt fehlt ohne Begriffe (null, leer, nur leere).
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task c_Feld_prompt_fehlt_ohne_Begriffe(int fall)
    {
        IReadOnlyList<string>? begriffe = fall switch
        {
            0 => null,
            1 => Array.Empty<string>(),
            _ => new[] { "", "   " },
        };
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest("de", begriffe), Abbruch);

        var anfrage = Assert.Single(dienst.Anfragen);
        Formular.KeinFeld(anfrage, "prompt");
        Assert.Single(Formular.Felder(anfrage, "model"));
    }

    // ---------- (d) ----------

    private const string AntwortMitAbschnitten = """
        {
          "task": "transcribe",
          "language": "German",
          "duration": 6.4,
          "text": " Hallo Welt. Rauschen eins Rauschen zwei la la la la la la Wie geht es dir?",
          "segments": [
            {"id":0,"seek":0,"start":0.0,"end":1.5,"text":" Hallo Welt.","tokens":[1,2,3],"temperature":0.0,"avg_logprob":-0.25,"compression_ratio":1.1,"no_speech_prob":0.01},
            {"id":1,"seek":0,"start":1.5,"end":2.5,"text":" Rauschen eins","tokens":[4],"temperature":0.0,"avg_logprob":-0.3,"compression_ratio":1.0,"no_speech_prob":0.95},
            {"id":2,"seek":0,"start":2.5,"end":3.5,"text":" Rauschen zwei","tokens":[5],"temperature":0.0,"avg_logprob":-1.5,"compression_ratio":1.0,"no_speech_prob":0.7},
            {"id":3,"seek":0,"start":3.5,"end":4.5,"text":" la la la la la la","tokens":[6],"temperature":0.0,"avg_logprob":-0.3,"compression_ratio":3.0,"no_speech_prob":0.02},
            {"id":4,"seek":0,"start":4.5,"end":6.4,"text":" Wie geht es dir?","tokens":[7,8],"temperature":0.0,"avg_logprob":-0.2,"compression_ratio":1.2,"no_speech_prob":0.03}
          ],
          "x_groq": {"id":"req_test"}
        }
        """;

    // Regel 6 (d): Text ist der gefilterte Text aus den Abschnitten, FilteredSegments die Zahl der verworfenen.
    [Fact]
    public async Task d_Text_ist_der_gefilterte_Text_aus_den_Abschnitten()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, AntwortMitAbschnitten);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("Hallo Welt. Wie geht es dir?", ergebnis.Text);
        Assert.Equal(3, ergebnis.FilteredSegments);
        Assert.Equal("groq", ergebnis.Engine);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 6 (d): Segments sind die behaltenen Abschnitte mit ihren Zeiten und Kennzahlen.
    [Fact]
    public async Task d_Segments_sind_die_behaltenen_Abschnitte()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, AntwortMitAbschnitten);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.NotNull(ergebnis.Segments);
        Assert.Equal(2, ergebnis.Segments.Count);
        var erster = ergebnis.Segments[0];
        var zweiter = ergebnis.Segments[1];
        Assert.Equal("Hallo Welt.", erster.Text.Trim());
        Assert.Equal(0.0, erster.StartSeconds, 0.0001);
        Assert.Equal(1.5, erster.EndSeconds, 0.0001);
        Assert.Equal(-0.25, Assert.NotNull(erster.AvgLogProb), 0.0001);
        Assert.Equal(0.01, Assert.NotNull(erster.NoSpeechProb), 0.0001);
        Assert.Equal(1.1, Assert.NotNull(erster.CompressionRatio), 0.0001);
        Assert.Equal("Wie geht es dir?", zweiter.Text.Trim());
        Assert.Equal(4.5, zweiter.StartSeconds, 0.0001);
        Assert.Equal(6.4, zweiter.EndSeconds, 0.0001);
        Assert.Equal(-0.2, Assert.NotNull(zweiter.AvgLogProb), 0.0001);
        Assert.Equal(0.03, Assert.NotNull(zweiter.NoSpeechProb), 0.0001);
        Assert.Equal(1.2, Assert.NotNull(zweiter.CompressionRatio), 0.0001);
    }

    // Regel 6 (d): DetectedLanguage klein geschrieben.
    [Theory]
    [InlineData("German", "german")]
    [InlineData("DE", "de")]
    [InlineData("italian", "italian")]
    public async Task d_DetectedLanguage_ist_klein_geschrieben(string gemeldet, string erwartet)
    {
        var antwort = "{\"text\":\"Hallo Welt\",\"language\":\"" + gemeldet + "\",\"segments\":["
            + "{\"id\":0,\"start\":0.0,\"end\":1.0,\"text\":\" Hallo Welt\",\"avg_logprob\":-0.2,\"compression_ratio\":1.0,\"no_speech_prob\":0.01}]}";
        var dienst = NachgebauterDienst.MitAntwort(200, antwort);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(erwartet, ergebnis.DetectedLanguage);
    }

    // Regel 6 (d): Timings vorhanden mit Werten größer gleich 0 und TotalMs größer gleich EncodeMs; FallbackReason null.
    [Fact]
    public async Task d_Timings_vorhanden_und_FallbackReason_null()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, AntwortMitAbschnitten);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.NotNull(ergebnis.Timings);
        Assert.True(ergebnis.Timings.EncodeMs >= 0);
        Assert.True(ergebnis.Timings.UploadMs >= 0);
        Assert.True(ergebnis.Timings.WaitMs >= 0);
        Assert.True(ergebnis.Timings.TotalMs >= 0);
        Assert.True(ergebnis.Timings.TotalMs >= ergebnis.Timings.EncodeMs);
        Assert.True(ergebnis.Elapsed >= TimeSpan.Zero);
        Assert.Null(ergebnis.FallbackReason);
    }

    // Regel 6 (d): Der Filter des Erkenners nimmt die Optionen aus dem Konstruktor (hier ein Zusatztext).
    [Fact]
    public async Task d_Filter_nimmt_die_Optionen_aus_dem_Konstruktor()
    {
        const string antwort = """
            {"text":" Guten Morgen. Das ist ein Testphantom.","language":"German","segments":[
              {"id":0,"start":0.0,"end":1.0,"text":" Guten Morgen.","avg_logprob":-0.2,"compression_ratio":1.0,"no_speech_prob":0.01},
              {"id":1,"start":1.0,"end":2.0,"text":" Das ist ein Testphantom.","avg_logprob":-0.2,"compression_ratio":1.0,"no_speech_prob":0.01}
            ]}
            """;
        var filter = new HallucinationFilterOptions(ExtraPhrases: new[] { "das ist ein testphantom" });

        var mitZusatz = await Erkenner(NachgebauterDienst.MitAntwort(200, antwort), filter: filter)
            .TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);
        var ohneZusatz = await Erkenner(NachgebauterDienst.MitAntwort(200, antwort))
            .TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("Guten Morgen.", mitZusatz.Text);
        Assert.Equal(1, mitZusatz.FilteredSegments);
        Assert.Equal("Guten Morgen. Das ist ein Testphantom.", ohneZusatz.Text);
        Assert.Equal(0, ohneZusatz.FilteredSegments);
    }

    // Regel 6 (d): Werden alle Abschnitte verworfen, ist der Text leer und alle zählen als verworfen.
    [Fact]
    public async Task d_Alle_Abschnitte_verworfen_ergibt_leeren_Text()
    {
        const string antwort = """
            {"text":" Rauschen noch mehr","language":"German","segments":[
              {"id":0,"start":0.0,"end":1.0,"text":" Rauschen","avg_logprob":-0.2,"compression_ratio":1.0,"no_speech_prob":0.97},
              {"id":1,"start":1.0,"end":2.0,"text":" noch mehr","avg_logprob":-0.2,"compression_ratio":5.0,"no_speech_prob":0.01}
            ]}
            """;
        var dienst = NachgebauterDienst.MitAntwort(200, antwort);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Equal(2, ergebnis.FilteredSegments);
    }

    // ---------- (e) ----------

    // Regel 6 (e): Antwort ohne Abschnitte (Feld fehlt oder leer): Text ist FilterText des Gesamttexts.
    [Theory]
    [InlineData("{\"text\":\"  Hallo Welt.  \",\"language\":\"German\"}")]
    [InlineData("{\"text\":\"  Hallo Welt.  \",\"language\":\"German\",\"segments\":[]}")]
    public async Task e_Ohne_Abschnitte_ist_Text_der_bereinigte_Gesamttext(string antwort)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, antwort);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal("Hallo Welt.", ergebnis.Text);
        Assert.Equal(0, ergebnis.FilteredSegments);
        Assert.Equal("groq", ergebnis.Engine);
        Assert.Equal("german", ergebnis.DetectedLanguage);
        Assert.Null(ergebnis.FallbackReason);
    }

    // Regel 6 (e): Ist der ganze Text ein Phantomtext, ist Text leer und FilteredSegments 1.
    [Theory]
    [InlineData("{\"text\":\" Untertitel im Auftrag des ZDF für funk, 2017 \",\"language\":\"German\"}")]
    [InlineData("{\"text\":\" Untertitel im Auftrag des ZDF für funk, 2017 \",\"language\":\"German\",\"segments\":[]}")]
    public async Task e_Ohne_Abschnitte_ganzer_Phantomtext_ergibt_leer_und_zaehlt_1(string antwort)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, antwort);

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(string.Empty, ergebnis.Text);
        Assert.Equal(1, ergebnis.FilteredSegments);
    }

    // Regel 6 (e): Auch hier gelten die Zusatztexte aus den Optionen des Konstruktors.
    [Fact]
    public async Task e_Ohne_Abschnitte_gilt_der_Zusatztext_aus_dem_Konstruktor()
    {
        const string antwort = "{\"text\":\" Das ist ein Testphantom. \",\"language\":\"German\"}";
        var filter = new HallucinationFilterOptions(ExtraPhrases: new[] { "das ist ein testphantom" });

        var mitZusatz = await Erkenner(NachgebauterDienst.MitAntwort(200, antwort), filter: filter)
            .TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);
        var ohneZusatz = await Erkenner(NachgebauterDienst.MitAntwort(200, antwort))
            .TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Equal(string.Empty, mitZusatz.Text);
        Assert.Equal(1, mitZusatz.FilteredSegments);
        Assert.Equal("Das ist ein Testphantom.", ohneZusatz.Text);
        Assert.Equal(0, ohneZusatz.FilteredSegments);
    }

    // ---------- (f) ----------

    // Regel 6 (f): Kein gültiges JSON, Wurzel kein Objekt oder text fehlt: BadResponse.
    [Theory]
    [InlineData("das ist kein json")]
    [InlineData("")]
    [InlineData("{\"text\":\"abgeschnitten")]
    [InlineData("<html><body>Wartung</body></html>")]
    [InlineData("[1,2,3]")]
    [InlineData("\"nur ein Text\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"language\":\"German\",\"segments\":[]}")]
    public async Task f_Unbrauchbare_Antwort_ergibt_BadResponse(string antwort)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.BadResponse, ausnahme.Error);
    }

    // ---------- (g) ----------

    // Regel 6 (g): 401 und 403 Unauthorized; 429 RateLimited; 413 TooLarge; 408 Timeout; 400, 404, 422 BadRequest;
    // ab 500 ServerError; alles andere BadResponse.
    [Theory]
    [InlineData(401, TranscriptionError.Unauthorized)]
    [InlineData(403, TranscriptionError.Unauthorized)]
    [InlineData(429, TranscriptionError.RateLimited)]
    [InlineData(413, TranscriptionError.TooLarge)]
    [InlineData(408, TranscriptionError.Timeout)]
    [InlineData(400, TranscriptionError.BadRequest)]
    [InlineData(404, TranscriptionError.BadRequest)]
    [InlineData(422, TranscriptionError.BadRequest)]
    [InlineData(500, TranscriptionError.ServerError)]
    [InlineData(502, TranscriptionError.ServerError)]
    [InlineData(503, TranscriptionError.ServerError)]
    [InlineData(504, TranscriptionError.ServerError)]
    [InlineData(599, TranscriptionError.ServerError)]
    [InlineData(402, TranscriptionError.BadResponse)]
    [InlineData(405, TranscriptionError.BadResponse)]
    [InlineData(409, TranscriptionError.BadResponse)]
    [InlineData(418, TranscriptionError.BadResponse)]
    [InlineData(302, TranscriptionError.BadResponse)]
    public async Task g_Status_wird_auf_den_Fehlergrund_abgebildet(int status, TranscriptionError erwartet)
    {
        var dienst = NachgebauterDienst.MitAntwort(status, Fehlerantwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(erwartet, ausnahme.Error);
    }

    // Regel 6 (g): Die Abbildung hängt am Status, nicht am Rumpf (Rumpf ohne JSON).
    [Theory]
    [InlineData(401, TranscriptionError.Unauthorized)]
    [InlineData(429, TranscriptionError.RateLimited)]
    [InlineData(413, TranscriptionError.TooLarge)]
    [InlineData(400, TranscriptionError.BadRequest)]
    [InlineData(502, TranscriptionError.ServerError)]
    public async Task g_Status_zaehlt_auch_bei_Rumpf_ohne_JSON(int status, TranscriptionError erwartet)
    {
        var dienst = NachgebauterDienst.MitAntwort(status, "<html><body>Fehler</body></html>");

        var ausnahme = await Fehler(dienst);

        Assert.Equal(erwartet, ausnahme.Error);
        Assert.Null(ausnahme.ServerCode);
    }

    // Regel 6 (g): 429 trägt RetryAfter aus der Kopfzeile retry-after.
    [Theory]
    [InlineData("7", 7.0)]
    [InlineData("120", 120.0)]
    public async Task g_429_traegt_RetryAfter_aus_der_Kopfzeile(string kopfwert, double sekunden)
    {
        var dienst = NachgebauterDienst.MitAntwort(429, Fehlerantwort, ("retry-after", kopfwert));

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.RateLimited, ausnahme.Error);
        Assert.Equal(sekunden, Assert.NotNull(ausnahme.RetryAfter).TotalSeconds, 0.001);
    }

    // Regel 6 (g): 429 ohne die Kopfzeile hat keine Wartezeit.
    [Fact]
    public async Task g_429_ohne_Kopfzeile_hat_keine_Wartezeit()
    {
        var dienst = NachgebauterDienst.MitAntwort(429, Fehlerantwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.RateLimited, ausnahme.Error);
        Assert.Null(ausnahme.RetryAfter);
    }

    // Regel 6 (g): ServerCode kommt aus error.code.
    [Fact]
    public async Task g_ServerCode_kommt_aus_error_code()
    {
        const string antwort =
            "{\"error\":{\"message\":\"Invalid API Key\",\"type\":\"invalid_request_error\",\"code\":\"invalid_api_key\"}}";
        var dienst = NachgebauterDienst.MitAntwort(401, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.Unauthorized, ausnahme.Error);
        Assert.Equal("invalid_api_key", ausnahme.ServerCode);
    }

    // Regel 6 (g): Fehlt error.code, kommt ServerCode aus error.type.
    [Fact]
    public async Task g_ServerCode_kommt_sonst_aus_error_type()
    {
        const string antwort = "{\"error\":{\"message\":\"Zu viele Anfragen\",\"type\":\"tokens\"}}";
        var dienst = NachgebauterDienst.MitAntwort(429, antwort, ("retry-after", "3"));

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.RateLimited, ausnahme.Error);
        Assert.Equal("tokens", ausnahme.ServerCode);
    }

    // Regel 6 (g), verfeinert (Abschnitt 15, Stand nach der Umsetzung, Punkt 7): Ist error.code leer oder nach dem Bereinigen leer,
    // kommt ServerCode aus error.type.
    [Theory]
    [InlineData("")]
    [InlineData("###")]
    public async Task g_ServerCode_kommt_aus_error_type_wenn_error_code_leer_bleibt(string code)
    {
        var antwort = "{\"error\":{\"message\":\"Fehler\",\"type\":\"invalid_request_error\",\"code\":\"" + code + "\"}}";
        var dienst = NachgebauterDienst.MitAntwort(401, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.Unauthorized, ausnahme.Error);
        Assert.Equal("invalid_request_error", ausnahme.ServerCode);
    }

    // Regel 6 (g), verfeinert: Mit gültigem error.code gewinnt weiterhin code vor type, bei sonst gleicher Antwort.
    [Fact]
    public async Task g_ServerCode_nimmt_bei_gueltigem_error_code_weiterhin_code()
    {
        const string antwort =
            "{\"error\":{\"message\":\"Fehler\",\"type\":\"invalid_request_error\",\"code\":\"model_not_found\"}}";
        var dienst = NachgebauterDienst.MitAntwort(401, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.Unauthorized, ausnahme.Error);
        Assert.Equal("model_not_found", ausnahme.ServerCode);
    }

    // Regel 6 (g): ServerCode ist bereinigt (Regel 5).
    [Theory]
    [InlineData("Rate-Limit Exceeded!", "ratelimitexceeded")]
    [InlineData("MODEL_NOT_FOUND", "model_not_found")]
    public async Task g_ServerCode_ist_bereinigt(string code, string erwartet)
    {
        var antwort = "{\"error\":{\"message\":\"Fehler\",\"type\":\"anderer_typ\",\"code\":\"" + code + "\"}}";
        var dienst = NachgebauterDienst.MitAntwort(400, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.BadRequest, ausnahme.Error);
        Assert.Equal(erwartet, ausnahme.ServerCode);
    }

    // Regel 6 (g): Ohne error.code und error.type gibt es kein Kürzel.
    [Theory]
    [InlineData("{\"error\":{\"message\":\"Fehler ohne Kürzel\"}}")]
    [InlineData("{\"hinweis\":\"kein Fehlerobjekt\"}")]
    [InlineData("")]
    public async Task g_ServerCode_fehlt_ohne_Angabe(string antwort)
    {
        var dienst = NachgebauterDienst.MitAntwort(500, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.ServerError, ausnahme.Error);
        Assert.Null(ausnahme.ServerCode);
    }

    // ---------- (h) ----------

    // Regel 6 (h): HttpRequestException des Dienstes wird Network.
    [Fact]
    public async Task h_HttpRequestException_wird_Network()
    {
        var dienst = NachgebauterDienst.MitNetzfehler();

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.Network, ausnahme.Error);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 6 (h): Antwortet der Dienst nicht innerhalb TimeoutMs, kommt Timeout.
    [Fact]
    public async Task h_Keine_Antwort_innerhalb_TimeoutMs_wird_Timeout()
    {
        var dienst = NachgebauterDienst.DerNichtAntwortet();
        var optionen = new GroqOptions(TimeoutMs: 150);

        var ausnahme = await Fehler(dienst, optionen: optionen);

        Assert.Equal(TranscriptionError.Timeout, ausnahme.Error);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 6 (h): Bricht der Aufrufer ab, kommt ein Abbruch (OperationCanceledException), kein TranscriptionException.
    [Fact]
    public async Task h_Abbruch_durch_den_Aufrufer_bleibt_Abbruch()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        var dienst = NachgebauterDienst.DerNichtAntwortet(beimEintreffen: quelle.Cancel);
        var erkenner = Erkenner(dienst);

        // TranscriptionException erbt nicht von OperationCanceledException, der Typ ist damit zugleich ausgeschlossen.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => erkenner.TranscribeAsync(Clip(), new TranscriptionRequest(), quelle.Token));

        Assert.Single(dienst.Anfragen);
    }

    // Regel 6 (h): Ist das Signal schon vor dem Aufruf gesetzt, kommt ebenfalls ein Abbruch.
    [Fact]
    public async Task h_Vorab_abgebrochen_bleibt_Abbruch()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);
        var erkenner = Erkenner(dienst);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => erkenner.TranscribeAsync(Clip(), new TranscriptionRequest(), quelle.Token));
    }

    // ---------- (i) ----------

    // Regel 6 (i): Limit Kopfzeilen stehen im Ergebnis unter RateLimit.
    [Fact]
    public async Task i_Limit_Kopfzeilen_stehen_im_Ergebnis()
    {
        var dienst = NachgebauterDienst.MitAntwort(
            200,
            EinfacheAntwort,
            ("x-ratelimit-limit-requests", "2000"),
            ("x-ratelimit-remaining-requests", "1999"),
            ("x-ratelimit-reset-requests", "2m59.56s"));

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.NotNull(ergebnis.RateLimit);
        Assert.Equal(2000, ergebnis.RateLimit.LimitRequests);
        Assert.Equal(1999, ergebnis.RateLimit.RemainingRequests);
        Assert.Equal(179.56, Assert.NotNull(ergebnis.RateLimit.ResetRequests).TotalSeconds, 0.001);
        Assert.Null(ergebnis.RateLimit.RetryAfter);
    }

    // Regel 6 (i) mit Regel 4: Ohne auswertbare Limit Kopfzeilen ist RateLimit null.
    [Fact]
    public async Task i_Ohne_Limit_Kopfzeilen_ist_RateLimit_null()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort, ("x-request-id", "req_test"));

        var ergebnis = await Erkenner(dienst).TranscribeAsync(Clip(), new TranscriptionRequest(), Abbruch);

        Assert.Null(ergebnis.RateLimit);
    }

    // ---------- (j) ----------

    // Regel 6 (j): Keine Meldung einer Ausnahme enthält den Schlüssel (alle Statusfehler).
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(408)]
    [InlineData(409)]
    [InlineData(413)]
    [InlineData(422)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task j_Meldung_enthaelt_bei_Statusfehlern_nie_den_Schluessel(int status)
    {
        var dienst = NachgebauterDienst.MitAntwort(status, Fehlerantwort, ("retry-after", "7"));

        var ausnahme = await Fehler(dienst);

        Assert.DoesNotContain(Schluessel, ausnahme.ToString());
        Assert.All(Meldungen(ausnahme), meldung => Assert.DoesNotContain(Schluessel, meldung));
    }

    // Regel 6 (j): Auch wenn der Dienst den Schlüssel in seiner Fehlermeldung zurückschickt,
    // steht er weder in der Meldung noch im Kürzel.
    [Fact]
    public async Task j_Meldung_enthaelt_den_Schluessel_auch_nicht_aus_der_Antwort_des_Dienstes()
    {
        var antwort = "{\"error\":{\"message\":\"Invalid API Key: " + Schluessel + "\",\"type\":\"invalid_request_error\","
            + "\"code\":\"invalid_api_key\"}}";
        var dienst = NachgebauterDienst.MitAntwort(401, antwort);

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.Unauthorized, ausnahme.Error);
        Assert.All(Meldungen(ausnahme), meldung => Assert.DoesNotContain(Schluessel, meldung));
        Assert.DoesNotContain(Schluessel, ausnahme.ServerCode ?? string.Empty);
    }

    // Regel 6 (j): Netzfehler, Zeitüberschreitung, unbrauchbare Antwort und fehlender Schlüssel.
    [Fact]
    public async Task j_Meldung_enthaelt_bei_anderen_Fehlern_nie_den_Schluessel()
    {
        var netz = await Fehler(NachgebauterDienst.MitNetzfehler());
        var zeit = await Fehler(NachgebauterDienst.DerNichtAntwortet(), optionen: new GroqOptions(TimeoutMs: 150));
        var antwort = await Fehler(NachgebauterDienst.MitAntwort(200, "kein json"));
        var ohneText = await Fehler(NachgebauterDienst.MitAntwort(200, "{\"language\":\"German\"}"));

        foreach (var ausnahme in new[] { netz, zeit, antwort, ohneText })
        {
            Assert.DoesNotContain(Schluessel, ausnahme.ToString());
            Assert.All(Meldungen(ausnahme), meldung => Assert.DoesNotContain(Schluessel, meldung));
        }
    }

    // Regel 6 (j): Wirft der Dienst eine Ausnahme, deren Text den Schlüssel enthält, steht er nicht in der Meldung
    // der Ausnahme des Erkenners.
    [Fact]
    public async Task j_Meldung_uebernimmt_keinen_Schluessel_aus_dem_Netzfehler()
    {
        var dienst = NachgebauterDienst.MitNetzfehler("Verbindung mit " + Schluessel + " fehlgeschlagen");

        var ausnahme = await Fehler(dienst);

        Assert.Equal(TranscriptionError.Network, ausnahme.Error);
        Assert.DoesNotContain(Schluessel, ausnahme.Message);
    }

    // ---------- (k) ----------

    // Regel 6 (k), Vertrag: Vorgabewerte der Optionen und Validate ohne Fehler.
    [Fact]
    public void k_Optionen_haben_die_Vorgabewerte_und_sind_gueltig()
    {
        var optionen = new GroqOptions();

        Assert.Equal(Endpunkt, optionen.Endpoint);
        Assert.Equal("whisper-large-v3-turbo", optionen.Model);
        Assert.Equal(15000, optionen.TimeoutMs);
        Assert.Equal(400, optionen.MaxPromptChars);
        Assert.Equal(Endpunkt, GroqOptions.DefaultEndpoint);
        Assert.Equal("whisper-large-v3-turbo", GroqOptions.DefaultModel);
        optionen.Validate();
    }

    // Regel 6 (k): Validate wirft bei leerem Endpunkt (ArgumentException).
    [Fact]
    public void k_Validate_wirft_bei_leerem_Endpunkt()
    {
        Assert.ThrowsAny<ArgumentException>(() => new GroqOptions(Endpoint: string.Empty).Validate());
    }

    // Regel 6 (k): Validate wirft bei leerem Modell (ArgumentException).
    [Fact]
    public void k_Validate_wirft_bei_leerem_Modell()
    {
        Assert.ThrowsAny<ArgumentException>(() => new GroqOptions(Model: string.Empty).Validate());
    }

    // Regel 6 (k): Validate wirft bei TimeoutMs kleiner gleich 0 (ArgumentOutOfRangeException).
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-15000)]
    public void k_Validate_wirft_bei_TimeoutMs_kleiner_gleich_0(int wert)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GroqOptions(TimeoutMs: wert).Validate());
    }

    // Regel 6 (k): Validate wirft bei MaxPromptChars kleiner gleich 0 (ArgumentOutOfRangeException).
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-400)]
    public void k_Validate_wirft_bei_MaxPromptChars_kleiner_gleich_0(int wert)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GroqOptions(MaxPromptChars: wert).Validate());
    }

    // Regel 6 (k): Die kleinsten gültigen Werte bestehen Validate.
    [Fact]
    public void k_Validate_nimmt_kleinste_gueltige_Werte()
    {
        new GroqOptions(TimeoutMs: 1, MaxPromptChars: 1).Validate();
    }

    // Regel 6 (k): Der Konstruktor wirft bei null für den Client.
    [Fact]
    public void k_Konstruktor_wirft_bei_null_fuer_den_Client()
    {
        Assert.ThrowsAny<ArgumentException>(() => new GroqTranscriber(null!, () => Schluessel));
    }

    // Regel 6 (k): Der Konstruktor wirft bei null für die Schlüsselfunktion.
    [Fact]
    public void k_Konstruktor_wirft_bei_null_fuer_die_Schluesselfunktion()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, EinfacheAntwort);

        Assert.ThrowsAny<ArgumentException>(() => new GroqTranscriber(new HttpClient(dienst), null!));
    }
}
