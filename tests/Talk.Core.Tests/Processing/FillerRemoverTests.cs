// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft das Entfernen der Füllwörter ausschließlich gegen PLAYBOOK.md, Abschnitt 16, "Regeln der reinen Logik",
/// Punkt 3 mit seinen acht Regeln und allen dort genannten Beispielen, dazu "Bekannte Grenzen" Punkt 3 und 4.
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer in den Kommentaren meint die acht Regeln aus Punkt 3.
/// </summary>
public sealed class FillerRemoverTests
{
    // ---------- Die Beispiele aus dem Playbook, wörtlich ----------

    // Beispiele Deutsch, die sich ändern.
    [Theory]
    [InlineData("Ich, ähm, wollte sagen, dass es klappt.", "Ich wollte sagen, dass es klappt.")]
    [InlineData("Ähm, ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Also, ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Also ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("Das war es, ähm.", "Das war es.")]
    [InlineData("Ähm... ich weiß nicht.", "Ich weiß nicht.")]
    [InlineData("Ich wollte ähm... sagen.", "Ich wollte... sagen.")]
    [InlineData("Gut. Naja, das geht.", "Gut. Das geht.")]
    [InlineData("Ich habe, na ja, keine Lust.", "Ich habe keine Lust.")]
    [InlineData("Ja, ähm ich wollte", "Ja, ich wollte")]
    public void Beispiel_Deutsch_wird_bereinigt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Beispiele Deutsch, die bleiben.
    [Theory]
    [InlineData("Ich habe also gewartet.")]
    [InlineData("Also.")]
    [InlineData("Am 3. also nicht.")]
    [InlineData("Um zu testen, kommt er.")]
    public void Beispiel_Deutsch_bleibt(string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, "de"));
    }

    // Beispiel Deutsch: Bleibt nur ein Zögerlaut, ist das Ergebnis leer.
    [Fact]
    public void Beispiel_Deutsch_nur_Zoegerlaut_wird_leer()
    {
        Assert.Equal(string.Empty, FillerRemover.Remove("Ähm.", "de"));
    }

    // Beispiele Englisch.
    [Theory]
    [InlineData("Um, I think, er, yes.", "I think yes.")]
    [InlineData("Well, that works.", "That works.")]
    public void Beispiel_Englisch_wird_bereinigt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "en"));
    }

    [Fact]
    public void Beispiel_Englisch_Well_done_bleibt()
    {
        Assert.Equal("Well done, team.", FillerRemover.Remove("Well done, team.", "en"));
    }

    // Beispiele Italienisch.
    [Fact]
    public void Beispiel_Italienisch_Allora_mit_Komma_entfaellt()
    {
        Assert.Equal("Andiamo.", FillerRemover.Remove("Allora, andiamo.", "it"));
    }

    [Fact]
    public void Beispiel_Italienisch_Allora_ohne_Komma_bleibt()
    {
        Assert.Equal("Allora andiamo.", FillerRemover.Remove("Allora andiamo.", "it"));
    }

    // Beispiel unbekannte Sprache: nur der gemeinsame Zögerlaut entfällt, "um" bleibt und beginnt groß.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("fr")]
    public void Beispiel_unbekannte_Sprache(string? sprache)
    {
        Assert.Equal("Um zu testen.", FillerRemover.Remove("Ähm, um zu testen.", sprache));
    }

    // Vertrag: Die Sprache ist ohne Angabe Deutsch.
    [Fact]
    public void Ohne_Angabe_gilt_Deutsch()
    {
        Assert.Equal("Ich wollte fragen.", FillerRemover.Remove("Also ich wollte fragen."));
        Assert.Equal("Um zu testen, kommt er.", FillerRemover.Remove("Um zu testen, kommt er."));
    }

    // ---------- Regel 1: Zögerlaute und Zusatzwörter entfallen überall ----------

    // Regel 1: Jeder gemeinsame Zögerlaut entfällt mitten im Satz, in jeder Sprache.
    [Theory]
    [InlineData("äh")]
    [InlineData("ähh")]
    [InlineData("ähm")]
    [InlineData("ähhm")]
    [InlineData("öh")]
    [InlineData("öhm")]
    [InlineData("hm")]
    [InlineData("hmm")]
    [InlineData("hmmm")]
    [InlineData("mhm")]
    [InlineData("ehm")]
    [InlineData("uhm")]
    [InlineData("erm")]
    public void Gemeinsamer_Zoegerlaut_entfaellt_in_jeder_Sprache(string laut)
    {
        string text = "Ich wollte " + laut + " gehen.";

        Assert.Equal("Ich wollte gehen.", FillerRemover.Remove(text, "de"));
        Assert.Equal("Ich wollte gehen.", FillerRemover.Remove(text, "en"));
        Assert.Equal("Ich wollte gehen.", FillerRemover.Remove(text, "it"));
        Assert.Equal("Ich wollte gehen.", FillerRemover.Remove(text, ""));
    }

    // Regel 1: ohne Groß und Klein.
    [Theory]
    [InlineData("Ich ÄHM wollte gehen.")]
    [InlineData("Ich Ähm wollte gehen.")]
    [InlineData("Ich HMM wollte gehen.")]
    [InlineData("Ich Öhm wollte gehen.")]
    public void Zoegerlaut_entfaellt_ohne_Gross_und_Klein(string text)
    {
        Assert.Equal("Ich wollte gehen.", FillerRemover.Remove(text, "de"));
    }

    // Regel 1: nur als ganze Wörter. Ein Zögerlaut als Teil eines Worts bleibt stehen.
    [Theory]
    [InlineData("Er erwähnt das Märchen.")]
    [InlineData("Ähnlich war es gestern.")]
    [InlineData("Die Währung ist stabil.")]
    [InlineData("Der Rahmen ist schmal.")]
    public void Zoegerlaut_als_Wortteil_bleibt(string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, "de"));
    }

    // Regel 1 mit Regel 2 der Listen: Englische Zögerlaute entfallen nur im Englischen.
    [Theory]
    [InlineData("I was uh going home.", "I was going home.")]
    [InlineData("I was uhh going home.", "I was going home.")]
    [InlineData("I was umm going home.", "I was going home.")]
    [InlineData("It was, um, fine.", "It was fine.")]
    [InlineData("It was, er, fine.", "It was fine.")]
    public void Englischer_Zoegerlaut_entfaellt_im_Englischen(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "en"));
    }

    // Regel 1 mit Regel 2 der Listen: Der italienische Zögerlaut "mh" entfällt im Italienischen.
    [Fact]
    public void Italienischer_Zoegerlaut_entfaellt_im_Italienischen()
    {
        Assert.Equal("Io vado a casa.", FillerRemover.Remove("Io mh vado a casa.", "it"));
    }

    // Regel 2 der Listen: Deutsch hat nur die gemeinsamen Zögerlaute, die Laute anderer Sprachen bleiben.
    [Theory]
    [InlineData("Ich uh gehe.")]
    [InlineData("Ich mh gehe.")]
    [InlineData("Er läuft um das Haus.")]
    public void Laute_anderer_Sprachen_bleiben_im_Deutschen(string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, "de"));
    }

    // Regel 1 und Regel 7: Ein Zögerlaut am Satzanfang entfällt auch ohne Komma, das nächste Wort beginnt groß.
    [Theory]
    [InlineData("Ähm ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("ähm das ist gut.", "Das ist gut.")]
    [InlineData("Gut. Ähm, das geht.", "Gut. Das geht.")]
    [InlineData("Gut. Äh das geht.", "Gut. Das geht.")]
    [InlineData("Ähm, über uns.", "Über uns.")]
    public void Zoegerlaut_am_Satzanfang_entfaellt_und_das_naechste_Wort_beginnt_gross(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 1: Mehrere Zögerlaute in einem Text entfallen alle.
    [Theory]
    [InlineData("Ähm, äh, ich wollte gehen.", "Ich wollte gehen.")]
    [InlineData("Ich wollte äh ähm gehen.", "Ich wollte gehen.")]
    [InlineData("Ähm, ich wollte, äh, gehen.", "Ich wollte gehen.")]
    public void Mehrere_Zoegerlaute_entfallen_alle(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 1: Zusatzwörter des Nutzers entfallen überall, als ganze Wörter, ohne Groß und Klein.
    [Theory]
    [InlineData("Das ist sozusagen fertig.", "Das ist fertig.")]
    [InlineData("Das ist SOZUSAGEN fertig.", "Das ist fertig.")]
    [InlineData("Das ist, sozusagen, fertig.", "Das ist fertig.")]
    [InlineData("Sozusagen das Ende.", "Das Ende.")]
    public void Zusatzwort_entfaellt_ueberall(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de", ["sozusagen"]));
    }

    // Regel 1: Zusatzwörter dürfen aus mehreren Wörtern bestehen.
    [Theory]
    [InlineData("Das ist sag ich mal fertig.", "Das ist fertig.")]
    [InlineData("Das ist, sag ich mal, fertig.", "Das ist fertig.")]
    [InlineData("Das ist Sag Ich Mal fertig.", "Das ist fertig.")]
    public void Zusatzwort_aus_mehreren_Woertern_entfaellt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de", ["sag ich mal"]));
    }

    // Regel 1: Zusatzwörter gelten nur als ganze Wörter.
    [Fact]
    public void Zusatzwort_als_Wortteil_bleibt()
    {
        Assert.Equal("Quasimodo läutet.", FillerRemover.Remove("Quasimodo läutet.", "de", ["quasi"]));
    }

    // Regel 1: Zusatzwörter gelten in jeder Sprache, auch in einer unbekannten.
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("")]
    [InlineData(null)]
    public void Zusatzwort_gilt_in_jeder_Sprache(string? sprache)
    {
        Assert.Equal("Das ist fertig.", FillerRemover.Remove("Das ist quasi fertig.", sprache, ["quasi"]));
    }

    // Ohne Zusatzwörter bleibt ein solches Wort stehen.
    [Fact]
    public void Ohne_Zusatzwoerter_bleibt_das_Wort()
    {
        Assert.Equal("Das ist sozusagen fertig.", FillerRemover.Remove("Das ist sozusagen fertig.", "de"));
        Assert.Equal("Das ist sozusagen fertig.", FillerRemover.Remove("Das ist sozusagen fertig.", "de", []));
    }

    // ---------- Regel 2: Einstiege der Gruppe "immer" ----------

    // Regel 2: mit folgendem Komma entfallen sie am Satzanfang immer, auch klein geschrieben.
    [Theory]
    [InlineData("Also, das geht.")]
    [InlineData("Naja, das geht.")]
    [InlineData("Na ja, das geht.")]
    [InlineData("Tja, das geht.")]
    [InlineData("also, das geht.")]
    [InlineData("naja, das geht.")]
    [InlineData("na ja, das geht.")]
    [InlineData("tja, das geht.")]
    public void Einstieg_immer_mit_Komma_entfaellt_am_Textanfang(string text)
    {
        Assert.Equal("Das geht.", FillerRemover.Remove(text, "de"));
    }

    // Regel 2: mit folgendem Komma immer, auch klein geschrieben nach einem Satzende.
    [Theory]
    [InlineData("Gut. Also, das geht.")]
    [InlineData("Gut. also, das geht.")]
    [InlineData("Gut. naja, das geht.")]
    [InlineData("Gut. Tja, das geht.")]
    [InlineData("Gut. Na ja, das geht.")]
    public void Einstieg_immer_mit_Komma_entfaellt_nach_Satzende(string text)
    {
        Assert.Equal("Gut. Das geht.", FillerRemover.Remove(text, "de"));
    }

    // Regel 2: ohne Komma entfällt der Einstieg, wenn er groß geschrieben ist.
    [Theory]
    [InlineData("Gut. Also das geht.", "Gut. Das geht.")]
    [InlineData("Gut. Naja das geht.", "Gut. Das geht.")]
    [InlineData("Gut. Tja das geht.", "Gut. Das geht.")]
    [InlineData("Gut. Na ja das geht.", "Gut. Das geht.")]
    [InlineData("Tja das war es dann.", "Das war es dann.")]
    [InlineData("Naja das geht schon.", "Das geht schon.")]
    public void Einstieg_immer_ohne_Komma_entfaellt_wenn_gross_geschrieben(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 2: ohne Komma entfällt der Einstieg auch klein geschrieben, wenn er ganz am Textanfang steht.
    [Theory]
    [InlineData("also ich wollte fragen.", "Ich wollte fragen.")]
    [InlineData("tja das war es dann.", "Das war es dann.")]
    [InlineData("na ja das geht schon.", "Das geht schon.")]
    public void Einstieg_immer_ohne_Komma_entfaellt_klein_am_Textanfang(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 2: ohne Komma und klein geschrieben bleibt der Einstieg nach einem Satzende stehen (wie "Am 3. also nicht.").
    [Theory]
    [InlineData("Gut. also das geht.")]
    [InlineData("Um 5. naja das geht.")]
    public void Einstieg_immer_ohne_Komma_klein_nach_Satzende_bleibt(string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, "de"));
    }

    // Regel 2 und 3: Mitten im Satz und ohne Kommas bleibt ein Einstieg stehen.
    [Theory]
    [InlineData("Das ist naja nicht gut.")]
    [InlineData("Das war tja schade.")]
    [InlineData("Ich denke, also bin ich.")]
    [InlineData("Ich bin also, glaube ich, fertig.")]
    public void Einstieg_mitten_im_Satz_bleibt(string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, "de"));
    }

    // Regel 2 der Listen: Die Gruppe "immer" gibt es nur im Deutschen.
    [Theory]
    [InlineData("en", "Also, I agree.")]
    [InlineData("en", "Also I agree.")]
    [InlineData("it", "Also, andiamo.")]
    [InlineData("", "Also, ich wollte fragen.")]
    [InlineData("", "Also ich wollte fragen.")]
    [InlineData("fr", "Naja, das geht.")]
    public void Einstieg_immer_bleibt_in_anderen_Sprachen(string sprache, string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, sprache));
    }

    // ---------- Regel 3: Einstiege beider Gruppen mit Komma ----------

    // Regel 3: Einstiege der Gruppe "nur mit Komma" entfallen am Satzanfang mit folgendem Komma.
    [Theory]
    [InlineData("de", "So, das war es.", "Das war es.")]
    [InlineData("de", "Gut. So, das war es.", "Gut. Das war es.")]
    [InlineData("en", "So, that works.", "That works.")]
    [InlineData("en", "Fine. Well, that works.", "Fine. That works.")]
    [InlineData("it", "Dunque, andiamo.", "Andiamo.")]
    [InlineData("it", "Bene. Allora, andiamo.", "Bene. Andiamo.")]
    public void Einstieg_mit_Komma_entfaellt_am_Satzanfang(string sprache, string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, sprache));
    }

    // Regel 3: Ohne folgendes Komma bleiben Einstiege der Gruppe "nur mit Komma" stehen.
    [Theory]
    [InlineData("de", "So geht das.")]
    [InlineData("de", "Ich bin so müde.")]
    [InlineData("en", "So far so good.")]
    [InlineData("en", "Well done.")]
    [InlineData("it", "Dunque andiamo.")]
    public void Einstieg_mit_Komma_bleibt_ohne_Komma(string sprache, string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, sprache));
    }

    // Regel 3: Einstiege beider Gruppen entfallen als Einschub zwischen zwei Kommas. Regel 6: beide Kommas entfallen.
    [Theory]
    [InlineData("de", "Ich habe, also, keine Lust.", "Ich habe keine Lust.")]
    [InlineData("de", "Das war, tja, schade.", "Das war schade.")]
    [InlineData("de", "Das ist, naja, nicht gut.", "Das ist nicht gut.")]
    [InlineData("de", "Das ist, so, nicht richtig.", "Das ist nicht richtig.")]
    [InlineData("en", "I think, well, it works.", "I think it works.")]
    [InlineData("en", "It is, so, not right.", "It is not right.")]
    [InlineData("it", "Andiamo, allora, a casa.", "Andiamo a casa.")]
    [InlineData("it", "Andiamo, dunque, a casa.", "Andiamo a casa.")]
    public void Einstieg_als_Einschub_zwischen_zwei_Kommas_entfaellt(string sprache, string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, sprache));
    }

    // Regel 2 der Listen: Einstiege einer anderen Sprache bleiben, unbekannte Sprache kennt keine Einstiege.
    [Theory]
    [InlineData("de", "Well, das geht.")]
    [InlineData("de", "Allora, das geht.")]
    [InlineData("it", "Well, andiamo.")]
    [InlineData("en", "Allora, that works.")]
    [InlineData("", "So, das geht.")]
    [InlineData("", "Well, that works.")]
    [InlineData("fr", "Allora, andiamo.")]
    public void Einstieg_einer_anderen_Sprache_bleibt(string sprache, string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, sprache));
    }

    // ---------- Regel 4: Einstieg vor Satzende oder Textende bleibt ----------

    [Theory]
    [InlineData("de", "Also.")]
    [InlineData("de", "Also!")]
    [InlineData("de", "Also?")]
    [InlineData("de", "Also")]
    [InlineData("de", "So.")]
    [InlineData("de", "Naja.")]
    [InlineData("de", "Na ja.")]
    [InlineData("de", "Tja.")]
    [InlineData("de", "Gut. Also.")]
    [InlineData("de", "Das war es. Tja.")]
    [InlineData("en", "Well.")]
    [InlineData("en", "So?")]
    [InlineData("it", "Allora.")]
    [InlineData("it", "Dunque!")]
    public void Einstieg_vor_Satzende_oder_Textende_bleibt(string sprache, string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, sprache));
    }

    // ---------- Regel 5: Satzanfang ----------

    // Regel 5: Satzanfang ist die Stelle nach Punkt, Ausrufezeichen, Fragezeichen oder Auslassungszeichen.
    [Theory]
    [InlineData("Stimmt. Also, das geht.", "Stimmt. Das geht.")]
    [InlineData("Stimmt! Also, das geht.", "Stimmt! Das geht.")]
    [InlineData("Wirklich? Naja, das geht.", "Wirklich? Das geht.")]
    [InlineData("Warte\u2026 Also, das geht.", "Warte\u2026 Das geht.")]
    [InlineData("Warte... Also, das geht.", "Warte... Das geht.")]
    [InlineData("Stimmt! Also das geht.", "Stimmt! Das geht.")]
    [InlineData("Wirklich? So, das geht.", "Wirklich? Das geht.")]
    public void Satzanfang_nach_Satzende_wird_erkannt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 5: Öffnende Anführungszeichen und Klammern zwischen Satzende und Wort zählen nicht.
    [Theory]
    [InlineData("\"Also, das geht.\"", "\"Das geht.\"")]
    [InlineData("\u201EAlso, das geht.\u201C", "\u201EDas geht.\u201C")]
    [InlineData("(Also, das geht.)", "(Das geht.)")]
    [InlineData("Gut. \"Also, das geht.\"", "Gut. \"Das geht.\"")]
    [InlineData("Gut. \u201ENaja, das geht.\u201C", "Gut. \u201EDas geht.\u201C")]
    [InlineData("Gut. (Also, das geht.)", "Gut. (Das geht.)")]
    [InlineData("(Ähm, das geht.)", "(Das geht.)")]
    public void Satzanfang_hinter_oeffnendem_Zeichen_wird_erkannt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 5: Ein Doppelpunkt oder ein Komma ist kein Satzende, dahinter ist also kein Satzanfang.
    [Theory]
    [InlineData("Merke: also, das geht.")]
    [InlineData("Merke: so, das geht.")]
    [InlineData("Ich denke; also, das geht.")]
    public void Kein_Satzanfang_nach_Doppelpunkt_oder_Semikolon(string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, "de"));
    }

    // ---------- Regel 6: Satzzeichen ----------

    // Regel 6: Zwischen zwei Kommas entfallen beide Kommas.
    [Theory]
    [InlineData("Ich, äh, komme.", "Ich komme.")]
    [InlineData("Wir gehen, hm, morgen.", "Wir gehen morgen.")]
    public void Zwischen_zwei_Kommas_entfallen_beide(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 6: Komma davor und Satzende danach: das Komma davor entfällt.
    [Theory]
    [InlineData("Das war es, ähm!", "Das war es!")]
    [InlineData("War es das, ähm?", "War es das?")]
    [InlineData("Das war es, äh. Gut.", "Das war es. Gut.")]
    public void Komma_davor_und_Satzende_danach_das_Komma_entfaellt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 6: Folgt nur ein Komma, entfällt dieses.
    [Theory]
    [InlineData("Ich wollte ähm, gehen.", "Ich wollte gehen.")]
    [InlineData("Hm, das stimmt.", "Das stimmt.")]
    public void Folgt_nur_ein_Komma_entfaellt_dieses(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 6, aus dem Beispiel "Ja, ähm ich wollte": Steht nur davor ein Komma, bleibt es stehen.
    [Fact]
    public void Steht_nur_davor_ein_Komma_bleibt_es()
    {
        Assert.Equal("Nein, ich wollte gehen.", FillerRemover.Remove("Nein, äh ich wollte gehen.", "de"));
    }

    // Regel 6: Ohne Kommas entfällt nur das Wort, der Leerraum wird aufgeräumt.
    [Fact]
    public void Ohne_Kommas_entfaellt_nur_das_Wort()
    {
        Assert.Equal("Das war es.", FillerRemover.Remove("Das war es ähm.", "de"));
    }

    // Regel 6: Auslassungspunkte direkt am Füllwort entfallen am Satzanfang mit.
    [Theory]
    [InlineData("Ähm\u2026 ich weiß nicht.", "Ich weiß nicht.")]
    [InlineData("Gut. Ähm... ich weiß nicht.", "Gut. Ich weiß nicht.")]
    [InlineData("Gut. Hm\u2026 ich weiß nicht.", "Gut. Ich weiß nicht.")]
    public void Auslassungspunkte_am_Satzanfang_entfallen_mit(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 6: Mitten im Satz bleiben die Auslassungspunkte und rücken an das Wort davor.
    [Theory]
    [InlineData("Ich wollte ähm\u2026 sagen.", "Ich wollte\u2026 sagen.")]
    [InlineData("Wir gehen hm... morgen.", "Wir gehen... morgen.")]
    public void Auslassungspunkte_mitten_im_Satz_ruecken_an_das_Wort_davor(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // ---------- Regel 7: Großschreibung ----------

    // Regel 7: Nur am Satzanfang beginnt das nächste Wort groß, mitten im Satz bleibt es, wie es ist.
    [Theory]
    [InlineData("Wir gehen ähm morgen los.", "Wir gehen morgen los.")]
    [InlineData("Wir gehen, ähm, morgen los.", "Wir gehen morgen los.")]
    public void Mitten_im_Satz_wird_nichts_gross_geschrieben(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Regel 7: Ein schon groß geschriebenes Wort bleibt groß.
    [Fact]
    public void Schon_grosses_Wort_bleibt_gross()
    {
        Assert.Equal("Stefano kommt.", FillerRemover.Remove("Ähm, Stefano kommt.", "de"));
    }

    // Regel 7 gilt für jedes entfernte Füllwort am Satzanfang, auch für Einstiege und in anderen Sprachen.
    [Theory]
    [InlineData("de", "So, über uns.", "Über uns.")]
    [InlineData("en", "Um, that works.", "That works.")]
    [InlineData("it", "Allora, \u00E8 vero.", "\u00C8 vero.")]
    public void Naechstes_Wort_beginnt_gross(string sprache, string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, sprache));
    }

    // ---------- Regel 8: Aufräumen und leeres Ergebnis ----------

    // Regel 8: Null oder nur Leerraum ergibt leer.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData(" \t\r\n ")]
    public void Null_oder_nur_Leerraum_ergibt_leer(string? text)
    {
        Assert.Equal(string.Empty, FillerRemover.Remove(text, "de"));
    }

    // Regel 8: Bleibt kein Buchstabe und keine Ziffer, ist das Ergebnis leer.
    [Theory]
    [InlineData("Ähm, äh.")]
    [InlineData("Äh, ähm!")]
    [InlineData("Ähm... hm.")]
    [InlineData("Hm\u2026")]
    [InlineData("ähm")]
    [InlineData("...")]
    [InlineData("?!")]
    public void Ohne_Buchstabe_und_Ziffer_ist_das_Ergebnis_leer(string text)
    {
        Assert.Equal(string.Empty, FillerRemover.Remove(text, "de"));
    }

    // Regel 8: Eine Ziffer reicht, damit das Ergebnis nicht leer ist.
    [Fact]
    public void Eine_Ziffer_bleibt_stehen()
    {
        Assert.Equal("3.", FillerRemover.Remove("Ähm, 3.", "de"));
    }

    // Regel 8: Am Ende wird der Leerraum aufgeräumt, auch an den Rändern.
    [Theory]
    [InlineData("Ich   wollte  ähm   gehen.", "Ich wollte gehen.")]
    [InlineData("  Ähm, ich wollte fragen.  ", "Ich wollte fragen.")]
    [InlineData("Ich wollte\r\nähm\r\ngehen.", "Ich wollte gehen.")]
    [InlineData("Hallo   Welt.", "Hallo Welt.")]
    [InlineData("  Hallo Welt.  ", "Hallo Welt.")]
    public void Leerraum_wird_aufgeraeumt(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    // Ein Text ohne Füllwörter bleibt, wie er ist.
    [Theory]
    [InlineData("de", "Ich wollte fragen, ob es klappt.")]
    [InlineData("de", "Das .NET SDK ist da.")]
    [InlineData("en", "I think it works.")]
    [InlineData("it", "Andiamo a casa.")]
    [InlineData("", "Bonjour tout le monde.")]
    public void Text_ohne_Fuellwoerter_bleibt(string sprache, string text)
    {
        Assert.Equal(text, FillerRemover.Remove(text, sprache));
    }

    // ---------- Bekannte Grenzen ----------

    // Bekannte Grenze 3: "Also" am Satzanfang ohne Komma entfällt auch dann, wenn es eine Folgerung meint.
    [Fact]
    public void Grenze_Also_als_Folgerung_entfaellt_trotzdem()
    {
        Assert.Equal("Gilt: das stimmt.", FillerRemover.Remove("Also gilt: das stimmt.", "de"));
    }

    // Bekannte Grenze 4: Die Stelle nach einer Abkürzung mit Punkt gilt als Satzanfang.
    [Theory]
    [InlineData("Das gilt z. B. Also nicht.", "Das gilt z. B. Nicht.")]
    [InlineData("Das gilt z. B. also, wie gesagt, nicht.", "Das gilt z. B. Wie gesagt, nicht.")]
    public void Grenze_nach_Abkuerzung_mit_Punkt_gilt_als_Satzanfang(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }
}
