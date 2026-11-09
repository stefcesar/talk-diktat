using System.Text.Json.Nodes;

using Talk.Core.Settings;

using Xunit;

namespace Talk.Core.Tests.Settings;

/// <summary>
/// Prüft die Einstellungen: erlaubte Bereiche, Lesen und Schreiben als JSON (Playbook Abschnitt 18, Teil 7c).
/// </summary>
public sealed class DictationSettingsTests
{
    [Fact]
    public void Standardwerte()
    {
        DictationSettings s = new();

        Assert.Null(s.MicrophoneId);
        Assert.Equal("de", s.Language);
        Assert.Equal(350, s.DoubleTapWindowMs);
        Assert.False(s.PushToTalk);
        Assert.False(s.LocalOnly);
        Assert.True(s.SoundsEnabled);
        Assert.True(s.HistoryEnabled);
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("IT", "it")]
    [InlineData(" en ", "en")]
    [InlineData("fr", "de")]
    [InlineData("auto", "de")]
    [InlineData("", "de")]
    public void Normalize_kennt_nur_drei_Sprachen(string sprache, string erwartet)
    {
        Assert.Equal(erwartet, new DictationSettings(Language: sprache).Normalize().Language);
    }

    [Theory]
    [InlineData(-5, 150)]
    [InlineData(149, 150)]
    [InlineData(150, 150)]
    [InlineData(350, 350)]
    [InlineData(1000, 1000)]
    [InlineData(1001, 1000)]
    [InlineData(int.MaxValue, 1000)]
    public void Normalize_begrenzt_das_Doppeltipp_Fenster(int wert, int erwartet)
    {
        Assert.Equal(erwartet, new DictationSettings(DoubleTapWindowMs: wert).Normalize().DoubleTapWindowMs);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" {abc} ", "{abc}")]
    public void Normalize_macht_eine_leere_Mikrofonkennung_zu_null(string? kennung, string? erwartet)
    {
        Assert.Equal(erwartet, new DictationSettings(MicrophoneId: kennung).Normalize().MicrophoneId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    public void Read_ohne_brauchbares_JSON_liefert_die_Standardwerte(string? json)
    {
        Assert.Equal(new DictationSettings(), DictationSettingsJson.Read(json));
    }

    [Fact]
    public void Read_liest_alle_Schluessel()
    {
        string json = """
            {"microphoneId":"{x}","language":"it","doubleTapMs":420,"pushToTalk":true,"localOnly":true,"soundsEnabled":false,"historyEnabled":false}
            """;

        Assert.Equal(new DictationSettings("{x}", "it", 420, true, true, false, false), DictationSettingsJson.Read(json));
    }

    [Fact]
    public void Read_nimmt_bei_falschem_Typ_den_Standardwert_und_normalisiert()
    {
        string json = """
            {"microphoneId":5,"language":"xx","doubleTapMs":"schnell","pushToTalk":"ja","localOnly":1,"soundsEnabled":null,"historyEnabled":"nein"}
            """;

        Assert.Equal(new DictationSettings(), DictationSettingsJson.Read(json));
    }

    [Fact]
    public void Read_begrenzt_Werte_aus_der_Datei()
    {
        Assert.Equal(1000, DictationSettingsJson.Read("""{"doubleTapMs":99999}""").DoubleTapWindowMs);
    }

    [Fact]
    public void Merge_behaelt_fremde_Schluessel()
    {
        string vorhanden = """{"modelsDirectory":"F:\\Modelle","whisperRuntime":"vulkan","language":"de"}""";

        string neu = DictationSettingsJson.Merge(vorhanden, new DictationSettings(Language: "en", LocalOnly: true))!;
        JsonObject root = JsonNode.Parse(neu)!.AsObject();

        Assert.Equal("F:\\Modelle", root["modelsDirectory"]!.GetValue<string>());
        Assert.Equal("vulkan", root["whisperRuntime"]!.GetValue<string>());
        Assert.Equal("en", root["language"]!.GetValue<string>());
        Assert.True(root["localOnly"]!.GetValue<bool>());
    }

    [Fact]
    public void Merge_und_Read_ergeben_dieselben_Einstellungen()
    {
        DictationSettings s = new("{mikro}", "it", 500, true, true, false, false);

        Assert.Equal(s, DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s)));
    }

    [Fact]
    public void Merge_entfernt_die_Mikrofonkennung_beim_Standardgeraet()
    {
        string neu = DictationSettingsJson.Merge("""{"microphoneId":"{alt}"}""", new DictationSettings())!;

        Assert.False(JsonNode.Parse(neu)!.AsObject().ContainsKey("microphoneId"));
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("[1]")]
    public void Merge_ueberschreibt_unlesbaren_Bestand_nicht(string vorhanden)
    {
        Assert.Null(DictationSettingsJson.Merge(vorhanden, new DictationSettings()));
        Assert.False(DictationSettingsJson.IsReadable(vorhanden));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("{}")]
    public void Merge_beginnt_bei_leerem_Bestand_neu(string? vorhanden)
    {
        Assert.True(DictationSettingsJson.IsReadable(vorhanden));
        Assert.Equal(new DictationSettings(), DictationSettingsJson.Read(DictationSettingsJson.Merge(vorhanden, new DictationSettings())));
    }

    [Fact]
    public void Merge_schreibt_normalisierte_Werte()
    {
        string neu = DictationSettingsJson.Merge(null, new DictationSettings(Language: "fr", DoubleTapWindowMs: 5))!;
        JsonObject root = JsonNode.Parse(neu)!.AsObject();

        Assert.Equal("de", root["language"]!.GetValue<string>());
        Assert.Equal(150, root["doubleTapMs"]!.GetValue<int>());
    }

    [Fact]
    public void Overlay_ist_an_und_laesst_sich_abstellen()
    {
        Assert.True(new DictationSettings().OverlayEnabled);
        Assert.True(DictationSettingsJson.Read("{}").OverlayEnabled);
        Assert.False(DictationSettingsJson.Read("""{"overlayEnabled":false}""").OverlayEnabled);

        DictationSettings aus = new(OverlayEnabled: false);
        Assert.Equal(aus, DictationSettingsJson.Read(DictationSettingsJson.Merge(null, aus)));
    }
}
