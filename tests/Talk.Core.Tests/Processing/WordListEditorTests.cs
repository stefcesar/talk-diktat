// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft den Wörterbuch-Editor ausschließlich gegen die vorgegebenen Regeln 1 bis 8 (Byte-Treue, Schlüsselvergleich,
/// Anhängen neuer Wörter, Entfernen, Ablehnen ungültiger Eingaben, SplitVariants, Hint, Entries).
/// Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class WordListEditorTests
{
    private const string Kopf = "# Talk Wörterbuch\r\n# Cesaretti steht hier nur im Kommentar\r\n";

    // Bestehende Datei mit Kommentarkopf, CRLF und Leerzeile.
    private const string CrlfDatei =
        Kopf +
        "\r\n" +
        "LUMÍORA = Lumi Ora\r\n" +
        "Cesaretti\r\n" +
        "Next.js = next js\r\n";

    private static void PruefeUngueltig(WordListEditResult ergebnis, string vorher)
    {
        Assert.Equal(WordListChange.Invalid, ergebnis.Change);
        Assert.False(ergebnis.Changed);
        Assert.Equal(vorher, ergebnis.Content);
        Assert.False(string.IsNullOrWhiteSpace(ergebnis.Problem));
    }

    // ---------- Changed ----------

    // Changed ist nur bei Added, Extended, Removed wahr.
    [Theory]
    [InlineData(WordListChange.Added, true)]
    [InlineData(WordListChange.Extended, true)]
    [InlineData(WordListChange.Removed, true)]
    [InlineData(WordListChange.Unchanged, false)]
    [InlineData(WordListChange.NotFound, false)]
    [InlineData(WordListChange.Invalid, false)]
    public void Changed_nur_bei_echten_Aenderungen(WordListChange change, bool erwartet)
    {
        Assert.Equal(erwartet, new WordListEditResult("x", change).Changed);
    }

    [Fact]
    public void MaxLength_ist_80()
    {
        Assert.Equal(80, WordListEditor.MaxLength);
        Assert.False(string.IsNullOrEmpty(WordListEditor.NewFileHeader));
    }

    // ---------- Add: neues Wort (Regel 3) ----------

    // Regel 1 und 3: neues Wort ans Ende einer CRLF-Datei, alles davor bleibt byte-gleich.
    [Fact]
    public void Add_neues_Wort_an_CRLF_Datei()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "Lüneburg", null);

        Assert.Equal(WordListChange.Added, ergebnis.Change);
        Assert.True(ergebnis.Changed);
        Assert.Null(ergebnis.Problem);
        Assert.Equal(CrlfDatei + "Lüneburg\r\n", ergebnis.Content);
    }

    // Regel 3: Varianten werden mit " | " verbunden.
    [Fact]
    public void Add_neues_Wort_mit_Varianten()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "Hannover", ["Hanover", "Hanno fer"]);

        Assert.Equal(WordListChange.Added, ergebnis.Change);
        Assert.Equal(CrlfDatei + "Hannover = Hanover | Hanno fer\r\n", ergebnis.Content);
    }

    // Regel 3: Eingaben werden getrimmt.
    [Fact]
    public void Add_trimmt_Eingaben()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "  Hannover  ", ["  Hanover  "]);

        Assert.Equal(CrlfDatei + "Hannover = Hanover\r\n", ergebnis.Content);
    }

    // Regel 3: LF-Datei bekommt LF.
    [Fact]
    public void Add_an_LF_Datei_nutzt_LF()
    {
        const string datei = "# Kopf\nCesaretti\n";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "Hannover", ["Hanover"]);

        Assert.Equal(WordListChange.Added, ergebnis.Change);
        Assert.Equal(datei + "Hannover = Hanover\n", ergebnis.Content);
    }

    // Regel 3: ohne Zeilenende am Schluss wird zuerst eines im Stil der Datei ergänzt.
    [Fact]
    public void Add_an_CRLF_Datei_ohne_Schlusszeilenende()
    {
        const string datei = "# Kopf\r\nCesaretti";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "Hannover", null);

        Assert.Equal("# Kopf\r\nCesaretti\r\nHannover\r\n", ergebnis.Content);
    }

    [Fact]
    public void Add_an_LF_Datei_ohne_Schlusszeilenende()
    {
        const string datei = "# Kopf\nCesaretti";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "Hannover", null);

        Assert.Equal("# Kopf\nCesaretti\nHannover\n", ergebnis.Content);
    }

    // Regel 3: Datei ohne jedes Zeilenende bekommt CRLF.
    [Fact]
    public void Add_an_Datei_ohne_Zeilenumbruch_nutzt_CRLF()
    {
        WordListEditResult ergebnis = WordListEditor.Add("Cesaretti", "Hannover", null);

        Assert.Equal("Cesaretti\r\nHannover\r\n", ergebnis.Content);
    }

    // Regel 3: leere oder fehlende Datei bekommt CRLF; die neue Zeile steht am Ende und endet mit CRLF.
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Add_an_leere_Datei_nutzt_CRLF(string? datei)
    {
        WordListEditResult ergebnis = WordListEditor.Add(datei, "Hannover", ["Hanover"]);

        Assert.Equal(WordListChange.Added, ergebnis.Change);
        Assert.EndsWith("Hannover = Hanover\r\n", ergebnis.Content);
        Assert.DoesNotContain("\n", ergebnis.Content.Replace("\r\n", ""));
        VocabularyEntry eintrag = Assert.Single(WordListEditor.Entries(ergebnis.Content));
        Assert.Equal("Hannover", eintrag.Term);
        Assert.Equal(["Hanover"], eintrag.Aliases.ToArray());
    }

    // Regel 5: Variante mit gleichem Schlüssel wie das Wort fällt still weg, Doppelte nur einmal, leere fallen weg.
    [Fact]
    public void Add_entfernt_leere_doppelte_und_wortgleiche_Varianten()
    {
        WordListEditResult ergebnis = WordListEditor.Add(
            CrlfDatei, "Lüneburg", ["lueneburg", "", "  ", "Luene Burg", "lüne burg", "Lineburg"]);

        // "lüne burg" hat den Schlüssel des Worts und fällt weg; "lueneburg" und "Luene Burg" teilen einen Schlüssel.
        Assert.Equal(WordListChange.Added, ergebnis.Change);
        Assert.StartsWith(CrlfDatei, ergebnis.Content);
        VocabularyEntry neu = WordListEditor.Entries(ergebnis.Content)[^1];
        Assert.Equal("Lüneburg", neu.Term);
        string termKey = VocabularyCorrector.Key("Lüneburg");
        Assert.DoesNotContain(neu.Aliases, a => VocabularyCorrector.Key(a) == termKey);
        Assert.DoesNotContain(neu.Aliases, string.IsNullOrWhiteSpace);
        Assert.Equal(neu.Aliases.Count, neu.Aliases.Select(VocabularyCorrector.Key).Distinct().Count());
        Assert.Single(neu.Aliases, a => VocabularyCorrector.Key(a) == VocabularyCorrector.Key("Luene Burg"));
        Assert.Contains("Lineburg", neu.Aliases);
    }

    // Regel 5: eindeutige Fassung der Doppelte-Regel.
    [Fact]
    public void Add_doppelte_Varianten_nur_einmal_wortgleiche_weg()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "Hannover", ["Hanover", "hanover", "HANNOVER", "hannöver"]);

        // "hannöver" hat den Schlüssel "hannover" und fällt als wortgleich weg.
        Assert.Equal(CrlfDatei + "Hannover = Hanover\r\n", ergebnis.Content);
    }

    // ---------- Add: vorhandenes Wort (Regel 2) ----------

    // Regel 2: vorhanden ohne neue Variante -> Unchanged, Inhalt identisch.
    [Fact]
    public void Add_vorhandenes_Wort_ohne_Neues_ist_Unchanged()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "Cesaretti", null);

        Assert.Equal(WordListChange.Unchanged, ergebnis.Change);
        Assert.False(ergebnis.Changed);
        Assert.Equal(CrlfDatei, ergebnis.Content);
    }

    // Regel 2: Groß/Klein und Akzente zählen nicht; "lumiora" ist "LUMÍORA".
    [Fact]
    public void Add_lumiora_klein_ist_vorhanden()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "lumiora", null);

        Assert.Equal(WordListChange.Unchanged, ergebnis.Change);
        Assert.Equal(CrlfDatei, ergebnis.Content);
    }

    // Regel 2: vorhandene Variante (per Schlüssel) wird nicht doppelt angehängt.
    [Fact]
    public void Add_vorhandene_Variante_per_Schluessel_ist_Unchanged()
    {
        const string datei = "# Kopf\r\nLUMÍORA = Lumiohra\r\nCesaretti\r\n";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "lumiora", ["LUMIOHRA", "lumi-ohra"]);

        Assert.Equal(WordListChange.Unchanged, ergebnis.Change);
        Assert.Equal(datei, ergebnis.Content);
    }

    // Regel 2: Zeile mit "=" wird mit " | v" erweitert, Schreibweise "LUMÍORA" bleibt.
    [Fact]
    public void Add_erweitert_Zeile_mit_Gleichheitszeichen()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "lumiora", ["Lumi Ora", "Lumiohra", "Lu Mi Ora"]);

        Assert.Equal(WordListChange.Extended, ergebnis.Change);
        Assert.True(ergebnis.Changed);
        // "Lu Mi Ora" hat den Schlüssel "lumiora" und gleicht damit dem Wort.
        Assert.Equal(CrlfDatei.Replace("LUMÍORA = Lumi Ora\r\n", "LUMÍORA = Lumi Ora | Lumiohra\r\n"), ergebnis.Content);
    }

    // Regel 2: Zeile ohne "=" bekommt " = v1 | v2"; nur genau diese Zeile ändert sich.
    [Fact]
    public void Add_erweitert_Zeile_ohne_Gleichheitszeichen()
    {
        WordListEditResult ergebnis = WordListEditor.Add(CrlfDatei, "CESARETTI", ["Tschesaretti", "Chesaretti"]);

        Assert.Equal(WordListChange.Extended, ergebnis.Change);
        Assert.Equal(
            CrlfDatei.Replace("\r\nCesaretti\r\n", "\r\nCesaretti = Tschesaretti | Chesaretti\r\n"),
            ergebnis.Content);
        // Kommentar mit "Cesaretti" bleibt unberührt.
        Assert.StartsWith(Kopf, ergebnis.Content);
    }

    // Regel 2 und 1: letzte Zeile ohne Zeilenende erweitern, es kommt kein Zeilenende dazu.
    [Fact]
    public void Add_erweitert_letzte_Zeile_ohne_Zeilenende()
    {
        const string datei = "# Kopf\nCesaretti";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "cesaretti", ["Chesaretti"]);

        Assert.Equal(WordListChange.Extended, ergebnis.Change);
        Assert.Equal("# Kopf\nCesaretti = Chesaretti", ergebnis.Content);
    }

    // Regel 2: Umlaut und ß: "Strasse" ist "Straße".
    [Fact]
    public void Add_Strasse_ist_Strasze()
    {
        const string datei = "Straße\r\n";

        WordListEditResult gleich = WordListEditor.Add(datei, "Strasse", null);
        Assert.Equal(WordListChange.Unchanged, gleich.Change);
        Assert.Equal(datei, gleich.Content);

        WordListEditResult erweitert = WordListEditor.Add(datei, "STRASSE", ["Strase"]);
        Assert.Equal(WordListChange.Extended, erweitert.Change);
        Assert.Equal("Straße = Strase\r\n", erweitert.Content);
    }

    // Regel 2: Umlautpunkte zählen nicht ("Luneburg" ist "Lüneburg").
    [Fact]
    public void Add_Umlautpunkte_zaehlen_nicht()
    {
        const string datei = "Lüneburg\n";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "luneburg", null);

        Assert.Equal(WordListChange.Unchanged, ergebnis.Change);
        Assert.Equal(datei, ergebnis.Content);
    }

    // Regel 2: Kommentar mit dem Wort zählt nicht als vorhanden.
    [Fact]
    public void Add_Wort_nur_im_Kommentar_wird_neu_angelegt()
    {
        const string datei = "# Cesaretti\r\n";

        WordListEditResult ergebnis = WordListEditor.Add(datei, "Cesaretti", null);

        Assert.Equal(WordListChange.Added, ergebnis.Change);
        Assert.Equal("# Cesaretti\r\nCesaretti\r\n", ergebnis.Content);
    }

    // ---------- Add / Validate: ungültig (Regel 5) ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("a=b")]
    [InlineData("a|b")]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("#Wort")]
    [InlineData("--")]
    public void Add_ungueltiges_Wort(string? wort)
    {
        PruefeUngueltig(WordListEditor.Add(CrlfDatei, wort, null), CrlfDatei);
        Assert.NotNull(WordListEditor.Validate(wort, null));
    }

    [Theory]
    [InlineData("v=w")]
    [InlineData("v|w")]
    [InlineData("v\rw")]
    [InlineData("v\nw")]
    [InlineData("#v")]
    public void Add_ungueltige_Variante(string variante)
    {
        PruefeUngueltig(WordListEditor.Add(CrlfDatei, "Hannover", [variante]), CrlfDatei);
        Assert.NotNull(WordListEditor.Validate("Hannover", [variante]));
    }

    // Ungültige Variante macht auch ein vorhandenes Wort nicht zur Erweiterung.
    [Fact]
    public void Add_ungueltige_Variante_bei_vorhandenem_Wort()
    {
        PruefeUngueltig(WordListEditor.Add(CrlfDatei, "Cesaretti", ["a|b"]), CrlfDatei);
    }

    // Regel 5: 80 Zeichen gültig, 81 ungültig, für Wort und Variante.
    [Fact]
    public void Laengengrenze_80()
    {
        string achtzig = new('a', 80);
        string einundachtzig = new('a', 81);

        Assert.Null(WordListEditor.Validate(achtzig, null));
        Assert.NotNull(WordListEditor.Validate(einundachtzig, null));
        Assert.Null(WordListEditor.Validate("Hannover", [achtzig]));
        Assert.NotNull(WordListEditor.Validate("Hannover", [einundachtzig]));

        Assert.Equal(WordListChange.Added, WordListEditor.Add("", achtzig, null).Change);
        PruefeUngueltig(WordListEditor.Add(CrlfDatei, einundachtzig, null), CrlfDatei);
        PruefeUngueltig(WordListEditor.Add(CrlfDatei, "Hannover", [einundachtzig]), CrlfDatei);
    }

    // Regel 5: gültige Fälle liefern null; leere und wortgleiche Varianten sind kein Fehler.
    [Fact]
    public void Validate_gueltig()
    {
        Assert.Null(WordListEditor.Validate("LUMÍORA", null));
        Assert.Null(WordListEditor.Validate("Next.js", ["next js"]));
        Assert.Null(WordListEditor.Validate("Stefanos Kulinarische Welt", []));
        Assert.Null(WordListEditor.Validate("Hannover", ["", "  ", "hannover"]));
        Assert.Null(WordListEditor.Validate("  Hannover  ", null));
    }

    // ---------- Remove (Regel 4) ----------

    // Mittlere Zeile entfernen, samt Zeilenende; Kommentar mit "Cesaretti" bleibt.
    [Fact]
    public void Remove_mittlere_Zeile_CRLF()
    {
        WordListEditResult ergebnis = WordListEditor.Remove(CrlfDatei, "cesaretti");

        Assert.Equal(WordListChange.Removed, ergebnis.Change);
        Assert.True(ergebnis.Changed);
        Assert.Equal(CrlfDatei.Replace("\r\nCesaretti\r\n", "\r\n"), ergebnis.Content);
        Assert.StartsWith(Kopf, ergebnis.Content);
    }

    // Entfernen per Schlüssel: "lumiora" trifft "LUMÍORA = Lumi Ora".
    [Fact]
    public void Remove_per_Schluessel()
    {
        WordListEditResult ergebnis = WordListEditor.Remove(CrlfDatei, "lumiora");

        Assert.Equal(WordListChange.Removed, ergebnis.Change);
        Assert.Equal(CrlfDatei.Replace("LUMÍORA = Lumi Ora\r\n", ""), ergebnis.Content);
    }

    // Letzte Zeile ohne Zeilenende entfernen: davor bleibt alles byte-gleich.
    [Fact]
    public void Remove_letzte_Zeile_ohne_Zeilenende()
    {
        const string datei = "# Kopf\r\nCesaretti\r\nHannover";

        WordListEditResult ergebnis = WordListEditor.Remove(datei, "Hannover");

        Assert.Equal(WordListChange.Removed, ergebnis.Change);
        Assert.Equal("# Kopf\r\nCesaretti\r\n", ergebnis.Content);
    }

    [Fact]
    public void Remove_letzte_Zeile_mit_Zeilenende()
    {
        WordListEditResult ergebnis = WordListEditor.Remove(CrlfDatei, "Next.js");

        Assert.Equal(CrlfDatei.Replace("Next.js = next js\r\n", ""), ergebnis.Content);
    }

    [Fact]
    public void Remove_in_LF_Datei()
    {
        WordListEditResult ergebnis = WordListEditor.Remove("A\nB\nC\n", "b");

        Assert.Equal("A\nC\n", ergebnis.Content);
    }

    // Zweimal vorhandenes Wort: beide Zeilen gehen.
    [Fact]
    public void Remove_doppeltes_Wort_entfernt_beide_Zeilen()
    {
        const string datei = "# Straße\r\nStraße\r\nHannover\r\nStrasse = Strase\r\n";

        WordListEditResult ergebnis = WordListEditor.Remove(datei, "STRASSE");

        Assert.Equal(WordListChange.Removed, ergebnis.Change);
        Assert.Equal("# Straße\r\nHannover\r\n", ergebnis.Content);
    }

    // Variante allein ist kein Treffer: nur die Schreibweise zählt.
    [Fact]
    public void Remove_Variante_ist_kein_Treffer()
    {
        // "Lumiohra" hat einen anderen Schlüssel als "LUMÍORA" ("Lumi Ora" dagegen denselben).
        const string datei = "LUMÍORA = Lumiohra\r\nCesaretti\r\n";

        WordListEditResult ergebnis = WordListEditor.Remove(datei, "Lumiohra");

        Assert.Equal(WordListChange.NotFound, ergebnis.Change);
        Assert.Equal(datei, ergebnis.Content);
    }

    // Kommentar mit dem Wort wird nie entfernt.
    [Fact]
    public void Remove_Kommentar_wird_nie_entfernt()
    {
        const string datei = "# Cesaretti\r\n";

        WordListEditResult ergebnis = WordListEditor.Remove(datei, "Cesaretti");

        Assert.Equal(WordListChange.NotFound, ergebnis.Change);
        Assert.False(ergebnis.Changed);
        Assert.Equal(datei, ergebnis.Content);
    }

    [Theory]
    [InlineData("Hannover")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Remove_nicht_gefunden(string? wort)
    {
        WordListEditResult ergebnis = WordListEditor.Remove(CrlfDatei, wort);

        Assert.Equal(WordListChange.NotFound, ergebnis.Change);
        Assert.Equal(CrlfDatei, ergebnis.Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Remove_aus_leerer_Datei(string? datei)
    {
        WordListEditResult ergebnis = WordListEditor.Remove(datei, "Hannover");

        Assert.Equal(WordListChange.NotFound, ergebnis.Change);
        Assert.Equal(datei ?? "", ergebnis.Content);
    }

    // ---------- SplitVariants (Regel 6) ----------

    [Fact]
    public void SplitVariants_trennt_an_Kommas()
    {
        Assert.Equal(["Lumi Ora", "Lumiohra", "x"], WordListEditor.SplitVariants(" Lumi Ora ,Lumiohra,, ,x ").ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ,")]
    public void SplitVariants_leer(string? text)
    {
        Assert.Empty(WordListEditor.SplitVariants(text));
    }

    // ---------- Hint (Regel 7) ----------

    [Fact]
    public void Hint_bei_einem_kleingeschriebenen_Wort()
    {
        Assert.NotNull(WordListEditor.Hint("haus"));
    }

    [Theory]
    [InlineData("Cesaretti")]
    [InlineData("LUMÍORA")]
    [InlineData("Next.js")]
    [InlineData("zwei Wörter")]
    [InlineData("")]
    [InlineData(null)]
    public void Hint_sonst_null(string? wort)
    {
        Assert.Null(WordListEditor.Hint(wort));
    }

    // ---------- Entries (Regel 8) ----------

    [Fact]
    public void Entries_ohne_Kommentare_und_Leerzeilen()
    {
        IReadOnlyList<VocabularyEntry> eintraege = WordListEditor.Entries(CrlfDatei);

        Assert.Equal(["LUMÍORA", "Cesaretti", "Next.js"], eintraege.Select(e => e.Term).ToArray());
        Assert.Equal(["Lumi Ora"], eintraege[0].Aliases.ToArray());
        Assert.Empty(eintraege[1].Aliases);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("# nur Kommentar\n\n")]
    public void Entries_leer(string? datei)
    {
        Assert.Empty(WordListEditor.Entries(datei));
    }
}
