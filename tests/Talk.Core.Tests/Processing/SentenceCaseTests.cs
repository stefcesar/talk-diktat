using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft die Satzanfang-Großschreibung ausschließlich gegen die vorgegebenen Regeln 1 bis 4
/// (Satzgrenzen, erstes Wort, Abkürzungen und Zahlen, sonst alles unverändert). Die Tests kennen die Umsetzung nicht.
/// </summary>
public sealed class SentenceCaseTests
{
    // ---------- Regel 4: leere Eingaben ----------

    // Regel 4: null und leer ergeben die leere Zeichenkette.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Apply_null_oder_leer_ergibt_leer(string? text)
    {
        Assert.Equal(string.Empty, SentenceCase.Apply(text));
    }

    // ---------- Regel 1: Satzgrenzen ----------

    // Regel 1: Textanfang und '.', '!', '?', '…' mit folgendem Leerraum beginnen einen Satz.
    [Theory]
    [InlineData("ich komme. du auch?", "Ich komme. Du auch?")]
    [InlineData("hallo! wie geht's? gut… danke", "Hallo! Wie geht's? Gut… Danke")]
    [InlineData("ende.", "Ende.")]
    [InlineData("echt?! ja", "Echt?! Ja")]
    [InlineData("ca? ja", "Ca? Ja")]
    [InlineData("es sind 3! super", "Es sind 3! Super")]
    public void Satzende_mit_Leerraum_beginnt_neuen_Satz(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 1: Jede Art Leerraum zählt, und der Leerraum bleibt Zeichen für Zeichen erhalten (Regel 4).
    [Theory]
    [InlineData("gut.\n\nneu hier", "Gut.\n\nNeu hier")]
    [InlineData("gut.\tneu", "Gut.\tNeu")]
    [InlineData("gut.  neu", "Gut.  Neu")]
    [InlineData("gut.\r\nneu", "Gut.\r\nNeu")]
    public void Jeder_Leerraum_nach_Satzende_zaehlt(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 1: Ein Satzzeichen ohne folgenden Leerraum ist kein Satzende.
    [Theory]
    [InlineData("klar.beispiel ist gut", "Klar.beispiel ist gut")]
    [InlineData("es sind 3.5 liter", "Es sind 3.5 liter")]
    [InlineData("gut…danke", "Gut…danke")]
    [InlineData("was?ja", "Was?ja")]
    public void Satzzeichen_ohne_Leerraum_ist_kein_Satzende(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 1: Schließende Zeichen zwischen Satzzeichen und Leerraum heben das Satzende nicht auf.
    [Theory]
    [InlineData("er sagte „ja.“ dann ging er", "Er sagte „ja.“ Dann ging er")]
    [InlineData("(das ist gut.) und jetzt", "(Das ist gut.) Und jetzt")]
    [InlineData("er rief \"hilfe!\" dann", "Er rief \"hilfe!\" Dann")]
    [InlineData("sie fragte »wer?« niemand", "Sie fragte »wer?« Niemand")]
    [InlineData("er sagte 'gut.' dann", "Er sagte 'gut.' Dann")]
    [InlineData("er sagte ‚gut.’ dann", "Er sagte ‚gut.’ Dann")]
    [InlineData("siehe [oben.] weiter", "Siehe [oben.] Weiter")]
    [InlineData("so «ja.» weiter", "So «ja.» Weiter")]
    [InlineData("so (wirklich.)\" weiter", "So (wirklich.)\" Weiter")]
    public void Schliessende_Zeichen_vor_dem_Leerraum(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 1 und 2: Öffnende Zeichen nach dem Leerraum werden übersprungen, der Buchstabe danach wird groß.
    [Theory]
    [InlineData("gut. „das“ sagte er", "Gut. „Das“ sagte er")]
    [InlineData("gut. (siehe oben)", "Gut. (Siehe oben)")]
    [InlineData("gut. 'ja' sagte er", "Gut. 'Ja' sagte er")]
    [InlineData("gut. »ja« sagte er", "Gut. »Ja« sagte er")]
    [InlineData("gut. «ja» sagte er", "Gut. «Ja» sagte er")]
    [InlineData("gut. \"ja\" sagte er", "Gut. \"Ja\" sagte er")]
    [InlineData("gut. “ja” sagte er", "Gut. “Ja” sagte er")]
    [InlineData("gut. ‚ja’ sagte er", "Gut. ‚Ja’ sagte er")]
    [InlineData("gut. [ja] sagte er", "Gut. [Ja] sagte er")]
    [InlineData("gut. („ja“) sagte er", "Gut. („Ja“) sagte er")]
    public void Oeffnende_Zeichen_nach_dem_Leerraum(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 2: Auch am Textanfang werden öffnende Zeichen übersprungen.
    [Theory]
    [InlineData("„ja“, sagte er", "„Ja“, sagte er")]
    [InlineData("(das ist gut.) und jetzt", "(Das ist gut.) Und jetzt")]
    [InlineData("»hallo«", "»Hallo«")]
    public void Oeffnende_Zeichen_am_Textanfang(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // ---------- Regel 2: erstes Wort ----------

    // Regel 2: Nur der erste Buchstabe des ersten Wortes wird groß, der Rest des Satzes bleibt.
    [Theory]
    [InlineData("über alles", "Über alles")]
    [InlineData("à la carte", "À la carte")]
    [InlineData("élan und mut", "Élan und mut")]
    [InlineData("iphone ist da", "Iphone ist da")]
    [InlineData("ä", "Ä")]
    public void Erster_Kleinbuchstabe_wird_gross(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 2: Ein Wort mit weiterem Großbuchstaben bleibt unverändert.
    [Theory]
    [InlineData("iPhone ist da")]
    [InlineData("eBay ist gut. iPhone auch")]
    [InlineData("mcDonald hat zu")]
    public void Wort_mit_innerem_Grossbuchstaben_bleibt(string text)
    {
        Assert.Equal(text, SentenceCase.Apply(text));
    }

    // Regel 2: Der innere Großbuchstabe schützt nur sein eigenes Wort, andere Sätze werden trotzdem groß.
    [Fact]
    public void Inneres_Grossbuchstabe_schuetzt_nur_das_eigene_Wort()
    {
        Assert.Equal("Ich mag es. eBay auch. Und mehr", SentenceCase.Apply("ich mag es. eBay auch. und mehr"));
    }

    // Regel 2: Beginnt der Satz mit Ziffer oder anderem Nicht-Buchstaben, ändert sich nichts.
    [Theory]
    [InlineData("3 äpfel")]
    [InlineData("- ja, genau")]
    [InlineData("Gut. 3 äpfel")]
    [InlineData("Gut. - ja")]
    [InlineData("Gut. (3 mal)")]
    [InlineData("Gut. „42“ sagte er")]
    public void Satzanfang_ohne_Buchstabe_bleibt(string text)
    {
        Assert.Equal(text, SentenceCase.Apply(text));
    }

    // Regel 2 und 4: Ein schon richtiger Text bleibt Zeichen für Zeichen gleich.
    [Theory]
    [InlineData("Ich komme. Du auch?")]
    [InlineData("Hallo! Wie geht's? Gut… Danke")]
    [InlineData("ÜBER alles. NEIN danke")]
    [InlineData("Das kostet ca. zehn euro. Danach")]
    public void Richtiger_Text_bleibt_unveraendert(string text)
    {
        Assert.Equal(text, SentenceCase.Apply(text));
    }

    // Regel 4: Andere Buchstaben und Satzzeichen bleiben, auch Großbuchstaben mitten im Satz werden nicht klein.
    [Fact]
    public void Uebriger_Text_bleibt_Zeichen_fuer_Zeichen()
    {
        Assert.Equal("Wir FAHREN nach Hannover, oder?  Ja; gern!", SentenceCase.Apply("wir FAHREN nach Hannover, oder?  ja; gern!"));
    }

    // ---------- Regel 3: Abkürzungen, Einzelbuchstaben, Zahlen ----------

    // Regel 3: Nach einer Abkürzung aus der Liste ist der Punkt kein Satzende.
    [Theory]
    [InlineData("ca. zehn leute", "Ca. zehn leute")]
    [InlineData("das kostet ca. zehn euro. danach", "Das kostet ca. zehn euro. Danach")]
    [InlineData("äpfel bzw. birnen", "Äpfel bzw. birnen")]
    [InlineData("äpfel, birnen usw. sind gut", "Äpfel, birnen usw. sind gut")]
    [InlineData("äpfel etc. sind gut", "Äpfel etc. sind gut")]
    [InlineData("siehe vgl. oben", "Siehe vgl. oben")]
    [InlineData("komm ggf. später", "Komm ggf. später")]
    [InlineData("komm evtl. später", "Komm evtl. später")]
    [InlineData("preis inkl. steuer", "Preis inkl. steuer")]
    [InlineData("siehe nr. drei", "Siehe nr. drei")]
    [InlineData("dr. müller kommt", "Dr. müller kommt")]
    [InlineData("prof. dr. meier kommt", "Prof. dr. meier kommt")]
    [InlineData("hallo hr. meier", "Hallo hr. meier")]
    [InlineData("hallo fr. meier", "Hallo fr. meier")]
    [InlineData("wir sind in st. pauli", "Wir sind in st. pauli")]
    public void Abkuerzung_beendet_keinen_Satz(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 3: Der Vergleich mit der Liste beachtet die Schreibweise nicht.
    [Theory]
    [InlineData("es kostet CA. zehn", "Es kostet CA. zehn")]
    [InlineData("es kostet Ca. zehn", "Es kostet Ca. zehn")]
    [InlineData("Dr. müller kommt", "Dr. müller kommt")]
    [InlineData("äpfel BZW. birnen", "Äpfel BZW. birnen")]
    public void Abkuerzung_ohne_Ruecksicht_auf_Schreibweise(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 3: Nach einem einzelnen Buchstaben ist der Punkt kein Satzende.
    [Theory]
    [InlineData("z. B. das", "Z. B. das")]
    [InlineData("d. h. wir gehen", "D. h. wir gehen")]
    [InlineData("er heißt k. müller", "Er heißt k. müller")]
    [InlineData("u. a. diese", "U. a. diese")]
    [InlineData("s. oben", "S. oben")]
    public void Einzelbuchstabe_beendet_keinen_Satz(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 3: Bei Abkürzungen mit Punkten zählt auch der letzte durch Punkt getrennte Teil.
    [Theory]
    [InlineData("siehe z.B. das", "Siehe z.B. das")]
    [InlineData("das heißt d.h. wir", "Das heißt d.h. wir")]
    [InlineData("äpfel u.s.w. sind gut", "Äpfel u.s.w. sind gut")]
    public void Letzter_Teil_einer_Punktabkuerzung_zaehlt(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 3: Nach einer Zahl ist der Punkt kein Satzende.
    [Theory]
    [InlineData("am 3. mai kommt er", "Am 3. mai kommt er")]
    [InlineData("kapitel 12. danach", "Kapitel 12. danach")]
    [InlineData("der 1. platz", "Der 1. platz")]
    public void Zahl_beendet_keinen_Satz(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // Regel 3: Ein Wort, das nur auf eine Abkürzung oder einen Buchstaben endet, ist keine Abkürzung.
    [Theory]
    [InlineData("das ist. dann", "Das ist. Dann")]
    [InlineData("ich mache zu. dann", "Ich mache zu. Dann")]
    [InlineData("wir sind da. jetzt", "Wir sind da. Jetzt")]
    [InlineData("er ist prima. danke", "Er ist prima. Danke")]
    public void Wortende_wie_Abkuerzung_ist_trotzdem_Satzende(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // ---------- Abkürzungsliste ----------

    // Vertrag: Die Liste enthält mindestens die vorgegebenen Abkürzungen ohne Punkt.
    [Theory]
    [InlineData("ca")]
    [InlineData("bzw")]
    [InlineData("usw")]
    [InlineData("etc")]
    [InlineData("vgl")]
    [InlineData("ggf")]
    [InlineData("evtl")]
    [InlineData("inkl")]
    [InlineData("nr")]
    [InlineData("dr")]
    [InlineData("prof")]
    [InlineData("hr")]
    [InlineData("fr")]
    [InlineData("st")]
    [InlineData("z")]
    [InlineData("d")]
    [InlineData("u")]
    [InlineData("s")]
    public void Abkuerzungsliste_enthaelt_Pflichteintraege(string abkuerzung)
    {
        Assert.Contains(abkuerzung, SentenceCase.Abbreviations);
    }

    // Vertrag: Die Liste vergleicht ohne Rücksicht auf die Schreibweise und führt Einträge ohne den abschließenden Punkt.
    [Fact]
    public void Abkuerzungsliste_ohne_Schreibweise_und_ohne_Punkte()
    {
        Assert.Contains("CA", SentenceCase.Abbreviations);
        Assert.Contains("Bzw", SentenceCase.Abbreviations);
        Assert.DoesNotContain(SentenceCase.Abbreviations, a => a.EndsWith('.'));
    }

    // ---------- Einbindung in die Nachbearbeitung ----------

    private const string Rohtext = "ich komme heute. du auch? gut… danke";

    // Optionen: Die Großschreibung ist ab Werk aus.
    [Fact]
    public void Optionen_Grossschreibung_ab_Werk_aus()
    {
        Assert.False(new PostProcessingOptions().CapitalizeSentences);
    }

    // Optionen: Eingeschaltet ist das Ergebnis die Satzanfang-Großschreibung des Ergebnisses ohne sie.
    [Fact]
    public void ProcessDetailed_mit_Grossschreibung_wendet_sie_nach_der_Bereinigung_an()
    {
        PostProcessor nachbearbeitung = new();
        string ohne = nachbearbeitung.ProcessDetailed(Rohtext, new PostProcessingOptions(RemoveFillers: false)).Text;
        string mit = nachbearbeitung.ProcessDetailed(Rohtext, new PostProcessingOptions(RemoveFillers: false, CapitalizeSentences: true)).Text;

        Assert.Equal(SentenceCase.Apply(ohne), mit);
        Assert.Contains("Du auch?", mit);
        Assert.Contains("Danke", mit);
    }

    // Optionen: Ausgeschaltet ändert sich an Groß- und Kleinschreibung nichts.
    [Fact]
    public void ProcessDetailed_ohne_Grossschreibung_laesst_Schreibweise()
    {
        PostProcessor nachbearbeitung = new();
        string ohne = nachbearbeitung.ProcessDetailed(Rohtext, new PostProcessingOptions(RemoveFillers: false)).Text;

        Assert.Contains("du auch?", ohne);
        Assert.Contains("danke", ohne);
    }

    // Optionen: Auch mit Füllwortentfernung kommt die Großschreibung nach der Bereinigung.
    [Fact]
    public void ProcessDetailed_Grossschreibung_nach_Fuellwortentfernung()
    {
        PostProcessor nachbearbeitung = new();
        const string text = "ähm ich komme. äh du auch?";
        string ohne = nachbearbeitung.ProcessDetailed(text, new PostProcessingOptions()).Text;
        string mit = nachbearbeitung.ProcessDetailed(text, new PostProcessingOptions(CapitalizeSentences: true)).Text;

        Assert.Equal(SentenceCase.Apply(ohne), mit);
    }

    // ---------- Adressen und Herkunft des Großbuchstabens (Befunde des Prüfers) ----------

    // Regel 2: Eine Adresse am Satzanfang bleibt, wie sie ist.
    [Theory]
    [InlineData("www.beispiel.de ist gut", "www.beispiel.de ist gut")]
    [InlineData("info@talk.de schreibt. dann", "info@talk.de schreibt. Dann")]
    [InlineData("gut. https://talk.de geht", "Gut. https://talk.de geht")]
    public void Adresse_am_Satzanfang_bleibt(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.Apply(eingabe));
    }

    // RaisedStart: true nur, wenn erst die Regel den Anfang groß gemacht hat, auch wenn davor ein Füllwort gestrichen wurde.
    [Theory]
    [InlineData("gestern beim Arzt.", "Gestern beim Arzt.", true)]
    [InlineData("äh gestern beim Arzt.", "Gestern beim Arzt.", true)]
    [InlineData("„gestern“ war gut", "„Gestern“ war gut", true)]
    [InlineData("Gestern beim Arzt.", "Gestern beim Arzt.", false)]
    [InlineData("gestern beim Arzt.", "gestern beim Arzt.", false)]
    [InlineData("heute geht es", "Morgen geht es", false)]
    [InlineData("", "Gestern", false)]
    [InlineData(null, null, false)]
    [InlineData("3 Äpfel", "3 Äpfel", false)]
    public void RaisedStart_erkennt_Grossbuchstaben_von_Talk(string? erkannt, string? fertig, bool erwartet)
    {
        Assert.Equal(erwartet, SentenceCase.RaisedStart(erkannt, fertig));
    }
}
