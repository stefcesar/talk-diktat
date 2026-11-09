using Talk.Core.History;

using Xunit;

namespace Talk.Core.Tests.History;

/// <summary>
/// Prüft, was im Verlauf bleibt, und das Dateiformat (Playbook Abschnitt 18, Teil 7b). Alle Texte sind erfunden.
/// </summary>
public sealed class HistoryRetentionTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(2));

    private static HistoryEntry Eintrag(double tageAlt, string text) => new(Jetzt.AddDays(-tageAlt), text, "test", false);

    [Fact]
    public void Prune_liefert_den_neuesten_zuerst()
    {
        IReadOnlyList<HistoryEntry> ergebnis = HistoryRetention.Prune([Eintrag(3, "alt"), Eintrag(0, "neu"), Eintrag(1, "mitte")], Jetzt);

        Assert.Equal(["neu", "mitte", "alt"], ergebnis.Select(e => e.Text));
    }

    [Theory]
    [InlineData(29.9, true)]
    [InlineData(30, true)]
    [InlineData(30.01, false)]
    [InlineData(400, false)]
    public void Prune_laesst_Eintraege_bis_30_Tage_und_keine_aelteren(double tageAlt, bool bleibt)
    {
        IReadOnlyList<HistoryEntry> ergebnis = HistoryRetention.Prune([Eintrag(tageAlt, "text")], Jetzt);

        Assert.Equal(bleibt ? 1 : 0, ergebnis.Count);
    }

    [Fact]
    public void Prune_haelt_hoechstens_500_und_behaelt_die_neuesten()
    {
        List<HistoryEntry> viele = Enumerable.Range(0, 520).Select(i => new HistoryEntry(Jetzt.AddSeconds(-i), $"t{i}", "test", false)).ToList();

        IReadOnlyList<HistoryEntry> ergebnis = HistoryRetention.Prune(viele, Jetzt);

        Assert.Equal(500, ergebnis.Count);
        Assert.Equal("t0", ergebnis[0].Text);
        Assert.Equal("t499", ergebnis[^1].Text);
    }

    [Fact]
    public void Prune_mit_eigener_Grenze_und_eigenem_Alter()
    {
        IReadOnlyList<HistoryEntry> ergebnis = HistoryRetention.Prune(
            [Eintrag(0, "a"), Eintrag(1, "b"), Eintrag(2, "c"), Eintrag(5, "d")],
            Jetzt,
            TimeSpan.FromDays(3),
            2);

        Assert.Equal(["a", "b"], ergebnis.Select(e => e.Text));
    }

    [Fact]
    public void Prune_wirft_leere_Texte_und_null_weg()
    {
        IReadOnlyList<HistoryEntry> ergebnis = HistoryRetention.Prune([Eintrag(0, "  "), null, Eintrag(0, ""), Eintrag(1, "bleibt")], Jetzt);

        Assert.Equal(["bleibt"], ergebnis.Select(e => e.Text));
    }

    [Fact]
    public void Prune_behaelt_bei_gleicher_Zeit_die_Reihenfolge()
    {
        IReadOnlyList<HistoryEntry> ergebnis = HistoryRetention.Prune([Eintrag(1, "erster"), Eintrag(1, "zweiter")], Jetzt);

        Assert.Equal(["erster", "zweiter"], ergebnis.Select(e => e.Text));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Prune_ohne_Platz_ist_leer(int grenze)
    {
        Assert.Empty(HistoryRetention.Prune([Eintrag(0, "text")], Jetzt, null, grenze));
    }

    [Fact]
    public void Prune_mit_null_ist_leer()
    {
        Assert.Empty(HistoryRetention.Prune(null, Jetzt));
    }

    [Fact]
    public void Codec_gibt_zurueck_was_hineinging()
    {
        List<HistoryEntry> eintraege =
        [
            new(Jetzt, "Erste Zeile\nzweite Zeile mit ä, ß und Í.", "lokal", true),
            new(Jetzt.AddHours(-2), "Anführung \"so\" und Schrägstrich \\ und Emoji \U0001F600", "groq", false),
        ];

        IReadOnlyList<HistoryEntry>? gelesen = HistoryCodec.Deserialize(HistoryCodec.Serialize(eintraege));

        Assert.NotNull(gelesen);
        Assert.Equal(eintraege, gelesen);
    }

    [Fact]
    public void Codec_mit_leerer_Liste()
    {
        IReadOnlyList<HistoryEntry>? gelesen = HistoryCodec.Deserialize(HistoryCodec.Serialize([]));

        Assert.NotNull(gelesen);
        Assert.Empty(gelesen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Version\":2,\"Entries\":[]}")]
    [InlineData("{\"Version\":1}")]
    [InlineData("{\"Version\":1,\"Entries\":\"falsch\"}")]
    public void Codec_liefert_null_bei_unlesbaren_Bytes(string inhalt)
    {
        Assert.Null(HistoryCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(inhalt)));
    }

    [Fact]
    public async Task Verlauf_im_Arbeitsspeicher_laesst_sich_leeren()
    {
        MemoryHistoryStore verlauf = new();
        await verlauf.AddAsync(Eintrag(0, "eins"), CancellationToken.None);
        await verlauf.AddAsync(Eintrag(0, "zwei"), CancellationToken.None);

        await verlauf.ClearAsync(CancellationToken.None);

        Assert.Empty(await verlauf.GetRecentAsync(10, CancellationToken.None));
    }
}
