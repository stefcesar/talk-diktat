using Talk.Core.History;
using Xunit;

namespace Talk.Core.Tests.History;

/// <summary>
/// Prüft den Verlauf im Arbeitsspeicher gegen PLAYBOOK.md, Abschnitt 17 (Ablauf Punkt 2: höchstens 50 Einträge;
/// Tests: Reihenfolge und Obergrenze) und gegen den Vertrag von <see cref="IHistoryStore"/> (neueste zuerst).
/// Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class MemoryHistoryStoreTests
{
    private static readonly DateTimeOffset Beginn = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Abbruch => TestContext.Current.CancellationToken;

    /// <summary>
    /// Ein Eintrag mit fortlaufender Nummer im Text und einer Uhrzeit, die mit der Nummer steigt.
    /// </summary>
    private static HistoryEntry Eintrag(int nummer) =>
        new(Beginn.AddSeconds(nummer), "Diktat " + nummer, "test", false);

    private static async Task<MemoryHistoryStore> MitEintraegen(MemoryHistoryStore verlauf, int anzahl)
    {
        for (var i = 1; i <= anzahl; i++)
        {
            await verlauf.AddAsync(Eintrag(i), Abbruch);
        }

        return verlauf;
    }

    private static IEnumerable<string> Texte(int von, int bis) =>
        Enumerable.Range(bis, von - bis + 1).Reverse().Select(n => "Diktat " + n);

    // ---------- Obergrenze ----------

    // Ablauf Punkt 2: höchstens 50 Einträge, wenn nichts anderes angegeben ist.
    [Fact]
    public void Obergrenze_ist_ohne_Angabe_50()
    {
        var verlauf = new MemoryHistoryStore();

        Assert.Equal(50, verlauf.Capacity);
    }

    // Die Obergrenze lässt sich angeben und wird dann gemeldet.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(50)]
    [InlineData(200)]
    public void Obergrenze_laesst_sich_angeben(int obergrenze)
    {
        var verlauf = new MemoryHistoryStore(obergrenze);

        Assert.Equal(obergrenze, verlauf.Capacity);
    }

    // Der Verlauf erfüllt den Vertrag, über den die App ihn anspricht.
    [Fact]
    public void Verlauf_ist_ein_IHistoryStore()
    {
        Assert.IsAssignableFrom<IHistoryStore>(new MemoryHistoryStore());
    }

    // Ablauf Punkt 2: Genau 50 Einträge passen hinein, keiner fällt heraus.
    [Fact]
    public async Task Fuenfzig_Eintraege_bleiben_alle()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 50);

        var alle = await verlauf.GetRecentAsync(1000, Abbruch);

        Assert.Equal(50, alle.Count);
        Assert.Equal("Diktat 50", alle[0].Text);
        Assert.Equal("Diktat 1", alle[49].Text);
    }

    // Ablauf Punkt 2: Beim 51. Eintrag bleiben 50, der älteste fällt heraus, der neueste ist da.
    [Fact]
    public async Task Einundfuenfzigster_Eintrag_verdraengt_den_aeltesten()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 51);

        var alle = await verlauf.GetRecentAsync(1000, Abbruch);

        Assert.Equal(50, alle.Count);
        Assert.Equal("Diktat 51", alle[0].Text);
        Assert.Equal("Diktat 2", alle[49].Text);
        Assert.DoesNotContain(alle, e => e.Text == "Diktat 1");
    }

    // Ablauf Punkt 2: Auch nach sehr vielen Einträgen bleiben genau die 50 jüngsten, in ihrer Reihenfolge.
    [Fact]
    public async Task Nach_vielen_Eintraegen_bleiben_die_50_juengsten()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 175);

        var alle = await verlauf.GetRecentAsync(1000, Abbruch);

        Assert.Equal(Texte(von: 175, bis: 126), alle.Select(e => e.Text));
    }

    // Obergrenze: gilt genauso für eine kleine, selbst gewählte Grenze.
    [Fact]
    public async Task Kleine_Obergrenze_wird_eingehalten()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(3), 5);

        var alle = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.Equal(new[] { "Diktat 5", "Diktat 4", "Diktat 3" }, alle.Select(e => e.Text));
    }

    // Obergrenze: Bei Grenze 1 bleibt immer nur der jüngste Eintrag.
    [Fact]
    public async Task Obergrenze_eins_behaelt_nur_den_juengsten()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(1), 4);

        var alle = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.Equal("Diktat 4", Assert.Single(alle).Text);
    }

    // Obergrenze: Die Zahl der Einträge überschreitet die Grenze zu keinem Zeitpunkt.
    [Fact]
    public async Task Obergrenze_wird_nach_jedem_Eintrag_eingehalten()
    {
        var verlauf = new MemoryHistoryStore(4);

        for (var i = 1; i <= 12; i++)
        {
            await verlauf.AddAsync(Eintrag(i), Abbruch);

            var alle = await verlauf.GetRecentAsync(100, Abbruch);

            Assert.Equal(Math.Min(i, 4), alle.Count);
            Assert.Equal("Diktat " + i, alle[0].Text);
        }
    }

    // ---------- Reihenfolge ----------

    // Ein neuer Verlauf ist leer.
    [Fact]
    public async Task Neuer_Verlauf_ist_leer()
    {
        var verlauf = new MemoryHistoryStore();

        var alle = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.NotNull(alle);
        Assert.Empty(alle);
    }

    // Reihenfolge: neueste zuerst, in der Folge des Ablegens.
    [Fact]
    public async Task Juengste_Eintraege_kommen_zuerst()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 4);

        var alle = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.Equal(new[] { "Diktat 4", "Diktat 3", "Diktat 2", "Diktat 1" }, alle.Select(e => e.Text));
    }

    // Reihenfolge: Es zählt die Folge des Ablegens, nicht die Uhrzeit im Eintrag.
    [Fact]
    public async Task Reihenfolge_folgt_dem_Ablegen_nicht_der_Uhrzeit()
    {
        var verlauf = new MemoryHistoryStore();
        await verlauf.AddAsync(new HistoryEntry(Beginn.AddHours(2), "zuerst abgelegt", "test", false), Abbruch);
        await verlauf.AddAsync(new HistoryEntry(Beginn.AddHours(1), "danach abgelegt", "test", false), Abbruch);
        await verlauf.AddAsync(new HistoryEntry(Beginn.AddHours(3), "zuletzt abgelegt", "test", false), Abbruch);

        var alle = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.Equal(
            new[] { "zuletzt abgelegt", "danach abgelegt", "zuerst abgelegt" },
            alle.Select(e => e.Text));
    }

    // Reihenfolge: Wer weniger verlangt, bekommt die jüngsten, neueste zuerst.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(9)]
    public async Task GetRecent_liefert_hoechstens_die_verlangte_Zahl(int anzahl)
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 9);

        var juengste = await verlauf.GetRecentAsync(anzahl, Abbruch);

        Assert.Equal(Texte(von: 9, bis: 9 - anzahl + 1), juengste.Select(e => e.Text));
    }

    // Reihenfolge: Wer mehr verlangt, als da ist, bekommt alles, was da ist.
    [Fact]
    public async Task GetRecent_liefert_alles_wenn_mehr_verlangt_wird()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 3);

        var alle = await verlauf.GetRecentAsync(50, Abbruch);

        Assert.Equal(new[] { "Diktat 3", "Diktat 2", "Diktat 1" }, alle.Select(e => e.Text));
    }

    // Lesen verändert den Verlauf nicht: zweimal Lesen liefert dasselbe.
    [Fact]
    public async Task Lesen_veraendert_den_Verlauf_nicht()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(), 6);

        var erstes = await verlauf.GetRecentAsync(2, Abbruch);
        var zweites = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.Equal(new[] { "Diktat 6", "Diktat 5" }, erstes.Select(e => e.Text));
        Assert.Equal(Texte(von: 6, bis: 1), zweites.Select(e => e.Text));
    }

    // Eine schon gelieferte Liste ändert sich nicht, wenn danach ein neuer Eintrag dazukommt.
    [Fact]
    public async Task Gelieferte_Liste_bleibt_wie_sie_war()
    {
        var verlauf = await MitEintraegen(new MemoryHistoryStore(2), 2);

        var vorher = await verlauf.GetRecentAsync(10, Abbruch);
        await verlauf.AddAsync(Eintrag(3), Abbruch);

        Assert.Equal(new[] { "Diktat 2", "Diktat 1" }, vorher.Select(e => e.Text));
    }

    // ---------- Nichts geht verloren ----------

    // Ziel aus Abschnitt 17: Es geht nie Text verloren. Der Eintrag kommt mit allen Angaben unverändert zurück.
    [Fact]
    public async Task Eintrag_kommt_unveraendert_zurueck()
    {
        var verlauf = new MemoryHistoryStore();
        var eintrag = new HistoryEntry(Beginn, "Zeile eins\r\nZeile zwei  mit Leerraum ", "groq", true);

        await verlauf.AddAsync(eintrag, Abbruch);
        var zurueck = Assert.Single(await verlauf.GetRecentAsync(10, Abbruch));

        Assert.Equal(eintrag, zurueck);
        Assert.Equal(Beginn, zurueck.At);
        Assert.Equal("Zeile eins\r\nZeile zwei  mit Leerraum ", zurueck.Text);
        Assert.Equal("groq", zurueck.Engine);
        Assert.True(zurueck.LocalOnly);
    }

    // Ziel aus Abschnitt 17: Es geht nie Text verloren. Zweimal derselbe Text ergibt zwei Einträge.
    [Fact]
    public async Task Gleicher_Text_zweimal_ergibt_zwei_Eintraege()
    {
        var verlauf = new MemoryHistoryStore();
        var eintrag = new HistoryEntry(Beginn, "Noch einmal", "test", false);

        await verlauf.AddAsync(eintrag, Abbruch);
        await verlauf.AddAsync(eintrag, Abbruch);
        await verlauf.AddAsync(new HistoryEntry(Beginn, "Noch einmal", "test", false), Abbruch);

        var alle = await verlauf.GetRecentAsync(10, Abbruch);

        Assert.Equal(3, alle.Count);
        Assert.All(alle, e => Assert.Equal("Noch einmal", e.Text));
    }

    // Zwei Verläufe teilen sich nichts: Jeder hält nur seine eigenen Einträge.
    [Fact]
    public async Task Zwei_Verlaeufe_sind_unabhaengig()
    {
        var erster = await MitEintraegen(new MemoryHistoryStore(), 2);
        var zweiter = new MemoryHistoryStore();

        Assert.Equal(2, (await erster.GetRecentAsync(10, Abbruch)).Count);
        Assert.Empty(await zweiter.GetRecentAsync(10, Abbruch));
    }
}
