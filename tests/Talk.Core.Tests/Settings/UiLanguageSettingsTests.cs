// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Text.Json.Nodes;

using Talk.Core.Settings;

using Xunit;

namespace Talk.Core.Tests.Settings;

/// <summary>
/// Prüft die Einstellung "Sprache der Oberfläche" nur aus dem Vertrag und den Regeln 1, 3 und 11 (Playbook Abschnitt 29).
/// </summary>
public sealed class UiLanguageSettingsTests
{
    // Vertrag: UiLanguage ist der letzte Parameter des Records, Typ string, Standard null.
    [Fact]
    public void Vertrag_UiLanguage_ist_letzter_Parameter_mit_Standard_null()
    {
        ConstructorInfo ctor = typeof(DictationSettings).GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        ParameterInfo letzter = ctor.GetParameters()[^1];

        Assert.Equal("UiLanguage", letzter.Name);
        Assert.Equal(typeof(string), letzter.ParameterType);
        Assert.True(letzter.HasDefaultValue);
        Assert.Null(letzter.DefaultValue);
        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(letzter).WriteState);
    }

    // Vertrag und Regel 3: Standard ist null, also noch nicht gewählt.
    [Fact]
    public void Regel3_Standard_ist_null()
    {
        Assert.Null(new DictationSettings().UiLanguage);
        Assert.Null(DictationSettingsJson.Read("{}").UiLanguage);
        Assert.Null(DictationSettingsJson.Read(null).UiLanguage);
    }

    // Vertrag: bestehende Aufrufe nach Position und bestehende Gleichheit bleiben.
    [Fact]
    public void Vertrag_bestehende_Gleichheit_bleibt()
    {
        string json = """
            {"microphoneId":"{x}","language":"it","doubleTapMs":420,"pushToTalk":true,"localOnly":true,"soundsEnabled":false,"historyEnabled":false}
            """;

        Assert.Equal(new DictationSettings("{x}", "it", 420, true, true, false, false), DictationSettingsJson.Read(json));
        Assert.Equal(new DictationSettings(), DictationSettingsJson.Read("{}"));
        Assert.Equal(new DictationSettings(), new DictationSettings(UiLanguage: null));
        Assert.NotEqual(new DictationSettings(), new DictationSettings(UiLanguage: "en"));
    }

    // Regel 3: Read nimmt uiLanguage getrimmt und klein, nur de, en, it.
    [Theory]
    [InlineData("""{"uiLanguage":"de"}""", "de")]
    [InlineData("""{"uiLanguage":"en"}""", "en")]
    [InlineData("""{"uiLanguage":"it"}""", "it")]
    [InlineData("""{"uiLanguage":"IT"}""", "it")]
    [InlineData("""{"uiLanguage":"De"}""", "de")]
    [InlineData("""{"uiLanguage":" EN "}""", "en")]
    [InlineData("""{"uiLanguage":"\tit\n"}""", "it")]
    [InlineData("""{}""", null)]
    [InlineData("""{"language":"it"}""", null)]
    [InlineData("""{"uiLanguage":""}""", null)]
    [InlineData("""{"uiLanguage":"   "}""", null)]
    [InlineData("""{"uiLanguage":"fr"}""", null)]
    [InlineData("""{"uiLanguage":"de-CH"}""", null)]
    [InlineData("""{"uiLanguage":"de_DE"}""", null)]
    [InlineData("""{"uiLanguage":"auto"}""", null)]
    [InlineData("""{"uiLanguage":"Deutsch"}""", null)]
    [InlineData("""{"uiLanguage":5}""", null)]
    [InlineData("""{"uiLanguage":0}""", null)]
    [InlineData("""{"uiLanguage":null}""", null)]
    [InlineData("""{"uiLanguage":true}""", null)]
    [InlineData("""{"uiLanguage":{}}""", null)]
    [InlineData("""{"uiLanguage":{"de":"de"}}""", null)]
    [InlineData("""{"uiLanguage":["de"]}""", null)]
    [InlineData("""{"UiLanguage":"de"}""", null)]
    public void Regel3_Read(string json, string? erwartet)
    {
        Assert.Equal(erwartet, DictationSettingsJson.Read(json).UiLanguage);
    }

    // Regel 3: unbrauchbares JSON insgesamt ergibt ebenfalls null.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("[1,2]")]
    [InlineData("\"de\"")]
    public void Regel3_Read_ohne_brauchbares_JSON(string? json)
    {
        Assert.Null(DictationSettingsJson.Read(json).UiLanguage);
    }

    // Regel 3: ein falscher uiLanguage stört die übrigen Schlüssel nicht.
    [Fact]
    public void Regel3_Read_falscher_Wert_laesst_andere_Schluessel()
    {
        DictationSettings s = DictationSettingsJson.Read("""{"language":"it","pushToTalk":true,"uiLanguage":42}""");

        Assert.Null(s.UiLanguage);
        Assert.Equal("it", s.Language);
        Assert.True(s.PushToTalk);
    }

    // Regel 3: Normalize macht dasselbe mit dem Wert im Record.
    [Theory]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    [InlineData("it", "it")]
    [InlineData("IT", "it")]
    [InlineData(" En ", "en")]
    [InlineData("\tDE\r\n", "de")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("fr", null)]
    [InlineData("de-CH", null)]
    [InlineData("de_DE", null)]
    [InlineData("auto", null)]
    public void Regel3_Normalize(string? wert, string? erwartet)
    {
        Assert.Equal(erwartet, new DictationSettings(UiLanguage: wert).Normalize().UiLanguage);
    }

    // Regel 1: Diktiersprache und Oberflächensprache sind unabhängig.
    [Fact]
    public void Regel1_Diktiersprache_und_Oberflaeche_unabhaengig()
    {
        DictationSettings s = new DictationSettings(Language: "it", UiLanguage: "en").Normalize();
        Assert.Equal("it", s.Language);
        Assert.Equal("en", s.UiLanguage);

        DictationSettings t = new DictationSettings(Language: "fr", UiLanguage: "it").Normalize();
        Assert.Equal("de", t.Language);
        Assert.Equal("it", t.UiLanguage);

        Assert.Equal("de", DictationSettingsJson.Read("""{"uiLanguage":"it"}""").Language);
        Assert.Null(DictationSettingsJson.Read("""{"language":"it"}""").UiLanguage);
        Assert.Equal("en", new DictationSettings(UiLanguage: "fr", Language: "en").Normalize().Language);
    }

    private static JsonObject MergeObjekt(string? vorhanden, DictationSettings s)
    {
        string? neu = DictationSettingsJson.Merge(vorhanden, s);
        Assert.NotNull(neu);
        return JsonNode.Parse(neu!)!.AsObject();
    }

    // Regel 3 und 11: Merge schreibt uiLanguage bei einer Wahl, normalisiert.
    [Theory]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    [InlineData("it", "it")]
    [InlineData(" IT ", "it")]
    [InlineData("En", "en")]
    public void Regel3_Merge_schreibt_uiLanguage_bei_Wahl(string wahl, string erwartet)
    {
        JsonObject root = MergeObjekt(null, new DictationSettings(UiLanguage: wahl));

        Assert.Equal(erwartet, root["uiLanguage"]!.GetValue<string>());
    }

    // Regel 3 und 11: ohne gültige Wahl wird uiLanguage entfernt, auch wenn er vorher in der Datei stand.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fr")]
    [InlineData("de-CH")]
    public void Regel3_Merge_entfernt_uiLanguage_ohne_Wahl(string? wert)
    {
        JsonObject root = MergeObjekt("""{"uiLanguage":"it","language":"de"}""", new DictationSettings(UiLanguage: wert));

        Assert.False(root.ContainsKey("uiLanguage"));
    }

    // Regel 3: Merge auf leerem Bestand ohne Wahl schreibt keinen uiLanguage.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    public void Regel3_Merge_leerer_Bestand_ohne_Wahl(string? vorhanden)
    {
        Assert.False(MergeObjekt(vorhanden, new DictationSettings()).ContainsKey("uiLanguage"));
    }

    // Regel 3: alle anderen Schlüssel bleiben, auch fremde.
    [Fact]
    public void Regel3_Merge_laesst_fremde_Schluessel()
    {
        string vorhanden = """{"modelsDirectory":"F:\\Modelle","whisperRuntime":"vulkan","language":"de","uiLanguage":"de"}""";

        JsonObject mitWahl = MergeObjekt(vorhanden, new DictationSettings(Language: "en", UiLanguage: "it"));
        Assert.Equal("F:\\Modelle", mitWahl["modelsDirectory"]!.GetValue<string>());
        Assert.Equal("vulkan", mitWahl["whisperRuntime"]!.GetValue<string>());
        Assert.Equal("en", mitWahl["language"]!.GetValue<string>());
        Assert.Equal("it", mitWahl["uiLanguage"]!.GetValue<string>());

        JsonObject ohneWahl = MergeObjekt(vorhanden, new DictationSettings(Language: "en"));
        Assert.Equal("F:\\Modelle", ohneWahl["modelsDirectory"]!.GetValue<string>());
        Assert.Equal("vulkan", ohneWahl["whisperRuntime"]!.GetValue<string>());
        Assert.Equal("en", ohneWahl["language"]!.GetValue<string>());
        Assert.False(ohneWahl.ContainsKey("uiLanguage"));
    }

    // Regel 11: Speichern anderer Einstellungen schreibt uiLanguage nicht, solange nichts gewählt ist.
    [Fact]
    public void Regel11_Speichern_anderer_Einstellungen_ohne_Wahl()
    {
        string datei = """{"language":"de","localOnly":false}""";
        DictationSettings gelesen = DictationSettingsJson.Read(datei);

        JsonObject root = MergeObjekt(datei, gelesen with { LocalOnly = true, Language = "it" });

        Assert.False(root.ContainsKey("uiLanguage"));
        Assert.True(root["localOnly"]!.GetValue<bool>());
    }

    // Regel 11: eine einmal gespeicherte Wahl bleibt beim Speichern anderer Einstellungen erhalten.
    [Fact]
    public void Regel11_gespeicherte_Wahl_bleibt_nach_Lesen_und_Speichern()
    {
        string datei = """{"language":"de","uiLanguage":"it"}""";
        DictationSettings gelesen = DictationSettingsJson.Read(datei);

        JsonObject root = MergeObjekt(datei, gelesen with { PushToTalk = true });

        Assert.Equal("it", root["uiLanguage"]!.GetValue<string>());
    }

    // Regel 3: Merge und Read ergeben dieselben Einstellungen, mit und ohne Wahl.
    [Theory]
    [InlineData(null)]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("it")]
    public void Regel3_Rundlauf(string? wahl)
    {
        DictationSettings s = new("{mikro}", "it", 500, true, true, false, false, UiLanguage: wahl);

        Assert.Equal(s, DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s)));
        Assert.Equal(s, DictationSettingsJson.Read(DictationSettingsJson.Merge("""{"uiLanguage":"de","fremd":1}""", s)));
    }

    // Regel 3: Rundlauf mit nicht normalisiertem Wert ergibt den normalisierten Record.
    [Theory]
    [InlineData(" IT ", "it")]
    [InlineData("fr", null)]
    public void Regel3_Rundlauf_normalisiert(string wahl, string? erwartet)
    {
        DictationSettings s = new(UiLanguage: wahl);

        Assert.Equal(erwartet, DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s)).UiLanguage);
        Assert.Equal(s.Normalize(), DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s)));
    }
}
