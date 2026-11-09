using Talk.Core.Insertion;
using Xunit;

namespace Talk.Core.Tests.Insertion;

/// <summary>
/// Prüft die Regeln für den Weg des Texts ausschließlich gegen PLAYBOOK.md, Abschnitt 17,
/// "Regeln für den Weg, in dieser Reihenfolge", und die festen Wörter aus dem Abschnitt "Protokoll".
/// Die Tests kennen die Umsetzung nicht. Die Reihenfolge der Regeln ist Teil der Regel und wird mit Fällen geprüft,
/// in denen mehrere Bedingungen zugleich zutreffen, dazu mit einer ausgeschriebenen Tabelle über alle 64 Kombinationen.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class InsertPlannerTests
{
    private const bool J = true;
    private const bool N = false;

    private static readonly InsertPlan KeinZiel = new(InsertResult.HistoryOnly, InsertReason.NoTarget);
    private static readonly InsertPlan FensterWeg = new(InsertResult.HistoryOnly, InsertReason.WindowGone);
    private static readonly InsertPlan HoehereRechte = new(InsertResult.Blocked, InsertReason.HigherRights);
    private static readonly InsertPlan Passwortfeld = new(InsertResult.Blocked, InsertReason.PasswordField);
    private static readonly InsertPlan Tippen = new(InsertResult.Typed, InsertReason.None);
    private static readonly InsertPlan Zwischenablage = new(InsertResult.Inserted, InsertReason.None);

    /// <summary>
    /// Ein gewöhnliches Ziel: vorhanden, fremdes Fenster, noch da, gleiche Rechte, kein Passwortfeld, kein Terminal.
    /// </summary>
    private static InsertTargetFacts Fakten(
        bool ziel = true,
        bool eigenesFenster = false,
        bool vorhanden = true,
        bool hoehereRechte = false,
        bool passwortfeld = false,
        bool terminal = false) =>
        new(ziel, eigenesFenster, vorhanden, hoehereRechte, passwortfeld, terminal);

    // ---------- Jede Regel für sich ----------

    // Regel 1: Kein Ziel ergibt nur Verlauf mit Grund kein_ziel.
    [Fact]
    public void Regel_1_kein_Ziel_ergibt_nur_Verlauf()
    {
        var plan = InsertPlanner.Decide(Fakten(ziel: false));

        Assert.Equal(InsertResult.HistoryOnly, plan.Way);
        Assert.Equal(InsertReason.NoTarget, plan.Reason);
    }

    // Regel 1: Ein Fenster von Talk selbst ergibt nur Verlauf mit Grund kein_ziel.
    [Fact]
    public void Regel_1_eigenes_Fenster_ergibt_nur_Verlauf()
    {
        var plan = InsertPlanner.Decide(Fakten(eigenesFenster: true));

        Assert.Equal(InsertResult.HistoryOnly, plan.Way);
        Assert.Equal(InsertReason.NoTarget, plan.Reason);
    }

    // Regel 2: Das Zielfenster gibt es nicht mehr, also nur Verlauf mit Grund fenster_weg.
    [Fact]
    public void Regel_2_Fenster_weg_ergibt_nur_Verlauf()
    {
        var plan = InsertPlanner.Decide(Fakten(vorhanden: false));

        Assert.Equal(InsertResult.HistoryOnly, plan.Way);
        Assert.Equal(InsertReason.WindowGone, plan.Reason);
    }

    // Regel 3: Höhere Rechte (oder Rechte nicht lesbar) ergibt gesperrt mit Grund hoehere_rechte.
    [Fact]
    public void Regel_3_hoehere_Rechte_ergibt_gesperrt()
    {
        var plan = InsertPlanner.Decide(Fakten(hoehereRechte: true));

        Assert.Equal(InsertResult.Blocked, plan.Way);
        Assert.Equal(InsertReason.HigherRights, plan.Reason);
    }

    // Regel 4: Ein Passwortfeld ergibt gesperrt mit Grund passwortfeld.
    [Fact]
    public void Regel_4_Passwortfeld_ergibt_gesperrt()
    {
        var plan = InsertPlanner.Decide(Fakten(passwortfeld: true));

        Assert.Equal(InsertResult.Blocked, plan.Way);
        Assert.Equal(InsertReason.PasswordField, plan.Reason);
    }

    // Regel 5: Ein Terminal bekommt den Text getippt, ohne besonderen Grund.
    [Fact]
    public void Regel_5_Terminal_ergibt_tippen()
    {
        var plan = InsertPlanner.Decide(Fakten(terminal: true));

        Assert.Equal(InsertResult.Typed, plan.Way);
        Assert.Equal(InsertReason.None, plan.Reason);
    }

    // Regel 6: Sonst geht der Text über die Zwischenablage, ohne besonderen Grund.
    [Fact]
    public void Regel_6_sonst_Zwischenablage()
    {
        var plan = InsertPlanner.Decide(Fakten());

        Assert.Equal(InsertResult.Inserted, plan.Way);
        Assert.Equal(InsertReason.None, plan.Reason);
    }

    // ---------- Reihenfolge: mehrere Bedingungen zugleich ----------

    // Regel 1 vor allen anderen: Ohne Ziel zählt nichts von dem, was sonst noch gemeldet wird.
    [Fact]
    public void Kein_Ziel_geht_vor_Fenster_weg_Rechten_Passwortfeld_und_Terminal()
    {
        var plan = InsertPlanner.Decide(Fakten(
            ziel: false, vorhanden: false, hoehereRechte: true, passwortfeld: true, terminal: true));

        Assert.Equal(KeinZiel, plan);
    }

    // Regel 1 vor allen anderen: In ein Fenster von Talk selbst wird nie geschrieben, auch wenn es wie ein Terminal aussieht.
    [Fact]
    public void Eigenes_Fenster_geht_vor_Fenster_weg_Rechten_Passwortfeld_und_Terminal()
    {
        var plan = InsertPlanner.Decide(Fakten(
            eigenesFenster: true, vorhanden: false, hoehereRechte: true, passwortfeld: true, terminal: true));

        Assert.Equal(KeinZiel, plan);
    }

    // Regel 1 vor Regel 5 und 6: Ein eigenes Fenster, das noch da ist, bekommt weder Tippen noch Zwischenablage.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Eigenes_Fenster_wird_nie_beschrieben(bool terminal)
    {
        var plan = InsertPlanner.Decide(Fakten(eigenesFenster: true, terminal: terminal));

        Assert.Equal(KeinZiel, plan);
    }

    // Regel 1 vor Regel 2: Kein Ziel und Fenster weg zugleich ergibt kein_ziel, nicht fenster_weg.
    [Fact]
    public void Kein_Ziel_geht_vor_Fenster_weg()
    {
        Assert.Equal(KeinZiel, InsertPlanner.Decide(Fakten(ziel: false, vorhanden: false)));
        Assert.Equal(KeinZiel, InsertPlanner.Decide(Fakten(eigenesFenster: true, vorhanden: false)));
    }

    // Regel 2 vor Regel 3, 4 und 5: Ein Fenster, das weg ist, ergibt fenster_weg und nicht gesperrt.
    [Fact]
    public void Fenster_weg_geht_vor_Rechten_Passwortfeld_und_Terminal()
    {
        var plan = InsertPlanner.Decide(Fakten(
            vorhanden: false, hoehereRechte: true, passwortfeld: true, terminal: true));

        Assert.Equal(FensterWeg, plan);
    }

    // Regel 2 vor Regel 3: Fenster weg und höhere Rechte zugleich ergibt fenster_weg.
    [Fact]
    public void Fenster_weg_geht_vor_hoeheren_Rechten()
    {
        Assert.Equal(FensterWeg, InsertPlanner.Decide(Fakten(vorhanden: false, hoehereRechte: true)));
    }

    // Regel 2 vor Regel 4: Fenster weg und Passwortfeld zugleich ergibt fenster_weg.
    [Fact]
    public void Fenster_weg_geht_vor_Passwortfeld()
    {
        Assert.Equal(FensterWeg, InsertPlanner.Decide(Fakten(vorhanden: false, passwortfeld: true)));
    }

    // Regel 3 vor Regel 4 und 5: Das Beispiel aus dem Auftrag, höhere Rechte und Passwortfeld und Terminal zugleich.
    [Fact]
    public void Hoehere_Rechte_gehen_vor_Passwortfeld_und_Terminal()
    {
        var plan = InsertPlanner.Decide(Fakten(hoehereRechte: true, passwortfeld: true, terminal: true));

        Assert.Equal(HoehereRechte, plan);
    }

    // Regel 3 vor Regel 4: Höhere Rechte und Passwortfeld zugleich ergibt den Grund hoehere_rechte.
    [Fact]
    public void Hoehere_Rechte_gehen_vor_Passwortfeld()
    {
        Assert.Equal(HoehereRechte, InsertPlanner.Decide(Fakten(hoehereRechte: true, passwortfeld: true)));
    }

    // Regel 3 vor Regel 5: In ein Terminal mit höheren Rechten wird nicht getippt.
    [Fact]
    public void Hoehere_Rechte_gehen_vor_Terminal()
    {
        Assert.Equal(HoehereRechte, InsertPlanner.Decide(Fakten(hoehereRechte: true, terminal: true)));
    }

    // Regel 4 vor Regel 5: In ein Passwortfeld in einem Terminal wird nicht getippt.
    [Fact]
    public void Passwortfeld_geht_vor_Terminal()
    {
        Assert.Equal(Passwortfeld, InsertPlanner.Decide(Fakten(passwortfeld: true, terminal: true)));
    }

    // ---------- Alle 64 Kombinationen gegen eine ausgeschriebene Erwartung ----------

    /// <summary>
    /// Alle 64 Kombinationen der sechs Wahrheitswerte mit dem Ergebnis, das die Regeln 1 bis 6 in ihrer Reihenfolge verlangen.
    /// Spalten: Ziel vorhanden, eigenes Fenster, Fenster gibt es noch, höhere Rechte, Passwortfeld, Terminal.
    /// </summary>
    private static readonly (bool Ziel, bool Eigen, bool Vorhanden, bool Rechte, bool Passwort, bool Terminal, InsertPlan Plan)[] Tabelle =
    {
        // Kein Ziel (Zeilen 0 bis 31): Regel 1 gewinnt immer.
        (N, N, N, N, N, N, KeinZiel),
        (N, N, N, N, N, J, KeinZiel),
        (N, N, N, N, J, N, KeinZiel),
        (N, N, N, N, J, J, KeinZiel),
        (N, N, N, J, N, N, KeinZiel),
        (N, N, N, J, N, J, KeinZiel),
        (N, N, N, J, J, N, KeinZiel),
        (N, N, N, J, J, J, KeinZiel),
        (N, N, J, N, N, N, KeinZiel),
        (N, N, J, N, N, J, KeinZiel),
        (N, N, J, N, J, N, KeinZiel),
        (N, N, J, N, J, J, KeinZiel),
        (N, N, J, J, N, N, KeinZiel),
        (N, N, J, J, N, J, KeinZiel),
        (N, N, J, J, J, N, KeinZiel),
        (N, N, J, J, J, J, KeinZiel),
        (N, J, N, N, N, N, KeinZiel),
        (N, J, N, N, N, J, KeinZiel),
        (N, J, N, N, J, N, KeinZiel),
        (N, J, N, N, J, J, KeinZiel),
        (N, J, N, J, N, N, KeinZiel),
        (N, J, N, J, N, J, KeinZiel),
        (N, J, N, J, J, N, KeinZiel),
        (N, J, N, J, J, J, KeinZiel),
        (N, J, J, N, N, N, KeinZiel),
        (N, J, J, N, N, J, KeinZiel),
        (N, J, J, N, J, N, KeinZiel),
        (N, J, J, N, J, J, KeinZiel),
        (N, J, J, J, N, N, KeinZiel),
        (N, J, J, J, N, J, KeinZiel),
        (N, J, J, J, J, N, KeinZiel),
        (N, J, J, J, J, J, KeinZiel),

        // Fremdes Ziel, Fenster weg (Zeilen 32 bis 39): Regel 2 gewinnt vor Regel 3, 4 und 5.
        (J, N, N, N, N, N, FensterWeg),
        (J, N, N, N, N, J, FensterWeg),
        (J, N, N, N, J, N, FensterWeg),
        (J, N, N, N, J, J, FensterWeg),
        (J, N, N, J, N, N, FensterWeg),
        (J, N, N, J, N, J, FensterWeg),
        (J, N, N, J, J, N, FensterWeg),
        (J, N, N, J, J, J, FensterWeg),

        // Fremdes Ziel, Fenster da (Zeilen 40 bis 47): Regel 3, dann 4, dann 5, dann 6.
        (J, N, J, N, N, N, Zwischenablage),
        (J, N, J, N, N, J, Tippen),
        (J, N, J, N, J, N, Passwortfeld),
        (J, N, J, N, J, J, Passwortfeld),
        (J, N, J, J, N, N, HoehereRechte),
        (J, N, J, J, N, J, HoehereRechte),
        (J, N, J, J, J, N, HoehereRechte),
        (J, N, J, J, J, J, HoehereRechte),

        // Ziel ist ein Fenster von Talk selbst (Zeilen 48 bis 63): Regel 1 gewinnt immer.
        (J, J, N, N, N, N, KeinZiel),
        (J, J, N, N, N, J, KeinZiel),
        (J, J, N, N, J, N, KeinZiel),
        (J, J, N, N, J, J, KeinZiel),
        (J, J, N, J, N, N, KeinZiel),
        (J, J, N, J, N, J, KeinZiel),
        (J, J, N, J, J, N, KeinZiel),
        (J, J, N, J, J, J, KeinZiel),
        (J, J, J, N, N, N, KeinZiel),
        (J, J, J, N, N, J, KeinZiel),
        (J, J, J, N, J, N, KeinZiel),
        (J, J, J, N, J, J, KeinZiel),
        (J, J, J, J, N, N, KeinZiel),
        (J, J, J, J, N, J, KeinZiel),
        (J, J, J, J, J, N, KeinZiel),
        (J, J, J, J, J, J, KeinZiel),
    };

    /// <summary>
    /// Die Nummern der Tabellenzeilen, damit jede Kombination als eigener Testfall erscheint.
    /// </summary>
    public static TheoryData<int> Zeilen()
    {
        var daten = new TheoryData<int>();
        for (var i = 0; i < 64; i++)
        {
            daten.Add(i);
        }

        return daten;
    }

    // Die Tabelle selbst: 64 Zeilen, jede Kombination genau einmal, und die Zeilennummer ist die Kombination als Dualzahl.
    [Fact]
    public void Tabelle_deckt_alle_64_Kombinationen_genau_einmal_ab()
    {
        Assert.Equal(64, Tabelle.Length);

        for (var i = 0; i < Tabelle.Length; i++)
        {
            var z = Tabelle[i];
            var nummer =
                (z.Ziel ? 32 : 0) + (z.Eigen ? 16 : 0) + (z.Vorhanden ? 8 : 0) +
                (z.Rechte ? 4 : 0) + (z.Passwort ? 2 : 0) + (z.Terminal ? 1 : 0);

            Assert.Equal(i, nummer);
        }
    }

    // Regeln 1 bis 6 in ihrer Reihenfolge, für jede der 64 Kombinationen.
    [Theory]
    [MemberData(nameof(Zeilen))]
    public void Decide_entspricht_der_Tabelle(int zeile)
    {
        var z = Tabelle[zeile];
        var fakten = new InsertTargetFacts(z.Ziel, z.Eigen, z.Vorhanden, z.Rechte, z.Passwort, z.Terminal);

        var plan = InsertPlanner.Decide(fakten);

        Assert.Equal(z.Plan.Way, plan.Way);
        Assert.Equal(z.Plan.Reason, plan.Reason);
    }

    // Ziel aus Abschnitt 17: Es wird nie in ein Passwortfeld oder ein Fenster mit höheren Rechten geschrieben,
    // nie in ein Fenster von Talk selbst, nie ohne Ziel und nie in ein Fenster, das es nicht mehr gibt.
    [Theory]
    [MemberData(nameof(Zeilen))]
    public void Decide_gibt_nie_in_ein_verbotenes_Ziel_ein(int zeile)
    {
        var z = Tabelle[zeile];
        var fakten = new InsertTargetFacts(z.Ziel, z.Eigen, z.Vorhanden, z.Rechte, z.Passwort, z.Terminal);
        var verboten = !z.Ziel || z.Eigen || !z.Vorhanden || z.Rechte || z.Passwort;

        var plan = InsertPlanner.Decide(fakten);
        var gibtEin = plan.Way is InsertResult.Inserted or InsertResult.Typed;

        Assert.Equal(!verboten, gibtEin);
    }

    // Regel 1 bis 4: Wird nichts eingegeben, steht immer ein Grund dabei. Regel 5 und 6: Wird eingegeben, gibt es keinen.
    [Theory]
    [MemberData(nameof(Zeilen))]
    public void Decide_nennt_genau_dann_einen_Grund_wenn_nichts_eingegeben_wird(int zeile)
    {
        var z = Tabelle[zeile];
        var fakten = new InsertTargetFacts(z.Ziel, z.Eigen, z.Vorhanden, z.Rechte, z.Passwort, z.Terminal);

        var plan = InsertPlanner.Decide(fakten);
        var gibtEin = plan.Way is InsertResult.Inserted or InsertResult.Typed;

        Assert.Equal(gibtEin, plan.Reason == InsertReason.None);
    }

    // ---------- Feste Wörter fürs Protokoll ----------

    // Protokoll: weg ist eines der vier festen Wörter zwischenablage, tippen, verlauf, gesperrt.
    [Theory]
    [InlineData(InsertResult.Inserted, "zwischenablage")]
    [InlineData(InsertResult.Typed, "tippen")]
    [InlineData(InsertResult.HistoryOnly, "verlauf")]
    [InlineData(InsertResult.Blocked, "gesperrt")]
    public void ToWord_fuer_den_Weg(InsertResult weg, string erwartet)
    {
        Assert.Equal(erwartet, weg.ToWord());
    }

    // Regeln für den Weg: die sechs Gründe, wie sie dort in Schreibmaschinenschrift stehen.
    [Theory]
    [InlineData(InsertReason.NoTarget, "kein_ziel")]
    [InlineData(InsertReason.WindowGone, "fenster_weg")]
    [InlineData(InsertReason.HigherRights, "hoehere_rechte")]
    [InlineData(InsertReason.PasswordField, "passwortfeld")]
    [InlineData(InsertReason.NotForeground, "fenster_nicht_vorn")]
    [InlineData(InsertReason.InputFailed, "eingabe_gescheitert")]
    public void ToWord_fuer_den_Grund(InsertReason grund, string erwartet)
    {
        Assert.Equal(erwartet, grund.ToWord());
    }

    // Protokoll: nur feste Wörter. Jeder Wert hat ein eigenes Wort ohne Leerraum, auch der Wert für "kein Grund",
    // dessen Wortlaut das Playbook nicht festlegt.
    [Fact]
    public void ToWord_liefert_fuer_jeden_Wert_ein_eigenes_festes_Wort()
    {
        var wege = Enum.GetValues<InsertResult>().Select(w => w.ToWord()).ToList();
        var gruende = Enum.GetValues<InsertReason>().Select(g => g.ToWord()).ToList();

        Assert.Equal(wege.Count, wege.Distinct().Count());
        Assert.Equal(gruende.Count, gruende.Distinct().Count());

        foreach (var wort in wege.Concat(gruende))
        {
            Assert.False(string.IsNullOrEmpty(wort));
            Assert.All(wort, zeichen => Assert.True(zeichen is (>= 'a' and <= 'z') or '_'));
        }
    }

    // Protokoll: Der Plan für jede Kombination lässt sich mit den festen Wörtern schreiben.
    [Fact]
    public void Plan_und_Woerter_passen_zu_den_Regeln()
    {
        Assert.Equal("verlauf", InsertPlanner.Decide(Fakten(ziel: false)).Way.ToWord());
        Assert.Equal("kein_ziel", InsertPlanner.Decide(Fakten(ziel: false)).Reason.ToWord());
        Assert.Equal("verlauf", InsertPlanner.Decide(Fakten(vorhanden: false)).Way.ToWord());
        Assert.Equal("fenster_weg", InsertPlanner.Decide(Fakten(vorhanden: false)).Reason.ToWord());
        Assert.Equal("gesperrt", InsertPlanner.Decide(Fakten(hoehereRechte: true)).Way.ToWord());
        Assert.Equal("hoehere_rechte", InsertPlanner.Decide(Fakten(hoehereRechte: true)).Reason.ToWord());
        Assert.Equal("gesperrt", InsertPlanner.Decide(Fakten(passwortfeld: true)).Way.ToWord());
        Assert.Equal("passwortfeld", InsertPlanner.Decide(Fakten(passwortfeld: true)).Reason.ToWord());
        Assert.Equal("tippen", InsertPlanner.Decide(Fakten(terminal: true)).Way.ToWord());
        Assert.Equal("zwischenablage", InsertPlanner.Decide(Fakten()).Way.ToWord());
    }
}
