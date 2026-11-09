// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;

using Talk.Core.Processing;
using Talk.Core.Settings;

using Xunit;

namespace Talk.Core.Tests.Settings;

/// <summary>
/// Prüft die Einstellungen für Schweizerdeutsch (Playbook Abschnitt 26, Schritt 1, Regeln R1 bis R4):
/// zusätzliche Sprachen "de-ch" und "gsw", der Dialekt als eigenes Feld und der JSON Schlüssel "dialect".
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class SchweizerdeutschSettingsTests
{
    // R1: Die Sprachliste ist genau de, it, en, de-ch, gsw in dieser Reihenfolge.
    [Fact]
    public void R1_Sprachliste_in_fester_Reihenfolge()
    {
        Assert.Equal(new[] { "de", "it", "en", "de-ch", "gsw" }, DictationSettings.Languages);
    }

    // R2: de-ch und gsw bleiben erhalten, vorher wird gekürzt und kleingeschrieben.
    [Theory]
    [InlineData("de-ch", "de-ch")]
    [InlineData("gsw", "gsw")]
    [InlineData("  DE-CH ", "de-ch")]
    [InlineData("GSW", "gsw")]
    [InlineData(" Gsw ", "gsw")]
    [InlineData("De-Ch", "de-ch")]
    [InlineData("de", "de")]
    [InlineData(" IT ", "it")]
    [InlineData("en", "en")]
    public void R2_Normalize_behaelt_Schweizer_Sprachen(string sprache, string erwartet)
    {
        Assert.Equal(erwartet, new DictationSettings(Language: sprache).Normalize().Language);
    }

    // R2: Unbekannte Sprachen werden "de".
    [Theory]
    [InlineData("de_ch")]
    [InlineData("ch")]
    [InlineData("swiss")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void R2_Normalize_macht_Unbekanntes_zu_de(string? sprache)
    {
        Assert.Equal("de", new DictationSettings(Language: sprache!).Normalize().Language);
    }

    // R3: Vorgabe des Dialekts ist "mix", die Konstante heißt DefaultDialect.
    [Fact]
    public void R3_Dialekt_Vorgabe_ist_mix()
    {
        Assert.Equal("mix", DictationSettings.DefaultDialect);
        Assert.Equal("mix", new DictationSettings().Dialect);
        Assert.Equal("mix", new DictationSettings().Normalize().Dialect);
    }

    // R3: Die Dialektliste ist genau zh, be, bs, mix in dieser Reihenfolge.
    [Fact]
    public void R3_Dialektliste_in_fester_Reihenfolge()
    {
        Assert.Equal(new[] { "zh", "be", "bs", "mix" }, DictationSettings.Dialects);
    }

    // R3: Dialekt ist der letzte Positionsparameter, nach DuckingEnabled.
    [Fact]
    public void R3_Dialekt_ist_letzter_Positionsparameter()
    {
        DictationSettings s = new(null, "de-ch", 350, false, false, true, true, true, false, SmoothingLevel.Smooth, null, true, "zh");

        Assert.True(s.DuckingEnabled);
        Assert.Equal("zh", s.Dialect);
    }

    // R3: Normalize kürzt und schreibt den Dialekt klein.
    [Theory]
    [InlineData("zh", "zh")]
    [InlineData("be", "be")]
    [InlineData("bs", "bs")]
    [InlineData("mix", "mix")]
    [InlineData("BE", "be")]
    [InlineData("  Zh ", "zh")]
    [InlineData(" MIX ", "mix")]
    public void R3_Normalize_behaelt_bekannte_Dialekte(string dialekt, string erwartet)
    {
        Assert.Equal(erwartet, new DictationSettings(Dialect: dialekt).Normalize().Dialect);
    }

    // R3: Unbekannter, leerer oder fehlender Dialekt wird "mix".
    [Theory]
    [InlineData("lu")]
    [InlineData("bern")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void R3_Normalize_macht_unbekannten_Dialekt_zu_mix(string? dialekt)
    {
        Assert.Equal("mix", new DictationSettings(Dialect: dialekt!).Normalize().Dialect);
    }

    // R3: Der Dialekt bleibt unabhängig von der Sprache gespeichert.
    [Theory]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("de-ch")]
    [InlineData("gsw")]
    public void R3_Dialekt_unabhaengig_von_der_Sprache(string sprache)
    {
        DictationSettings s = new DictationSettings(Language: sprache, Dialect: "be").Normalize();

        Assert.Equal(sprache, s.Language);
        Assert.Equal("be", s.Dialect);
    }

    // R4: Fehlt der Schlüssel dialect, liest Read "mix".
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"language":"gsw"}""")]
    public void R4_Read_ohne_dialect_liefert_mix(string json)
    {
        Assert.Equal("mix", DictationSettingsJson.Read(json).Dialect);
    }

    // R4: Falscher Typ oder unbekannter Wert liefert "mix", Großschreibung wird klein.
    [Theory]
    [InlineData("""{"dialect":3}""", "mix")]
    [InlineData("""{"dialect":true}""", "mix")]
    [InlineData("""{"dialect":null}""", "mix")]
    [InlineData("""{"dialect":"BE"}""", "be")]
    [InlineData("""{"dialect":" zh "}""", "zh")]
    [InlineData("""{"dialect":"bs"}""", "bs")]
    [InlineData("""{"dialect":"wallis"}""", "mix")]
    [InlineData("""{"dialect":""}""", "mix")]
    public void R4_Read_liest_dialect(string json, string erwartet)
    {
        Assert.Equal(erwartet, DictationSettingsJson.Read(json).Dialect);
    }

    // R4: Read liest die Schweizer Sprachen.
    [Theory]
    [InlineData("""{"language":"de-ch"}""", "de-ch")]
    [InlineData("""{"language":"GSW"}""", "gsw")]
    [InlineData("""{"language":"de_ch"}""", "de")]
    public void R4_Read_liest_Schweizer_Sprachen(string json, string erwartet)
    {
        Assert.Equal(erwartet, DictationSettingsJson.Read(json).Language);
    }

    // R4: de-ch und gsw samt Dialekt überstehen Merge und Read.
    [Theory]
    [InlineData("de-ch", "zh")]
    [InlineData("gsw", "be")]
    [InlineData("gsw", "mix")]
    [InlineData("de", "bs")]
    public void R4_Merge_und_Read_ergeben_dieselben_Werte(string sprache, string dialekt)
    {
        DictationSettings s = new(Language: sprache, Dialect: dialekt);

        DictationSettings gelesen = DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s));

        Assert.Equal(sprache, gelesen.Language);
        Assert.Equal(dialekt, gelesen.Dialect);
        Assert.Equal(s, gelesen);
    }

    // R4: Merge schreibt dialect immer, auch beim Vorgabewert.
    [Fact]
    public void R4_Merge_schreibt_dialect_immer()
    {
        string neu = DictationSettingsJson.Merge(null, new DictationSettings())!;

        JsonObject root = JsonNode.Parse(neu)!.AsObject();
        Assert.True(root.ContainsKey("dialect"));
        Assert.Equal("mix", root["dialect"]!.GetValue<string>());
    }

    // R4: Merge schreibt den normalisierten Dialekt.
    [Theory]
    [InlineData("BE", "be")]
    [InlineData("  zh ", "zh")]
    [InlineData("wallis", "mix")]
    [InlineData("", "mix")]
    public void R4_Merge_schreibt_den_normalisierten_Dialekt(string dialekt, string erwartet)
    {
        string neu = DictationSettingsJson.Merge(null, new DictationSettings(Dialect: dialekt))!;

        Assert.Equal(erwartet, JsonNode.Parse(neu)!.AsObject()["dialect"]!.GetValue<string>());
    }

    // R4: Merge ersetzt einen vorhandenen dialect und behält fremde Schlüssel.
    [Fact]
    public void R4_Merge_behaelt_fremde_Schluessel_und_ersetzt_dialect()
    {
        string vorhanden = """{"modelsDirectory":"F:\\Modelle","dialect":"zh","language":"de"}""";

        string neu = DictationSettingsJson.Merge(vorhanden, new DictationSettings(Language: "gsw", Dialect: "bs"))!;
        JsonObject root = JsonNode.Parse(neu)!.AsObject();

        Assert.Equal("F:\\Modelle", root["modelsDirectory"]!.GetValue<string>());
        Assert.Equal("bs", root["dialect"]!.GetValue<string>());
        Assert.Equal("gsw", root["language"]!.GetValue<string>());
    }
}
