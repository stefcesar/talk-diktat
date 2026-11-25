// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

using Talk.Core.Localization;

using Xunit;

namespace Talk.Core.Tests.Localization;

/// <summary>
/// Prüft die Übersetzungstabelle nur aus dem Vertrag und den Regeln 4 bis 7 (Playbook Abschnitt 29).
/// </summary>
public sealed class UiTextTests
{
    private static readonly string[] Sprachen = ["de", "en", "it"];

    private static readonly string[] ErlaubteErsteGlieder =
        ["tray", "settings", "history", "wordfix", "key", "overlay", "diag", "msg", "lang", "common"];

    private static readonly Regex Schluesselmuster = new(@"^[a-z][a-z0-9_]*(\.[a-z0-9_]+)+$", RegexOptions.CultureInvariant);

    private static string[] AlleSchluessel()
    {
        IEnumerable<string> keys = UiText.Keys;
        return keys.ToArray();
    }

    public static TheoryData<string> Schluessel()
    {
        TheoryData<string> daten = [];
        foreach (string key in AlleSchluessel())
        {
            daten.Add(key);
        }

        return daten;
    }

    private static int[]? Nummern(string? text)
    {
        IEnumerable<int>? n = UiText.PlaceholderNumbers(text);
        return n?.Distinct().OrderBy(x => x).ToArray();
    }

    // Regel 4: Schlüssel

    // Regel 4: Keys ist nicht leer.
    [Fact]
    public void Regel4_Keys_ist_nicht_leer()
    {
        Assert.NotEmpty(AlleSchluessel());
    }

    // Regel 4: ohne Doppel, ordinal verglichen.
    [Fact]
    public void Regel4_Keys_ohne_Doppel()
    {
        string[] keys = AlleSchluessel();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    // Regel 4: kein Schlüssel ist null oder leer.
    [Fact]
    public void Regel4_Keys_nicht_leer_und_nicht_null()
    {
        Assert.All(AlleSchluessel(), k => Assert.False(string.IsNullOrWhiteSpace(k)));
    }

    // Regel 4: Muster und erlaubtes erstes Glied.
    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Regel4_Schluessel_folgt_Muster_und_erstem_Glied(string key)
    {
        Assert.Matches(Schluesselmuster, key);
        Assert.Contains(key.Split('.')[0], ErlaubteErsteGlieder);
    }

    // Regel 4: das Muster selbst, damit der Test oben nicht ins Leere prüft.
    [Theory]
    [InlineData("common.close", true)]
    [InlineData("a.b.c", true)]
    [InlineData("lang.de_ch", true)]
    [InlineData("x1.2", true)]
    [InlineData("common", false)]
    [InlineData("Common.close", false)]
    [InlineData("common.Close", false)]
    [InlineData("1common.close", false)]
    [InlineData("common..close", false)]
    [InlineData("common.close.", false)]
    [InlineData(".common.close", false)]
    [InlineData("common.clo-se", false)]
    [InlineData("common.close ", false)]
    public void Regel4_Gegenprobe_des_Musters(string key, bool gueltig)
    {
        Assert.Equal(gueltig, Schluesselmuster.IsMatch(key));
    }

    // Regel 4 und 7: Has kennt jeden Schlüssel aus Keys.
    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Regel4_Has_fuer_jeden_Schluessel(string key)
    {
        Assert.True(UiText.Has(key));
    }

    // Regel 4: ordinaler Vergleich, Groß und Klein zählt, Leerraum gehört zum Schlüssel.
    [Theory]
    [InlineData("COMMON.CLOSE")]
    [InlineData("Common.close")]
    [InlineData("common.Close")]
    [InlineData(" common.close")]
    [InlineData("common.close ")]
    [InlineData("common.closee")]
    [InlineData("common")]
    [InlineData("gibt.es.nicht")]
    public void Regel4_Has_vergleicht_ordinal(string key)
    {
        Assert.False(UiText.Has(key));
    }

    // Regel 5: Vollständigkeit

    // Regel 5: alle drei Fassungen nicht leer und ohne Randleerraum.
    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Regel5_drei_Fassungen_nicht_leer_ohne_Randleerraum(string key)
    {
        foreach (string sprache in Sprachen)
        {
            string text = UiText.Get(key, sprache);

            Assert.False(string.IsNullOrWhiteSpace(text), $"{key} [{sprache}] ist leer");
            Assert.Equal(text.Trim(), text);
        }
    }

    // Regel 5: lang. deckt alle Diktiersprachen und Dialekte ab, sobald es lang. Schlüssel gibt (ab Schritt 3).
    [Fact]
    public void Regel5_lang_vollstaendig_sobald_vorhanden()
    {
        string[] lang = AlleSchluessel().Where(k => k.StartsWith("lang.", StringComparison.Ordinal)).ToArray();
        if (lang.Length == 0)
        {
            return;
        }

        string[] codes = ["de", "it", "en", "de-ch", "gsw", "zh", "be", "bs", "mix"];
        foreach (string code in codes)
        {
            string erwartet = "lang." + code.Replace('-', '_');
            Assert.Contains(erwartet, lang);
        }
    }

    // Regel 6: Platzhalter in der Tabelle

    // Regel 6: je Fassung gültige Platzhalter, in allen drei dieselbe Menge, lückenlos ab 0.
    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Regel6_Platzhalter_je_Schluessel(string key)
    {
        int[]? de = Nummern(UiText.Get(key, "de"));
        int[]? en = Nummern(UiText.Get(key, "en"));
        int[]? it = Nummern(UiText.Get(key, "it"));

        Assert.NotNull(de);
        Assert.NotNull(en);
        Assert.NotNull(it);
        Assert.Equal(de, en);
        Assert.Equal(de, it);
        Assert.Equal(Enumerable.Range(0, de!.Length).ToArray(), de);
    }

    // Vertrag: Startschlüssel

    [Theory]
    [InlineData("common.close", "Schließen", "Close", "Chiudi")]
    [InlineData("history.count.newest", "Die neuesten {0} Einträge, neuester zuerst", "The newest {0} entries, newest first", "Le {0} voci più recenti, prima la più recente")]
    [InlineData("common.name_with_detail", "{0} ({1})", "{0} ({1})", "{0} ({1})")]
    public void Vertrag_Startschluessel(string key, string de, string en, string it)
    {
        Assert.Contains(key, AlleSchluessel());
        Assert.True(UiText.Has(key));
        Assert.Equal(de, UiText.Get(key, "de"));
        Assert.Equal(en, UiText.Get(key, "en"));
        Assert.Equal(it, UiText.Get(key, "it"));
    }

    // Regel 7: Get

    // Regel 7: Sprache getrimmt und klein; sonst gilt en.
    [Theory]
    [InlineData("de", "Schließen")]
    [InlineData("en", "Close")]
    [InlineData("it", "Chiudi")]
    [InlineData("DE", "Schließen")]
    [InlineData(" It ", "Chiudi")]
    [InlineData("\tEN\n", "Close")]
    [InlineData(null, "Close")]
    [InlineData("", "Close")]
    [InlineData("   ", "Close")]
    [InlineData("de-CH", "Close")]
    [InlineData("de_DE", "Close")]
    [InlineData("it-IT", "Close")]
    [InlineData("fr", "Close")]
    [InlineData("Deutsch", "Close")]
    public void Regel7_Get_Sprache(string? sprache, string erwartet)
    {
        Assert.Equal(erwartet, UiText.Get("common.close", sprache));
    }

    // Regel 7: unbekannter Schlüssel ergibt den Schlüssel selbst, in jeder Sprache.
    [Theory]
    [InlineData("gibt.es.nicht", "de")]
    [InlineData("gibt.es.nicht", "it")]
    [InlineData("gibt.es.nicht", null)]
    [InlineData("COMMON.CLOSE", "de")]
    [InlineData("common.close ", "de")]
    [InlineData("x", "en")]
    [InlineData("Text mit {0}", "de")]
    public void Regel7_Get_unbekannter_Schluessel_liefert_den_Schluessel(string key, string? sprache)
    {
        Assert.Equal(key, UiText.Get(key, sprache));
    }

    // Regel 7: null oder leerer Schlüssel ergibt ?.
    [Theory]
    [InlineData(null, "de")]
    [InlineData("", "de")]
    [InlineData(null, null)]
    [InlineData("", "xx")]
    public void Regel7_Get_null_oder_leer_liefert_Fragezeichen(string? key, string? sprache)
    {
        Assert.Equal("?", UiText.Get(key, sprache));
    }

    // Regel 7: Has(null) und Has("") sind false.
    [Fact]
    public void Regel7_Has_null_und_leer()
    {
        Assert.False(UiText.Has(null));
        Assert.False(UiText.Has(""));
    }

    // Regel 7: Get ist nie leer, für jeden Schlüssel und jede beliebige Sprache.
    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Regel7_Get_nie_leer_auch_bei_fremder_Sprache(string key)
    {
        foreach (string? sprache in new[] { null, "", "fr", "de-CH", "\0" })
        {
            string text = UiText.Get(key, sprache);
            Assert.False(string.IsNullOrEmpty(text));
            Assert.Equal(UiText.Get(key, "en"), text);
        }
    }

    // Regel 7: Format

    // Regel 7: Platzhalter werden durch die Argumente ersetzt.
    [Theory]
    [InlineData("de", "Die neuesten 50 Einträge, neuester zuerst")]
    [InlineData("en", "The newest 50 entries, newest first")]
    [InlineData("it", "Le 50 voci più recenti, prima la più recente")]
    [InlineData("fr", "The newest 50 entries, newest first")]
    [InlineData(" DE ", "Die neuesten 50 Einträge, neuester zuerst")]
    public void Regel7_Format_ersetzt_Platzhalter(string sprache, string erwartet)
    {
        Assert.Equal(erwartet, UiText.Format("history.count.newest", sprache, "50"));
    }

    // Regel 7: zwei Platzhalter in Reihenfolge.
    [Fact]
    public void Regel7_Format_zwei_Platzhalter()
    {
        Assert.Equal("Word (Notepad)", UiText.Format("common.name_with_detail", "en", "Word", "Notepad"));
    }

    // Regel 7: mehr Argumente als Platzhalter werden ignoriert.
    [Fact]
    public void Regel7_Format_ignoriert_ueberzaehlige_Argumente()
    {
        Assert.Equal("a (b)", UiText.Format("common.name_with_detail", "de", "a", "b", "c", "d"));
        Assert.Equal("Die neuesten 7 Einträge, neuester zuerst", UiText.Format("history.count.newest", "de", "7", "8"));
        Assert.Equal("Chiudi", UiText.Format("common.close", "it", "x"));
    }

    // Regel 7: zu wenige Argumente ergeben die unveränderte Fassung.
    [Fact]
    public void Regel7_Format_zu_wenige_Argumente()
    {
        Assert.Equal("{0} ({1})", UiText.Format("common.name_with_detail", "de", "a"));
        Assert.Equal("{0} ({1})", UiText.Format("common.name_with_detail", "de"));
        Assert.Equal("Die neuesten {0} Einträge, neuester zuerst", UiText.Format("history.count.newest", "de"));
        Assert.Equal("Die neuesten {0} Einträge, neuester zuerst", UiText.Format("history.count.newest", "de", []));
    }

    // Regel 7: args null ergibt die unveränderte Fassung.
    [Fact]
    public void Regel7_Format_args_null()
    {
        Assert.Equal("{0} ({1})", UiText.Format("common.name_with_detail", "en", null));
        Assert.Equal("The newest {0} entries, newest first", UiText.Format("history.count.newest", "en", (string?[]?)null));
        Assert.Equal("Close", UiText.Format("common.close", "en", null));
    }

    // Regel 7: ein null Argument wird leerer Text.
    [Fact]
    public void Regel7_Format_null_Argument_wird_leer()
    {
        Assert.Equal(" (b)", UiText.Format("common.name_with_detail", "de", null, "b"));
        Assert.Equal("a ()", UiText.Format("common.name_with_detail", "de", "a", null));
        Assert.Equal("Die neuesten  Einträge, neuester zuerst", UiText.Format("history.count.newest", "de", new string?[] { null }));
    }

    // Regel 7: Argumente mit Klammern führen zu keiner Ausnahme und werden nicht erneut ersetzt.
    [Fact]
    public void Regel7_Format_Argumente_mit_Klammern()
    {
        Assert.Equal("{ ({{)", UiText.Format("common.name_with_detail", "de", "{", "{{"));
        Assert.Equal("} (}})", UiText.Format("common.name_with_detail", "de", "}", "}}"));
        Assert.Equal("{1} (b)", UiText.Format("common.name_with_detail", "de", "{1}", "b"));
        Assert.Equal("{0:N0} ({0,5})", UiText.Format("common.name_with_detail", "de", "{0:N0}", "{0,5}"));
    }

    // Regel 7: Format mit unbekanntem, null oder leerem Schlüssel wie Get, ohne Ausnahme.
    [Fact]
    public void Regel7_Format_unbekannter_und_leerer_Schluessel()
    {
        Assert.Equal("gibt.es.nicht", UiText.Format("gibt.es.nicht", "de", "a"));
        Assert.Equal("gibt.es.nicht", UiText.Format("gibt.es.nicht", "de", null));
        Assert.Equal("?", UiText.Format(null, "de", "a"));
        Assert.Equal("?", UiText.Format("", null, null));
    }

    // Regel 7: Format ist für jeden Schlüssel und jede Sprache ohne Argumente gleich Get, mit genug Argumenten nie leer.
    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Regel7_Format_ohne_Ausnahme_fuer_alle_Schluessel(string key)
    {
        foreach (string sprache in Sprachen)
        {
            string fassung = UiText.Get(key, sprache);
            Assert.Equal(fassung, UiText.Format(key, sprache, null));

            int[] n = Nummern(fassung) ?? [];
            string?[] args = Enumerable.Range(0, n.Length).Select(i => "W" + i).ToArray();
            string ergebnis = UiText.Format(key, sprache, args);

            Assert.False(string.IsNullOrEmpty(ergebnis));
            foreach (int i in n)
            {
                Assert.DoesNotContain("{" + i + "}", ergebnis);
                Assert.Contains("W" + i, ergebnis);
            }
        }
    }

    // Regel 6: PlaceholderNumbers selbst

    // Regel 6: gültige Texte liefern die Menge der Nummern.
    [Theory]
    [InlineData("", new int[0])]
    [InlineData("ohne Platzhalter", new int[0])]
    [InlineData("Schließen (ß, ä, più)", new int[0])]
    [InlineData("{0}", new[] { 0 })]
    [InlineData("a {0} b", new[] { 0 })]
    [InlineData("{0} und {0}", new[] { 0 })]
    [InlineData("{0} ({1})", new[] { 0, 1 })]
    [InlineData("{1} vor {0}", new[] { 0, 1 })]
    [InlineData("{0}{1}", new[] { 0, 1 })]
    [InlineData("{2}{0}{1}{0}", new[] { 0, 1, 2 })]
    [InlineData("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    public void Regel6_PlaceholderNumbers_gueltig(string text, int[] erwartet)
    {
        Assert.Equal(erwartet, Nummern(text));
    }

    // Regel 6: verbotene Texte liefern null.
    [Theory]
    [InlineData("{0:N0}")]
    [InlineData("{0,5}")]
    [InlineData("{0,-5}")]
    [InlineData("{0:D}")]
    [InlineData("{{")]
    [InlineData("}}")]
    [InlineData("{{0}}")]
    [InlineData("a {{ b")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("a}b")]
    [InlineData("a{b")]
    [InlineData("{x}")]
    [InlineData("{}")]
    [InlineData("{ 0}")]
    [InlineData("{0 }")]
    [InlineData("{-1}")]
    [InlineData("{+1}")]
    [InlineData("{0")]
    [InlineData("0}")]
    [InlineData("{0}}")]
    [InlineData("{{0}")]
    [InlineData("{0} und {x}")]
    [InlineData("{0} }")]
    [InlineData("{a.b}")]
    public void Regel6_PlaceholderNumbers_ungueltig(string text)
    {
        Assert.Null(UiText.PlaceholderNumbers(text));
    }

    // Regel 6: Lücke in den Nummern. Ob PlaceholderNumbers hier null liefert oder die Menge, lässt der Vertrag offen;
    // die Tabellenprüfung oben verlangt in jedem Fall lückenlos ab 0.
    [Theory]
    [InlineData("{1}", new[] { 1 })]
    [InlineData("{0} {2}", new[] { 0, 2 })]
    public void Regel6_PlaceholderNumbers_mit_Luecke_ist_die_Menge(string text, int[] menge)
    {
        int[]? n = Nummern(text);

        Assert.NotNull(n);
        Assert.Equal(menge, n);
    }

    // Regel 6: null und leerer Text ergeben die leere Menge.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Regel6_PlaceholderNumbers_null_und_leer_ist_leere_Menge(string? text)
    {
        int[]? n = Nummern(text);

        Assert.NotNull(n);
        Assert.Empty(n);
    }

    // Regel 6: führende Nullen gelten als dieselbe Nummer; mehr als sechs Ziffern sind ein Verstoß.
    [Theory]
    [InlineData("{01}", new[] { 1 })]
    [InlineData("{000000}", new[] { 0 })]
    public void Regel6_PlaceholderNumbers_fuehrende_Nullen(string text, int[] menge)
    {
        Assert.Equal(menge, Nummern(text));
    }

    // Regel 6 und 7: sehr lange Ziffernfolgen werfen nie.
    [Fact]
    public void Regel6_und_7_lange_Ziffern_werfen_nicht()
    {
        Assert.Null(Nummern("{99999999999}"));
        Assert.Null(Nummern("{0000001}"));
        Assert.Equal("{9999999999}", UiText.Format("{9999999999}", "de", "a"));
    }

    // Regel 7: Schlüssel nur aus Leerraum ist unbekannt und kommt selbst zurück; Format mit unbekanntem Schlüssel ersetzt darin.
    [Fact]
    public void Regel7_Leerraum_und_unbekannter_Schluessel()
    {
        Assert.Equal("   ", UiText.Get("   ", "de"));
        Assert.False(UiText.Has("   "));
        Assert.Equal("nicht.da", UiText.Format("nicht.da", "de", "x"));
    }
}
