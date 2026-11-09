using System.Text.Json.Nodes;

using Talk.Core.Audio;
using Talk.Core.Settings;

using Xunit;

namespace Talk.Core.Tests.Audio;

/// <summary>
/// Prüft das Leiserstellen anderer Programme während der Aufnahme (Playbook Abschnitt 25, Regeln 1 bis 5).
/// Geschrieben nur aus den Regeln, nicht aus der Umsetzung. Alle Kennungen sind erfunden.
/// </summary>
public sealed class DuckingTests
{
    private const int EigenePid = 4242;
    private const float FloatToleranz = 0.0001f;

    // ---------------------------------------------------------------
    // Regel 2: Senken planen
    // ---------------------------------------------------------------

    [Fact]
    public void Regel2_Konstanten_haben_die_Werte_aus_dem_Playbook()
    {
        Assert.Equal(0.2f, DuckingRules.Factor, FloatToleranz);
        Assert.Equal(0.001f, DuckingRules.SilentBelow, 0.000001f);
        Assert.Equal(0.02f, DuckingRules.RestoreTolerance, 0.000001f);
    }

    [Fact]
    public void Regel2_null_ergibt_leere_Liste()
    {
        Assert.Empty(DuckingRules.PlanLower(null, EigenePid));
    }

    [Fact]
    public void Regel2_leere_Liste_ergibt_leere_Liste()
    {
        Assert.Empty(DuckingRules.PlanLower([], EigenePid));
    }

    [Fact]
    public void Regel2_normale_Sitzung_wird_auf_ein_Fuenftel_geplant()
    {
        IReadOnlyList<DuckEntry> plan = DuckingRules.PlanLower([new DuckSession("{musik-a}", 1000, 0.8f)], EigenePid);

        DuckEntry e = Assert.Single(plan);
        Assert.Equal("{musik-a}", e.Id);
        Assert.Equal(0.8f, e.Original, FloatToleranz);
        Assert.Equal(0.16f, e.Target, FloatToleranz);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.5f)]
    [InlineData(0.37f)]
    [InlineData(0.05f)]
    [InlineData(0.0011f)]
    public void Regel2_Target_ist_Original_mal_Factor(float lautstaerke)
    {
        DuckEntry e = Assert.Single(DuckingRules.PlanLower([new DuckSession("{video-b}", 2000, lautstaerke)], EigenePid));

        Assert.Equal(lautstaerke, e.Original, FloatToleranz);
        Assert.Equal(e.Original * DuckingRules.Factor, e.Target, FloatToleranz);
    }

    [Fact]
    public void Regel2_Systemklaenge_mit_Prozess_0_bleiben_unberuehrt()
    {
        Assert.Empty(DuckingRules.PlanLower([new DuckSession("{system-c}", 0, 0.9f)], EigenePid));
    }

    [Fact]
    public void Regel2_eigene_Prozessnummer_bleibt_unberuehrt()
    {
        Assert.Empty(DuckingRules.PlanLower([new DuckSession("{talk-d}", EigenePid, 0.9f)], EigenePid));
    }

    [Fact]
    public void Regel2_leere_Kennung_bleibt_unberuehrt()
    {
        Assert.Empty(DuckingRules.PlanLower([new DuckSession("", 3000, 0.9f)], EigenePid));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.001f)]
    [InlineData(0.0005f)]
    public void Regel2_stumme_Sitzung_bis_einschliesslich_0_001_bleibt_unberuehrt(float lautstaerke)
    {
        Assert.Empty(DuckingRules.PlanLower([new DuckSession("{podcast-e}", 3000, lautstaerke)], EigenePid));
    }

    [Fact]
    public void Regel2_knapp_ueber_0_001_wird_geplant()
    {
        DuckEntry e = Assert.Single(DuckingRules.PlanLower([new DuckSession("{podcast-f}", 3000, 0.0011f)], EigenePid));
        Assert.Equal("{podcast-f}", e.Id);
    }

    [Fact]
    public void Regel2_Lautstaerke_ueber_1_wird_auf_1_begrenzt()
    {
        DuckEntry e = Assert.Single(DuckingRules.PlanLower([new DuckSession("{laut-g}", 3000, 1.7f)], EigenePid));

        Assert.Equal(1f, e.Original, FloatToleranz);
        Assert.Equal(0.2f, e.Target, FloatToleranz);
    }

    [Fact]
    public void Regel2_Lautstaerke_unter_0_ergibt_keinen_Eintrag()
    {
        // Auf 0 begrenzt liegt sie nicht über 0,001.
        Assert.Empty(DuckingRules.PlanLower([new DuckSession("{negativ-h}", 3000, -0.4f)], EigenePid));
    }

    [Fact]
    public void Regel2_NaN_ergibt_keinen_Eintrag()
    {
        // NaN liegt nicht über 0,001.
        Assert.Empty(DuckingRules.PlanLower([new DuckSession("{nan-i}", 3000, float.NaN)], EigenePid));
    }

    [Fact]
    public void Regel2_bei_doppelter_Kennung_zaehlt_die_erste_Sitzung()
    {
        IReadOnlyList<DuckEntry> plan = DuckingRules.PlanLower(
            [new DuckSession("{doppelt-j}", 3000, 0.6f), new DuckSession("{doppelt-j}", 3001, 0.9f)],
            EigenePid);

        DuckEntry e = Assert.Single(plan);
        Assert.Equal(0.6f, e.Original, FloatToleranz);
    }

    [Fact]
    public void Regel2_Reihenfolge_bleibt_erhalten_und_ungeeignete_fallen_weg()
    {
        IReadOnlyList<DuckEntry> plan = DuckingRules.PlanLower(
            [
                new DuckSession("{eins}", 5001, 0.5f),
                new DuckSession("{system}", 0, 0.5f),
                new DuckSession("{zwei}", 5002, 0.7f),
                new DuckSession("{talk}", EigenePid, 0.5f),
                new DuckSession("{stumm}", 5003, 0f),
                new DuckSession("", 5004, 0.5f),
                new DuckSession("{drei}", 5005, 0.3f),
            ],
            EigenePid);

        Assert.Equal(["{eins}", "{zwei}", "{drei}"], plan.Select(e => e.Id).ToArray());
    }

    [Fact]
    public void Regel2_Gruppenkennung_wird_uebernommen()
    {
        DuckEntry e = Assert.Single(DuckingRules.PlanLower(
            [new DuckSession("{musik-gruppe}", 3000, 0.5f, "gruppe-musik")], EigenePid));

        Assert.Equal("gruppe-musik", e.GroupId);
    }

    [Fact]
    public void Regel2_Gruppenkennung_null_wird_leer()
    {
        DuckEntry e = Assert.Single(DuckingRules.PlanLower(
            [new DuckSession("{ohne-gruppe}", 3000, 0.5f, null)], EigenePid));

        Assert.Equal("", e.GroupId);
    }

    [Fact]
    public void Regel2_doppelte_Kennung_erste_stumm_zweite_laut_ergibt_keinen_Eintrag()
    {
        // Regel 2: Bei gleicher Kennung zählt nur die erste, auch wenn sie wegen Stille wegfällt.
        Assert.Empty(DuckingRules.PlanLower(
            [new DuckSession("{doppelt-stumm}", 3000, 0f), new DuckSession("{doppelt-stumm}", 3001, 0.9f)],
            EigenePid));
    }

    // ---------------------------------------------------------------
    // Regel 3: Zurücksetzen entscheiden
    // ---------------------------------------------------------------

    private static readonly DuckEntry Gesenkt = new("{musik-k}", 0.5f, 0.1f);

    [Fact]
    public void Regel3_genau_Target_wird_zurueckgesetzt()
    {
        Assert.True(DuckingRules.ShouldRestore(Gesenkt, 0.1f));
    }

    [Theory]
    [InlineData(0.119f)]
    [InlineData(0.081f)]
    [InlineData(0.11f)]
    [InlineData(0.09f)]
    public void Regel3_Abweichung_bis_0_02_wird_zurueckgesetzt(float jetzt)
    {
        // Knapp innerhalb des Randes, um float Rundung am Rand zu vermeiden.
        Assert.True(DuckingRules.ShouldRestore(Gesenkt, jetzt));
    }

    [Theory]
    [InlineData(0.13f)]
    [InlineData(0.07f)]
    [InlineData(0.121f)]
    [InlineData(0.079f)]
    public void Regel3_Abweichung_ueber_0_02_bleibt_stehen(float jetzt)
    {
        Assert.False(DuckingRules.ShouldRestore(Gesenkt, jetzt));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Regel3_Nutzer_hat_selbst_auf_0_oder_1_gestellt(float jetzt)
    {
        Assert.False(DuckingRules.ShouldRestore(Gesenkt, jetzt));
    }

    [Fact]
    public void Regel3_mit_geplantem_Eintrag_zurueck_auf_Target_ist_wahr()
    {
        DuckEntry e = Assert.Single(DuckingRules.PlanLower([new DuckSession("{musik-l}", 6000, 0.65f)], EigenePid));
        Assert.True(DuckingRules.ShouldRestore(e, e.Target));
        Assert.False(DuckingRules.ShouldRestore(e, e.Original));
    }

    // ---------------------------------------------------------------
    // Regel 3a: Zurücksetzen planen
    // ---------------------------------------------------------------

    [Fact]
    public void Regel3a_null_fuer_beide_Parameter_ergibt_leeren_Plan()
    {
        DuckRestorePlan plan = DuckingRules.PlanRestore(null, null);

        Assert.Empty(plan.Sets);
        Assert.Empty(plan.Done);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_null_fuer_Sitzungen_laesst_alle_Eintraege_offen()
    {
        DuckEntry e = new("{offen-a}", 0.5f, 0.1f, "gruppe-a");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], null);

        Assert.Empty(plan.Sets);
        Assert.Empty(plan.Done);
        Assert.Equal([e], plan.Remaining);
    }

    [Fact]
    public void Regel3a_null_fuer_Eintraege_ergibt_leeren_Plan()
    {
        DuckRestorePlan plan = DuckingRules.PlanRestore(null, [new DuckSession("{s-b}", 3000, 0.1f, "gruppe-b")]);

        Assert.Empty(plan.Sets);
        Assert.Empty(plan.Done);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_Treffer_ueber_Kennung_bekommt_Auftrag_mit_Original()
    {
        DuckEntry e = new("{musik-c}", 0.7f, 0.14f, "gruppe-c");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], [new DuckSession("{musik-c}", 3000, 0.14f, "gruppe-c")]);

        DuckSet set = Assert.Single(plan.Sets);
        Assert.Equal("{musik-c}", set.SessionId);
        Assert.Equal(0.7f, set.Volume, FloatToleranz);
        Assert.Equal(e, set.Entry);
        Assert.Equal([e], plan.Done);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_Treffer_nur_ueber_Gruppe_wenn_keine_Kennung_passt()
    {
        // Das Programm wurde neu gestartet: neue Kennung, gleiche Gruppe, gesenkte Lautstärke behalten.
        DuckEntry e = new("{alt-d}", 0.5f, 0.1f, "gruppe-d");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], [new DuckSession("{neu-d}", 3100, 0.1f, "gruppe-d")]);

        DuckSet set = Assert.Single(plan.Sets);
        Assert.Equal("{neu-d}", set.SessionId);
        Assert.Equal(0.5f, set.Volume, FloatToleranz);
        Assert.Equal([e], plan.Done);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_Kennung_geht_vor_Gruppe()
    {
        DuckEntry e = new("{musik-e}", 0.5f, 0.1f, "gruppe-e");

        DuckRestorePlan plan = DuckingRules.PlanRestore(
            [e],
            [new DuckSession("{musik-e}", 3000, 0.1f, "gruppe-e"), new DuckSession("{zweite-e}", 3001, 0.1f, "gruppe-e")]);

        DuckSet set = Assert.Single(plan.Sets);
        Assert.Equal("{musik-e}", set.SessionId);
    }

    [Fact]
    public void Regel3a_leere_Gruppe_trifft_nie()
    {
        DuckEntry e = new("{alt-f}", 0.5f, 0.1f, "");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], [new DuckSession("{neu-f}", 3000, 0.1f, "")]);

        Assert.Empty(plan.Sets);
        Assert.Empty(plan.Done);
        Assert.Equal([e], plan.Remaining);
    }

    [Fact]
    public void Regel3a_leere_Gruppe_trifft_auch_Sitzung_ohne_Gruppe_nicht()
    {
        DuckEntry e = new("{alt-g}", 0.5f, 0.1f, "");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], [new DuckSession("{neu-g}", 3000, 0.1f, null)]);

        Assert.Empty(plan.Sets);
        Assert.Equal([e], plan.Remaining);
    }

    [Fact]
    public void Regel3a_ohne_Treffer_bleibt_der_Eintrag_offen()
    {
        DuckEntry e = new("{weg-h}", 0.5f, 0.1f, "gruppe-h");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], [new DuckSession("{anders-h}", 3000, 0.1f, "gruppe-anders")]);

        Assert.Empty(plan.Sets);
        Assert.Empty(plan.Done);
        Assert.Equal([e], plan.Remaining);
    }

    [Fact]
    public void Regel3a_vom_Nutzer_veraenderter_Treffer_ist_erledigt_ohne_Auftrag()
    {
        DuckEntry e = new("{musik-i}", 0.5f, 0.1f, "gruppe-i");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e], [new DuckSession("{musik-i}", 3000, 0.8f, "gruppe-i")]);

        Assert.Empty(plan.Sets);
        Assert.Equal([e], plan.Done);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_mehrere_Sitzungen_gleicher_Gruppe_bekommen_je_einen_Auftrag()
    {
        DuckEntry e = new("{alt-j}", 0.6f, 0.12f, "gruppe-j");

        DuckRestorePlan plan = DuckingRules.PlanRestore(
            [e],
            [
                new DuckSession("{neu-j1}", 3000, 0.12f, "gruppe-j"),
                new DuckSession("{neu-j2}", 3000, 0.12f, "gruppe-j"),
                new DuckSession("{neu-j3}", 3000, 0.9f, "gruppe-j"),
            ]);

        Assert.Equal(["{neu-j1}", "{neu-j2}"], plan.Sets.Select(s => s.SessionId).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.All(plan.Sets, s => Assert.Equal(0.6f, s.Volume, FloatToleranz));
        Assert.Equal([e], plan.Done);
    }

    [Fact]
    public void Regel3a_eine_Sitzung_bekommt_hoechstens_einen_Auftrag_der_erste_Eintrag_gewinnt()
    {
        // Beide Einträge treffen dieselbe Sitzung über die Gruppe.
        DuckEntry erster = new("{alt-k1}", 0.5f, 0.1f, "gruppe-k");
        DuckEntry zweiter = new("{alt-k2}", 0.9f, 0.1f, "gruppe-k");

        DuckRestorePlan plan = DuckingRules.PlanRestore([erster, zweiter], [new DuckSession("{neu-k}", 3000, 0.1f, "gruppe-k")]);

        DuckSet set = Assert.Single(plan.Sets);
        Assert.Equal("{neu-k}", set.SessionId);
        Assert.Equal(erster, set.Entry);
        Assert.Equal(0.5f, set.Volume, FloatToleranz);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_Kennungstreffer_eines_spaeteren_Eintrags_geht_vor_Gruppentreffer_eines_frueheren()
    {
        // Regel 3a, zwei Durchgänge: erst alle Kennungstreffer, dann die Gruppentreffer.
        DuckEntry e1 = new("weg", 1.0f, 0.2f, "g");
        DuckEntry e2 = new("live", 0.9f, 0.18f, "g");

        DuckRestorePlan plan = DuckingRules.PlanRestore([e1, e2], [new DuckSession("live", 3000, 0.18f, "g")]);

        DuckSet set = Assert.Single(plan.Sets);
        Assert.Equal("live", set.SessionId);
        Assert.Equal(0.9f, set.Volume, FloatToleranz);
        Assert.Equal(e2, set.Entry);
    }

    [Fact]
    public void Regel3a_Gruppentreffer_bekommt_freie_Sitzung_wenn_andere_ueber_Kennung_vergeben_ist()
    {
        DuckEntry neuGestartet = new("{alt-p}", 0.7f, 0.14f, "gruppe-p");
        DuckEntry laufend = new("{laufend-p}", 0.5f, 0.1f, "gruppe-p");

        DuckRestorePlan plan = DuckingRules.PlanRestore(
            [neuGestartet, laufend],
            [new DuckSession("{laufend-p}", 3000, 0.1f, "gruppe-p"), new DuckSession("{frei-p}", 3001, 0.14f, "gruppe-p")]);

        Assert.Equal(2, plan.Sets.Count);
        DuckSet frei = plan.Sets.Single(s => s.SessionId == "{frei-p}");
        Assert.Equal(neuGestartet, frei.Entry);
        Assert.Equal(0.7f, frei.Volume, FloatToleranz);
        DuckSet eigen = plan.Sets.Single(s => s.SessionId == "{laufend-p}");
        Assert.Equal(laufend, eigen.Entry);
        Assert.Equal(0.5f, eigen.Volume, FloatToleranz);
        Assert.Empty(plan.Remaining);
    }

    [Fact]
    public void Regel3a_Volume_des_Auftrags_ist_Original_auch_bei_mehreren_Eintraegen()
    {
        DuckEntry a = new("{l1}", 0.4f, 0.08f, "gruppe-l1");
        DuckEntry b = new("{l2}", 1f, 0.2f, "gruppe-l2");

        DuckRestorePlan plan = DuckingRules.PlanRestore(
            [a, b],
            [new DuckSession("{l2}", 3000, 0.2f, "gruppe-l2"), new DuckSession("{l1}", 3001, 0.08f, "gruppe-l1")]);

        Assert.Equal(2, plan.Sets.Count);
        Assert.Equal(0.4f, plan.Sets.Single(s => s.SessionId == "{l1}").Volume, FloatToleranz);
        Assert.Equal(1f, plan.Sets.Single(s => s.SessionId == "{l2}").Volume, FloatToleranz);
        Assert.Equal(2, plan.Done.Count);
        Assert.Empty(plan.Remaining);
    }

    // ---------------------------------------------------------------
    // Regel 4: Merkzettel
    // ---------------------------------------------------------------

    [Fact]
    public void Regel4_group_hin_und_zurueck()
    {
        DuckEntry[] eintraege =
        [
            new("{gm1}", 0.8f, 0.16f, "gruppe-m"),
            new("{gm2}", 0.5f, 0.1f, ""),
        ];

        Assert.Equal(eintraege, DuckingJournal.Parse(DuckingJournal.Serialize(eintraege)));
    }

    [Fact]
    public void Regel4_Serialize_schreibt_group()
    {
        string text = DuckingJournal.Serialize([new DuckEntry("{gn}", 0.5f, 0.1f, "gruppe-n")]);

        JsonObject eintrag = Assert.Single(JsonNode.Parse(text)!.AsArray())!.AsObject();
        Assert.Equal("gruppe-n", eintrag["group"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""[{"id":"{go}","original":0.5,"target":0.1}]""")]
    [InlineData("""[{"id":"{go}","original":0.5,"target":0.1,"group":17}]""")]
    [InlineData("""[{"id":"{go}","original":0.5,"target":0.1,"group":null}]""")]
    [InlineData("""[{"id":"{go}","original":0.5,"target":0.1,"group":{"x":1}}]""")]
    [InlineData("""[{"id":"{go}","original":0.5,"target":0.1,"group":true}]""")]
    public void Regel4_fehlendes_oder_falsch_typisiertes_group_wird_leer(string text)
    {
        DuckEntry e = Assert.Single(DuckingJournal.Parse(text));

        Assert.Equal("{go}", e.Id);
        Assert.Equal("", e.GroupId);
    }

    [Theory]
    [InlineData("""{"id":"{dp}","id":"{dp2}","original":0.5,"target":0.1}""")]
    [InlineData("""{"id":"{dp}","original":0.5,"original":0.6,"target":0.1}""")]
    [InlineData("""{"id":"{dp}","original":0.5,"target":0.1,"target":0.2}""")]
    [InlineData("""{"id":"{dp}","original":0.5,"target":0.1,"group":"a","group":"b"}""")]
    public void Regel4_doppelte_Schluessel_lassen_nur_diesen_Eintrag_wegfallen(string kaputt)
    {
        string text = "[" + """{"id":"{vorher}","original":0.4,"target":0.08}""" + "," + kaputt + ","
            + """{"id":"{nachher}","original":0.9,"target":0.18,"group":"gruppe-q"}""" + "]";

        IReadOnlyList<DuckEntry>? liste = null;
        Exception? fehler = Record.Exception(() => liste = DuckingJournal.Parse(text));

        Assert.Null(fehler);
        Assert.Equal(["{vorher}", "{nachher}"], liste!.Select(e => e.Id).ToArray());
    }

    [Fact]
    public void Regel4_hin_und_zurueck_ergibt_dieselben_Eintraege()
    {
        DuckEntry[] eintraege =
        [
            new("{spotify-m}", 0.8f, 0.16f),
            new("{browser-n}", 1f, 0.2f),
            new("{player-o}", 0.33f, 0.066f),
        ];

        IReadOnlyList<DuckEntry> zurueck = DuckingJournal.Parse(DuckingJournal.Serialize(eintraege));

        Assert.Equal(eintraege, zurueck);
    }

    [Fact]
    public void Regel4_Serialize_schreibt_eine_JSON_Liste_mit_id_original_target()
    {
        string text = DuckingJournal.Serialize([new DuckEntry("{x-p}", 0.5f, 0.1f)]);

        JsonArray liste = JsonNode.Parse(text)!.AsArray();
        JsonObject eintrag = Assert.Single(liste)!.AsObject();
        Assert.Equal("{x-p}", eintrag["id"]!.GetValue<string>());
        Assert.Equal(0.5, eintrag["original"]!.GetValue<double>(), 0.0001);
        Assert.Equal(0.1, eintrag["target"]!.GetValue<double>(), 0.0001);
    }

    [Fact]
    public void Regel4_leere_Liste_hin_und_zurueck()
    {
        Assert.Empty(DuckingJournal.Parse(DuckingJournal.Serialize([])));
    }

    [Fact]
    public void Regel4_Serialize_von_null_ergibt_leeren_Merkzettel()
    {
        Assert.Empty(DuckingJournal.Parse(DuckingJournal.Serialize(null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein json")]
    [InlineData("[{\"id\":")]
    [InlineData("{\"id\":\"{q}\",\"original\":0.5,\"target\":0.1}")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Regel4_Parse_ohne_brauchbare_Liste_ergibt_leere_Liste(string? text)
    {
        Assert.Empty(DuckingJournal.Parse(text));
    }

    [Fact]
    public void Regel4_Eintrag_ohne_id_faellt_weg()
    {
        IReadOnlyList<DuckEntry> liste = DuckingJournal.Parse("""
            [{"original":0.5,"target":0.1},{"id":"{gut-r}","original":0.5,"target":0.1}]
            """);

        Assert.Equal("{gut-r}", Assert.Single(liste).Id);
    }

    [Fact]
    public void Regel4_id_als_Zahl_faellt_weg()
    {
        Assert.Empty(DuckingJournal.Parse("""[{"id":17,"original":0.5,"target":0.1}]"""));
    }

    [Fact]
    public void Regel4_original_als_Text_faellt_weg()
    {
        Assert.Empty(DuckingJournal.Parse("""[{"id":"{s}","original":"0.5","target":0.1}]"""));
    }

    [Fact]
    public void Regel4_fehlendes_target_faellt_weg()
    {
        Assert.Empty(DuckingJournal.Parse("""[{"id":"{t}","original":0.5}]"""));
    }

    [Fact]
    public void Regel4_Eintrag_der_kein_Objekt_ist_faellt_weg()
    {
        IReadOnlyList<DuckEntry> liste = DuckingJournal.Parse("""
            [5,"text",null,[1],{"id":"{gut-u}","original":0.4,"target":0.08}]
            """);

        Assert.Equal("{gut-u}", Assert.Single(liste).Id);
    }

    [Fact]
    public void Regel4_Zahlen_werden_auf_0_bis_1_begrenzt()
    {
        DuckEntry e = Assert.Single(DuckingJournal.Parse("""[{"id":"{v}","original":1.8,"target":-0.3}]"""));

        Assert.Equal(1f, e.Original, FloatToleranz);
        Assert.Equal(0f, e.Target, FloatToleranz);
    }

    [Fact]
    public void Regel4_bei_doppelter_Kennung_gilt_der_erste_Eintrag()
    {
        IReadOnlyList<DuckEntry> liste = DuckingJournal.Parse("""
            [{"id":"{w}","original":0.6,"target":0.12},{"id":"{w}","original":0.9,"target":0.18}]
            """);

        DuckEntry e = Assert.Single(liste);
        Assert.Equal(0.6f, e.Original, FloatToleranz);
        Assert.Equal(0.12f, e.Target, FloatToleranz);
    }

    [Theory]
    [InlineData("[{\"id\":\"{y}\",\"original\":1e999,\"target\":0.1}]")]
    [InlineData("[{\"id\":\"{y}\",\"original\":NaN,\"target\":0.1}]")]
    [InlineData("[{\"id\":null,\"original\":null,\"target\":null}]")]
    [InlineData("[{\"id\":{},\"original\":[],\"target\":true}]")]
    [InlineData("\uFEFF[")]
    [InlineData("[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]")]
    [InlineData("[{\"id\":\"{z}\",\"original\":0.5,\"target\":0.1},]")]
    public void Regel4_Parse_wirft_nie(string text)
    {
        Exception? fehler = Record.Exception(() => DuckingJournal.Parse(text));
        Assert.Null(fehler);
    }

    [Fact]
    public void Regel4_Parse_wirft_nie_bei_sehr_tiefer_Verschachtelung()
    {
        string text = new string('[', 5000) + new string(']', 5000);
        Exception? fehler = Record.Exception(() => DuckingJournal.Parse(text));
        Assert.Null(fehler);
    }

    // ---------------------------------------------------------------
    // Regel 5: Schalter in den Einstellungen
    // ---------------------------------------------------------------

    [Fact]
    public void Regel5_Standard_ist_aus()
    {
        Assert.False(new DictationSettings().DuckingEnabled);
        Assert.False(DictationSettingsJson.Read("{}").DuckingEnabled);
        Assert.False(DictationSettingsJson.Read(null).DuckingEnabled);
    }

    [Theory]
    [InlineData("""{"duckingEnabled":true}""", true)]
    [InlineData("""{"duckingEnabled":false}""", false)]
    public void Regel5_Read_liest_den_Schalter(string json, bool erwartet)
    {
        Assert.Equal(erwartet, DictationSettingsJson.Read(json).DuckingEnabled);
    }

    [Theory]
    [InlineData("""{"duckingEnabled":"ja"}""")]
    [InlineData("""{"duckingEnabled":1}""")]
    [InlineData("""{"duckingEnabled":null}""")]
    [InlineData("""{"duckingEnabled":"true"}""")]
    public void Regel5_falscher_Typ_ergibt_den_Standard(string json)
    {
        Assert.False(DictationSettingsJson.Read(json).DuckingEnabled);
    }

    [Fact]
    public void Regel5_Merge_setzt_den_Schluessel()
    {
        string neu = DictationSettingsJson.Merge(null, new DictationSettings(DuckingEnabled: true))!;

        Assert.True(JsonNode.Parse(neu)!.AsObject()["duckingEnabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Regel5_Merge_und_Read_ergeben_denselben_Schalter()
    {
        DictationSettings an = new(DuckingEnabled: true);
        DictationSettings aus = new(DuckingEnabled: false);

        Assert.Equal(an, DictationSettingsJson.Read(DictationSettingsJson.Merge(null, an)));
        Assert.Equal(aus, DictationSettingsJson.Read(DictationSettingsJson.Merge("""{"duckingEnabled":true}""", aus)));
    }

    [Fact]
    public void Regel5_Merge_behaelt_fremde_Schluessel()
    {
        string vorhanden = """{"modelsDirectory":"F:\\Modelle","eigenerSchluessel":7}""";

        string neu = DictationSettingsJson.Merge(vorhanden, new DictationSettings(DuckingEnabled: true))!;
        JsonObject root = JsonNode.Parse(neu)!.AsObject();

        Assert.Equal("F:\\Modelle", root["modelsDirectory"]!.GetValue<string>());
        Assert.Equal(7, root["eigenerSchluessel"]!.GetValue<int>());
        Assert.True(root["duckingEnabled"]!.GetValue<bool>());
    }
}
