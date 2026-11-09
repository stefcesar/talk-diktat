using Talk.Core.Processing.Smoothing;

using Xunit;

namespace Talk.Core.Tests.Processing.Smoothing;

/// <summary>
/// Prüft Regel 8 aus Playbook Abschnitt 21 ("Satzzeichen am Ende"): Endet der Eingang mit Punkt, Ausrufezeichen,
/// Fragezeichen oder Auslassungszeichen und die Antwort mit Buchstabe oder Ziffer, wird das letzte Satzzeichen
/// des Eingangs angehängt. Geschrieben nur aus der Regel, nicht aus der Umsetzung. Alle Sätze sind erfunden.
/// </summary>
public sealed class FinalPunctuationTests
{
    // Regel 8: jedes der vier Satzzeichen am Eingang, Antwort endet mit Buchstabe.
    [Theory]
    [InlineData("Der Zug fährt um acht Uhr ab.", "Der Zug fährt um acht Uhr ab", "Der Zug fährt um acht Uhr ab.")]
    [InlineData("Das ist ja großartig!", "Das ist ja großartig", "Das ist ja großartig!")]
    [InlineData("Kommst du morgen mit?", "Kommst du morgen mit", "Kommst du morgen mit?")]
    [InlineData("Und dann war es still…", "Und dann war es still", "Und dann war es still…")]
    public void Satzzeichen_wird_angehaengt_wenn_Antwort_mit_Buchstabe_endet(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Antwort endet mit Ziffer.
    [Theory]
    [InlineData("Das Treffen ist im Jahr 2026.", "Das Treffen ist im Jahr 2026", "Das Treffen ist im Jahr 2026.")]
    [InlineData("Wir brauchen genau 3!", "Wir brauchen genau 3", "Wir brauchen genau 3!")]
    [InlineData("Ist es Zimmer 12?", "Ist es Zimmer 12", "Ist es Zimmer 12?")]
    [InlineData("Wir zählen bis 10…", "Wir zählen bis 10", "Wir zählen bis 10…")]
    public void Satzzeichen_wird_angehaengt_wenn_Antwort_mit_Ziffer_endet(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Umlaut und ß zählen als Buchstabe.
    [Theory]
    [InlineData("Das Wetter ist schön.", "Das Wetter ist schön", "Das Wetter ist schön.")]
    [InlineData("Das klingt nach Spaß!", "Das klingt nach Spaß", "Das klingt nach Spaß!")]
    [InlineData("Ist das Brot schon gebräunt, oder ist es zu früh?", "Ist das Brot schon gebräunt oder zu früh", "Ist das Brot schon gebräunt oder zu früh?")]
    [InlineData("Dann gehen wir über die Brücke…", "Dann gehen wir über die Brücke", "Dann gehen wir über die Brücke…")]
    [InlineData("Wir fahren nach Malmö.", "Wir fahren nach Malmö", "Wir fahren nach Malmö.")]
    [InlineData("Das ist ein Ü.", "Das ist ein Ü", "Das ist ein Ü.")]
    public void Satzzeichen_wird_angehaengt_wenn_Antwort_mit_Umlaut_endet(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Leerraum am Ende des Eingangs zählt nicht ("ohne Leerraum am Ende").
    [Theory]
    [InlineData("Der Kaffee ist fertig. ", "Der Kaffee ist fertig", "Der Kaffee ist fertig.")]
    [InlineData("Der Kaffee ist fertig.\n", "Der Kaffee ist fertig", "Der Kaffee ist fertig.")]
    [InlineData("Der Kaffee ist fertig?\t  \r\n", "Der Kaffee ist fertig", "Der Kaffee ist fertig?")]
    public void Leerraum_am_Ende_des_Eingangs_wird_uebergangen(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Leerraum am Ende der Antwort entfällt, wenn angehängt wird.
    [Theory]
    [InlineData("Der Kaffee ist fertig.", "Der Kaffee ist fertig ", "Der Kaffee ist fertig.")]
    [InlineData("Der Kaffee ist fertig!", "Der Kaffee ist fertig\n", "Der Kaffee ist fertig!")]
    [InlineData("Der Kaffee ist fertig…", "Der Kaffee ist fertig \t \r\n", "Der Kaffee ist fertig…")]
    [InlineData("Ist der Kaffee fertig? ", "Ist der Kaffee fertig  ", "Ist der Kaffee fertig?")]
    public void Leerraum_am_Ende_der_Antwort_entfaellt_beim_Anhaengen(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Endet die Antwort mit einem anderen Zeichen als Buchstabe oder Ziffer, bleibt sie unverändert.
    [Theory]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen.")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen!")]
    [InlineData("Wir sehen uns morgen.", "Sehen wir uns morgen?")]
    [InlineData("Wir sehen uns morgen!", "Wir sehen uns morgen…")]
    [InlineData("Sehen wir uns morgen?", "Wir sehen uns morgen.")]
    [InlineData("Wir sehen uns morgen…", "Wir sehen uns morgen.")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen (vielleicht)")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns [morgen]")]
    [InlineData("Er sagte, wir sehen uns morgen.", "Er sagte „wir sehen uns morgen“")]
    [InlineData("Er sagte, wir sehen uns morgen.", "Er sagte \"wir sehen uns morgen\"")]
    [InlineData("Er sagte, wir sehen uns morgen.", "Er sagte 'wir sehen uns morgen'")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen 😊")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen 👍")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen:")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen,")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen;")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen -")]
    public void Antwort_mit_eigenem_Schlusszeichen_bleibt_unveraendert(string eingang, string antwort)
    {
        Assert.Equal(antwort, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Eigenes Schlusszeichen mit Leerraum danach: Die Antwort bleibt unverändert, auch der Leerraum.
    [Theory]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen! ")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen (vielleicht)\n")]
    [InlineData("Wir sehen uns morgen.", "Wir sehen uns morgen 😊  ")]
    public void Antwort_mit_eigenem_Schlusszeichen_und_Leerraum_bleibt_unveraendert(string eingang, string antwort)
    {
        Assert.Equal(antwort, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Endet der Eingang nicht mit Punkt, Ausrufezeichen, Fragezeichen oder Auslassungszeichen,
    // bleibt die Antwort unverändert.
    [Theory]
    [InlineData("Wir sehen uns morgen", "Wir sehen uns morgen")]
    [InlineData("Wir sehen uns morgen,", "Wir sehen uns morgen")]
    [InlineData("Wir sehen uns morgen:", "Wir sehen uns morgen")]
    [InlineData("Wir sehen uns morgen;", "Wir sehen uns morgen")]
    [InlineData("Er sagte „wir sehen uns morgen.“", "Er sagte wir sehen uns morgen")]
    [InlineData("Er sagte \"wir sehen uns morgen.\"", "Er sagte wir sehen uns morgen")]
    [InlineData("Wir sehen uns morgen (vielleicht.)", "Wir sehen uns morgen vielleicht")]
    [InlineData("Wir sehen uns um 8", "Wir sehen uns um 8")]
    [InlineData("Wir sehen uns morgen 😊", "Wir sehen uns morgen")]
    [InlineData("Wir sehen uns morgen ", "Wir sehen uns morgen ")]
    public void Eingang_ohne_Schlusssatzzeichen_laesst_Antwort_unveraendert(string eingang, string antwort)
    {
        Assert.Equal(antwort, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: "das letzte Satzzeichen des Eingangs" wird angehängt, bei mehreren also nur das letzte.
    [Theory]
    [InlineData("Wirklich?!", "Wirklich", "Wirklich!")]
    [InlineData("Wirklich!?", "Wirklich", "Wirklich?")]
    [InlineData("Ist das dein Ernst!!!", "Ist das dein Ernst", "Ist das dein Ernst!")]
    [InlineData("Na gut…?", "Na gut", "Na gut?")]
    [InlineData("Na gut?…", "Na gut", "Na gut…")]
    [InlineData("Na ja...", "Na ja", "Na ja.")]
    public void Bei_mehreren_Satzzeichen_wird_nur_das_letzte_angehaengt(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.KeepFinalPunctuation(eingang, antwort));
    }

    // Regel 8: Null oder leere Werte ändern nichts, die Antwort kommt zurück, null bleibt null.
    [Fact]
    public void Antwort_null_bleibt_null()
    {
        Assert.Null(SmoothingGuard.KeepFinalPunctuation("Der Zug fährt ab.", null));
    }

    [Fact]
    public void Leere_Antwort_bleibt_leer()
    {
        Assert.Equal(string.Empty, SmoothingGuard.KeepFinalPunctuation("Der Zug fährt ab.", string.Empty));
    }

    [Fact]
    public void Eingang_null_gibt_die_Antwort_unveraendert_zurueck()
    {
        Assert.Equal("Der Zug fährt ab", SmoothingGuard.KeepFinalPunctuation(null, "Der Zug fährt ab"));
    }

    [Fact]
    public void Leerer_Eingang_gibt_die_Antwort_unveraendert_zurueck()
    {
        Assert.Equal("Der Zug fährt ab", SmoothingGuard.KeepFinalPunctuation(string.Empty, "Der Zug fährt ab"));
    }

    [Fact]
    public void Beide_null_ergibt_null()
    {
        Assert.Null(SmoothingGuard.KeepFinalPunctuation(null, null));
    }

    [Fact]
    public void Beide_leer_ergibt_leer()
    {
        Assert.Equal(string.Empty, SmoothingGuard.KeepFinalPunctuation(string.Empty, string.Empty));
    }

    [Fact]
    public void Eingang_null_und_leere_Antwort_ergibt_leer()
    {
        Assert.Equal(string.Empty, SmoothingGuard.KeepFinalPunctuation(null, string.Empty));
    }

    [Fact]
    public void Leerer_Eingang_und_Antwort_null_ergibt_null()
    {
        Assert.Null(SmoothingGuard.KeepFinalPunctuation(string.Empty, null));
    }

    // Regel 8 über Accept (Regeln 3 und 8): Eine sonst gültige Antwort ohne Schlusspunkt bekommt das Satzzeichen.
    [Theory]
    [InlineData(
        "ich habe heute vor um indisch zu kochen und wollte jemanden einladen.",
        "Ich habe heute vor, indisch zu kochen, und wollte jemanden einladen",
        "Ich habe heute vor, indisch zu kochen, und wollte jemanden einladen.")]
    [InlineData(
        "kannst du morgen vielleicht die Unterlagen für die Besprechung mitbringen?",
        "Kannst du morgen vielleicht die Unterlagen für die Besprechung mitbringen",
        "Kannst du morgen vielleicht die Unterlagen für die Besprechung mitbringen?")]
    [InlineData(
        "das Konzert gestern Abend war wirklich unglaublich schön!",
        "Das Konzert gestern Abend war wirklich unglaublich schön",
        "Das Konzert gestern Abend war wirklich unglaublich schön!")]
    [InlineData(
        "die Lieferung kommt laut Auskunft am Dienstag um 14 Uhr…",
        "Die Lieferung kommt laut Auskunft am Dienstag um 14 Uhr",
        "Die Lieferung kommt laut Auskunft am Dienstag um 14 Uhr…")]
    public void Accept_haengt_das_Satzzeichen_an_eine_gueltige_Antwort(string eingang, string antwort, string erwartet)
    {
        Assert.Equal(erwartet, SmoothingGuard.Accept(eingang, antwort));
    }

    // Regel 8 über Accept: Leerraum am Ende einer gültigen Antwort entfällt beim Anhängen.
    [Fact]
    public void Accept_entfernt_Leerraum_am_Ende_beim_Anhaengen()
    {
        const string eingang = "wir treffen uns nach der Arbeit vor dem Bahnhof in der Stadt.";
        const string antwort = "Wir treffen uns nach der Arbeit vor dem Bahnhof in der Stadt  ";

        Assert.Equal("Wir treffen uns nach der Arbeit vor dem Bahnhof in der Stadt.", SmoothingGuard.Accept(eingang, antwort));
    }

    // Regel 8 über Accept: Eine gültige Antwort mit eigenem Schlusszeichen bleibt, wie sie ist.
    [Fact]
    public void Accept_laesst_gueltige_Antwort_mit_eigenem_Satzzeichen_unveraendert()
    {
        const string eingang = "kannst du morgen vielleicht die Unterlagen für die Besprechung mitbringen.";
        const string antwort = "Kannst du morgen vielleicht die Unterlagen für die Besprechung mitbringen?";

        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    // Regel 8 über Accept: Eingang ohne Schlusssatzzeichen, die gültige Antwort bleibt ohne Punkt.
    [Fact]
    public void Accept_haengt_nichts_an_wenn_der_Eingang_ohne_Satzzeichen_endet()
    {
        const string eingang = "ich habe heute vor um indisch zu kochen und wollte jemanden einladen";
        const string antwort = "Ich habe heute vor, indisch zu kochen, und wollte jemanden einladen";

        Assert.Equal(antwort, SmoothingGuard.Accept(eingang, antwort));
    }

    // Regeln 3 und 8: Eine verworfene Antwort bleibt null, es wird nichts angehängt.
    [Theory]
    [InlineData("")]
    [InlineData("Hier ist der Text: Ich habe heute vor, indisch zu kochen, und wollte jemanden einladen")]
    [InlineData("```Ich habe heute vor, indisch zu kochen, und wollte jemanden einladen```")]
    [InlineData("**Ich habe heute vor, indisch zu kochen, und wollte jemanden einladen**")]
    [InlineData("I plan to cook Indian food today and wanted to invite someone over")]
    [InlineData("Ja")]
    public void Accept_verwirft_ungueltige_Antwort_und_haengt_nichts_an(string antwort)
    {
        const string eingang = "ich habe heute vor um indisch zu kochen und wollte jemanden einladen.";

        Assert.Null(SmoothingGuard.Accept(eingang, antwort));
    }

    // Regeln 3 und 8: Antwort null bleibt über Accept null.
    [Fact]
    public void Accept_mit_Antwort_null_bleibt_null()
    {
        Assert.Null(SmoothingGuard.Accept("ich habe heute vor um indisch zu kochen und wollte jemanden einladen.", null));
    }
}
