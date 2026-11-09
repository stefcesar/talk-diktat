using System.Text.Json.Nodes;

using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Settings;

using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft die Glättungsstufe je Programm nach Playbook Abschnitt 23, Regeln 1 bis 8.
/// Geschrieben nur aus den Regeln, nicht aus der Umsetzung. Alle Programmnamen sind erfunden.
/// </summary>
public sealed class AppSmoothingMapTests
{
    private static KeyValuePair<string, SmoothingLevel> P(string name, SmoothingLevel stufe) => new(name, stufe);

    private static AppSmoothingMap Liste(params KeyValuePair<string, SmoothingLevel>[] paare) => AppSmoothingMap.FromEntries(paare);

    private static AppSmoothingMap Voll(int anzahl) =>
        AppSmoothingMap.FromEntries(Enumerable.Range(1, anzahl).Select(i => P($"Werkzeug{i:D2}", SmoothingLevel.Smooth)));

    // ---------------------------------------------------------------- Regel 1

    // Regel 1: Konstanten nennen die Grenzen aus der Regel.
    [Fact]
    public void Regel1_Grenzen_sind_50_Eintraege_und_64_Zeichen()
    {
        Assert.Equal(50, AppSmoothingMap.MaxEntries);
        Assert.Equal(64, AppSmoothingMap.MaxNameLength);
    }

    // Regel 1: Rand ohne Leerraum, ".exe" am Ende entfällt in jeder Schreibweise, Groß und Klein bleibt erhalten.
    [Theory]
    [InlineData("Schreibtool", "Schreibtool")]
    [InlineData("  Schreibtool  ", "Schreibtool")]
    [InlineData("\tSchreibtool\r\n", "Schreibtool")]
    [InlineData("Schreibtool.exe", "Schreibtool")]
    [InlineData("Schreibtool.EXE", "Schreibtool")]
    [InlineData("Schreibtool.Exe", "Schreibtool")]
    [InlineData("Schreibtool.eXe", "Schreibtool")]
    [InlineData("  Schreibtool.exe  ", "Schreibtool")]
    [InlineData("SCHREIBTOOL", "SCHREIBTOOL")]
    [InlineData("mein Tool", "mein Tool")]
    [InlineData("Mein_Tool-2.Root", "Mein_Tool-2.Root")]
    [InlineData("123", "123")]
    [InlineData("a", "a")]
    [InlineData("foo .exe", "foo")]
    [InlineData("Schreibtool   .EXE", "Schreibtool")]
    [InlineData("  mein Tool .Exe  ", "mein Tool")]
    public void Regel1_Name_wird_bereinigt(string eingang, string erwartet)
    {
        Assert.Equal(erwartet, AppSmoothingMap.NormalizeName(eingang));
    }

    // Regel 1: Nur ein ".exe" am Ende entfällt; ein ".exe" in der Mitte bleibt.
    [Fact]
    public void Regel1_nur_ein_exe_am_Ende_entfaellt()
    {
        Assert.Equal("Schreibtool.exe", AppSmoothingMap.NormalizeName("Schreibtool.exe.exe"));
        Assert.Equal("Schreib.exe.tool", AppSmoothingMap.NormalizeName("Schreib.exe.tool"));
        Assert.Equal("Schreibtoolexe", AppSmoothingMap.NormalizeName("Schreibtoolexe"));
    }

    // Regel 1: null, leer, nur Leerraum und nur ".exe" sind ungültig.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(".exe")]
    [InlineData(".EXE")]
    [InlineData("  .exe  ")]
    public void Regel1_leerer_Name_ist_ungueltig(string? eingang)
    {
        Assert.Null(AppSmoothingMap.NormalizeName(eingang));
    }

    // Regel 1: 64 Zeichen sind erlaubt, 65 nicht; gezählt wird nach dem Entfernen von ".exe" und vom Rand.
    [Fact]
    public void Regel1_Laenge_64_gueltig_65_ungueltig()
    {
        string vierundsechzig = new('a', 64);
        string fuenfundsechzig = new('a', 65);

        Assert.Equal(vierundsechzig, AppSmoothingMap.NormalizeName(vierundsechzig));
        Assert.Equal(vierundsechzig, AppSmoothingMap.NormalizeName(vierundsechzig + ".exe"));
        Assert.Equal(vierundsechzig, AppSmoothingMap.NormalizeName("  " + vierundsechzig + "  "));
        Assert.Null(AppSmoothingMap.NormalizeName(fuenfundsechzig));
        Assert.Null(AppSmoothingMap.NormalizeName(fuenfundsechzig + ".exe"));
    }

    // Regel 1: Zeichen außer Buchstaben, Ziffern, Leerzeichen, Punkt, Unterstrich und Bindestrich machen den Namen ungültig.
    [Theory]
    [InlineData("Schreib/tool")]
    [InlineData("Schreib\\tool")]
    [InlineData("C:\\Programme\\Schreibtool.exe")]
    [InlineData("Schreib:tool")]
    [InlineData("Schreib*tool")]
    [InlineData("Schreib?tool")]
    [InlineData("Schreib\"tool")]
    [InlineData("Schreib<tool>")]
    [InlineData("Schreib|tool")]
    [InlineData("Schreib(tool)")]
    [InlineData("Schreib+tool")]
    [InlineData("Schreib@tool")]
    [InlineData("Schreib=tool")]
    [InlineData("Schreib|tool.exe")]
    [InlineData("Schreib\ttool")]
    [InlineData("Schreib\ntool")]
    [InlineData("Schreib\u0000tool")]
    [InlineData("Schreib\u001Btool")]
    [InlineData("Schreib\u200Btool")]
    public void Regel1_verbotene_Zeichen_machen_den_Namen_ungueltig(string eingang)
    {
        Assert.Null(AppSmoothingMap.NormalizeName(eingang));
    }

    // ---------------------------------------------------------------- Regel 2

    // Regel 2: Empty ist leer, FromEntries(null) und eine leere Folge ebenfalls.
    [Fact]
    public void Regel2_leere_Listen()
    {
        Assert.Equal(0, AppSmoothingMap.Empty.Count);
        Assert.Empty(AppSmoothingMap.Empty.Entries);
        Assert.Equal(0, AppSmoothingMap.FromEntries(null).Count);
        Assert.Equal(0, AppSmoothingMap.FromEntries([]).Count);
        Assert.Equal(AppSmoothingMap.Empty, AppSmoothingMap.FromEntries(null));
    }

    // Regel 2: Alle vier Stufen sind erlaubt; die Namen werden nach Regel 1 bereinigt.
    [Fact]
    public void Regel2_vier_Stufen_und_bereinigte_Namen()
    {
        AppSmoothingMap liste = Liste(
            P("Alphaschreiber.exe", SmoothingLevel.Off),
            P("  Betaschreiber ", SmoothingLevel.Correct),
            P("Gammaschreiber", SmoothingLevel.Smooth),
            P("Deltaschreiber.EXE", SmoothingLevel.Message));

        Assert.Equal(4, liste.Count);
        Assert.Equal(
            [P("Alphaschreiber", SmoothingLevel.Off), P("Betaschreiber", SmoothingLevel.Correct), P("Deltaschreiber", SmoothingLevel.Message), P("Gammaschreiber", SmoothingLevel.Smooth)],
            liste.Entries);
    }

    // Regel 2: Ungültige Namen und unbekannte Stufen fallen weg.
    [Fact]
    public void Regel2_ungueltige_Namen_und_unbekannte_Stufen_fallen_weg()
    {
        AppSmoothingMap liste = Liste(
            P("", SmoothingLevel.Smooth),
            P(".exe", SmoothingLevel.Smooth),
            P("Schreib/tool", SmoothingLevel.Smooth),
            P(new string('x', 65), SmoothingLevel.Smooth),
            P("Rechenhilfe", (SmoothingLevel)99),
            P("Zahlenhilfe", (SmoothingLevel)(-1)),
            P("Notizhilfe", SmoothingLevel.Correct));

        Assert.Equal(1, liste.Count);
        Assert.Equal([P("Notizhilfe", SmoothingLevel.Correct)], liste.Entries);
        Assert.Null(liste.Get("Rechenhilfe"));
        Assert.Null(liste.Get("Zahlenhilfe"));
    }

    // Regel 2: Bei gleichem Namen (ohne Groß und Klein, mit oder ohne ".exe") gewinnt der erste, samt seiner Schreibweise.
    [Fact]
    public void Regel2_bei_gleichem_Namen_gewinnt_der_erste()
    {
        AppSmoothingMap liste = Liste(
            P("Notizblock", SmoothingLevel.Correct),
            P("NOTIZBLOCK", SmoothingLevel.Message),
            P("notizblock.exe", SmoothingLevel.Off),
            P("  NotizBlock.EXE ", SmoothingLevel.Smooth));

        Assert.Equal(1, liste.Count);
        Assert.Equal([P("Notizblock", SmoothingLevel.Correct)], liste.Entries);
    }

    // Regel 2: Ein weggefallener Eintrag mit unbekannter Stufe blockiert einen späteren gültigen Eintrag gleichen Namens nicht.
    [Fact]
    public void Regel2_weggefallener_Eintrag_blockiert_den_Namen_nicht()
    {
        AppSmoothingMap liste = Liste(P("Notizblock", (SmoothingLevel)99), P("notizblock", SmoothingLevel.Message));

        Assert.Equal([P("notizblock", SmoothingLevel.Message)], liste.Entries);
    }

    // Regel 2: Genau 50 gültige Einträge bleiben alle erhalten.
    [Fact]
    public void Regel2_genau_50_Eintraege_bleiben()
    {
        AppSmoothingMap liste = Voll(50);

        Assert.Equal(50, liste.Count);
        Assert.Equal(SmoothingLevel.Smooth, liste.Get("Werkzeug50"));
    }

    // Regel 2: Bei mehr als 50 gültigen bleiben die ersten 50 in der Reihenfolge des Eingangs, unabhängig von der Sortierung.
    [Fact]
    public void Regel2_mehr_als_50_es_bleiben_die_ersten_50_des_Eingangs()
    {
        // Eingang absteigend, damit "die ersten 50" nicht mit "die ersten 50 nach Namen" verwechselt werden kann.
        List<KeyValuePair<string, SmoothingLevel>> eingang = Enumerable.Range(1, 55).Reverse()
            .Select(i => P($"Werkzeug{i:D2}", SmoothingLevel.Correct)).ToList();

        AppSmoothingMap liste = AppSmoothingMap.FromEntries(eingang);

        Assert.Equal(50, liste.Count);
        Assert.Equal(SmoothingLevel.Correct, liste.Get("Werkzeug55"));
        Assert.Equal(SmoothingLevel.Correct, liste.Get("Werkzeug06"));
        Assert.Null(liste.Get("Werkzeug05"));
        Assert.Null(liste.Get("Werkzeug01"));
    }

    // Regel 2: Ungültige Einträge und doppelte Namen zählen nicht gegen die 50.
    [Fact]
    public void Regel2_ungueltige_und_doppelte_zaehlen_nicht_gegen_die_50()
    {
        List<KeyValuePair<string, SmoothingLevel>> eingang = [];
        for (int i = 1; i <= 50; i++)
        {
            eingang.Add(P($"Kaputt/{i}", SmoothingLevel.Smooth));
            eingang.Add(P($"Unbekannt{i:D2}", (SmoothingLevel)99));
            eingang.Add(P($"Werkzeug{i:D2}", SmoothingLevel.Message));
            eingang.Add(P($"WERKZEUG{i:D2}.exe", SmoothingLevel.Off));
        }
        eingang.Add(P("Werkzeug51", SmoothingLevel.Message));

        AppSmoothingMap liste = AppSmoothingMap.FromEntries(eingang);

        Assert.Equal(50, liste.Count);
        Assert.Equal(SmoothingLevel.Message, liste.Get("Werkzeug50"));
        Assert.Equal(SmoothingLevel.Message, liste.Get("Werkzeug01"));
        Assert.Null(liste.Get("Werkzeug51"));
    }

    // Regel 2: Entries ist nach Namen sortiert, ohne Groß und Klein.
    [Fact]
    public void Regel2_Entries_sortiert_ohne_Gross_und_Klein()
    {
        AppSmoothingMap liste = Liste(
            P("delta", SmoothingLevel.Smooth),
            P("Bravo", SmoothingLevel.Smooth),
            P("charlie", SmoothingLevel.Smooth),
            P("ALPHA", SmoothingLevel.Smooth),
            P("echo", SmoothingLevel.Smooth));

        Assert.Equal(["ALPHA", "Bravo", "charlie", "delta", "echo"], liste.Entries.Select(e => e.Key).ToArray());
        Assert.Equal(liste.Count, liste.Entries.Count);
    }

    // ---------------------------------------------------------------- Regel 3

    // Regel 3: Setzen nimmt einen neuen Eintrag mit bereinigtem Namen auf; das Original bleibt unverändert.
    [Fact]
    public void Regel3_setzen_und_Original_bleibt()
    {
        AppSmoothingMap original = Liste(P("Notizblock", SmoothingLevel.Correct));

        AppSmoothingMap neu = original.With("  Rechenhilfe.exe ", SmoothingLevel.Message);

        Assert.Equal(2, neu.Count);
        Assert.Equal(SmoothingLevel.Message, neu.Get("Rechenhilfe"));
        Assert.Contains(P("Rechenhilfe", SmoothingLevel.Message), neu.Entries);
        Assert.Equal(1, original.Count);
        Assert.Null(original.Get("Rechenhilfe"));
        Assert.Equal([P("Notizblock", SmoothingLevel.Correct)], original.Entries);
    }

    // Regel 3: Setzen auf Empty ändert Empty nicht.
    [Fact]
    public void Regel3_Empty_bleibt_leer()
    {
        AppSmoothingMap neu = AppSmoothingMap.Empty.With("Notizblock", SmoothingLevel.Off);

        Assert.Equal(1, neu.Count);
        Assert.Equal(0, AppSmoothingMap.Empty.Count);
    }

    // Regel 3: Ersetzen übernimmt die neue Stufe und die neue Schreibweise des Namens.
    [Fact]
    public void Regel3_ersetzen_mit_neuer_Schreibweise()
    {
        AppSmoothingMap original = Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe", SmoothingLevel.Smooth));

        AppSmoothingMap neu = original.With("NOTIZBLOCK.exe", SmoothingLevel.Off);

        Assert.Equal(2, neu.Count);
        Assert.Equal([P("NOTIZBLOCK", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Smooth)], neu.Entries);
        Assert.Equal([P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe", SmoothingLevel.Smooth)], original.Entries);
    }

    // Regel 3: Stufe null entfernt den Eintrag ("Wie Standard"), auch bei anderer Schreibweise.
    [Fact]
    public void Regel3_null_entfernt()
    {
        AppSmoothingMap original = Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe", SmoothingLevel.Smooth));

        AppSmoothingMap neu = original.With("notizblock.EXE", null);

        Assert.Equal([P("Rechenhilfe", SmoothingLevel.Smooth)], neu.Entries);
        Assert.Null(neu.Get("Notizblock"));
        Assert.Equal(2, original.Count);
    }

    // Regel 3: Entfernen eines nicht vorhandenen Namens ändert nichts.
    [Fact]
    public void Regel3_entfernen_eines_fehlenden_Namens_aendert_nichts()
    {
        AppSmoothingMap original = Liste(P("Notizblock", SmoothingLevel.Correct));

        Assert.Equal(original, original.With("Rechenhilfe", null));
        Assert.Equal(AppSmoothingMap.Empty, AppSmoothingMap.Empty.With("Rechenhilfe", null));
    }

    // Regel 3: Ein ungültiger Name ändert nichts, weder beim Setzen noch beim Entfernen.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".exe")]
    [InlineData("Notiz/block")]
    public void Regel3_ungueltiger_Name_aendert_nichts(string? name)
    {
        AppSmoothingMap original = Liste(P("Notizblock", SmoothingLevel.Correct));

        AppSmoothingMap gesetzt = original.With(name, SmoothingLevel.Message);
        AppSmoothingMap entfernt = original.With(name, null);

        Assert.Equal([P("Notizblock", SmoothingLevel.Correct)], gesetzt.Entries);
        Assert.Equal([P("Notizblock", SmoothingLevel.Correct)], entfernt.Entries);
    }

    // Regel 3: Ein Name mit 65 Zeichen ist ungültig und wird nicht aufgenommen.
    [Fact]
    public void Regel3_zu_langer_Name_wird_nicht_aufgenommen()
    {
        Assert.Equal(0, AppSmoothingMap.Empty.With(new string('z', 65), SmoothingLevel.Smooth).Count);
        Assert.Equal(1, AppSmoothingMap.Empty.With(new string('z', 64), SmoothingLevel.Smooth).Count);
    }

    // Regel 3: Eine unbekannte Stufe wird wie in Regel 2 nicht aufgenommen.
    [Fact]
    public void Regel3_unbekannte_Stufe_wird_nicht_aufgenommen()
    {
        AppSmoothingMap original = Liste(P("Notizblock", SmoothingLevel.Correct));

        Assert.Null(original.With("Rechenhilfe", (SmoothingLevel)99).Get("Rechenhilfe"));
    }

    // Regel 3: Bei 50 Einträgen wird kein neuer Name aufgenommen.
    [Fact]
    public void Regel3_bei_50_kein_neuer_Name()
    {
        AppSmoothingMap voll = Voll(50);

        AppSmoothingMap neu = voll.With("Werkzeug51", SmoothingLevel.Message);

        Assert.Equal(50, neu.Count);
        Assert.Null(neu.Get("Werkzeug51"));
        Assert.Equal(voll, neu);
    }

    // Regel 3: Bei 49 Einträgen wird der fünfzigste noch aufgenommen.
    [Fact]
    public void Regel3_bei_49_wird_der_fuenfzigste_aufgenommen()
    {
        AppSmoothingMap neu = Voll(49).With("Werkzeug50", SmoothingLevel.Message);

        Assert.Equal(50, neu.Count);
        Assert.Equal(SmoothingLevel.Message, neu.Get("Werkzeug50"));
    }

    // Regel 3: Bei 50 Einträgen lässt sich ein vorhandener Name weiter ändern und entfernen; danach ist wieder Platz.
    [Fact]
    public void Regel3_bei_50_vorhandener_Name_bleibt_aenderbar()
    {
        AppSmoothingMap voll = Voll(50);

        AppSmoothingMap geaendert = voll.With("WERKZEUG07.exe", SmoothingLevel.Off);
        Assert.Equal(50, geaendert.Count);
        Assert.Equal(SmoothingLevel.Off, geaendert.Get("Werkzeug07"));
        Assert.Contains(P("WERKZEUG07", SmoothingLevel.Off), geaendert.Entries);

        AppSmoothingMap entfernt = voll.With("Werkzeug07", null);
        Assert.Equal(49, entfernt.Count);

        AppSmoothingMap wiederVoll = entfernt.With("Werkzeug51", SmoothingLevel.Correct);
        Assert.Equal(50, wiederVoll.Count);
        Assert.Equal(SmoothingLevel.Correct, wiederVoll.Get("Werkzeug51"));
    }

    // ---------------------------------------------------------------- Regel 3a

    // Regel 3a: Nichts geändert (alt gleich gewünscht) ergibt die Basis unverändert.
    [Fact]
    public void Regel3a_nichts_geaendert_ergibt_die_Basis()
    {
        AppSmoothingMap basis = Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Message));
        AppSmoothingMap alt = Liste(P("Plauderbox", SmoothingLevel.Correct));
        AppSmoothingMap gewuenscht = Liste(P("Plauderbox", SmoothingLevel.Correct));

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(basis, alt, gewuenscht);

        Assert.Equal(basis, ergebnis);
        Assert.Equal(basis.Entries, ergebnis.Entries);
    }

    // Regel 3a: Ein neu dazugekommener Eintrag kommt zur Basis dazu, die übrigen Basiseinträge bleiben.
    [Fact]
    public void Regel3a_neuer_Eintrag_kommt_dazu()
    {
        AppSmoothingMap basis = Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Message));
        AppSmoothingMap alt = AppSmoothingMap.Empty;
        AppSmoothingMap gewuenscht = Liste(P("Plauderbox", SmoothingLevel.Correct));

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(basis, alt, gewuenscht);

        Assert.Equal(
            Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Message), P("Plauderbox", SmoothingLevel.Correct)),
            ergebnis);
    }

    // Regel 3a: Ein entfernter Eintrag (in alt, nicht mehr in gewünscht) wird aus der Basis entfernt.
    [Fact]
    public void Regel3a_entfernter_Eintrag_wird_aus_der_Basis_entfernt()
    {
        AppSmoothingMap basis = Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Message));
        AppSmoothingMap alt = Liste(P("Notizblock", SmoothingLevel.Off));
        AppSmoothingMap gewuenscht = AppSmoothingMap.Empty;

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(basis, alt, gewuenscht);

        Assert.Equal([P("Rechenhilfe", SmoothingLevel.Message)], ergebnis.Entries);
    }

    // Regel 3a: Eine geänderte Stufe ersetzt die Stufe der Basis, auch wenn die Basis eine dritte Stufe hatte.
    [Fact]
    public void Regel3a_geaenderte_Stufe_ersetzt_die_der_Basis()
    {
        AppSmoothingMap basis = Liste(P("Notizblock", SmoothingLevel.Smooth), P("Rechenhilfe", SmoothingLevel.Message));
        AppSmoothingMap alt = Liste(P("Notizblock", SmoothingLevel.Correct));
        AppSmoothingMap gewuenscht = Liste(P("Notizblock", SmoothingLevel.Off));

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(basis, alt, gewuenscht);

        Assert.Equal(Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Message)), ergebnis);
    }

    // Regel 3a: Ein Name, dessen Stufe sich zwischen alt und gewünscht nicht unterscheidet, behält die Stufe der Basis.
    [Fact]
    public void Regel3a_unveraenderter_Name_behaelt_die_Stufe_der_Basis()
    {
        AppSmoothingMap basis = Liste(P("Notizblock", SmoothingLevel.Off));
        AppSmoothingMap alt = Liste(P("Notizblock", SmoothingLevel.Smooth));
        AppSmoothingMap gewuenscht = Liste(P("Notizblock", SmoothingLevel.Smooth), P("Rechenhilfe", SmoothingLevel.Correct));

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(basis, alt, gewuenscht);

        Assert.Equal(Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Correct)), ergebnis);
    }

    // Regel 3a: Einträge der Basis, die weder in alt noch in gewünscht stehen, bleiben.
    [Fact]
    public void Regel3a_fremde_Basiseintraege_bleiben()
    {
        AppSmoothingMap basis = Liste(P("Briefkasten", SmoothingLevel.Correct), P("Zeichenbrett", SmoothingLevel.Off), P("Notizblock", SmoothingLevel.Smooth));
        AppSmoothingMap alt = Liste(P("Notizblock", SmoothingLevel.Smooth), P("Rechenhilfe", SmoothingLevel.Off));
        AppSmoothingMap gewuenscht = Liste(P("Notizblock", SmoothingLevel.Message));

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(basis, alt, gewuenscht);

        Assert.Equal(
            Liste(P("Briefkasten", SmoothingLevel.Correct), P("Zeichenbrett", SmoothingLevel.Off), P("Notizblock", SmoothingLevel.Message)),
            ergebnis);
    }

    // Regel 3a: Namen mit anderer Schreibweise (Groß und Klein, ".exe") zählen als gleich.
    [Fact]
    public void Regel3a_andere_Schreibweise_zaehlt_als_gleich()
    {
        AppSmoothingMap basis = Liste(P("notizblock", SmoothingLevel.Off), P("rechenhilfe", SmoothingLevel.Message));

        // Nur die Schreibweise unterscheidet sich zwischen alt und gewünscht: nichts geändert, Basis bleibt.
        AppSmoothingMap gleich = AppSmoothingMap.MergeChanges(basis, Liste(P("NOTIZBLOCK", SmoothingLevel.Smooth)), Liste(P("Notizblock.exe", SmoothingLevel.Smooth)));
        Assert.Equal(basis, gleich);
        Assert.Equal(2, gleich.Count);

        // Geänderte Stufe bei anderer Schreibweise ersetzt den Basiseintrag, ohne einen zweiten anzulegen.
        AppSmoothingMap geaendert = AppSmoothingMap.MergeChanges(basis, Liste(P("NOTIZBLOCK", SmoothingLevel.Off)), Liste(P("NotizBlock", SmoothingLevel.Correct)));
        Assert.Equal(2, geaendert.Count);
        Assert.Equal(SmoothingLevel.Correct, geaendert.Get("notizblock"));

        // Entfernen bei anderer Schreibweise trifft den Basiseintrag.
        AppSmoothingMap entfernt = AppSmoothingMap.MergeChanges(basis, Liste(P("RECHENHILFE.exe", SmoothingLevel.Message)), AppSmoothingMap.Empty);
        Assert.Equal([P("notizblock", SmoothingLevel.Off)], entfernt.Entries);
    }

    // Regel 3a: Null gilt für jeden der drei Werte als leere Liste.
    [Fact]
    public void Regel3a_null_gilt_als_leere_Liste()
    {
        AppSmoothingMap basis = Liste(P("Notizblock", SmoothingLevel.Off));
        AppSmoothingMap gewuenscht = Liste(P("Rechenhilfe", SmoothingLevel.Message));

        // Basis null: nur das Geänderte steht im Ergebnis.
        Assert.Equal(gewuenscht, AppSmoothingMap.MergeChanges(null, AppSmoothingMap.Empty, gewuenscht));

        // Alt null: alles Gewünschte gilt als neu.
        Assert.Equal(Liste(P("Notizblock", SmoothingLevel.Off), P("Rechenhilfe", SmoothingLevel.Message)),
            AppSmoothingMap.MergeChanges(basis, null, gewuenscht));

        // Gewünscht null: alles aus alt gilt als entfernt.
        Assert.Equal(AppSmoothingMap.Empty, AppSmoothingMap.MergeChanges(basis, Liste(P("Notizblock", SmoothingLevel.Correct)), null));

        // Alt und gewünscht null: Basis bleibt.
        Assert.Equal(basis, AppSmoothingMap.MergeChanges(basis, null, null));

        // Alles null: leere Liste.
        AppSmoothingMap leer = AppSmoothingMap.MergeChanges(null, null, null);
        Assert.NotNull(leer);
        Assert.Equal(AppSmoothingMap.Empty, leer);
    }

    // Regel 3a: Beispiel der Regel: Datei mit zwei Programmen, beim Start leer gelesen, Nutzer setzt ein drittes; alle drei bleiben.
    [Fact]
    public void Regel3a_Beispiel_gesperrte_Datei_beim_Start()
    {
        AppSmoothingMap datei = DictationSettingsJson.Read(
            """{"smoothingPerApp":{"Notizblock":"aus","Rechenhilfe":"nachricht"}}""").SmoothingPerApp ?? AppSmoothingMap.Empty;
        AppSmoothingMap beimStart = AppSmoothingMap.Empty;
        AppSmoothingMap gewuenscht = beimStart.With("Plauderbox", SmoothingLevel.Correct);

        AppSmoothingMap ergebnis = AppSmoothingMap.MergeChanges(datei, beimStart, gewuenscht);

        Assert.Equal(3, ergebnis.Count);
        Assert.Equal(SmoothingLevel.Off, ergebnis.Get("Notizblock"));
        Assert.Equal(SmoothingLevel.Message, ergebnis.Get("Rechenhilfe"));
        Assert.Equal(SmoothingLevel.Correct, ergebnis.Get("Plauderbox"));
    }

    // Regel 3a mit Regel 3: Bei voller Basis wird ein neuer Name nicht aufgenommen, eine Änderung an einem vorhandenen schon.
    [Fact]
    public void Regel3a_volle_Basis_gilt_Regel3()
    {
        AppSmoothingMap basis = Voll(50);

        AppSmoothingMap neu = AppSmoothingMap.MergeChanges(basis, AppSmoothingMap.Empty, Liste(P("Werkzeug51", SmoothingLevel.Off)));
        Assert.Equal(50, neu.Count);
        Assert.Null(neu.Get("Werkzeug51"));

        AppSmoothingMap geaendert = AppSmoothingMap.MergeChanges(basis, AppSmoothingMap.Empty, Liste(P("Werkzeug07", SmoothingLevel.Off)));
        Assert.Equal(50, geaendert.Count);
        Assert.Equal(SmoothingLevel.Off, geaendert.Get("Werkzeug07"));
    }

    // ---------------------------------------------------------------- Regel 4

    // Regel 4: Gleich bei denselben Namen und Stufen, unabhängig von Schreibweise und Eingangsreihenfolge; gleicher Hashwert.
    [Fact]
    public void Regel4_gleich_unabhaengig_von_Schreibweise_und_Reihenfolge()
    {
        AppSmoothingMap a = Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe.exe", SmoothingLevel.Off));
        AppSmoothingMap b = Liste(P("RECHENHILFE", SmoothingLevel.Off), P("notizblock", SmoothingLevel.Correct));

        Assert.True(a.Equals(b));
        Assert.True(b.Equals(a));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // Regel 4: Über With und über FromEntries entstandene gleiche Listen sind gleich.
    [Fact]
    public void Regel4_gleich_auch_ueber_With()
    {
        AppSmoothingMap a = AppSmoothingMap.Empty.With("notizblock", SmoothingLevel.Message).With("Rechenhilfe", SmoothingLevel.Smooth);
        AppSmoothingMap b = Liste(P("Rechenhilfe", SmoothingLevel.Smooth), P("NotizBlock", SmoothingLevel.Message));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // Regel 4: Andere Stufe, anderer Name oder andere Anzahl ergibt Ungleichheit; null ist ungleich.
    [Fact]
    public void Regel4_ungleich_bei_anderer_Stufe_anderem_Namen_oder_anderer_Anzahl()
    {
        AppSmoothingMap a = Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe", SmoothingLevel.Off));

        Assert.NotEqual(a, Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe", SmoothingLevel.Smooth)));
        Assert.NotEqual(a, Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe2", SmoothingLevel.Off)));
        Assert.NotEqual(a, Liste(P("Notizblock", SmoothingLevel.Correct)));
        Assert.NotEqual(a, AppSmoothingMap.Empty);
        Assert.False(a.Equals((AppSmoothingMap?)null));
        Assert.False(a.Equals((object?)null));
    }

    // Regel 4: Leere Listen sind untereinander gleich.
    [Fact]
    public void Regel4_leere_Listen_sind_gleich()
    {
        Assert.Equal(AppSmoothingMap.Empty, AppSmoothingMap.FromEntries([]));
        Assert.Equal(AppSmoothingMap.Empty.GetHashCode(), AppSmoothingMap.FromEntries([]).GetHashCode());
        Assert.Equal(AppSmoothingMap.Empty, Liste(P("Notizblock", SmoothingLevel.Off)).With("notizblock", null));
    }

    // Regel 4: Einstellungen mit gleichen Listen (verschiedene Objekte, andere Schreibweise) sind gleich.
    [Fact]
    public void Regel4_Einstellungen_mit_gleichen_Listen_sind_gleich()
    {
        DictationSettings a = new(SmoothingPerApp: Liste(P("Notizblock", SmoothingLevel.Message), P("Rechenhilfe", SmoothingLevel.Off)));
        DictationSettings b = new(SmoothingPerApp: Liste(P("rechenhilfe.exe", SmoothingLevel.Off), P("NOTIZBLOCK", SmoothingLevel.Message)));
        DictationSettings c = new(SmoothingPerApp: Liste(P("Notizblock", SmoothingLevel.Correct), P("Rechenhilfe", SmoothingLevel.Off)));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    // ---------------------------------------------------------------- Regel 5

    // Regel 5: Treffer liefert die Stufe der Liste, auch "Aus"; Name mit ".exe", Rand und anderer Schreibweise trifft.
    [Theory]
    [InlineData("Notizblock", SmoothingLevel.Off)]
    [InlineData("notizblock.exe", SmoothingLevel.Off)]
    [InlineData("  NOTIZBLOCK.EXE ", SmoothingLevel.Off)]
    [InlineData("Rechenhilfe", SmoothingLevel.Message)]
    [InlineData("RECHENHILFE.exe", SmoothingLevel.Message)]
    [InlineData("Plauderbox", SmoothingLevel.Correct)]
    public void Regel5_Treffer_liefert_die_Stufe_der_Liste(string prozess, SmoothingLevel erwartet)
    {
        AppSmoothingMap liste = Liste(
            P("Notizblock", SmoothingLevel.Off),
            P("Rechenhilfe", SmoothingLevel.Message),
            P("plauderbox.exe", SmoothingLevel.Correct));

        Assert.Equal(erwartet, AppSmoothingMap.Resolve(SmoothingLevel.Smooth, liste, prozess));
    }

    // Regel 5: Get trifft ebenso nach Regel 1.
    [Fact]
    public void Regel5_Get_trifft_nach_Regel1()
    {
        AppSmoothingMap liste = Liste(P("Notizblock", SmoothingLevel.Off));

        Assert.Equal(SmoothingLevel.Off, liste.Get("NOTIZBLOCK.exe"));
        Assert.Equal(SmoothingLevel.Off, liste.Get(" notizblock "));
        Assert.Null(liste.Get("Rechenhilfe"));
        Assert.Null(liste.Get(null));
        Assert.Null(liste.Get(""));
        Assert.Null(liste.Get("Notiz/block"));
    }

    // Regel 5: Kein Treffer liefert die Standardstufe.
    [Theory]
    [InlineData(SmoothingLevel.Correct)]
    [InlineData(SmoothingLevel.Smooth)]
    [InlineData(SmoothingLevel.Message)]
    public void Regel5_kein_Treffer_liefert_den_Standard(SmoothingLevel standard)
    {
        AppSmoothingMap liste = Liste(P("Notizblock", SmoothingLevel.Off));

        Assert.Equal(standard, AppSmoothingMap.Resolve(standard, liste, "Rechenhilfe"));
        Assert.Equal(standard, AppSmoothingMap.Resolve(standard, liste, "Notizblock2"));
    }

    // Regel 5: Ohne Liste (null oder leer) gilt die Standardstufe.
    [Fact]
    public void Regel5_ohne_Liste_gilt_der_Standard()
    {
        Assert.Equal(SmoothingLevel.Message, AppSmoothingMap.Resolve(SmoothingLevel.Message, null, "Notizblock"));
        Assert.Equal(SmoothingLevel.Correct, AppSmoothingMap.Resolve(SmoothingLevel.Correct, AppSmoothingMap.Empty, "Notizblock"));
    }

    // Regel 5: Ohne gültigen Namen gilt die Standardstufe.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".exe")]
    [InlineData("Notiz/block")]
    public void Regel5_ohne_gueltigen_Namen_gilt_der_Standard(string? prozess)
    {
        AppSmoothingMap liste = Liste(P("Notizblock", SmoothingLevel.Off));

        Assert.Equal(SmoothingLevel.Message, AppSmoothingMap.Resolve(SmoothingLevel.Message, liste, prozess));
    }

    // ---------------------------------------------------------------- Regel 6

    // Regel 6: Jeder bekannte Name bekommt seinen lesbaren Namen, Groß und Klein egal.
    [Theory]
    [InlineData("WINWORD", "Word")]
    [InlineData("winword", "Word")]
    [InlineData("OUTLOOK", "Outlook")]
    [InlineData("outlook", "Outlook")]
    [InlineData("olk", "Outlook")]
    [InlineData("OLK", "Outlook")]
    [InlineData("EXCEL", "Excel")]
    [InlineData("excel", "Excel")]
    [InlineData("POWERPNT", "PowerPoint")]
    [InlineData("powerpnt", "PowerPoint")]
    [InlineData("chrome", "Chrome")]
    [InlineData("CHROME", "Chrome")]
    [InlineData("msedge", "Edge")]
    [InlineData("MSEDGE", "Edge")]
    [InlineData("firefox", "Firefox")]
    [InlineData("FireFox", "Firefox")]
    [InlineData("WhatsApp", "WhatsApp")]
    [InlineData("whatsapp", "WhatsApp")]
    [InlineData("WhatsApp.Root", "WhatsApp")]
    [InlineData("whatsapp.root", "WhatsApp")]
    [InlineData("claude", "Claude")]
    [InlineData("CLAUDE", "Claude")]
    [InlineData("notepad", "Editor")]
    [InlineData("NOTEPAD", "Editor")]
    [InlineData("Telegram", "Telegram")]
    [InlineData("telegram", "Telegram")]
    [InlineData("ms-teams", "Teams")]
    [InlineData("MS-TEAMS", "Teams")]
    [InlineData("Signal", "Signal")]
    [InlineData("signal", "Signal")]
    public void Regel6_bekannte_Programme_bekommen_einen_lesbaren_Namen(string prozess, string erwartet)
    {
        Assert.Equal(erwartet, AppDisplayNames.For(prozess));
    }

    // Regel 6: Bekannte Namen werden auch mit ".exe" und Rand erkannt (Name wird nach Regel 1 bereinigt).
    [Theory]
    [InlineData("WINWORD.EXE", "Word")]
    [InlineData("  notepad.exe ", "Editor")]
    [InlineData("WhatsApp.Root.exe", "WhatsApp")]
    public void Regel6_bekannte_Programme_auch_mit_exe(string prozess, string erwartet)
    {
        Assert.Equal(erwartet, AppDisplayNames.For(prozess));
    }

    // Regel 6: Unbekannte Programme behalten ihren Namen nach Regel 1.
    [Theory]
    [InlineData("Rechenhilfe", "Rechenhilfe")]
    [InlineData("rechenhilfe.exe", "rechenhilfe")]
    [InlineData("  Mein Tool.EXE ", "Mein Tool")]
    [InlineData("WINWORD2", "WINWORD2")]
    [InlineData("chrome_helper", "chrome_helper")]
    public void Regel6_unbekannte_Programme_behalten_den_Namen(string prozess, string erwartet)
    {
        Assert.Equal(erwartet, AppDisplayNames.For(prozess));
    }

    // Regel 6: Ein ungültiger Name ergibt einen leeren Anzeigenamen.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".exe")]
    [InlineData("Notiz/block")]
    public void Regel6_ungueltiger_Name_ergibt_leer(string? prozess)
    {
        Assert.Equal(string.Empty, AppDisplayNames.For(prozess));
    }

    // Regel 6: Ein Name mit 65 Zeichen ist ungültig und ergibt leer.
    [Fact]
    public void Regel6_zu_langer_Name_ergibt_leer()
    {
        Assert.Equal(string.Empty, AppDisplayNames.For(new string('q', 65)));
    }

    // ---------------------------------------------------------------- Regel 7

    // Regel 7: Reihenfolge der Gründe: Schalter aus, dann nur lokal, dann Terminal, erst dann aus für dieses Programm.
    [Theory]
    [InlineData(false, false, true, false, true, SmoothingOutcome.Off)]
    [InlineData(false, true, false, true, true, SmoothingOutcome.Off)]
    [InlineData(true, true, true, false, true, SmoothingOutcome.LocalOnly)]
    [InlineData(true, false, false, false, true, SmoothingOutcome.LocalOnly)]
    [InlineData(true, true, false, true, true, SmoothingOutcome.LocalOnly)]
    [InlineData(true, false, true, true, true, SmoothingOutcome.Terminal)]
    [InlineData(true, false, true, false, true, SmoothingOutcome.OffForApp)]
    public void Regel7_Reihenfolge_der_Gruende(bool an, bool lokalerLauf, bool ueberNetz, bool terminal, bool ausFuerProgramm, SmoothingOutcome erwartet)
    {
        Assert.Equal(erwartet, SmoothingGate.Check(an, lokalerLauf, ueberNetz, terminal, ausFuerProgramm));
    }

    // Regel 7: Ist alles in Ordnung und das Programm nicht ausgeschaltet, lässt das Gate durch.
    [Fact]
    public void Regel7_ohne_Grund_laesst_das_Gate_durch()
    {
        Assert.Null(SmoothingGate.Check(true, false, true, false, false));
        Assert.Null(SmoothingGate.Check(enabled: true, localRun: false, recognizedOnline: true, terminal: false, offForApp: false));
    }

    // Regel 7: Ohne den fünften Wert verhält sich das Gate wie bisher (Standardwert false).
    [Theory]
    [InlineData(false, false, true, false, SmoothingOutcome.Off)]
    [InlineData(true, true, true, false, SmoothingOutcome.LocalOnly)]
    [InlineData(true, false, false, false, SmoothingOutcome.LocalOnly)]
    [InlineData(true, false, true, true, SmoothingOutcome.Terminal)]
    public void Regel7_ohne_fuenften_Wert_wie_bisher(bool an, bool lokalerLauf, bool ueberNetz, bool terminal, SmoothingOutcome erwartet)
    {
        Assert.Equal(erwartet, SmoothingGate.Check(an, lokalerLauf, ueberNetz, terminal));
        Assert.Equal(SmoothingGate.Check(an, lokalerLauf, ueberNetz, terminal, false), SmoothingGate.Check(an, lokalerLauf, ueberNetz, terminal));
    }

    // Regel 7: Ohne den fünften Wert lässt das Gate bei allem in Ordnung durch.
    [Fact]
    public void Regel7_ohne_fuenften_Wert_laesst_durch()
    {
        Assert.Null(SmoothingGate.Check(true, false, true, false));
    }

    // Regel 7: Protokollwort des neuen Grundes.
    [Fact]
    public void Regel7_Protokollwort_ist_aus_programm()
    {
        Assert.Equal("aus_programm", SmoothingOutcome.OffForApp.ToWord());
    }

    // Regel 7: Das neue Wort ist eindeutig unter allen Ausgängen.
    [Fact]
    public void Regel7_Protokollwort_ist_eindeutig()
    {
        Assert.Single(Enum.GetValues<SmoothingOutcome>(), a => a.ToWord() == "aus_programm");
    }

    // ---------------------------------------------------------------- Regel 8

    private static AppSmoothingMap Gelesen(string json) => DictationSettingsJson.Read(json).SmoothingPerApp ?? AppSmoothingMap.Empty;

    // Regel 8: Gültige Einträge werden als Name auf Wort gelesen.
    [Fact]
    public void Regel8_liest_gueltige_Eintraege()
    {
        AppSmoothingMap liste = Gelesen("""{"smoothingPerApp":{"Notizblock":"aus","Rechenhilfe.exe":"fehler","Plauderbox":"saetze","Briefkasten":"nachricht"}}""");

        Assert.Equal(4, liste.Count);
        Assert.Equal(SmoothingLevel.Off, liste.Get("Notizblock"));
        Assert.Equal(SmoothingLevel.Correct, liste.Get("Rechenhilfe"));
        Assert.Equal(SmoothingLevel.Smooth, liste.Get("Plauderbox"));
        Assert.Equal(SmoothingLevel.Message, liste.Get("Briefkasten"));
    }

    // Regel 8: Fehlt der Schlüssel, ist die Liste leer.
    [Fact]
    public void Regel8_fehlender_Schluessel_ergibt_leere_Liste()
    {
        Assert.Equal(0, Gelesen("{}").Count);
        Assert.Equal(0, Gelesen("""{"smoothingEnabled":true}""").Count);
    }

    // Regel 8: Ist der Wert kein Objekt, wird er übergangen.
    [Theory]
    [InlineData("""{"smoothingPerApp":null}""")]
    [InlineData("""{"smoothingPerApp":"Notizblock"}""")]
    [InlineData("""{"smoothingPerApp":42}""")]
    [InlineData("""{"smoothingPerApp":true}""")]
    [InlineData("""{"smoothingPerApp":["Notizblock","aus"]}""")]
    [InlineData("""{"smoothingPerApp":[{"Notizblock":"aus"}]}""")]
    public void Regel8_kein_Objekt_wird_uebergangen(string json)
    {
        Assert.Equal(0, Gelesen(json).Count);
    }

    // Regel 8: Kein Objekt verdirbt die übrigen Einstellungen nicht.
    [Fact]
    public void Regel8_kein_Objekt_laesst_die_uebrigen_Einstellungen_stehen()
    {
        DictationSettings s = DictationSettingsJson.Read("""{"smoothingEnabled":true,"smoothingLevel":"nachricht","smoothingPerApp":"kaputt"}""");

        Assert.True(s.SmoothingEnabled);
        Assert.Equal(SmoothingLevel.Message, s.Smoothing);
    }

    // Regel 8: Werte ohne Text werden übergangen, gültige daneben bleiben.
    [Fact]
    public void Regel8_Werte_ohne_Text_werden_uebergangen()
    {
        AppSmoothingMap liste = Gelesen(
            """{"smoothingPerApp":{"Zahl":1,"Leer":null,"Wahr":true,"Feld":["aus"],"Objekt":{"stufe":"aus"},"Notizblock":"fehler"}}""");

        Assert.Equal([P("Notizblock", SmoothingLevel.Correct)], liste.Entries);
    }

    // Regel 8: Unbekannte Wörter werden übergangen, auch Großschreibung der Wörter und Namen der Aufzählung.
    [Fact]
    public void Regel8_unbekannte_Woerter_werden_uebergangen()
    {
        AppSmoothingMap liste = Gelesen(
            """{"smoothingPerApp":{"Eins":"unsinn","Zwei":"","Drei":"Off","Vier":"standard","Notizblock":"saetze"}}""");

        Assert.Equal([P("Notizblock", SmoothingLevel.Smooth)], liste.Entries);
    }

    // Regel 8 mit Regel 2: Ungültige Namen fallen beim Lesen weg, bei gleichem Namen gewinnt der erste.
    [Fact]
    public void Regel8_beim_Lesen_gilt_Regel2()
    {
        AppSmoothingMap liste = Gelesen(
            """{"smoothingPerApp":{"Notiz/block":"aus","":"aus","Rechenhilfe":"fehler","RECHENHILFE.exe":"nachricht"}}""");

        Assert.Equal([P("Rechenhilfe", SmoothingLevel.Correct)], liste.Entries);
    }

    // Regel 8 mit Regel 2: Beim Lesen bleiben höchstens 50 Einträge.
    [Fact]
    public void Regel8_beim_Lesen_hoechstens_50()
    {
        JsonObject eintraege = [];
        for (int i = 1; i <= 60; i++) eintraege[$"Werkzeug{i:D2}"] = "fehler";
        JsonObject wurzel = new() { ["smoothingPerApp"] = eintraege };

        Assert.Equal(50, Gelesen(wurzel.ToJsonString()).Count);
    }

    // Regel 8: Geschrieben wird ein Objekt Name auf Wort.
    [Fact]
    public void Regel8_schreibt_Name_auf_Wort()
    {
        DictationSettings s = new(SmoothingPerApp: Liste(
            P("Notizblock", SmoothingLevel.Off),
            P("Rechenhilfe", SmoothingLevel.Correct),
            P("Plauderbox", SmoothingLevel.Smooth),
            P("Briefkasten", SmoothingLevel.Message)));

        JsonObject wurzel = JsonNode.Parse(DictationSettingsJson.Merge(null, s)!)!.AsObject();
        JsonObject liste = wurzel["smoothingPerApp"]!.AsObject();

        Assert.Equal(4, liste.Count);
        Assert.Equal("aus", liste["Notizblock"]!.GetValue<string>());
        Assert.Equal("fehler", liste["Rechenhilfe"]!.GetValue<string>());
        Assert.Equal("saetze", liste["Plauderbox"]!.GetValue<string>());
        Assert.Equal("nachricht", liste["Briefkasten"]!.GetValue<string>());
    }

    // Regel 8: Eine leere Liste (und keine Liste) wird als fehlender Schlüssel gespeichert, auch wenn er vorher da war.
    [Fact]
    public void Regel8_leere_Liste_entfernt_den_Schluessel()
    {
        const string vorhanden = """{"smoothingPerApp":{"Notizblock":"aus"},"language":"de"}""";

        string mitLeer = DictationSettingsJson.Merge(vorhanden, new DictationSettings(SmoothingPerApp: AppSmoothingMap.Empty))!;
        string ohne = DictationSettingsJson.Merge(vorhanden, new DictationSettings())!;
        string neu = DictationSettingsJson.Merge(null, new DictationSettings(SmoothingPerApp: AppSmoothingMap.Empty))!;

        Assert.False(JsonNode.Parse(mitLeer)!.AsObject().ContainsKey("smoothingPerApp"));
        Assert.False(JsonNode.Parse(ohne)!.AsObject().ContainsKey("smoothingPerApp"));
        Assert.False(JsonNode.Parse(neu)!.AsObject().ContainsKey("smoothingPerApp"));
    }

    // Regel 8: Fremde Schlüssel bleiben beim Schreiben erhalten.
    [Fact]
    public void Regel8_fremde_Schluessel_bleiben()
    {
        const string vorhanden = """{"modelsDirectory":"F:\\Modelle","whisperRuntime":"vulkan","zukunft":{"a":1},"smoothingPerApp":{"Alt":"aus"}}""";
        DictationSettings s = new(SmoothingPerApp: Liste(P("Notizblock", SmoothingLevel.Message)));

        JsonObject wurzel = JsonNode.Parse(DictationSettingsJson.Merge(vorhanden, s)!)!.AsObject();

        Assert.Equal("F:\\Modelle", wurzel["modelsDirectory"]!.GetValue<string>());
        Assert.Equal("vulkan", wurzel["whisperRuntime"]!.GetValue<string>());
        Assert.Equal(1, wurzel["zukunft"]!["a"]!.GetValue<int>());
        JsonObject liste = wurzel["smoothingPerApp"]!.AsObject();
        Assert.Single(liste);
        Assert.Equal("nachricht", liste["Notizblock"]!.GetValue<string>());
    }

    // Regel 8: Schreiben und Lesen ergibt dieselbe Liste und dieselben Einstellungen.
    [Fact]
    public void Regel8_hin_und_zurueck_ergibt_dieselbe_Liste()
    {
        AppSmoothingMap liste = Liste(
            P("Notizblock", SmoothingLevel.Off),
            P("rechenhilfe", SmoothingLevel.Correct),
            P("Plauder Box", SmoothingLevel.Smooth),
            P("Brief_kasten-2.Root", SmoothingLevel.Message));
        DictationSettings s = new(SmoothingEnabled: true, Smoothing: SmoothingLevel.Correct, SmoothingPerApp: liste);

        DictationSettings zurueck = DictationSettingsJson.Read(DictationSettingsJson.Merge(null, s));

        Assert.Equal(liste, zurueck.SmoothingPerApp);
        Assert.Equal(liste.Entries, zurueck.SmoothingPerApp!.Entries);
        Assert.Equal(s.Normalize(), zurueck);
    }

    // Regel 8: Eine Liste mit 50 Einträgen übersteht Schreiben und Lesen vollständig.
    [Fact]
    public void Regel8_hin_und_zurueck_mit_50_Eintraegen()
    {
        AppSmoothingMap liste = Voll(50);

        DictationSettings zurueck = DictationSettingsJson.Read(DictationSettingsJson.Merge(null, new DictationSettings(SmoothingPerApp: liste)));

        Assert.Equal(liste, zurueck.SmoothingPerApp);
    }

    // Regel 8: Normalize macht aus keiner Liste die leere Liste und lässt eine vorhandene Liste stehen.
    [Fact]
    public void Regel8_Normalize_macht_aus_null_die_leere_Liste()
    {
        // Nachgeschärft am 15.10.2026 (Regel 8): Schon der Konstruktor macht aus null die leere Liste.
        Assert.Equal(AppSmoothingMap.Empty, new DictationSettings().SmoothingPerApp);
        Assert.Equal(AppSmoothingMap.Empty, new DictationSettings().Normalize().SmoothingPerApp);
        Assert.Equal(AppSmoothingMap.Empty, new DictationSettings(SmoothingPerApp: null).Normalize().SmoothingPerApp);

        AppSmoothingMap liste = Liste(P("Notizblock", SmoothingLevel.Off));
        Assert.Equal(liste, new DictationSettings(SmoothingPerApp: liste).Normalize().SmoothingPerApp);
    }
}
