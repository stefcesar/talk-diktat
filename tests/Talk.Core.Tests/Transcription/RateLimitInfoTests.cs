using System.Globalization;
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft die Limit Angaben ausschließlich gegen Regel 4 der reinen Logik in PLAYBOOK.md, Abschnitt 15.
/// Die Tests kennen die Umsetzung nicht. Dauern werden mit einer Toleranz von einer Millisekunde verglichen.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class RateLimitInfoTests
{
    private const string KopfLimit = "x-ratelimit-limit-requests";
    private const string KopfRest = "x-ratelimit-remaining-requests";
    private const string KopfReset = "x-ratelimit-reset-requests";
    private const string KopfWarten = "retry-after";

    private static void GleicheSekunden(double erwartet, TimeSpan? dauer)
    {
        var wert = Assert.NotNull(dauer);
        Assert.Equal(erwartet, wert.TotalSeconds, 0.001);
    }

    /// <summary>Kopfzeilen als Funktion, Namen ohne Groß und Klein wie bei HTTP üblich.</summary>
    private static Func<string, string?> Kopfzeilen(params (string Name, string Wert)[] zeilen)
    {
        var tabelle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, wert) in zeilen)
        {
            tabelle[name] = wert;
        }

        return name => tabelle.TryGetValue(name, out var wert) ? wert : null;
    }

    // ---------- ParseDuration ----------

    // Regel 4: reine Zahl in Sekunden ("7", "7.5").
    [Theory]
    [InlineData("7", 7.0)]
    [InlineData("7.5", 7.5)]
    [InlineData("0", 0.0)]
    [InlineData("120", 120.0)]
    [InlineData("0.25", 0.25)]
    public void ParseDuration_reine_Zahl_sind_Sekunden(string eingabe, double sekunden)
    {
        GleicheSekunden(sekunden, RateLimitInfo.ParseDuration(eingabe));
    }

    // Regel 4: Zahl mit Einheit h, m, s, ms.
    [Theory]
    [InlineData("1h", 3600.0)]
    [InlineData("2m", 120.0)]
    [InlineData("3s", 3.0)]
    [InlineData("59.56s", 59.56)]
    [InlineData("500ms", 0.5)]
    [InlineData("250ms", 0.25)]
    public void ParseDuration_Zahl_mit_Einheit(string eingabe, double sekunden)
    {
        GleicheSekunden(sekunden, RateLimitInfo.ParseDuration(eingabe));
    }

    // Regel 4: auch zusammengesetzt ("2m59.56s" sind 179,56 Sekunden, "1h0m0s" sind 3.600).
    [Theory]
    [InlineData("2m59.56s", 179.56)]
    [InlineData("1h0m0s", 3600.0)]
    [InlineData("1m30s", 90.0)]
    [InlineData("1h2m3s", 3723.0)]
    public void ParseDuration_zusammengesetzt(string eingabe, double sekunden)
    {
        GleicheSekunden(sekunden, RateLimitInfo.ParseDuration(eingabe));
    }

    // Regel 4: Leerzeichen an den Rändern sind egal.
    [Theory]
    [InlineData(" 7 ", 7.0)]
    [InlineData("\t7.5\t", 7.5)]
    [InlineData("  2m59.56s  ", 179.56)]
    [InlineData(" 500ms ", 0.5)]
    public void ParseDuration_Leerzeichen_an_den_Raendern_egal(string eingabe, double sekunden)
    {
        GleicheSekunden(sekunden, RateLimitInfo.ParseDuration(eingabe));
    }

    // Regel 4: null, leer, "abc", "-5", "5x", "1.2.3s" ergeben null.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("5x")]
    [InlineData("1.2.3s")]
    public void ParseDuration_nicht_auswertbar_ergibt_null(string? eingabe)
    {
        Assert.Null(RateLimitInfo.ParseDuration(eingabe));
    }

    // Regel 4: Der Punkt ist das Dezimalzeichen, unabhängig von der Sprache des Rechners (hier Deutsch mit Komma).
    [Fact]
    public void ParseDuration_haengt_nicht_von_der_Sprache_des_Rechners_ab()
    {
        var vorher = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            GleicheSekunden(7.5, RateLimitInfo.ParseDuration("7.5"));
            GleicheSekunden(179.56, RateLimitInfo.ParseDuration("2m59.56s"));
            GleicheSekunden(0.5, RateLimitInfo.ParseDuration("500ms"));
        }
        finally
        {
            CultureInfo.CurrentCulture = vorher;
        }
    }

    // ---------- FromHeaders ----------

    // Regel 4 (Vertrag): Die Namen der vier Kopfzeilen.
    [Fact]
    public void Namen_der_Kopfzeilen_stimmen()
    {
        Assert.Equal(KopfLimit, RateLimitInfo.HeaderLimitRequests);
        Assert.Equal(KopfRest, RateLimitInfo.HeaderRemainingRequests);
        Assert.Equal(KopfReset, RateLimitInfo.HeaderResetRequests);
        Assert.Equal(KopfWarten, RateLimitInfo.HeaderRetryAfter);
    }

    // Regel 4: FromHeaders liest alle vier Kopfzeilen.
    [Fact]
    public void FromHeaders_liest_alle_vier_Angaben()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen(
            (KopfLimit, "2000"),
            (KopfRest, "1999"),
            (KopfReset, "2m59.56s"),
            (KopfWarten, "7")));

        Assert.NotNull(info);
        Assert.Equal(2000, info.LimitRequests);
        Assert.Equal(1999, info.RemainingRequests);
        GleicheSekunden(179.56, info.ResetRequests);
        GleicheSekunden(7.0, info.RetryAfter);
    }

    // Regel 4: FromHeaders fragt die vier genannten Namen ab.
    [Fact]
    public void FromHeaders_fragt_die_vier_genannten_Namen_ab()
    {
        var gefragt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _ = RateLimitInfo.FromHeaders(name =>
        {
            gefragt.Add(name);
            return "1";
        });

        Assert.Contains(KopfLimit, gefragt);
        Assert.Contains(KopfRest, gefragt);
        Assert.Contains(KopfReset, gefragt);
        Assert.Contains(KopfWarten, gefragt);
    }

    // Regel 4: Ist nur eine der vier Angaben auswertbar, kommt ein Ergebnis mit dieser einen Angabe.
    [Fact]
    public void FromHeaders_mit_nur_Limit()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen((KopfLimit, "20")));

        Assert.NotNull(info);
        Assert.Equal(20, info.LimitRequests);
        Assert.Null(info.RemainingRequests);
        Assert.Null(info.ResetRequests);
        Assert.Null(info.RetryAfter);
    }

    // Regel 4: nur die Restzahl.
    [Fact]
    public void FromHeaders_mit_nur_Rest()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen((KopfRest, "0")));

        Assert.NotNull(info);
        Assert.Null(info.LimitRequests);
        Assert.Equal(0, info.RemainingRequests);
        Assert.Null(info.ResetRequests);
        Assert.Null(info.RetryAfter);
    }

    // Regel 4: nur die Dauer bis zum Zurücksetzen.
    [Fact]
    public void FromHeaders_mit_nur_Reset()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen((KopfReset, "1h0m0s")));

        Assert.NotNull(info);
        Assert.Null(info.LimitRequests);
        Assert.Null(info.RemainingRequests);
        GleicheSekunden(3600.0, info.ResetRequests);
        Assert.Null(info.RetryAfter);
    }

    // Regel 4: nur die Wartezeit.
    [Fact]
    public void FromHeaders_mit_nur_RetryAfter()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen((KopfWarten, "7.5")));

        Assert.NotNull(info);
        Assert.Null(info.LimitRequests);
        Assert.Null(info.RemainingRequests);
        Assert.Null(info.ResetRequests);
        GleicheSekunden(7.5, info.RetryAfter);
    }

    // Regel 4: Die Zahlen sind ganze Zahlen ohne Vorzeichen, sonst null.
    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("12x")]
    [InlineData("")]
    public void FromHeaders_ungueltige_Zahl_ergibt_null_fuer_das_Feld(string wert)
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen(
            (KopfLimit, wert),
            (KopfRest, wert),
            (KopfWarten, "3")));

        Assert.NotNull(info);
        Assert.Null(info.LimitRequests);
        Assert.Null(info.RemainingRequests);
        GleicheSekunden(3.0, info.RetryAfter);
    }

    // Regel 4: Nicht auswertbare Dauern ergeben null für das Feld, die übrigen Angaben bleiben.
    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("5x")]
    [InlineData("1.2.3s")]
    public void FromHeaders_ungueltige_Dauer_ergibt_null_fuer_das_Feld(string wert)
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen(
            (KopfLimit, "2000"),
            (KopfReset, wert),
            (KopfWarten, wert)));

        Assert.NotNull(info);
        Assert.Equal(2000, info.LimitRequests);
        Assert.Null(info.ResetRequests);
        Assert.Null(info.RetryAfter);
    }

    // Regel 4: Ist keine der vier Angaben vorhanden, kommt null zurück.
    [Fact]
    public void FromHeaders_ohne_Kopfzeilen_ergibt_null()
    {
        Assert.Null(RateLimitInfo.FromHeaders(Kopfzeilen()));
    }

    // Regel 4: Fremde Kopfzeilen zählen nicht.
    [Fact]
    public void FromHeaders_mit_nur_fremden_Kopfzeilen_ergibt_null()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen(
            ("content-type", "application/json"),
            ("x-ratelimit-limit-tokens", "5000"),
            ("x-request-id", "req_test")));

        Assert.Null(info);
    }

    // Regel 4: Sind alle vier vorhanden, aber keine auswertbar, kommt null zurück.
    [Fact]
    public void FromHeaders_mit_vier_unbrauchbaren_Angaben_ergibt_null()
    {
        var info = RateLimitInfo.FromHeaders(Kopfzeilen(
            (KopfLimit, "abc"),
            (KopfRest, "-5"),
            (KopfReset, "5x"),
            (KopfWarten, "1.2.3s")));

        Assert.Null(info);
    }
}
