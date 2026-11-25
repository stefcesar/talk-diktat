// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Localization;

using Xunit;

namespace Talk.Core.Tests.Localization;

/// <summary>
/// Prüft die Oberflächensprachen nur aus dem Vertrag und den Regeln 1, 2 und 10 (Playbook Abschnitt 29).
/// </summary>
public sealed class UiLanguagesTests
{
    // Vertrag und Regel 1: genau de, en, it in dieser Reihenfolge.
    [Fact]
    public void Regel1_All_ist_de_en_it_in_dieser_Reihenfolge()
    {
        IEnumerable<string> all = UiLanguages.All;

        Assert.Equal(new[] { "de", "en", "it" }, all.ToArray());
    }

    // Vertrag und Regel 2: Rückfall ist Englisch.
    [Fact]
    public void Regel2_Fallback_ist_en()
    {
        Assert.Equal("en", UiLanguages.Fallback);
    }

    // Regel 2: Beispiele aus dem Playbook und weitere Grenzfälle.
    [Theory]
    // Beispiele aus Regel 2
    [InlineData("DE", null, "de")]
    [InlineData(null, "DE", "de")]
    [InlineData(null, "de_DE", "de")]
    [InlineData(" it ", null, "it")]
    [InlineData(null, " it ", "it")]
    [InlineData(null, "zh-Hans-CN", "en")]
    [InlineData(null, "-de", "en")]
    [InlineData(null, "fr-FR", "en")]
    [InlineData(null, "rm-CH", "en")]
    [InlineData(null, null, "en")]
    [InlineData(null, "", "en")]
    [InlineData("", "", "en")]
    // Einstellung geht vor Systemsprache
    [InlineData("it", "de-DE", "it")]
    [InlineData("EN", "de-DE", "en")]
    [InlineData(" De\t", "it-IT", "de")]
    // ungültige Einstellung, dann gilt die Systemsprache
    [InlineData("fr", "de-DE", "de")]
    [InlineData("de-CH", "it-IT", "it")]
    [InlineData("de_DE", "it-IT", "it")]
    [InlineData("   ", "it-IT", "it")]
    [InlineData("auto", "en-US", "en")]
    // Systemsprache: Trimmen, Teil vor dem ersten Trenner, Kleinschreibung
    [InlineData(null, " de-AT ", "de")]
    [InlineData(null, "\tde-CH\n", "de")]
    [InlineData(null, "IT-it", "it")]
    [InlineData(null, "en_US", "en")]
    [InlineData(null, "it_CH-x", "it")]
    [InlineData(null, "de-", "de")]
    [InlineData(null, "de_", "de")]
    [InlineData(null, "_it", "en")]
    [InlineData(null, "deu", "en")]
    [InlineData(null, "d", "en")]
    [InlineData(null, "de CH", "en")]
    [InlineData(null, "   ", "en")]
    [InlineData(null, "-", "en")]
    [InlineData("xx", "fr", "en")]
    public void Regel2_Resolve(string? setting, string? systemCulture, string erwartet)
    {
        Assert.Equal(erwartet, UiLanguages.Resolve(setting, systemCulture));
    }

    // Regel 2: das Ergebnis ist immer ein Element von All, Resolve wirft nie.
    [Theory]
    [InlineData(null, null)]
    [InlineData("\0", "\0")]
    [InlineData("{0}", "}{")]
    [InlineData("de ", " it")]
    [InlineData("ÄÖÜ", "ß-DE")]
    [InlineData("------", "______")]
    [InlineData("i̇t", "İT-it")]
    public void Regel2_Resolve_liefert_immer_ein_Element_von_All(string? setting, string? systemCulture)
    {
        string ergebnis = UiLanguages.Resolve(setting, systemCulture);

        Assert.Contains(ergebnis, UiLanguages.All);
    }

    // Regel 2: auch eine sehr lange Eingabe ergibt eine gültige Sprache.
    [Fact]
    public void Regel2_Resolve_mit_langer_Eingabe()
    {
        string lang = new('x', 100_000);

        Assert.Equal("en", UiLanguages.Resolve(lang, lang));
        Assert.Equal("de", UiLanguages.Resolve(lang, "de-" + lang));
    }

    // Vertrag und Regel 10: Name in der eigenen Sprache, Wert vorher getrimmt und klein geschrieben.
    [Theory]
    [InlineData("de", "Deutsch")]
    [InlineData("en", "English")]
    [InlineData("it", "Italiano")]
    [InlineData("DE ", "Deutsch")]
    [InlineData(" It", "Italiano")]
    [InlineData("\tEN\n", "English")]
    [InlineData(null, "English")]
    [InlineData("", "English")]
    [InlineData("  ", "English")]
    [InlineData("fr", "English")]
    [InlineData("de-CH", "English")]
    [InlineData("de_DE", "English")]
    [InlineData("Deutsch", "English")]
    public void Regel10_NativeName(string? code, string erwartet)
    {
        Assert.Equal(erwartet, UiLanguages.NativeName(code));
    }

    // Regel 10: jede Sprache aus All hat einen eigenen Namen.
    [Fact]
    public void Regel10_NativeName_fuer_alle_Sprachen_verschieden()
    {
        string[] namen = UiLanguages.All.Select(UiLanguages.NativeName).ToArray();

        Assert.Equal(new[] { "Deutsch", "English", "Italiano" }, namen);
    }

    // Vertrag: Normalize liefert getrimmt und klein, wenn gültig, sonst null.
    [Theory]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    [InlineData("it", "it")]
    [InlineData(" EN ", "en")]
    [InlineData("It", "it")]
    [InlineData("\tDE\r\n", "de")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("fr", null)]
    [InlineData("de-CH", null)]
    [InlineData("de_DE", null)]
    [InlineData("auto", null)]
    [InlineData("d e", null)]
    public void Vertrag_Normalize(string? code, string? erwartet)
    {
        Assert.Equal(erwartet, UiLanguages.Normalize(code));
    }

    // Vertrag: Normalize ist mit Resolve verträglich, eine normalisierte Einstellung gilt immer.
    [Fact]
    public void Regel2_Normalize_und_Resolve_passen_zusammen()
    {
        foreach (string sprache in UiLanguages.All)
        {
            Assert.Equal(sprache, UiLanguages.Normalize(sprache.ToUpperInvariant()));
            Assert.Equal(sprache, UiLanguages.Resolve(UiLanguages.Normalize(" " + sprache + " "), "fr-FR"));
        }
    }
}
