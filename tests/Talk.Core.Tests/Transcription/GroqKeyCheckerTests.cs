using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft die Schlüsselprüfung ausschließlich gegen Regel 7 der reinen Logik in PLAYBOOK.md, Abschnitt 15,
/// mit dem nachgebauten Dienst aus GroqTranscriberTests.cs. Es geht nie etwas ins Netz, der Schlüssel ist ein
/// erfundener Testwert, und die Windows Anmeldeinformationsverwaltung wird nicht berührt.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class GroqKeyCheckerTests
{
    private const string Schluessel = "test-schluessel-123";
    private const string Modellliste = "https://api.groq.com/openai/v1/models";
    private const string Erfolg = "{\"object\":\"list\",\"data\":[]}";
    private const string Ablehnung =
        "{\"error\":{\"message\":\"Invalid API Key\",\"type\":\"invalid_request_error\",\"code\":\"invalid_api_key\"}}";

    private static CancellationToken Abbruch => TestContext.Current.CancellationToken;

    // Regel 7 (Vertrag): Die vier Ergebnisse und die Vorgabeadresse.
    [Fact]
    public void Ergebnisse_und_Vorgabeadresse_stimmen()
    {
        Assert.Equal(
            new[] { "Valid", "Invalid", "Unreachable", "Missing" },
            Enum.GetNames<KeyCheckResult>());
        Assert.Equal(Modellliste, GroqKeyChecker.DefaultModelsEndpoint);
    }

    // Regel 7: GET an die Adresse mit Authorization Bearer (Vorgabeadresse).
    [Fact]
    public async Task Anfrage_ist_ein_GET_an_die_Vorgabeadresse_mit_Bearer()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, Erfolg);

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch);

        Assert.Equal(KeyCheckResult.Valid, ergebnis);
        var anfrage = Assert.Single(dienst.Anfragen);
        Assert.Equal(HttpMethod.Get, anfrage.Methode);
        Assert.Equal(Modellliste, anfrage.Adresse);
        Assert.Equal("Bearer " + Schluessel, anfrage.Authorization);
    }

    // Regel 7: GET an die angegebene Adresse.
    [Fact]
    public async Task Anfrage_geht_an_die_angegebene_Adresse()
    {
        const string eigeneAdresse = "https://api.groq.com/openai/v1/eigene-liste";
        var dienst = NachgebauterDienst.MitAntwort(200, Erfolg);

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch, eigeneAdresse);

        Assert.Equal(KeyCheckResult.Valid, ergebnis);
        var anfrage = Assert.Single(dienst.Anfragen);
        Assert.Equal(HttpMethod.Get, anfrage.Methode);
        Assert.Equal(eigeneAdresse, anfrage.Adresse);
        Assert.Equal("Bearer " + Schluessel, anfrage.Authorization);
    }

    // Regel 7: Die Prüfung sendet kein Audio und keinen Rumpf (es ist eine reine Leseanfrage).
    [Fact]
    public async Task Anfrage_hat_keinen_Rumpf()
    {
        var dienst = NachgebauterDienst.MitAntwort(200, Erfolg);

        await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch);

        Assert.Empty(Assert.Single(dienst.Anfragen).Inhalt);
    }

    // Regel 7: 200 bis 299 ergibt Valid.
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(299)]
    public async Task Status_200_bis_299_ergibt_Valid(int status)
    {
        var dienst = NachgebauterDienst.MitAntwort(status, Erfolg);

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch);

        Assert.Equal(KeyCheckResult.Valid, ergebnis);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 7: 401 und 403 ergeben Invalid.
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Status_401_und_403_ergeben_Invalid(int status)
    {
        var dienst = NachgebauterDienst.MitAntwort(status, Ablehnung);

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch);

        Assert.Equal(KeyCheckResult.Invalid, ergebnis);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 7: Anderer Status ergibt Unreachable.
    [Theory]
    [InlineData(300)]
    [InlineData(302)]
    [InlineData(400)]
    [InlineData(402)]
    [InlineData(404)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Anderer_Status_ergibt_Unreachable(int status)
    {
        var dienst = NachgebauterDienst.MitAntwort(status, Ablehnung);

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch);

        Assert.Equal(KeyCheckResult.Unreachable, ergebnis);
    }

    // Regel 7: HttpRequestException ergibt Unreachable.
    [Fact]
    public async Task HttpRequestException_ergibt_Unreachable()
    {
        var dienst = NachgebauterDienst.MitNetzfehler();

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch);

        Assert.Equal(KeyCheckResult.Unreachable, ergebnis);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 7: Zeitüberschreitung ergibt Unreachable.
    [Fact]
    public async Task Zeitueberschreitung_ergibt_Unreachable()
    {
        var dienst = NachgebauterDienst.DerNichtAntwortet();

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), Schluessel, Abbruch, Modellliste, 150);

        Assert.Equal(KeyCheckResult.Unreachable, ergebnis);
        Assert.Single(dienst.Anfragen);
    }

    // Regel 7: Ohne Schlüssel kommt Missing, ohne Anfrage.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ohne_Schluessel_ergibt_Missing_ohne_Anfrage(string? schluessel)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, Erfolg);

        var ergebnis = await GroqKeyChecker.CheckAsync(new HttpClient(dienst), schluessel, Abbruch);

        Assert.Equal(KeyCheckResult.Missing, ergebnis);
        Assert.Empty(dienst.Anfragen);
    }

    // Regel 7: Abbruch durch den Aufrufer bleibt Abbruch (während die Anfrage läuft).
    [Fact]
    public async Task Abbruch_durch_den_Aufrufer_bleibt_Abbruch()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        var dienst = NachgebauterDienst.DerNichtAntwortet(beimEintreffen: quelle.Cancel);
        var client = new HttpClient(dienst);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GroqKeyChecker.CheckAsync(client, Schluessel, quelle.Token));

        Assert.Single(dienst.Anfragen);
    }

    // Regel 7: Abbruch durch den Aufrufer bleibt Abbruch (Signal schon vor dem Aufruf gesetzt).
    [Fact]
    public async Task Vorab_abgebrochen_bleibt_Abbruch()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();
        var client = new HttpClient(NachgebauterDienst.MitAntwort(200, Erfolg));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GroqKeyChecker.CheckAsync(client, Schluessel, quelle.Token));
    }

    // Regel 7: timeoutMs kleiner gleich 0 wirft.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-8000)]
    public async Task TimeoutMs_kleiner_gleich_0_wirft(int timeoutMs)
    {
        var dienst = NachgebauterDienst.MitAntwort(200, Erfolg);
        var client = new HttpClient(dienst);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => GroqKeyChecker.CheckAsync(client, Schluessel, Abbruch, Modellliste, timeoutMs));

        Assert.Empty(dienst.Anfragen);
    }
}
