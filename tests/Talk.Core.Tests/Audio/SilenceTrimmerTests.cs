using Talk.Core.Audio;
using Xunit;

namespace Talk.Core.Tests.Audio;

/// <summary>
/// Prüft die reine Rechnung auf Sprachabschnitten ausschließlich gegen die Regeln 4 (Zusammenführen),
/// 5 (Sprachdauer) und 6 (Zuschneiden) der reinen Logik in PLAYBOOK.md, Abschnitt 14.
/// Die Tests kennen die Umsetzung nicht. Bei 16000 Hz entspricht eine Millisekunde 16 Abtastwerten:
/// MinSilenceMs 300 sind 4800, MinSpeechMs 250 sind 4000, PaddingMs 200 sind 3200 Abtastwerte.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class SilenceTrimmerTests
{
    private const int Abtastrate = 16000;

    private static readonly VadOptions Standard = new();

    private static SpeechSegment Abschnitt(int anfang, int ende) => new(anfang, ende);

    private static IReadOnlyList<SpeechSegment> Liste(params SpeechSegment[] abschnitte) => abschnitte;

    /// <summary>Clip mit aufsteigenden Werten (Wert gleich Index), damit ein Ausschnitt eindeutig prüfbar ist.</summary>
    private static AudioClip ClipMitIndexwerten(int anzahl, int abtastrate = Abtastrate)
    {
        var werte = new short[anzahl];
        for (var i = 0; i < anzahl; i++)
        {
            werte[i] = (short)i;
        }

        return new AudioClip(werte, abtastrate);
    }

    // Regel 4: Abschnitte mit Lücke kleiner als MinSilenceMs werden zu einem.
    [Fact]
    public void Luecke_kleiner_als_MinSilence_fuehrt_zusammen()
    {
        // Lücke 2000 Abtastwerte (125 ms) ist kleiner als 4800 (300 ms).
        var eingabe = Liste(Abschnitt(0, 8000), Abschnitt(10000, 20000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 20000)), ergebnis);
    }

    // Regel 4: Drei Abschnitte mit kleinen Lücken werden zu einem einzigen.
    [Fact]
    public void Mehrere_kleine_Luecken_fuehren_zu_einem_Abschnitt()
    {
        var eingabe = Liste(Abschnitt(0, 8000), Abschnitt(9000, 16000), Abschnitt(17000, 24000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 24000)), ergebnis);
    }

    // Regel 4: Abschnitte mit Lücke größer als MinSilenceMs bleiben getrennt und unverändert.
    [Fact]
    public void Luecke_groesser_als_MinSilence_trennt()
    {
        // Lücke 8000 Abtastwerte (500 ms) ist größer als 4800 (300 ms).
        var eingabe = Liste(Abschnitt(0, 8000), Abschnitt(16000, 24000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 8000), Abschnitt(16000, 24000)), ergebnis);
    }

    // Regel 4: Eine Lücke genau gleich MinSilenceMs ist nicht kleiner und trennt deshalb.
    [Fact]
    public void Luecke_genau_gleich_MinSilence_trennt()
    {
        var eingabe = Liste(Abschnitt(0, 8000), Abschnitt(12800, 20000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 8000), Abschnitt(12800, 20000)), ergebnis);
    }

    // Regel 4: Die Lücke wird über die Abtastrate in Abtastwerte umgerechnet. Dieselbe Lücke von 3000 Abtastwerten
    // ist bei 16000 Hz (187 ms) kleiner als 300 ms und führt zusammen, bei 8000 Hz (375 ms) größer und trennt.
    [Fact]
    public void Luecke_wird_mit_der_Abtastrate_umgerechnet()
    {
        var eingabe = Liste(Abschnitt(0, 8000), Abschnitt(11000, 20000));

        var bei16000 = SilenceTrimmer.Merge(eingabe, Standard, 16000);
        var bei8000 = SilenceTrimmer.Merge(eingabe, Standard, 8000);

        Assert.Equal(Liste(Abschnitt(0, 20000)), bei16000);
        Assert.Equal(Liste(Abschnitt(0, 8000), Abschnitt(11000, 20000)), bei8000);
    }

    // Regel 4: Abschnitte mit Länge kleiner als MinSpeechMs fallen weg, längere bleiben.
    [Fact]
    public void Kurze_Abschnitte_fallen_weg()
    {
        // 3200 Abtastwerte (200 ms) sind kürzer als 4000 (250 ms); 8000 (500 ms) bleiben.
        var eingabe = Liste(Abschnitt(0, 3200), Abschnitt(16000, 24000), Abschnitt(40000, 41600));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(16000, 24000)), ergebnis);
    }

    // Regel 4: Ein Abschnitt genau so lang wie MinSpeechMs ist nicht kürzer und bleibt.
    [Fact]
    public void Abschnitt_genau_gleich_MinSpeech_bleibt()
    {
        var eingabe = Liste(Abschnitt(1000, 5000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(1000, 5000)), ergebnis);
    }

    // Regel 4: Die Mindestlänge gilt nach dem Zusammenführen. Zwei einzeln zu kurze Abschnitte mit kleiner Lücke
    // ergeben zusammen einen ausreichend langen Abschnitt und bleiben.
    [Fact]
    public void Mindestlaenge_gilt_nach_dem_Zusammenfuehren()
    {
        // Je 2400 Abtastwerte (150 ms), Lücke 1600 (100 ms), zusammen 6400 (400 ms).
        var eingabe = Liste(Abschnitt(0, 2400), Abschnitt(4000, 6400));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 6400)), ergebnis);
    }

    // Regel 4: Sind alle Abschnitte zu kurz und weit auseinander, bleibt nichts übrig.
    [Fact]
    public void Nur_kurze_getrennte_Abschnitte_ergeben_eine_leere_Liste()
    {
        var eingabe = Liste(Abschnitt(0, 1000), Abschnitt(20000, 21000), Abschnitt(40000, 41000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Empty(ergebnis);
    }

    // Regel 4: Leere oder verkehrte Abschnitte (Ende kleiner gleich Anfang) fallen weg, gültige bleiben.
    [Fact]
    public void Leere_und_verkehrte_Abschnitte_fallen_weg()
    {
        var eingabe = Liste(
            Abschnitt(0, 8000),
            Abschnitt(8000, 8000),
            Abschnitt(20000, 15000),
            Abschnitt(30000, 40000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 8000), Abschnitt(30000, 40000)), ergebnis);
    }

    // Regel 4: Nur leere oder verkehrte Abschnitte ergeben eine leere Liste.
    [Fact]
    public void Nur_leere_oder_verkehrte_Abschnitte_ergeben_eine_leere_Liste()
    {
        var eingabe = Liste(Abschnitt(5000, 5000), Abschnitt(9000, 1000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Empty(ergebnis);
    }

    // Regel 4: Ohne Eingabe kommt eine leere Liste.
    [Fact]
    public void Leer_bleibt_leer()
    {
        var ergebnis = SilenceTrimmer.Merge(Array.Empty<SpeechSegment>(), Standard, Abtastrate);

        Assert.NotNull(ergebnis);
        Assert.Empty(ergebnis);
    }

    // Regel 4: Die Reihenfolge bleibt aufsteigend, auch wenn in der Mitte zusammengeführt wird.
    [Fact]
    public void Reihenfolge_bleibt_aufsteigend()
    {
        var eingabe = Liste(
            Abschnitt(0, 8000),
            Abschnitt(20000, 28000),
            Abschnitt(29000, 36000),
            Abschnitt(50000, 60000));

        var ergebnis = SilenceTrimmer.Merge(eingabe, Standard, Abtastrate);

        Assert.Equal(Liste(Abschnitt(0, 8000), Abschnitt(20000, 36000), Abschnitt(50000, 60000)), ergebnis);
        for (var i = 1; i < ergebnis.Count; i++)
        {
            Assert.True(ergebnis[i - 1].EndSample <= ergebnis[i].StartSample);
        }
    }

    // Regel 4: Mit MinSilenceMs 0 und MinSpeechMs 0 bleiben getrennte gültige Abschnitte genau so, wie sie kamen.
    [Fact]
    public void Ohne_Mindestwerte_bleiben_gueltige_Abschnitte_unveraendert()
    {
        var optionen = new VadOptions(MinSpeechMs: 0, MinSilenceMs: 0);
        var eingabe = Liste(Abschnitt(0, 100), Abschnitt(200, 300), Abschnitt(301, 302));

        var ergebnis = SilenceTrimmer.Merge(eingabe, optionen, Abtastrate);

        Assert.Equal(eingabe, ergebnis);
    }

    // Regel 5: Sprachdauer ist die Summe der Längen geteilt durch Abtastrate.
    [Fact]
    public void Sprachdauer_ist_Summe_der_Laengen_geteilt_durch_Abtastrate()
    {
        // 8000 plus 4000 Abtastwerte sind 12000, bei 16000 Hz also 0,75 Sekunden.
        var abschnitte = Liste(Abschnitt(0, 8000), Abschnitt(16000, 20000));

        var dauer = SilenceTrimmer.SpeechDuration(abschnitte, Abtastrate);

        Assert.Equal(TimeSpan.FromSeconds(0.75), dauer);
    }

    // Regel 5: Die Sprachdauer rechnet mit der übergebenen Abtastrate.
    [Fact]
    public void Sprachdauer_nutzt_die_Abtastrate()
    {
        var abschnitte = Liste(Abschnitt(0, 8000), Abschnitt(16000, 20000));

        var dauer = SilenceTrimmer.SpeechDuration(abschnitte, 8000);

        Assert.Equal(TimeSpan.FromSeconds(1.5), dauer);
    }

    // Regel 5: Ohne Abschnitte ist die Sprachdauer null; ein einzelner Abschnitt zählt seine Länge.
    [Fact]
    public void Sprachdauer_ohne_Abschnitte_ist_null_und_ein_Abschnitt_zaehlt_seine_Laenge()
    {
        Assert.Equal(TimeSpan.Zero, SilenceTrimmer.SpeechDuration(Array.Empty<SpeechSegment>(), Abtastrate));
        Assert.Equal(TimeSpan.FromSeconds(1), SilenceTrimmer.SpeechDuration(Liste(Abschnitt(4000, 20000)), Abtastrate));
    }

    // Regel 6: Das Ergebnis reicht vom ersten Anfang minus PaddingMs bis zum letzten Ende plus PaddingMs
    // und ist eine exakte Kopie dieses Ausschnitts mit derselben Abtastrate.
    [Fact]
    public void Zuschneiden_nimmt_den_Rand_vor_und_nach_dem_Abschnitt_mit()
    {
        var clip = ClipMitIndexwerten(32000);
        var abschnitte = Liste(Abschnitt(8000, 16000));

        var ergebnis = SilenceTrimmer.Trim(clip, abschnitte, Standard);

        // 8000 minus 3200 ist 4800, 16000 plus 3200 ist 19200, also 14400 Werte.
        Assert.Equal(14400, ergebnis.Samples.Length);
        Assert.Equal(clip.Samples[4800..19200], ergebnis.Samples);
        Assert.Equal(4800, ergebnis.Samples[0]);
        Assert.Equal(19199, ergebnis.Samples[^1]);
        Assert.Equal(clip.SampleRate, ergebnis.SampleRate);
    }

    // Regel 6: Der Rand wird an den Clip Grenzen begrenzt, am Anfang und am Ende.
    [Fact]
    public void Zuschneiden_begrenzt_den_Rand_an_den_Clip_Grenzen()
    {
        var clip = ClipMitIndexwerten(32000);

        // Anfang 1000 minus 3200 wäre negativ, Ende 31000 plus 3200 wäre hinter dem Clip: es bleibt der ganze Clip.
        var ganz = SilenceTrimmer.Trim(clip, Liste(Abschnitt(1000, 31000)), Standard);
        Assert.Equal(clip.Samples, ganz.Samples);
        Assert.Equal(clip.SampleRate, ganz.SampleRate);

        // Nur der Anfang wird begrenzt.
        var vorne = SilenceTrimmer.Trim(clip, Liste(Abschnitt(1000, 8000)), Standard);
        Assert.Equal(clip.Samples[0..11200], vorne.Samples);

        // Nur das Ende wird begrenzt.
        var hinten = SilenceTrimmer.Trim(clip, Liste(Abschnitt(24000, 31000)), Standard);
        Assert.Equal(clip.Samples[20800..32000], hinten.Samples);
    }

    // Regel 6: Ein Abschnitt, der den ganzen Clip umfasst, ergibt eine exakte Kopie des ganzen Clips.
    [Fact]
    public void Abschnitt_ueber_den_ganzen_Clip_ergibt_eine_Kopie_des_ganzen_Clips()
    {
        var clip = ClipMitIndexwerten(16000);

        var ergebnis = SilenceTrimmer.Trim(clip, Liste(Abschnitt(0, 16000)), Standard);

        Assert.Equal(clip.Samples, ergebnis.Samples);
        Assert.Equal(clip.SampleRate, ergebnis.SampleRate);
    }

    // Regel 6: Pausen zwischen den Abschnitten bleiben erhalten; das Ergebnis reicht vom ersten Anfang bis zum letzten Ende.
    [Fact]
    public void Pausen_zwischen_den_Abschnitten_bleiben_erhalten()
    {
        var clip = ClipMitIndexwerten(32000);
        var abschnitte = Liste(Abschnitt(4000, 8000), Abschnitt(20000, 24000));

        var ergebnis = SilenceTrimmer.Trim(clip, abschnitte, Standard);

        // 4000 minus 3200 ist 800, 24000 plus 3200 ist 27200, also 26400 Werte samt der Pause von 8000 bis 20000.
        Assert.Equal(26400, ergebnis.Samples.Length);
        Assert.Equal(clip.Samples[800..27200], ergebnis.Samples);
    }

    // Regel 6: Ohne Abschnitte ist das Ergebnis leer (0 Werte) mit gleicher Abtastrate.
    [Theory]
    [InlineData(16000)]
    [InlineData(8000)]
    [InlineData(44100)]
    public void Ohne_Abschnitte_ist_das_Ergebnis_leer_mit_gleicher_Abtastrate(int abtastrate)
    {
        var clip = ClipMitIndexwerten(16000, abtastrate);

        var ergebnis = SilenceTrimmer.Trim(clip, Array.Empty<SpeechSegment>(), Standard);

        Assert.NotNull(ergebnis);
        Assert.Empty(ergebnis.Samples);
        Assert.Equal(abtastrate, ergebnis.SampleRate);
        Assert.Equal(TimeSpan.Zero, ergebnis.Duration);
    }

    // Regel 6: Ohne Rand (PaddingMs 0) sind die Werte genau die Kopie des Abschnitts.
    [Fact]
    public void Ohne_Rand_ist_das_Ergebnis_genau_der_Abschnitt()
    {
        var clip = ClipMitIndexwerten(32000);
        var optionen = new VadOptions(PaddingMs: 0);

        var ergebnis = SilenceTrimmer.Trim(clip, Liste(Abschnitt(8000, 16000)), optionen);

        Assert.Equal(8000, ergebnis.Samples.Length);
        Assert.Equal(clip.Samples[8000..16000], ergebnis.Samples);
    }

    // Regel 6: Der Rand wird mit der Abtastrate des Clips umgerechnet. Bei 8000 Hz sind 200 ms 1600 Abtastwerte.
    [Fact]
    public void Rand_wird_mit_der_Abtastrate_des_Clips_umgerechnet()
    {
        var clip = ClipMitIndexwerten(16000, 8000);

        var ergebnis = SilenceTrimmer.Trim(clip, Liste(Abschnitt(4000, 8000)), Standard);

        // 4000 minus 1600 ist 2400, 8000 plus 1600 ist 9600, also 7200 Werte.
        Assert.Equal(7200, ergebnis.Samples.Length);
        Assert.Equal(clip.Samples[2400..9600], ergebnis.Samples);
        Assert.Equal(8000, ergebnis.SampleRate);
    }

    // Regel 6: Das Ergebnis ist eine Kopie, der ursprüngliche Clip bleibt unverändert.
    [Fact]
    public void Zuschneiden_veraendert_den_urspruenglichen_Clip_nicht()
    {
        var clip = ClipMitIndexwerten(32000);
        var vorher = (short[])clip.Samples.Clone();

        _ = SilenceTrimmer.Trim(clip, Liste(Abschnitt(8000, 16000)), Standard);

        Assert.Equal(vorher, clip.Samples);
        Assert.Equal(32000, clip.Samples.Length);
    }

    // Vertrag: Length eines Abschnitts ist Ende minus Anfang.
    [Fact]
    public void SpeechSegment_Length_ist_Ende_minus_Anfang()
    {
        Assert.Equal(8000, Abschnitt(4000, 12000).Length);
        Assert.Equal(0, Abschnitt(5000, 5000).Length);
        Assert.Equal(-1000, Abschnitt(5000, 4000).Length);
    }
}
