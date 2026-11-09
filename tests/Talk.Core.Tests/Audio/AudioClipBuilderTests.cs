using Talk.Core.Audio;
using Xunit;

namespace Talk.Core.Tests.Audio;

/// <summary>
/// Prüft den Sammler ausschließlich gegen Regel 2 der reinen Logik in PLAYBOOK.md, Abschnitt 14,
/// und gegen den Vertrag (Abtastrate 16000 und Obergrenze fünf Minuten als Standard).
/// Die Tests kennen die Umsetzung nicht. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class AudioClipBuilderTests
{
    /// <summary>Block mit aufsteigenden Werten ab einem Startwert, damit Reihenfolge und Inhalt prüfbar sind.</summary>
    private static short[] Aufsteigend(int start, int anzahl)
    {
        var block = new short[anzahl];
        for (var i = 0; i < anzahl; i++)
        {
            block[i] = (short)(start + i);
        }

        return block;
    }

    // Regel 2: Append zählt Werte, auch über mehrere Blöcke; ein leerer Block ändert nichts.
    [Fact]
    public void Append_zaehlt_Werte_ueber_mehrere_Bloecke()
    {
        var sammler = new AudioClipBuilder();

        Assert.Equal(0, sammler.Count);

        sammler.Append(new short[100]);
        Assert.Equal(100, sammler.Count);

        sammler.Append(new short[250]);
        Assert.Equal(350, sammler.Count);

        sammler.Append(ReadOnlySpan<short>.Empty);
        Assert.Equal(350, sammler.Count);

        Assert.False(sammler.Truncated);
    }

    // Regel 2: Duration ist Count geteilt durch Abtastrate.
    [Fact]
    public void Duration_ist_Count_geteilt_durch_Abtastrate()
    {
        var sammler = new AudioClipBuilder(16000);

        Assert.Equal(TimeSpan.Zero, sammler.Duration);

        sammler.Append(new short[8000]);
        Assert.Equal(TimeSpan.FromSeconds(0.5), sammler.Duration);

        sammler.Append(new short[4000]);
        Assert.Equal(TimeSpan.FromSeconds(0.75), sammler.Duration);
    }

    // Regel 2: Duration richtet sich nach der übergebenen Abtastrate, nicht nach 16000.
    [Fact]
    public void Duration_nutzt_die_uebergebene_Abtastrate()
    {
        var sammler = new AudioClipBuilder(8000);

        sammler.Append(new short[8000]);

        Assert.Equal(TimeSpan.FromSeconds(1), sammler.Duration);
    }

    // Regel 2: Build liefert genau die gesammelten Werte in Reihenfolge, mit der Abtastrate des Sammlers.
    [Fact]
    public void Build_liefert_genau_die_gesammelten_Werte_in_Reihenfolge()
    {
        var sammler = new AudioClipBuilder(16000);
        sammler.Append(new short[] { 1, 2, 3 });
        sammler.Append(new short[] { 4, 5 });
        sammler.Append(new short[] { -6, short.MaxValue, short.MinValue });

        var clip = sammler.Build();

        Assert.Equal(new short[] { 1, 2, 3, 4, 5, -6, short.MaxValue, short.MinValue }, clip.Samples);
        Assert.Equal(16000, clip.SampleRate);
        Assert.Equal(8, sammler.Count);
    }

    // Regel 2: Build übernimmt die Abtastrate des Sammlers in den Clip, auch wenn sie nicht 16000 ist.
    [Fact]
    public void Build_uebernimmt_die_Abtastrate_des_Sammlers()
    {
        var sammler = new AudioClipBuilder(8000);
        sammler.Append(new short[] { 7, 8, 9 });

        var clip = sammler.Build();

        Assert.Equal(8000, clip.SampleRate);
        Assert.Equal(new short[] { 7, 8, 9 }, clip.Samples);
    }

    // Regel 2: Build liefert die gesammelten Werte; ein zweiter Aufruf liefert dieselben Werte noch einmal.
    [Fact]
    public void Build_zweimal_liefert_dieselben_Werte()
    {
        var sammler = new AudioClipBuilder();
        sammler.Append(Aufsteigend(100, 50));

        var erster = sammler.Build();
        var zweiter = sammler.Build();

        Assert.Equal(erster.Samples, zweiter.Samples);
        Assert.Equal(50, sammler.Count);
    }

    // Regel 2: Über der Obergrenze wird abgeschnitten, Truncated ist true und Count bleibt bei der Obergrenze.
    // Build liefert genau die Werte bis zur Obergrenze in Reihenfolge.
    [Fact]
    public void Ueber_der_Obergrenze_wird_abgeschnitten_und_Count_bleibt_bei_der_Obergrenze()
    {
        // 16000 Hz und 500 ms ergeben eine Obergrenze von 8000 Werten.
        var sammler = new AudioClipBuilder(16000, TimeSpan.FromMilliseconds(500));

        sammler.Append(Aufsteigend(0, 6000));
        Assert.Equal(6000, sammler.Count);
        Assert.False(sammler.Truncated);

        sammler.Append(Aufsteigend(6000, 6000));

        Assert.Equal(8000, sammler.Count);
        Assert.True(sammler.Truncated);
        Assert.Equal(TimeSpan.FromMilliseconds(500), sammler.Duration);

        var clip = sammler.Build();
        Assert.Equal(8000, clip.Samples.Length);
        Assert.Equal(Aufsteigend(0, 8000), clip.Samples);

        // Weitere Werte ändern nichts mehr.
        sammler.Append(Aufsteigend(20000, 100));
        Assert.Equal(8000, sammler.Count);
        Assert.True(sammler.Truncated);
        Assert.Equal(8000, sammler.Build().Samples.Length);
    }

    // Regel 2: Genau an der Obergrenze ist nichts abgeschnitten, erst der nächste Wert darüber setzt Truncated.
    [Fact]
    public void Genau_an_der_Obergrenze_ist_nichts_abgeschnitten()
    {
        var sammler = new AudioClipBuilder(16000, TimeSpan.FromMilliseconds(500));

        sammler.Append(new short[8000]);

        Assert.Equal(8000, sammler.Count);
        Assert.False(sammler.Truncated);

        sammler.Append(new short[1]);

        Assert.Equal(8000, sammler.Count);
        Assert.True(sammler.Truncated);
    }

    // Regel 2: Clear setzt Count auf 0 und Truncated auf false; danach kann wieder gesammelt werden.
    [Fact]
    public void Clear_setzt_Count_auf_0_und_Truncated_auf_false()
    {
        var sammler = new AudioClipBuilder(16000, TimeSpan.FromMilliseconds(500));
        sammler.Append(new short[9000]);
        Assert.True(sammler.Truncated);

        sammler.Clear();

        Assert.Equal(0, sammler.Count);
        Assert.False(sammler.Truncated);
        Assert.Equal(TimeSpan.Zero, sammler.Duration);

        sammler.Append(new short[] { 42 });
        Assert.Equal(1, sammler.Count);
        Assert.False(sammler.Truncated);
        Assert.Equal(new short[] { 42 }, sammler.Build().Samples);
    }

    // Regel 2: Build nach Clear ist leer, die Abtastrate bleibt.
    [Fact]
    public void Build_nach_Clear_ist_leer()
    {
        var sammler = new AudioClipBuilder(8000);
        sammler.Append(Aufsteigend(1, 300));

        sammler.Clear();
        var clip = sammler.Build();

        Assert.Empty(clip.Samples);
        Assert.Equal(8000, clip.SampleRate);
        Assert.Equal(TimeSpan.Zero, clip.Duration);
    }

    // Regel 2: Ungültige Abtastrate wirft ArgumentOutOfRangeException.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-16000)]
    public void Ungueltige_Abtastrate_wirft_ArgumentOutOfRangeException(int abtastrate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClipBuilder(abtastrate));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClipBuilder(abtastrate, TimeSpan.FromMinutes(1)));
    }

    // Regel 2: Ungültige Obergrenze (null Dauer oder negativ) wirft ArgumentOutOfRangeException.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60000)]
    public void Ungueltige_Obergrenze_wirft_ArgumentOutOfRangeException(int millisekunden)
    {
        var obergrenze = TimeSpan.FromMilliseconds(millisekunden);

        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClipBuilder(16000, obergrenze));
    }

    // Vertrag: Ohne Argumente gelten Abtastrate 16000 und Obergrenze fünf Minuten; der Sammler startet leer.
    [Fact]
    public void Ohne_Argumente_gelten_die_Standardwerte()
    {
        var sammler = new AudioClipBuilder();

        Assert.Equal(TimeSpan.FromMinutes(5), AudioClipBuilder.DefaultMaxDuration);
        Assert.Equal(0, sammler.Count);
        Assert.Equal(TimeSpan.Zero, sammler.Duration);
        Assert.False(sammler.Truncated);

        var clip = sammler.Build();
        Assert.Empty(clip.Samples);
        Assert.Equal(16000, clip.SampleRate);
    }
}
