// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Secrets;
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft Kürzel und Sprache ausschließlich gegen Regel 5 der reinen Logik in PLAYBOOK.md, Abschnitt 15,
/// dazu die Vorgabewerte der Datentypen aus dem Vertrag desselben Abschnitts.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class TranscriptionBasicsTests
{
    // ---------- SanitizeCode ----------

    // Regel 5: die drei Beispiele aus der Regel.
    [Theory]
    [InlineData("invalid_api_key", "invalid_api_key")]
    [InlineData("Rate-Limit Exceeded!", "ratelimitexceeded")]
    [InlineData("###", null)]
    public void SanitizeCode_Beispiele_aus_der_Regel(string eingabe, string? erwartet)
    {
        Assert.Equal(erwartet, TranscriptionException.SanitizeCode(eingabe));
    }

    // Regel 5: Kleinbuchstaben.
    [Theory]
    [InlineData("INVALID_API_KEY", "invalid_api_key")]
    [InlineData("RateLimitExceeded", "ratelimitexceeded")]
    public void SanitizeCode_macht_Kleinbuchstaben(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, TranscriptionException.SanitizeCode(eingabe));
    }

    // Regel 5: Nur a bis z, 0 bis 9 und Unterstrich bleiben.
    [Theory]
    [InlineData("model_not_found_404", "model_not_found_404")]
    [InlineData("a b\tc", "abc")]
    [InlineData("code: 42; type=x", "code42typex")]
    [InlineData("a.b-c/d\\e", "abcde")]
    [InlineData("Fehler_Ä1", "fehler_1")]
    [InlineData("<script>alert(1)</script>", "scriptalert1script")]
    [InlineData("zeile1\r\nzeile2", "zeile1zeile2")]
    public void SanitizeCode_behaelt_nur_erlaubte_Zeichen(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, TranscriptionException.SanitizeCode(eingabe));
    }

    // Regel 5: Höchstens 40 Zeichen.
    [Fact]
    public void SanitizeCode_kuerzt_auf_40_Zeichen()
    {
        var lang = new string('a', 39) + "bcdefghijk";

        var ergebnis = TranscriptionException.SanitizeCode(lang);

        Assert.Equal(new string('a', 39) + "b", ergebnis);
    }

    // Regel 5: Genau 40 Zeichen bleiben vollständig.
    [Fact]
    public void SanitizeCode_laesst_genau_40_Zeichen_stehen()
    {
        var genau = new string('x', 40);

        Assert.Equal(genau, TranscriptionException.SanitizeCode(genau));
    }

    // Regel 5: Das Ergebnis ist nie länger als 40 Zeichen und enthält nur erlaubte Zeichen.
    [Theory]
    [InlineData("Ein sehr langer Fehlertext mit vielen Wörtern, Satzzeichen und Zahlen 1234567890 am Ende!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("___---___---___---___---___---___---___---___---___---___---___---___---___---")]
    public void SanitizeCode_ist_nie_laenger_als_40_und_nur_aus_erlaubten_Zeichen(string eingabe)
    {
        var ergebnis = TranscriptionException.SanitizeCode(eingabe);

        Assert.NotNull(ergebnis);
        Assert.InRange(ergebnis.Length, 1, 40);
        Assert.All(ergebnis, zeichen => Assert.True(
            (zeichen >= 'a' && zeichen <= 'z') || (zeichen >= '0' && zeichen <= '9') || zeichen == '_'));
    }

    // Regel 5: Leer ergibt null (auch null, nur Leerzeichen und Text ohne erlaubte Zeichen).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("###")]
    [InlineData("-.-!?")]
    [InlineData("ÄÖÜ")]
    public void SanitizeCode_leer_ergibt_null(string? eingabe)
    {
        Assert.Null(TranscriptionException.SanitizeCode(eingabe));
    }

    // Regel 5 (Vertrag): Die Ausnahme trägt Grund, Meldung, innere Ausnahme, Wartezeit und Kürzel.
    [Fact]
    public void TranscriptionException_traegt_ihre_Angaben()
    {
        var innen = new InvalidOperationException("innen");

        var ausnahme = new TranscriptionException(
            TranscriptionError.RateLimited,
            "Limit erreicht",
            innen,
            TimeSpan.FromSeconds(7),
            "rate_limit_exceeded");

        Assert.Equal(TranscriptionError.RateLimited, ausnahme.Error);
        Assert.Equal("Limit erreicht", ausnahme.Message);
        Assert.Same(innen, ausnahme.InnerException);
        Assert.Equal(TimeSpan.FromSeconds(7), ausnahme.RetryAfter);
        Assert.Equal("rate_limit_exceeded", ausnahme.ServerCode);
    }

    // Regel 5 (Vertrag): Ohne Zusatzangaben sind innere Ausnahme, Wartezeit und Kürzel leer.
    [Fact]
    public void TranscriptionException_ohne_Zusatzangaben()
    {
        var ausnahme = new TranscriptionException(TranscriptionError.Network, "kein Netz");

        Assert.Equal(TranscriptionError.Network, ausnahme.Error);
        Assert.Null(ausnahme.InnerException);
        Assert.Null(ausnahme.RetryAfter);
        Assert.Null(ausnahme.ServerCode);
    }

    // Regel 5 (Vertrag): Die zwölf Fehlergründe des Vertrags sind vorhanden.
    [Fact]
    public void TranscriptionError_hat_die_zwoelf_Gruende_des_Vertrags()
    {
        var erwartet = new[]
        {
            "NoKey", "Unauthorized", "RateLimited", "Network", "Timeout", "ServerError",
            "TooLarge", "BadRequest", "BadResponse", "ModelMissing", "LocalUnavailable", "EngineFailed",
        };

        Assert.Equal(erwartet, Enum.GetNames<TranscriptionError>());
    }

    // ---------- AutoDetect ----------

    // Regel 5: AutoDetect ist wahr für leer, nur Leerzeichen, "auto", "AUTO", " Auto ".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData(" Auto ")]
    public void AutoDetect_ist_wahr(string sprache)
    {
        Assert.True(new TranscriptionRequest(sprache).AutoDetect);
    }

    // Regel 5: sonst falsch.
    [Theory]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("DE")]
    [InlineData(" de ")]
    [InlineData("automatisch")]
    [InlineData("au to")]
    public void AutoDetect_ist_sonst_falsch(string sprache)
    {
        Assert.False(new TranscriptionRequest(sprache).AutoDetect);
    }

    // Regel 5 (Vertrag): Vorgabe ist Deutsch ohne Wörterbuch, also keine automatische Erkennung.
    [Fact]
    public void TranscriptionRequest_Vorgabe_ist_Deutsch_ohne_Woerterbuch()
    {
        var anfrage = new TranscriptionRequest();

        Assert.Equal("de", anfrage.Language);
        Assert.Null(anfrage.Vocabulary);
        Assert.False(anfrage.AutoDetect);
    }

    // Regel 5 (Vertrag): Das Kürzel für die automatische Erkennung ist "auto".
    [Fact]
    public void AutoLanguage_ist_auto()
    {
        Assert.Equal("auto", TranscriptionRequest.AutoLanguage);
        Assert.True(new TranscriptionRequest(TranscriptionRequest.AutoLanguage).AutoDetect);
    }

    // ---------- Vorgabewerte aus dem Vertrag ----------

    // Vertrag: TranscriptionResult mit Segments, Timings, RateLimit gleich null, FilteredSegments 0, FallbackReason null.
    [Fact]
    public void TranscriptionResult_hat_die_Vorgabewerte_des_Vertrags()
    {
        var ergebnis = new TranscriptionResult("Hallo", "de", TimeSpan.FromMilliseconds(250), "groq");

        Assert.Equal("Hallo", ergebnis.Text);
        Assert.Equal("de", ergebnis.DetectedLanguage);
        Assert.Equal(TimeSpan.FromMilliseconds(250), ergebnis.Elapsed);
        Assert.Equal("groq", ergebnis.Engine);
        Assert.Null(ergebnis.Segments);
        Assert.Null(ergebnis.Timings);
        Assert.Null(ergebnis.RateLimit);
        Assert.Equal(0, ergebnis.FilteredSegments);
        Assert.Null(ergebnis.FallbackReason);
    }

    // Vertrag: TranscriptionSegment und TranscriptionTimings tragen ihre Werte in der Reihenfolge des Vertrags.
    [Fact]
    public void Abschnitt_und_Zeiten_tragen_ihre_Werte()
    {
        var abschnitt = new TranscriptionSegment(1.5, 2.5, "Hallo", -0.3, 0.02, 1.1);
        var zeiten = new TranscriptionTimings(3, 40, 200, 250);

        Assert.Equal(1.5, abschnitt.StartSeconds);
        Assert.Equal(2.5, abschnitt.EndSeconds);
        Assert.Equal("Hallo", abschnitt.Text);
        Assert.Equal(-0.3, abschnitt.AvgLogProb);
        Assert.Equal(0.02, abschnitt.NoSpeechProb);
        Assert.Equal(1.1, abschnitt.CompressionRatio);
        Assert.Equal(3, zeiten.EncodeMs);
        Assert.Equal(40, zeiten.UploadMs);
        Assert.Equal(200, zeiten.WaitMs);
        Assert.Equal(250, zeiten.TotalMs);
    }

    // Vertrag: Die Kennzahlen eines Abschnitts dürfen fehlen.
    [Fact]
    public void Abschnitt_ohne_Kennzahlen_hat_null()
    {
        var abschnitt = new TranscriptionSegment(0, 1, "Hallo");

        Assert.Null(abschnitt.AvgLogProb);
        Assert.Null(abschnitt.NoSpeechProb);
        Assert.Null(abschnitt.CompressionRatio);
    }

    // Vertrag: RateLimitInfo trägt die vier Angaben.
    [Fact]
    public void RateLimitInfo_traegt_die_vier_Angaben()
    {
        var info = new RateLimitInfo(2000, 1999, TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(7));

        Assert.Equal(2000, info.LimitRequests);
        Assert.Equal(1999, info.RemainingRequests);
        Assert.Equal(TimeSpan.FromSeconds(180), info.ResetRequests);
        Assert.Equal(TimeSpan.FromSeconds(7), info.RetryAfter);
    }

    // Vertrag: Der Name des Geheimnisses für den Groq Schlüssel. Hier wird nur die Konstante gelesen,
    // die Anmeldeinformationsverwaltung wird nicht berührt.
    [Fact]
    public void SecretNames_GroqApiKey_heisst_GroqApiKey()
    {
        Assert.Equal("GroqApiKey", SecretNames.GroqApiKey);
    }
}
