// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System;
using System.Linq;
using Talk.Core.Insertion;
using Xunit;

namespace Talk.Core.Tests.Insertion;

/// <summary>
/// Prüft InsertText.AdaptToContext und InsertText.MidSentenceWords ausschließlich gegen
/// PLAYBOOK.md, Abschnitt 24, Regeln 1 bis 6. Die Tests kennen die Umsetzung nicht.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class InsertContextTests
{
    // Vollständige Liste aus Regel 5, wörtlich aus dem Playbook übernommen.
    private static readonly string[] ListeAusDemPlaybook =
    [
        // Deutsch
        "aber", "als", "also", "am", "an", "auch", "auf", "aus", "bei", "beim", "bin", "bis", "bist",
        "da", "damit", "dann", "das", "dass", "dem", "den", "denn", "der", "des", "dich", "die", "dir",
        "doch", "du", "durch", "ein", "eine", "einem", "einen", "einer", "eines", "er", "es", "für",
        "gerade", "habe", "haben", "hat", "hatte", "hier", "ich", "im", "immer", "in", "ins", "ist",
        "ja", "jetzt", "kann", "kein", "keine", "man", "mal", "mehr", "mich", "mir", "mit", "muss",
        "nach", "nein", "nicht", "noch", "nur", "ob", "oder", "ohne", "schon", "sehr", "sein", "seine",
        "sind", "so", "sondern", "um", "und", "uns", "unter", "vom", "von", "vor", "war", "waren",
        "was", "weil", "wenn", "wer", "wie", "wir", "wird", "wo", "zu", "zum", "zur", "über",
        // Englisch
        "a", "and", "are", "as", "at", "be", "because", "but", "by", "for", "from", "had", "has",
        "have", "he", "her", "his", "if", "is", "it", "its", "my", "not", "of", "on", "or", "our",
        "she", "that", "the", "their", "then", "there", "they", "this", "to", "was", "we", "were",
        "what", "when", "which", "who", "will", "with", "would", "you", "your",
        // Italienisch
        "al", "alla", "anche", "che", "come", "con", "del", "della", "di", "e", "è", "gli", "ho",
        "il", "la", "le", "lo", "ma", "mi", "nel", "nella", "non", "o", "per", "perché", "più", "se",
        "si", "sono", "su", "ti", "tra", "un", "una", "uno",
    ];

    public static TheoryData<string> WoerterDerListe()
    {
        var daten = new TheoryData<string>();
        foreach (var wort in ListeAusDemPlaybook.Distinct())
            daten.Add(wort);
        return daten;
    }

    // ---------- Regel 1: Kein Wissen, keine Änderung ----------

    // Regel 1: null als Text ergibt einen leeren Text, egal was davor steht.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ich habe")]
    [InlineData("Fertig.")]
    public void Regel1_Text_null_ergibt_leer(string? davor)
    {
        Assert.Equal("", InsertText.AdaptToContext(null, davor));
    }

    // Regel 1: Ein leerer Text kommt unverändert zurück.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ich habe")]
    public void Regel1_leerer_Text_bleibt_leer(string? davor)
    {
        Assert.Equal("", InsertText.AdaptToContext("", davor));
    }

    // Regel 1: Ein Text nur aus Leerraum ist nicht einfügbar und kommt unverändert zurück.
    [Theory]
    [InlineData(" ", "Ich habe")]
    [InlineData("   ", "Ich habe")]
    [InlineData("\t", "Seite 3")]
    [InlineData("\r\n", "Hallo,")]
    [InlineData(" \t ", null)]
    public void Regel1_Text_nur_aus_Leerraum_bleibt_unveraendert(string text, string? davor)
    {
        Assert.Equal(text, InsertText.AdaptToContext(text, davor));
    }

    // Regel 1: Ist davor null (unbekannt), bleibt der Text unverändert, auch ein Wort aus der Liste.
    [Theory]
    [InlineData("Und dann gehen wir.")]
    [InlineData("Hund bellt.")]
    [InlineData(" Und dann.")]
    [InlineData("Die Sonne scheint.")]
    public void Regel1_davor_null_laesst_Text_unveraendert(string text)
    {
        Assert.Equal(text, InsertText.AdaptToContext(text, null));
    }

    // Regel 1: Ist davor leer (Cursor am Anfang des Felds), bleibt der Text unverändert.
    [Theory]
    [InlineData("Und dann gehen wir.")]
    [InlineData("Hund bellt.")]
    [InlineData("Die Sonne scheint.")]
    public void Regel1_davor_leer_laesst_Text_unveraendert(string text)
    {
        Assert.Equal(text, InsertText.AdaptToContext(text, ""));
    }

    // ---------- Regel 2: Begriffe ----------

    // Regel 2: Besteht davor nur aus Leerraum oder Zeilenumbruch, gilt der Cursor als am
    // Zeilenanfang, der Text bleibt unverändert (kein Leerzeichen, nicht klein).
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r\n\r\n")]
    [InlineData(" \n ")]
    public void Regel2_davor_nur_Leerraum_laesst_Text_unveraendert(string davor)
    {
        Assert.Equal("Und dann gehen wir.", InsertText.AdaptToContext("Und dann gehen wir.", davor));
        Assert.Equal("Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // Regel 2 (Nachtrag): Steht zwischen dem letzten sichtbaren Zeichen und dem Ende von davor ein
    // Zeilenumbruch (CR, LF, U+0085, U+2028, U+2029), bleibt der Text unverändert:
    // kein Leerzeichen vorn, nicht klein. Geprüft nach Buchstabe, Ziffer, Komma und Semikolon.
    [Theory]
    [InlineData("Ich habe\r")]
    [InlineData("Ich habe\n")]
    [InlineData("Ich habe\u0085")]
    [InlineData("Ich habe\u2028")]
    [InlineData("Ich habe\u2029")]
    [InlineData("Einkaufsliste\r\n")]
    [InlineData("Einkaufsliste\n ")]
    [InlineData("Einkaufsliste\n\t")]
    [InlineData("Einkaufsliste \n")]
    [InlineData("Einkaufsliste\t\r\n")]
    [InlineData("Einkaufsliste \u2028 ")]
    [InlineData("Seite 3\n")]
    [InlineData("Wir kommen,\r\n")]
    [InlineData("erstens;\u2029")]
    public void Regel2_Zeilenumbruch_nach_letztem_sichtbaren_Zeichen_laesst_Text_unveraendert(string davor)
    {
        Assert.Equal("Und dann gehen wir.", InsertText.AdaptToContext("Und dann gehen wir.", davor));
        Assert.Equal("Die Milch.", InsertText.AdaptToContext("Die Milch.", davor));
        Assert.Equal("Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // Regel 2 (Nachtrag): Auch nach Satzende mit Zeilenumbruch bleibt der Text unverändert.
    [Theory]
    [InlineData("Fertig.\n")]
    [InlineData("Fertig.\r\n")]
    [InlineData("Toll!\u2028")]
    public void Regel2_Zeilenumbruch_nach_Satzende_laesst_Text_unveraendert(string davor)
    {
        Assert.Equal("Und dann.", InsertText.AdaptToContext("Und dann.", davor));
    }

    // Regel 2 (Nachtrag): Ein Zeilenumbruch vor dem letzten sichtbaren Zeichen zählt nicht;
    // es gelten Regel 3 und 4 wie sonst.
    [Fact]
    public void Regel2_Zeilenumbruch_vor_letztem_sichtbaren_Zeichen_zaehlt_nicht()
    {
        Assert.Equal(" und dann.", InsertText.AdaptToContext("Und dann.", "Liste\nIch habe"));
    }

    // ---------- Regel 3: Leerzeichen vorn ----------
    // Für Regel 3 dient ein erstes Wort außerhalb der Liste ("Hund"), damit Regel 4 nicht mitwirkt.

    // Regel 3: Nach jedem Zeichen aus . , ; : ! ? … ) ] } % ” kommt vorn ein Leerzeichen dazu.
    [Theory]
    [InlineData("Fertig.")]
    [InlineData("Hallo,")]
    [InlineData("erstens;")]
    [InlineData("Achtung:")]
    [InlineData("Toll!")]
    [InlineData("Wirklich?")]
    [InlineData("Na ja…")]
    [InlineData("(siehe oben)")]
    [InlineData("[1]")]
    [InlineData("{x}")]
    [InlineData("50%")]
    [InlineData("„gut”")]
    public void Regel3_nach_Zeichen_der_Liste_kommt_Leerzeichen(string davor)
    {
        Assert.Equal(" Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // Regel 3: Nach einem Buchstaben, einer Ziffer oder einem Umlaut kommt vorn ein Leerzeichen dazu.
    [Theory]
    [InlineData("Ich sehe einen")]
    [InlineData("Seite 3")]
    [InlineData("Nummer 42")]
    [InlineData("schön")]
    [InlineData("Straße")]
    [InlineData("Ä")]
    public void Regel3_nach_Buchstabe_Ziffer_Umlaut_kommt_Leerzeichen(string davor)
    {
        Assert.Equal(" Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // Regel 3: Nach öffnenden Zeichen ( [ { „ “ « » " ', nach Schrägstrich und Bindestrich kommt keines dazu.
    [Theory]
    [InlineData("Er sagte (")]
    [InlineData("Liste [")]
    [InlineData("Menge {")]
    [InlineData("Er sagte „")]
    [InlineData("He said “")]
    [InlineData("Il dit «")]
    [InlineData("Er sagte »")]
    [InlineData("Er sagte \"")]
    [InlineData("Er sagte '")]
    [InlineData("und/")]
    [InlineData("Nord-")]
    public void Regel3_nach_oeffnendem_Zeichen_Schraegstrich_Bindestrich_kein_Leerzeichen(string davor)
    {
        Assert.Equal("Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // Regel 3: Nach allen anderen Zeichen kommt kein Leerzeichen dazu.
    [Theory]
    [InlineData("a+")]
    [InlineData("x=")]
    [InlineData("#")]
    [InlineData("@")]
    [InlineData("*")]
    public void Regel3_nach_anderen_Zeichen_kein_Leerzeichen(string davor)
    {
        Assert.Equal("Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // Regel 3: Beginnt der Text schon mit Leerraum, kommt kein weiteres Leerzeichen dazu.
    [Theory]
    [InlineData(" Hund bellt.", "Ich sehe einen")]
    [InlineData("\tHund bellt.", "Fertig.")]
    [InlineData("\nHund bellt.", "Hallo,")]
    [InlineData("  Hund bellt.", "Seite 3")]
    public void Regel3_Text_beginnt_mit_Leerraum_kein_weiteres_Leerzeichen(string text, string davor)
    {
        Assert.Equal(text, InsertText.AdaptToContext(text, davor));
    }

    // Regel 3: Endet davor mit einem Leerzeichen, kommt keines dazu.
    [Theory]
    [InlineData("Ich sehe einen ")]
    [InlineData("Fertig. ")]
    [InlineData("Hallo,\t")]
    [InlineData("Seite 3\n")]
    public void Regel3_davor_endet_mit_Leerraum_kein_Leerzeichen(string davor)
    {
        Assert.Equal("Hund bellt.", InsertText.AdaptToContext("Hund bellt.", davor));
    }

    // ---------- Regel 4: Klein am Anfang ----------

    // Regel 4 (mit Regel 3): Ein Wort aus der Liste nach Buchstabe, Ziffer, Komma oder Semikolon wird klein.
    [Theory]
    [InlineData("Ich habe", "Und dann gehen wir.", " und dann gehen wir.")]
    [InlineData("Seite 3", "Und dann weiter.", " und dann weiter.")]
    [InlineData("Wir kommen,", "Wenn es passt.", " wenn es passt.")]
    [InlineData("erstens;", "Aber nicht heute.", " aber nicht heute.")]
    [InlineData("Ich gehe nach Hause", "Weil es spät ist.", " weil es spät ist.")]
    [InlineData("It works", "And then it stops.", " and then it stops.")]
    public void Regel4_Wort_der_Liste_wird_klein(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Maßgeblich ist das letzte sichtbare Zeichen; steht danach noch Leerraum, wird trotzdem klein
    // (und nach Regel 3 kommt wegen des Leerraums kein Leerzeichen dazu).
    [Theory]
    [InlineData("Ich habe ", "Und dann.", "und dann.")]
    [InlineData("Seite 3 ", "Und dann.", "und dann.")]
    [InlineData("Wir kommen, ", "Wenn es passt.", "wenn es passt.")]
    [InlineData("erstens;\t", "Aber nicht heute.", "aber nicht heute.")]
    [InlineData("Ich habe  ", "Und dann.", "und dann.")]
    [InlineData("Ich habe \t", "Und dann.", "und dann.")]
    public void Regel4_Leerraum_nach_letztem_sichtbaren_Zeichen_wird_trotzdem_klein(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Nach Punkt, Ausrufezeichen, Fragezeichen, Auslassungszeichen und Doppelpunkt bleibt es groß.
    [Theory]
    [InlineData("Fertig.")]
    [InlineData("Toll!")]
    [InlineData("Wirklich?")]
    [InlineData("Na ja…")]
    [InlineData("Achtung:")]
    public void Regel4_nach_Satzende_und_Doppelpunkt_bleibt_gross(string davor)
    {
        Assert.Equal(" Und dann.", InsertText.AdaptToContext("Und dann.", davor));
    }

    // Regel 4: Nach Satzende mit folgendem Leerraum bleibt es ebenfalls groß.
    [Theory]
    [InlineData("Fertig. ")]
    [InlineData("Toll! ")]
    [InlineData("Wirklich?\n")]
    [InlineData("Achtung: ")]
    public void Regel4_nach_Satzende_mit_Leerraum_bleibt_gross(string davor)
    {
        Assert.Equal("Und dann.", InsertText.AdaptToContext("Und dann.", davor));
    }

    // Regel 4: Nach allen anderen Zeichen bleibt der Anfang, wie er ist.
    [Theory]
    [InlineData("(siehe oben)", " Und dann.")]
    [InlineData("50%", " Und dann.")]
    [InlineData("Er sagte (", "Und dann.")]
    [InlineData("Nord-", "Und dann.")]
    [InlineData("a+", "Und dann.")]
    public void Regel4_nach_anderen_Zeichen_bleibt_gross(string davor, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext("Und dann.", davor));
    }

    // Regel 4: Ein Hauptwort außerhalb der Liste bleibt groß.
    [Theory]
    [InlineData("Ich sehe einen", "Hund im Garten.", " Hund im Garten.")]
    [InlineData("Wir haben", "Gestern gearbeitet.", " Gestern gearbeitet.")]
    [InlineData("Ich habe,", "Tische gekauft.", " Tische gekauft.")]
    public void Regel4_Hauptwort_bleibt_gross(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4 und 5: Höflichkeitsform und englisches "I" bleiben groß.
    [Theory]
    [InlineData("Ich danke", "Sie haben recht.", " Sie haben recht.")]
    [InlineData("Ich danke", "Ihnen sehr.", " Ihnen sehr.")]
    [InlineData("Wir zeigen", "Ihr Zimmer.", " Ihr Zimmer.")]
    [InlineData("Hier ist", "Ihre Karte.", " Ihre Karte.")]
    [InlineData("Yesterday", "I went home.", " I went home.")]
    public void Regel4_Hoeflichkeitsform_und_I_bleiben_gross(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Hat das erste Wort außer dem ersten Buchstaben noch einen Großbuchstaben, bleibt es.
    [Theory]
    [InlineData("Ich habe", "UNd dann.", " UNd dann.")]
    [InlineData("Ich habe", "UnD dann.", " UnD dann.")]
    [InlineData("Wir nutzen", "DaS Werkzeug.", " DaS Werkzeug.")]
    public void Regel4_Wort_mit_mehreren_Grossbuchstaben_bleibt(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Ein Wort aus der Liste ganz in Großbuchstaben bleibt.
    [Theory]
    [InlineData("Ich habe", "UND dann.", " UND dann.")]
    [InlineData("It works", "THE end.", " THE end.")]
    [InlineData("Ich gehe", "NICHT mit.", " NICHT mit.")]
    public void Regel4_Wort_der_Liste_in_Grossbuchstaben_bleibt(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Ein Wort der Liste, das schon klein ist, bleibt klein (nur Leerzeichen nach Regel 3).
    [Fact]
    public void Regel4_Wort_der_Liste_schon_klein_bleibt_klein()
    {
        Assert.Equal(" und dann.", InsertText.AdaptToContext("und dann.", "Ich habe"));
    }

    // Regel 4: Beginnt der Text mit Leerraum, zählt das erste Wort ab dem ersten Zeichen, das kein
    // Leerraum ist; der Leerraum bleibt, nach Regel 3 kommt kein Leerzeichen dazu.
    [Theory]
    [InlineData("Ich habe", " Und dann.", " und dann.")]
    [InlineData("Ich habe", "  Und dann.", "  und dann.")]
    [InlineData("Hallo,", "\tWenn es passt.", "\twenn es passt.")]
    [InlineData("Ich sehe einen", " Hund.", " Hund.")]
    public void Regel4_Text_beginnt_mit_Leerraum(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Beginnt der Text mit einem Anführungszeichen, ist das erste Zeichen kein Buchstabe,
    // das erste Wort also leer und nicht in der Liste; der Anfang bleibt.
    [Theory]
    [InlineData("\"Und dann\" sagte er.")]
    [InlineData("„Und dann“ sagte er.")]
    [InlineData("'Und' ist kurz.")]
    public void Regel4_Text_beginnt_mit_Anfuehrungszeichen_bleibt(string text)
    {
        Assert.Equal(" " + text, InsertText.AdaptToContext(text, "Ich habe"));
    }

    // Regel 4: Das erste Wort endet am ersten Zeichen, das kein Buchstabe ist; "Und," zählt als "und".
    [Theory]
    [InlineData("Ich habe", "Und, dann.", " und, dann.")]
    [InlineData("Ich habe", "Und.", " und.")]
    [InlineData("Ich habe", "Aber!", " aber!")]
    [InlineData("Ich habe", "Und-so-weiter.", " und-so-weiter.")]
    [InlineData("Ich habe", "Und", " und")]
    public void Regel4_Wortende_an_Satzzeichen(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Endet das erste Wort an einem Satzzeichen, aber ist es kein Wort der Liste, bleibt es.
    [Fact]
    public void Regel4_Wortende_an_Satzzeichen_Hauptwort_bleibt()
    {
        Assert.Equal(" Hund, Katze.", InsertText.AdaptToContext("Hund, Katze.", "Ich habe"));
    }

    // Regel 4: Ein Wort, das mit einem Listenwort beginnt, aber länger ist, ist nicht in der Liste.
    [Theory]
    [InlineData("Ich habe", "Undank ist übel.", " Undank ist übel.")]
    [InlineData("Ich habe", "Dannemann gesehen.", " Dannemann gesehen.")]
    public void Regel4_laengeres_Wort_ist_nicht_in_der_Liste(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4: Umlaut am Anfang, "Über" ist in der Liste und wird klein.
    [Fact]
    public void Regel4_Umlaut_Ueber_wird_klein()
    {
        Assert.Equal(" über die Brücke.", InsertText.AdaptToContext("Über die Brücke.", "Wir gehen"));
    }

    // Regel 4: Italienisch "È" und "Perché" sind in der Liste und werden klein.
    [Theory]
    [InlineData("Questo", "È vero.", " è vero.")]
    [InlineData("Non so", "Perché no.", " perché no.")]
    [InlineData("Lui dice,", "Più tardi.", " più tardi.")]
    [InlineData("Io vado", "E tu?", " e tu?")]
    public void Regel4_Italienisch_wird_klein(string davor, string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor));
    }

    // Regel 4 und 5: Jedes Wort der Liste wird nach einem Buchstaben klein.
    [Theory]
    [MemberData(nameof(WoerterDerListe))]
    public void Regel4_jedes_Wort_der_Liste_wird_nach_Buchstabe_klein(string wort)
    {
        var gross = char.ToUpperInvariant(wort[0]) + wort[1..];
        var text = gross + " weiter.";
        Assert.Equal(" " + wort + " weiter.", InsertText.AdaptToContext(text, "Wort"));
    }

    // ---------- Regel 5: Liste ----------

    // Regel 5: Jedes Wort der Liste aus dem Playbook ist in MidSentenceWords.
    [Theory]
    [MemberData(nameof(WoerterDerListe))]
    public void Regel5_Wort_ist_in_MidSentenceWords(string wort)
    {
        Assert.Contains(wort, InsertText.MidSentenceWords);
    }

    // Regel 5: MidSentenceWords enthält genau die Wörter der Liste, keines mehr
    // (Vergleich ohne Groß und Klein, "was" steht im Playbook zweimal und zählt einmal).
    [Fact]
    public void Regel5_MidSentenceWords_enthaelt_keine_weiteren_Woerter()
    {
        var erwartet = ListeAusDemPlaybook.Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var tatsaechlich = InsertText.MidSentenceWords.Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var zuviel = tatsaechlich.Except(erwartet).ToArray();
        var fehlt = erwartet.Except(tatsaechlich).ToArray();
        Assert.Empty(zuviel);
        Assert.Empty(fehlt);
    }

    // Regel 5: Die bewusst ausgeschlossenen Wörter fehlen in der Liste (auch nicht in anderer Schreibweise).
    [Theory]
    [InlineData("Sie")]
    [InlineData("Ihr")]
    [InlineData("Ihnen")]
    [InlineData("Ihre")]
    [InlineData("I")]
    public void Regel5_ausgeschlossene_Woerter_fehlen(string wort)
    {
        Assert.DoesNotContain(InsertText.MidSentenceWords,
            w => string.Equals(w, wort, StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Regel 6: Reihenfolge und Zusammenspiel ----------

    // Regel 6: Zuerst Regel 4 (klein), dann Regel 3 (Leerzeichen vorn).
    [Fact]
    public void Regel6_erst_klein_dann_Leerzeichen()
    {
        Assert.Equal(" und dann.", InsertText.AdaptToContext("Und dann.", "Ich habe"));
    }

    // Regel 6: Danach gelten die Regeln der Zwischenablage (Abschnitt 17, Regel 1: Leerzeichen am Ende).
    [Fact]
    public void Regel6_Verkettung_mit_ForClipboard()
    {
        var angepasst = InsertText.AdaptToContext("Und dann.", "Ich habe");
        Assert.Equal(" und dann. ", InsertText.ForClipboard(angepasst));
    }

    // Regel 6: Verkettung mit ForClipboard, wenn davor unbekannt ist: nur das Leerzeichen am Ende.
    [Fact]
    public void Regel6_Verkettung_mit_ForClipboard_davor_unbekannt()
    {
        var angepasst = InsertText.AdaptToContext("Und dann.", null);
        Assert.Equal("Und dann. ", InsertText.ForClipboard(angepasst));
    }

    // Regel 6: Verkettung mit ForClipboard nach Satzende: Leerzeichen vorn und hinten, groß bleibt.
    [Fact]
    public void Regel6_Verkettung_mit_ForClipboard_nach_Satzende()
    {
        var angepasst = InsertText.AdaptToContext("Und dann.", "Fertig.");
        Assert.Equal(" Und dann. ", InsertText.ForClipboard(angepasst));
    }

    // startRaised (Playbook Abschnitt 28): Mitten im Satz wird jedes erste Wort klein, wenn erst Talk es groß gemacht hat.
    [Theory]
    [InlineData("Gestern beim Arzt.", "Ich war", true, " gestern beim Arzt.")]
    [InlineData("Gestern beim Arzt.", "Ich war", false, " Gestern beim Arzt.")]
    [InlineData("Gestern beim Arzt.", "Ich war. ", true, "Gestern beim Arzt.")]
    [InlineData("Gestern beim Arzt.", "Ich war,", true, " gestern beim Arzt.")]
    [InlineData("IPhone kaputt", "Mein", true, " IPhone kaputt")]
    public void StartRaised_macht_Wort_mitten_im_Satz_klein(string text, string davor, bool startRaised, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.AdaptToContext(text, davor, startRaised));
    }
}
