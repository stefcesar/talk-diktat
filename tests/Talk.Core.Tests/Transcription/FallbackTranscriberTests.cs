// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Audio;
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft die Weiche ausschließlich gegen Regel 8 (Ziffern 1 bis 9 und den Zusatz) der reinen Logik in PLAYBOOK.md,
/// Abschnitt 15. Dazu dienen Testfassungen von ITranscriber (zählen Aufrufe, liefern ein Ergebnis oder werfen eine
/// vorgegebene Ausnahme, auch nacheinander verschiedene), eine eigene Uhr und eine eigene Wartefunktion, die die
/// verlangte Wartezeit aufzeichnet, die Uhr um diese Zeit vorstellt und sofort zurückkehrt.
/// Die Tests kennen die Umsetzung nicht. Regelnummer und Ziffer stehen jeweils im Kommentar über dem Test.
/// </summary>
public sealed class FallbackTranscriberTests
{
    private static readonly DateTimeOffset Anfang = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly TestUhr _uhr = new(Anfang);
    private readonly TestWarten _warten;
    private readonly AudioClip _clip = new(new short[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    private readonly TranscriptionRequest _anfrage = new("de", new[] { "Cesaretti" });

    public FallbackTranscriberTests()
    {
        _warten = new TestWarten(_uhr);
    }

    private static CancellationToken Abbruch => TestContext.Current.CancellationToken;

    /// <summary>Eigene Uhr: steht still, bis der Test sie vorstellt.</summary>
    private sealed class TestUhr
    {
        public TestUhr(DateTimeOffset jetzt)
        {
            Jetzt = jetzt;
        }

        public DateTimeOffset Jetzt { get; private set; }

        public DateTimeOffset Lesen() => Jetzt;

        public void Vor(TimeSpan dauer) => Jetzt += dauer;
    }

    /// <summary>
    /// Eigene Wartefunktion: zeichnet die verlangte Wartezeit auf, stellt die Uhr um diese Zeit vor und kehrt
    /// sofort zurück. Bei gesetztem Abbruchsignal verhält sie sich wie Task.Delay und meldet den Abbruch.
    /// </summary>
    private sealed class TestWarten
    {
        private readonly TestUhr _uhr;

        public TestWarten(TestUhr uhr)
        {
            _uhr = uhr;
        }

        public List<TimeSpan> Wartezeiten { get; } = new();

        /// <summary>Wird zu Beginn jedes Wartens gerufen, zum Beispiel um den Aufrufer abbrechen zu lassen.</summary>
        public Action? BeimWarten { get; set; }

        public Task Warten(TimeSpan dauer, CancellationToken ct)
        {
            Wartezeiten.Add(dauer);
            BeimWarten?.Invoke();
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled(ct);
            }

            _uhr.Vor(dauer);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Testfassung eines Erkenners: zählt Aufrufe, merkt sich die Eingaben und arbeitet eine Liste von Schritten ab
    /// (Ergebnis liefern, Ausnahme werfen oder eine eigene Handlung).
    /// </summary>
    private sealed class TestErkenner : ITranscriber
    {
        private readonly Queue<Func<CancellationToken, Task<TranscriptionResult>>> _schritte = new();

        public TestErkenner(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public int Aufrufe { get; private set; }

        public List<(AudioClip Clip, TranscriptionRequest Anfrage)> Eingaben { get; } = new();

        public TestErkenner Liefert(TranscriptionResult ergebnis)
        {
            _schritte.Enqueue(_ => Task.FromResult(ergebnis));
            return this;
        }

        public TestErkenner Wirft(Exception ausnahme)
        {
            _schritte.Enqueue(_ => Task.FromException<TranscriptionResult>(ausnahme));
            return this;
        }

        public TestErkenner Tut(Func<CancellationToken, Task<TranscriptionResult>> schritt)
        {
            _schritte.Enqueue(schritt);
            return this;
        }

        public async Task<TranscriptionResult> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken ct)
        {
            Aufrufe++;
            Eingaben.Add((clip, request));
            ct.ThrowIfCancellationRequested();
            if (_schritte.Count == 0)
            {
                throw new InvalidOperationException("Testfassung: Für diesen Aufruf ist kein Schritt vorgesehen.");
            }

            return await _schritte.Dequeue()(ct);
        }
    }

    private static TestErkenner Erster() => new("erster");

    private static TestErkenner Zweiter() => new("zweiter");

    private static TranscriptionResult Ergebnis(string text, string engine) =>
        new(
            text,
            "de",
            TimeSpan.FromMilliseconds(120),
            engine,
            new[] { new TranscriptionSegment(0, 1, text, -0.2, 0.01, 1.1) },
            new TranscriptionTimings(1, 2, 3, 6),
            new RateLimitInfo(2000, 1999, TimeSpan.FromSeconds(60), null),
            2);

    private static TranscriptionException Fehler(TranscriptionError grund, TimeSpan? wartezeit = null, string? kuerzel = null) =>
        new(grund, "Testfehler " + grund, null, wartezeit, kuerzel);

    private static TimeSpan Ms(int millisekunden) => TimeSpan.FromMilliseconds(millisekunden);

    private static TimeSpan Sek(int sekunden) => TimeSpan.FromSeconds(sekunden);

    private FallbackTranscriber Weiche(
        ITranscriber erster,
        ITranscriber? zweiter,
        Func<bool>? nurLokal = null,
        FallbackOptions? optionen = null) =>
        new(erster, zweiter, nurLokal ?? (() => false), optionen, _warten.Warten, _uhr.Lesen);

    private Task<TranscriptionResult> Erkennen(FallbackTranscriber weiche) =>
        weiche.TranscribeAsync(_clip, _anfrage, Abbruch);

    private Task<TranscriptionException> Scheitern(FallbackTranscriber weiche) =>
        Assert.ThrowsAsync<TranscriptionException>(() => weiche.TranscribeAsync(_clip, _anfrage, Abbruch));

    // ---------- Vertrag und Zusatz ----------

    // Regel 8 (Vertrag): Name ist "auto".
    [Fact]
    public void Name_ist_auto()
    {
        var weiche = Weiche(Erster(), Zweiter());

        Assert.Equal("auto", weiche.Name);
        Assert.Equal("auto", FallbackTranscriber.EngineName);
    }

    // Regel 8 (Vertrag): Vorgabewerte der Optionen sind 300 und 2000, Validate wirft dafür nicht.
    [Fact]
    public void Optionen_haben_die_Vorgabewerte()
    {
        var optionen = new FallbackOptions();

        Assert.Equal(300, optionen.RetryDelayMs);
        Assert.Equal(2000, optionen.MaxRetryAfterMs);
        optionen.Validate();
    }

    // Regel 8 (Zusatz): FallbackOptions.Validate wirft bei negativen Werten.
    [Theory]
    [InlineData(-1, 2000)]
    [InlineData(-300, 2000)]
    [InlineData(300, -1)]
    [InlineData(300, -2000)]
    [InlineData(-1, -1)]
    public void Optionen_Validate_wirft_bei_negativen_Werten(int wiederholung, int hoechstwartezeit)
    {
        var optionen = new FallbackOptions(wiederholung, hoechstwartezeit);

        Assert.ThrowsAny<ArgumentException>(() => optionen.Validate());
    }

    // Regel 8 (Zusatz): Null ist nicht negativ, Validate wirft nicht.
    [Fact]
    public void Optionen_Validate_nimmt_null_als_Wert()
    {
        new FallbackOptions(0, 0).Validate();
    }

    // Regel 8 (Zusatz): Der Konstruktor wirft bei null für den ersten Erkenner.
    [Fact]
    public void Konstruktor_wirft_bei_null_fuer_den_ersten_Erkenner()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FallbackTranscriber(null!, Zweiter(), () => false));
    }

    // Regel 8 (Zusatz): Der Konstruktor wirft bei null für die Funktion "nur lokal".
    [Fact]
    public void Konstruktor_wirft_bei_null_fuer_die_Funktion_nur_lokal()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FallbackTranscriber(Erster(), Zweiter(), null!));
    }

    // Regel 8 (Vertrag): Der zweite Erkenner, die Optionen, die Wartefunktion und die Uhr dürfen fehlen.
    [Fact]
    public async Task Konstruktor_kommt_ohne_die_wahlfreien_Angaben_aus()
    {
        var erster = Erster().Liefert(Ergebnis("hallo", "groq"));
        var weiche = new FallbackTranscriber(erster, null, () => false);

        var ergebnis = await weiche.TranscribeAsync(_clip, _anfrage, Abbruch);

        Assert.Equal("hallo", ergebnis.Text);
        Assert.Null(weiche.PrimaryBlockedUntil);
    }

    // ---------- Ziffer 1 ----------

    // Regel 8 (1): Nur lokal: nur der zweite Erkenner läuft, der erste wird nicht gerufen.
    [Fact]
    public async Task Z1_Nur_lokal_laeuft_nur_der_zweite()
    {
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var lokal = Ergebnis("vom zweiten", "lokal");
        var zweiter = Zweiter().Liefert(lokal);
        var weiche = Weiche(erster, zweiter, () => true);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal("lokal", ergebnis.Engine);
        Assert.Null(ergebnis.FallbackReason);
        Assert.Equal(0, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
        Assert.Same(_clip, zweiter.Eingaben[0].Clip);
        Assert.Equal(_anfrage, zweiter.Eingaben[0].Anfrage);
    }

    // Regel 8 (1): Nur lokal: Fehler des zweiten gehen unverändert weiter.
    [Theory]
    [InlineData(TranscriptionError.ModelMissing)]
    [InlineData(TranscriptionError.EngineFailed)]
    [InlineData(TranscriptionError.Timeout)]
    public async Task Z1_Nur_lokal_gehen_Fehler_des_zweiten_unveraendert_weiter(TranscriptionError grund)
    {
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var fehler = Fehler(grund);
        var zweiter = Zweiter().Wirft(fehler);
        var weiche = Weiche(erster, zweiter, () => true);

        var ausnahme = await Scheitern(weiche);

        Assert.Same(fehler, ausnahme);
        Assert.Equal(0, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (1): Nur lokal ohne zweiten Erkenner ergibt LocalUnavailable; der erste wird nicht gerufen.
    [Fact]
    public async Task Z1_Nur_lokal_ohne_zweiten_ergibt_LocalUnavailable()
    {
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var weiche = Weiche(erster, null, () => true);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.LocalUnavailable, ausnahme.Error);
        Assert.Equal(0, erster.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (1): Die Einstellung "nur lokal" wird bei jedem Aufruf neu gelesen.
    [Fact]
    public async Task Z1_Nur_lokal_wird_je_Aufruf_neu_gelesen()
    {
        var nurLokal = true;
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal")).Liefert(Ergebnis("noch einmal lokal", "lokal"));
        var weiche = Weiche(erster, zweiter, () => nurLokal);

        var lokalEins = await Erkennen(weiche);
        nurLokal = false;
        var ueberDenErsten = await Erkennen(weiche);
        nurLokal = true;
        var lokalZwei = await Erkennen(weiche);

        Assert.Equal("vom zweiten", lokalEins.Text);
        Assert.Equal("vom ersten", ueberDenErsten.Text);
        Assert.Equal("noch einmal lokal", lokalZwei.Text);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(2, zweiter.Aufrufe);
    }

    // ---------- Ziffer 2 ----------

    // Regel 8 (2): Erfolg des ersten geht unverändert zurück; der zweite wird nicht gerufen, es wird nicht gewartet.
    [Fact]
    public async Task Z2_Erfolg_des_ersten_geht_unveraendert_zurueck()
    {
        var vomErsten = Ergebnis("vom ersten", "groq");
        var erster = Erster().Liefert(vomErsten);
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(vomErsten, ergebnis);
        Assert.Equal("groq", ergebnis.Engine);
        Assert.Null(ergebnis.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(0, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
        Assert.Null(weiche.PrimaryBlockedUntil);
    }

    // Regel 8 (2): Clip und Anfrage gehen unverändert an den ersten Erkenner.
    [Fact]
    public async Task Z2_Clip_und_Anfrage_gehen_an_den_ersten()
    {
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var weiche = Weiche(erster, Zweiter());

        await Erkennen(weiche);

        var eingabe = Assert.Single(erster.Eingaben);
        Assert.Same(_clip, eingabe.Clip);
        Assert.Equal(_anfrage, eingabe.Anfrage);
    }

    // ---------- Ziffer 3 ----------

    // Regel 8 (3): Genau eine Wiederholung bei Network und ServerError nach RetryDelayMs (Vorgabe 300 ms).
    [Theory]
    [InlineData(TranscriptionError.Network)]
    [InlineData(TranscriptionError.ServerError)]
    public async Task Z3_Eine_Wiederholung_nach_RetryDelayMs_dann_Erfolg(TranscriptionError grund)
    {
        var vomErsten = Ergebnis("zweiter Versuch", "groq");
        var erster = Erster().Wirft(Fehler(grund)).Liefert(vomErsten);
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(vomErsten, ergebnis);
        Assert.Null(ergebnis.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(0, zweiter.Aufrufe);
        Assert.Equal(new[] { Ms(300) }, _warten.Wartezeiten);
    }

    // Regel 8 (3) und Zusatz: Die Wartefunktion bekommt genau RetryDelayMs aus den Optionen.
    [Theory]
    [InlineData(TranscriptionError.Network, 750)]
    [InlineData(TranscriptionError.ServerError, 50)]
    [InlineData(TranscriptionError.Network, 1)]
    public async Task Z3_Wiederholung_wartet_genau_RetryDelayMs_aus_den_Optionen(TranscriptionError grund, int wartezeitMs)
    {
        var erster = Erster().Wirft(Fehler(grund)).Liefert(Ergebnis("zweiter Versuch", "groq"));
        var weiche = Weiche(erster, Zweiter(), optionen: new FallbackOptions(RetryDelayMs: wartezeitMs));

        await Erkennen(weiche);

        Assert.Equal(new[] { Ms(wartezeitMs) }, _warten.Wartezeiten);
        Assert.Equal(2, erster.Aufrufe);
    }

    // Regel 8 (3): Es bleibt bei genau einer Wiederholung, danach läuft der zweite.
    [Theory]
    [InlineData(TranscriptionError.Network)]
    [InlineData(TranscriptionError.ServerError)]
    public async Task Z3_Nach_einer_Wiederholung_ist_Schluss(TranscriptionError grund)
    {
        var erster = Erster()
            .Wirft(Fehler(grund))
            .Wirft(Fehler(grund))
            .Liefert(Ergebnis("dritter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(grund, ergebnis.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Equal(new[] { Ms(300) }, _warten.Wartezeiten);
    }

    // Regel 8 (3): Auch wenn der zweite Versuch mit einem anderen wiederholbaren Fehler endet, gibt es keinen dritten.
    [Fact]
    public async Task Z3_Kein_dritter_Versuch_bei_wechselndem_Fehler()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.Network))
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(1)))
            .Liefert(Ergebnis("dritter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(TranscriptionError.RateLimited, ergebnis.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(new[] { Ms(300) }, _warten.Wartezeiten);
    }

    // Regel 8 (3) und Zusatz: Bei RateLimited mit genannter Wartezeit bis höchstens MaxRetryAfterMs
    // wird nach genau dieser Wartezeit wiederholt.
    [Theory]
    [InlineData(1)]
    [InlineData(700)]
    [InlineData(1500)]
    [InlineData(2000)]
    public async Task Z3_RateLimited_mit_kurzer_Wartezeit_wiederholt_nach_genau_dieser_Zeit(int wartezeitMs)
    {
        var vomErsten = Ergebnis("zweiter Versuch", "groq");
        var erster = Erster().Wirft(Fehler(TranscriptionError.RateLimited, Ms(wartezeitMs))).Liefert(vomErsten);
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(vomErsten, ergebnis);
        Assert.Null(ergebnis.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(0, zweiter.Aufrufe);
        Assert.Equal(new[] { Ms(wartezeitMs) }, _warten.Wartezeiten);
    }

    // Regel 8 (3): Die Obergrenze kommt aus den Optionen (MaxRetryAfterMs).
    [Fact]
    public async Task Z3_RateLimited_Obergrenze_kommt_aus_den_Optionen()
    {
        var optionen = new FallbackOptions(RetryDelayMs: 300, MaxRetryAfterMs: 5000);
        var erster = Erster().Wirft(Fehler(TranscriptionError.RateLimited, Ms(4500))).Liefert(Ergebnis("zweiter Versuch", "groq"));
        var weiche = Weiche(erster, Zweiter(), optionen: optionen);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("zweiter Versuch", ergebnis.Text);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(new[] { Ms(4500) }, _warten.Wartezeiten);
    }

    // Regel 8 (3): RateLimited mit Wartezeit über MaxRetryAfterMs wird nicht wiederholt.
    [Theory]
    [InlineData(2001)]
    [InlineData(5000)]
    [InlineData(60000)]
    public async Task Z3_RateLimited_mit_langer_Wartezeit_wird_nicht_wiederholt(int wartezeitMs)
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Ms(wartezeitMs)))
            .Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(TranscriptionError.RateLimited, ergebnis.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (3): RateLimited ohne genannte Wartezeit wird nicht wiederholt.
    [Fact]
    public async Task Z3_RateLimited_ohne_Wartezeit_wird_nicht_wiederholt()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited))
            .Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(TranscriptionError.RateLimited, ergebnis.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (3): Andere Fehler ohne Wiederholung.
    [Theory]
    [InlineData(TranscriptionError.NoKey)]
    [InlineData(TranscriptionError.Unauthorized)]
    [InlineData(TranscriptionError.Timeout)]
    [InlineData(TranscriptionError.TooLarge)]
    [InlineData(TranscriptionError.BadRequest)]
    [InlineData(TranscriptionError.BadResponse)]
    [InlineData(TranscriptionError.ModelMissing)]
    [InlineData(TranscriptionError.LocalUnavailable)]
    [InlineData(TranscriptionError.EngineFailed)]
    public async Task Z3_Andere_Fehler_werden_nicht_wiederholt(TranscriptionError grund)
    {
        var erster = Erster().Wirft(Fehler(grund)).Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(grund, ergebnis.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // ---------- Ziffer 4 ----------

    // Regel 8 (4): Scheitert der erste endgültig, läuft der zweite; das Ergebnis trägt als FallbackReason den Fehler
    // des ersten und als Engine den Namen aus dem Ergebnis des zweiten (nicht den Namen des Erkenners).
    [Fact]
    public async Task Z4_Ergebnis_des_zweiten_traegt_Grund_und_Engine()
    {
        var vomZweiten = Ergebnis("vom zweiten", "lokal");
        var erster = Erster().Wirft(Fehler(TranscriptionError.Unauthorized));
        var zweiter = Zweiter().Liefert(vomZweiten);
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.Unauthorized, ergebnis.FallbackReason);
        Assert.Equal("lokal", ergebnis.Engine);
        Assert.NotEqual(zweiter.Name, ergebnis.Engine);
        Assert.NotEqual("auto", ergebnis.Engine);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // Regel 8 (4): Es ist das Ergebnis des zweiten: Text, Sprache und Abschnitte bleiben erhalten.
    // (Zeiten und Limit Angaben nennt die Regel nicht, sie werden hier bewusst nicht festgelegt.)
    [Fact]
    public async Task Z4_Text_Sprache_und_Abschnitte_des_zweiten_bleiben_erhalten()
    {
        var vomZweiten = Ergebnis("vom zweiten", "lokal");
        var erster = Erster().Wirft(Fehler(TranscriptionError.NoKey));
        var zweiter = Zweiter().Liefert(vomZweiten);
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.NoKey, ergebnis.FallbackReason);
        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal("de", ergebnis.DetectedLanguage);
        Assert.Equal(vomZweiten.Segments, ergebnis.Segments);
        Assert.Equal(2, ergebnis.FilteredSegments);
    }

    // Regel 8 (4): Clip und Anfrage gehen unverändert an den zweiten Erkenner.
    [Fact]
    public async Task Z4_Clip_und_Anfrage_gehen_an_den_zweiten()
    {
        var erster = Erster().Wirft(Fehler(TranscriptionError.BadRequest));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Erkennen(weiche);

        var eingabe = Assert.Single(zweiter.Eingaben);
        Assert.Same(_clip, eingabe.Clip);
        Assert.Equal(_anfrage, eingabe.Anfrage);
    }

    // Regel 8 (4): Der Grund ist der letzte Fehler des ersten (nach der Wiederholung).
    [Fact]
    public async Task Z4_Grund_ist_der_letzte_Fehler_des_ersten()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.Network))
            .Wirft(Fehler(TranscriptionError.ServerError));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.ServerError, ergebnis.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // Regel 8 (4): Nach einem Wechsel ohne Sperre wird beim nächsten Aufruf wieder zuerst der erste gerufen.
    [Fact]
    public async Task Z4_Naechster_Aufruf_beginnt_wieder_beim_ersten()
    {
        var erster = Erster().Wirft(Fehler(TranscriptionError.Unauthorized)).Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var mitWechsel = await Erkennen(weiche);
        var ohneWechsel = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.Unauthorized, mitWechsel.FallbackReason);
        Assert.Null(weiche.PrimaryBlockedUntil);
        Assert.Equal("vom ersten", ohneWechsel.Text);
        Assert.Null(ohneWechsel.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // ---------- Ziffer 5 ----------

    // Regel 8 (5): Am Anfang gibt es keine Sperre.
    [Fact]
    public void Z5_Am_Anfang_keine_Sperre()
    {
        Assert.Null(Weiche(Erster(), Zweiter()).PrimaryBlockedUntil);
    }

    // Regel 8 (5): Endet der erste mit RateLimited und genannter Wartezeit, bleibt er bis zum Ablauf gesperrt.
    [Fact]
    public async Task Z5_RateLimited_mit_Wartezeit_sperrt_bis_zum_Ablauf()
    {
        var erster = Erster().Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.RateLimited, ergebnis.FallbackReason);
        Assert.Equal(Anfang + Sek(60), weiche.PrimaryBlockedUntil);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (5): Solange die Sperre gilt, läuft sofort der zweite mit Grund RateLimited; der erste wird nicht gerufen.
    [Fact]
    public async Task Z5_Waehrend_der_Sperre_laeuft_sofort_der_zweite()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)))
            .Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter()
            .Liefert(Ergebnis("lokal eins", "lokal"))
            .Liefert(Ergebnis("lokal zwei", "lokal"))
            .Liefert(Ergebnis("lokal drei", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Erkennen(weiche);
        _uhr.Vor(Sek(1));
        var kurzDanach = await Erkennen(weiche);
        _uhr.Vor(Sek(58));
        var kurzVorAblauf = await Erkennen(weiche);

        Assert.Equal("lokal zwei", kurzDanach.Text);
        Assert.Equal("lokal", kurzDanach.Engine);
        Assert.Equal(TranscriptionError.RateLimited, kurzDanach.FallbackReason);
        Assert.Equal("lokal drei", kurzVorAblauf.Text);
        Assert.Equal(TranscriptionError.RateLimited, kurzVorAblauf.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(3, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (5): Nach Ablauf der Sperre wird der erste wieder gerufen.
    [Fact]
    public async Task Z5_Nach_Ablauf_wird_der_erste_wieder_gerufen()
    {
        var vomErsten = Ergebnis("vom ersten", "groq");
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)))
            .Liefert(vomErsten);
        var zweiter = Zweiter()
            .Liefert(Ergebnis("lokal eins", "lokal"))
            .Liefert(Ergebnis("lokal zwei", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Erkennen(weiche);
        _uhr.Vor(Sek(30));
        var gesperrt = await Erkennen(weiche);
        _uhr.Vor(Sek(31));
        var wiederFrei = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.RateLimited, gesperrt.FallbackReason);
        Assert.Equal(vomErsten, wiederFrei);
        Assert.Null(wiederFrei.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(2, zweiter.Aufrufe);
    }

    // Regel 8 (5): Auch nach der einen Wiederholung sperrt ein RateLimited mit genannter Wartezeit.
    [Fact]
    public async Task Z5_RateLimited_nach_Wiederholung_sperrt_ebenfalls()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(1)))
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(90)))
            .Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter()
            .Liefert(Ergebnis("lokal eins", "lokal"))
            .Liefert(Ergebnis("lokal zwei", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);
        _uhr.Vor(Sek(10));
        var gesperrt = await Erkennen(weiche);

        Assert.Equal(TranscriptionError.RateLimited, ergebnis.FallbackReason);
        Assert.Equal(new[] { Sek(1) }, _warten.Wartezeiten);
        Assert.NotNull(weiche.PrimaryBlockedUntil);
        Assert.Equal("lokal zwei", gesperrt.Text);
        Assert.Equal(TranscriptionError.RateLimited, gesperrt.FallbackReason);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(2, zweiter.Aufrufe);
    }

    // Regel 8 (5): RateLimited ohne genannte Wartezeit sperrt nicht.
    [Fact]
    public async Task Z5_RateLimited_ohne_Wartezeit_sperrt_nicht()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited))
            .Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Erkennen(weiche);
        var danach = await Erkennen(weiche);

        Assert.Null(weiche.PrimaryBlockedUntil);
        Assert.Equal("vom ersten", danach.Text);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // Regel 8 (5): Andere Fehler sperren nicht.
    [Theory]
    [InlineData(TranscriptionError.Unauthorized)]
    [InlineData(TranscriptionError.Network)]
    [InlineData(TranscriptionError.ServerError)]
    [InlineData(TranscriptionError.Timeout)]
    public async Task Z5_Andere_Fehler_sperren_nicht(TranscriptionError grund)
    {
        var erster = Erster()
            .Wirft(Fehler(grund, Sek(60)))
            .Wirft(Fehler(grund, Sek(60)));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal(grund, ergebnis.FallbackReason);
        Assert.Null(weiche.PrimaryBlockedUntil);
    }

    // Regel 8 (5): Nach einer erfolgreichen Wiederholung wegen RateLimited ist der erste beim nächsten Diktat
    // wieder an der Reihe (die genannte Wartezeit ist dann abgelaufen).
    [Fact]
    public async Task Z5_Nach_erfolgreicher_Wiederholung_ist_der_erste_wieder_an_der_Reihe()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Ms(1500)))
            .Liefert(Ergebnis("zweiter Versuch", "groq"))
            .Liefert(Ergebnis("naechstes Diktat", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var erstes = await Erkennen(weiche);
        _uhr.Vor(Sek(10));
        var zweites = await Erkennen(weiche);

        Assert.Equal("zweiter Versuch", erstes.Text);
        Assert.Equal("naechstes Diktat", zweites.Text);
        Assert.Null(zweites.FallbackReason);
        Assert.Equal(3, erster.Aufrufe);
        Assert.Equal(0, zweiter.Aufrufe);
    }

    // ---------- Ziffer 6 ----------

    // Regel 8 (6): Ohne zweiten Erkenner geht der letzte Fehler des ersten weiter.
    [Theory]
    [InlineData(TranscriptionError.NoKey)]
    [InlineData(TranscriptionError.Unauthorized)]
    [InlineData(TranscriptionError.TooLarge)]
    [InlineData(TranscriptionError.BadResponse)]
    public async Task Z6_Ohne_zweiten_geht_der_Fehler_des_ersten_weiter(TranscriptionError grund)
    {
        var erster = Erster().Wirft(Fehler(grund, kuerzel: "test_code"));
        var weiche = Weiche(erster, null);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(grund, ausnahme.Error);
        Assert.Equal("test_code", ausnahme.ServerCode);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (6): Ohne zweiten Erkenner geht nach der Wiederholung der letzte Fehler weiter.
    [Fact]
    public async Task Z6_Ohne_zweiten_geht_der_letzte_Fehler_nach_der_Wiederholung_weiter()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.Network))
            .Wirft(Fehler(TranscriptionError.ServerError));
        var weiche = Weiche(erster, null);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.ServerError, ausnahme.Error);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(new[] { Ms(300) }, _warten.Wartezeiten);
    }

    // Regel 8 (6): Ohne zweiten Erkenner geht RateLimited mit der genannten Wartezeit weiter und sperrt.
    [Fact]
    public async Task Z6_Ohne_zweiten_geht_RateLimited_weiter_und_sperrt()
    {
        var erster = Erster().Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)));
        var weiche = Weiche(erster, null);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.RateLimited, ausnahme.Error);
        Assert.Equal(Sek(60), ausnahme.RetryAfter);
        Assert.Equal(Anfang + Sek(60), weiche.PrimaryBlockedUntil);
    }

    // Regel 8 (6): Während der Sperre kommt ohne zweiten Erkenner RateLimited mit der Restwartezeit;
    // der erste wird nicht gerufen.
    [Fact]
    public async Task Z6_Waehrend_der_Sperre_ohne_zweiten_kommt_RateLimited_mit_Restwartezeit()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)))
            .Liefert(Ergebnis("vom ersten", "groq"));
        var weiche = Weiche(erster, null);

        await Scheitern(weiche);
        _uhr.Vor(Sek(20));
        var nach20 = await Scheitern(weiche);
        _uhr.Vor(Sek(25));
        var nach45 = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.RateLimited, nach20.Error);
        Assert.Equal(Sek(40), nach20.RetryAfter);
        Assert.Equal(TranscriptionError.RateLimited, nach45.Error);
        Assert.Equal(Sek(15), nach45.RetryAfter);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (6) mit (5): Nach Ablauf der Sperre wird der erste auch ohne zweiten Erkenner wieder gerufen.
    [Fact]
    public async Task Z6_Nach_Ablauf_der_Sperre_wird_der_erste_wieder_gerufen()
    {
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)))
            .Liefert(Ergebnis("vom ersten", "groq"));
        var weiche = Weiche(erster, null);

        await Scheitern(weiche);
        _uhr.Vor(Sek(61));
        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom ersten", ergebnis.Text);
        Assert.Equal(2, erster.Aufrufe);
    }

    // ---------- Ziffer 7 ----------

    // Regel 8 (7): Scheitern beide, geht der Fehler des ersten weiter, der des zweiten hängt als innere Ausnahme an.
    [Fact]
    public async Task Z7_Scheitern_beide_geht_der_Fehler_des_ersten_weiter()
    {
        var fehlerDesZweiten = Fehler(TranscriptionError.ModelMissing);
        var erster = Erster().Wirft(Fehler(TranscriptionError.Unauthorized, kuerzel: "invalid_api_key"));
        var zweiter = Zweiter().Wirft(fehlerDesZweiten);
        var weiche = Weiche(erster, zweiter);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.Unauthorized, ausnahme.Error);
        Assert.Same(fehlerDesZweiten, ausnahme.InnerException);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // Regel 8 (7): Das gilt auch nach der Wiederholung: der letzte Fehler des ersten geht weiter.
    [Fact]
    public async Task Z7_Scheitern_beide_nach_Wiederholung()
    {
        var fehlerDesZweiten = Fehler(TranscriptionError.EngineFailed);
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.ServerError))
            .Wirft(Fehler(TranscriptionError.Network));
        var zweiter = Zweiter().Wirft(fehlerDesZweiten);
        var weiche = Weiche(erster, zweiter);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.Network, ausnahme.Error);
        Assert.Same(fehlerDesZweiten, ausnahme.InnerException);
        Assert.Equal(2, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Equal(new[] { Ms(300) }, _warten.Wartezeiten);
    }

    // Regel 8 (7) mit (5): Scheitern beide während der Sperre, ist der Fehler des ersten RateLimited.
    [Fact]
    public async Task Z7_Scheitern_beide_waehrend_der_Sperre()
    {
        var erster = Erster().Wirft(Fehler(TranscriptionError.RateLimited, Sek(60)));
        var fehlerDesZweiten = Fehler(TranscriptionError.ModelMissing);
        var zweiter = Zweiter().Liefert(Ergebnis("lokal eins", "lokal")).Wirft(fehlerDesZweiten);
        var weiche = Weiche(erster, zweiter);

        await Erkennen(weiche);
        _uhr.Vor(Sek(5));
        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.RateLimited, ausnahme.Error);
        Assert.Same(fehlerDesZweiten, ausnahme.InnerException);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(2, zweiter.Aufrufe);
    }

    // ---------- Ziffer 8 ----------

    // Regel 8 (8): Andere Ausnahmen des ersten zählen als EngineFailed (ohne Wiederholung), der zweite läuft.
    [Fact]
    public async Task Z8_Andere_Ausnahme_des_ersten_zaehlt_als_EngineFailed()
    {
        var erster = Erster()
            .Wirft(new InvalidOperationException("unerwartet"))
            .Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(TranscriptionError.EngineFailed, ergebnis.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (8): Ohne zweiten Erkenner kommt die andere Ausnahme als TranscriptionException mit EngineFailed.
    [Fact]
    public async Task Z8_Andere_Ausnahme_des_ersten_ohne_zweiten_wird_EngineFailed()
    {
        var erster = Erster().Wirft(new InvalidOperationException("unerwartet"));
        var weiche = Weiche(erster, null);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.EngineFailed, ausnahme.Error);
        Assert.Equal(1, erster.Aufrufe);
    }

    // Regel 8 (8): Ein interner Abbruch des ersten ohne Abbruch des Aufrufers zählt als Timeout (ohne Wiederholung).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Z8_Interner_Abbruch_des_ersten_zaehlt_als_Timeout(bool alsTaskCanceled)
    {
        Exception abbruch = alsTaskCanceled ? new TaskCanceledException() : new OperationCanceledException();
        var erster = Erster().Wirft(abbruch).Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        var ergebnis = await Erkennen(weiche);

        Assert.Equal("vom zweiten", ergebnis.Text);
        Assert.Equal(TranscriptionError.Timeout, ergebnis.FallbackReason);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (8): Ohne zweiten Erkenner kommt der interne Abbruch als TranscriptionException mit Timeout,
    // nicht als Abbruch.
    [Fact]
    public async Task Z8_Interner_Abbruch_des_ersten_ohne_zweiten_wird_Timeout()
    {
        var erster = Erster().Wirft(new OperationCanceledException());
        var weiche = Weiche(erster, null);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.Timeout, ausnahme.Error);
        Assert.Equal(1, erster.Aufrufe);
    }

    // Regel 8 (8) mit (7): Wirft der zweite eine andere Ausnahme, geht der Fehler des ersten weiter
    // und der Fehler des zweiten hängt als innere Ausnahme an.
    [Fact]
    public async Task Z8_Andere_Ausnahme_des_zweiten_haengt_am_Fehler_des_ersten()
    {
        var erster = Erster().Wirft(Fehler(TranscriptionError.NoKey));
        var zweiter = Zweiter().Wirft(new InvalidOperationException("unerwartet"));
        var weiche = Weiche(erster, zweiter);

        var ausnahme = await Scheitern(weiche);

        Assert.Equal(TranscriptionError.NoKey, ausnahme.Error);
        Assert.NotNull(ausnahme.InnerException);
        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // ---------- Ziffer 9 ----------

    // Regel 8 (9): Abbruch durch den Aufrufer während der erste läuft geht sofort weiter,
    // ohne Wiederholung und ohne Wechsel.
    [Fact]
    public async Task Z9_Abbruch_waehrend_der_erste_laeuft()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        var erster = Erster()
            .Tut(_ =>
            {
                quelle.Cancel();
                return Task.FromCanceled<TranscriptionResult>(quelle.Token);
            })
            .Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => weiche.TranscribeAsync(_clip, _anfrage, quelle.Token));

        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(0, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (9): Abbruch durch den Aufrufer während des Wartens vor der Wiederholung geht sofort weiter.
    [Fact]
    public async Task Z9_Abbruch_waehrend_des_Wartens()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        _warten.BeimWarten = quelle.Cancel;
        var erster = Erster()
            .Wirft(Fehler(TranscriptionError.Network))
            .Liefert(Ergebnis("zweiter Versuch", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => weiche.TranscribeAsync(_clip, _anfrage, quelle.Token));

        Assert.Equal(0, zweiter.Aufrufe);
        Assert.Equal(new[] { Ms(300) }, _warten.Wartezeiten);
    }

    // Regel 8 (9): Abbruch durch den Aufrufer während der zweite läuft bleibt ein Abbruch.
    [Fact]
    public async Task Z9_Abbruch_waehrend_der_zweite_laeuft()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        var erster = Erster().Wirft(Fehler(TranscriptionError.Unauthorized));
        var zweiter = Zweiter().Tut(_ =>
        {
            quelle.Cancel();
            return Task.FromCanceled<TranscriptionResult>(quelle.Token);
        });
        var weiche = Weiche(erster, zweiter);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => weiche.TranscribeAsync(_clip, _anfrage, quelle.Token));

        Assert.Equal(1, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // Regel 8 (9): Abbruch durch den Aufrufer im Modus nur lokal bleibt ein Abbruch.
    [Fact]
    public async Task Z9_Abbruch_im_Modus_nur_lokal()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter().Tut(_ =>
        {
            quelle.Cancel();
            return Task.FromCanceled<TranscriptionResult>(quelle.Token);
        });
        var weiche = Weiche(erster, zweiter, () => true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => weiche.TranscribeAsync(_clip, _anfrage, quelle.Token));

        Assert.Equal(0, erster.Aufrufe);
        Assert.Equal(1, zweiter.Aufrufe);
    }

    // Regel 8 (9): Ist das Signal schon vor dem Aufruf gesetzt, kommt ein Abbruch ohne Wechsel und ohne Warten.
    [Fact]
    public async Task Z9_Vorab_abgebrochen()
    {
        using var quelle = new CancellationTokenSource();
        quelle.Cancel();
        var erster = Erster().Liefert(Ergebnis("vom ersten", "groq"));
        var zweiter = Zweiter().Liefert(Ergebnis("vom zweiten", "lokal"));
        var weiche = Weiche(erster, zweiter);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => weiche.TranscribeAsync(_clip, _anfrage, quelle.Token));

        Assert.Equal(0, zweiter.Aufrufe);
        Assert.Empty(_warten.Wartezeiten);
    }

    // Regel 8 (9): Ein Abbruch durch den Aufrufer setzt keine Sperre und hinterlässt die Weiche benutzbar.
    [Fact]
    public async Task Z9_Nach_Abbruch_bleibt_die_Weiche_benutzbar()
    {
        using var quelle = CancellationTokenSource.CreateLinkedTokenSource(Abbruch);
        var erster = Erster()
            .Tut(_ =>
            {
                quelle.Cancel();
                return Task.FromCanceled<TranscriptionResult>(quelle.Token);
            })
            .Liefert(Ergebnis("naechstes Diktat", "groq"));
        var weiche = Weiche(erster, Zweiter());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => weiche.TranscribeAsync(_clip, _anfrage, quelle.Token));
        var ergebnis = await Erkennen(weiche);

        Assert.Null(weiche.PrimaryBlockedUntil);
        Assert.Equal("naechstes Diktat", ergebnis.Text);
        Assert.Equal(2, erster.Aufrufe);
    }
}
