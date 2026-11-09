using Talk.Core.Audio;
using Xunit;

namespace Talk.Core.Tests.Audio;

/// <summary>
/// Prüft den Pegelmesser ausschließlich gegen Regel 1 der reinen Logik in PLAYBOOK.md, Abschnitt 14.
/// Die Tests kennen die Umsetzung nicht. Pegel sind Zahlen zwischen 0 und 1 auf einer Dezibel Skala
/// von FloorDb (minus 60 dB) bis 0 dB. Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class AudioLevelMeterTests
{
    private const int Abtastrate = 16000;

    /// <summary>Block mit lauter demselben Wert.</summary>
    private static short[] Konstant(short wert, int anzahl = 1600)
    {
        var block = new short[anzahl];
        Array.Fill(block, wert);
        return block;
    }

    /// <summary>Eine Sekunde Sinus mit 440 Hz und der angegebenen Amplitude, also 440 volle Schwingungen.</summary>
    private static short[] Sinus(int amplitude, int anzahl = Abtastrate)
    {
        var block = new short[anzahl];
        for (var n = 0; n < anzahl; n++)
        {
            block[n] = (short)Math.Round(amplitude * Math.Sin(2 * Math.PI * 440 * n / Abtastrate));
        }

        return block;
    }

    // Regel 1: Stille (alle Werte 0) ergibt 0, beim Effektivwert und beim Spitzenwert.
    [Fact]
    public void Stille_ergibt_Pegel_0()
    {
        var stille = new short[1600];

        Assert.Equal(0f, AudioLevelMeter.Rms(stille));
        Assert.Equal(0f, AudioLevelMeter.Peak(stille));
    }

    // Regel 1: Vollausschlag (32767 oder minus 32768) ergibt 1 beim Spitzenwert.
    // Ein einzelner Wert mit Vollausschlag reicht, der Rest des Blocks darf leise sein.
    [Theory]
    [InlineData((short)32767)]
    [InlineData((short)-32768)]
    public void Vollausschlag_ergibt_Spitzenwert_1(short spitze)
    {
        var block = new short[1600];
        block[800] = spitze;

        var pegel = AudioLevelMeter.Peak(block);

        Assert.InRange(pegel, 0.999f, 1f);
    }

    // Regel 1: Ein Block, der nur aus Vollausschlag besteht, ergibt auch beim Spitzenwert 1, egal welches Vorzeichen.
    [Fact]
    public void Block_aus_lauter_Vollausschlag_ergibt_Spitzenwert_1()
    {
        Assert.InRange(AudioLevelMeter.Peak(Konstant(32767)), 0.999f, 1f);
        Assert.InRange(AudioLevelMeter.Peak(Konstant(-32768)), 0.999f, 1f);
    }

    // Regel 1: Ein Sinus mit Amplitude 32767 hat einen Effektivwert von minus 3 dB, also einen Pegel von etwa 0,95.
    [Fact]
    public void Sinus_mit_Vollausschlag_ergibt_Effektivwert_etwa_0_95()
    {
        var sinus = Sinus(32767);

        var pegel = AudioLevelMeter.Rms(sinus);

        Assert.InRange(pegel, 0.95f - 0.02f, 0.95f + 0.02f);
    }

    // Regel 1: Ein Block mit Amplitude 328 liegt bei etwa minus 40 dB und ergibt beim Spitzenwert etwa 0,33.
    [Fact]
    public void Amplitude_328_ergibt_Spitzenwert_etwa_0_33()
    {
        var block = Konstant(328);

        var pegel = AudioLevelMeter.Peak(block);

        Assert.InRange(pegel, 0.33f - 0.02f, 0.33f + 0.02f);
    }

    // Regel 1: Ein konstanter Block mit Amplitude 328 hat denselben Effektivwert wie Spitzenwert, also auch etwa 0,33.
    [Fact]
    public void Konstante_Amplitude_328_ergibt_auch_Effektivwert_etwa_0_33()
    {
        var block = Konstant(328);

        var pegel = AudioLevelMeter.Rms(block);

        Assert.InRange(pegel, 0.33f - 0.02f, 0.33f + 0.02f);
    }

    // Regel 1: Alles leiser als minus 60 dB ergibt 0. Minus 60 dB entspricht etwa Amplitude 33,
    // Amplitude 16 liegt bei etwa minus 66 dB und muss deshalb 0 ergeben, beim Effektivwert und beim Spitzenwert.
    [Theory]
    [InlineData((short)16)]
    [InlineData((short)-16)]
    [InlineData((short)1)]
    public void Leiser_als_minus_60_dB_ergibt_0(short amplitude)
    {
        var block = Konstant(amplitude);

        Assert.Equal(0f, AudioLevelMeter.Rms(block));
        Assert.Equal(0f, AudioLevelMeter.Peak(block));
    }

    // Regel 1: Ein leerer Block ergibt 0, beim Effektivwert und beim Spitzenwert.
    [Fact]
    public void Leerer_Block_ergibt_0()
    {
        Assert.Equal(0f, AudioLevelMeter.Rms(ReadOnlySpan<short>.Empty));
        Assert.Equal(0f, AudioLevelMeter.Peak(ReadOnlySpan<short>.Empty));
        Assert.Equal(0f, AudioLevelMeter.Rms(Array.Empty<short>()));
        Assert.Equal(0f, AudioLevelMeter.Peak(Array.Empty<short>()));
    }

    // Regel 1: Werte liegen immer zwischen 0 und 1, auch bei beliebigen Zufallsblöcken über den ganzen Wertebereich.
    [Fact]
    public void Zufallsbloecke_liegen_immer_zwischen_0_und_1()
    {
        var zufall = new Random(20261009);

        for (var lauf = 0; lauf < 200; lauf++)
        {
            var laenge = zufall.Next(1, 4000);
            var block = new short[laenge];
            for (var i = 0; i < laenge; i++)
            {
                block[i] = (short)zufall.Next(short.MinValue, short.MaxValue + 1);
            }

            var rms = AudioLevelMeter.Rms(block);
            var peak = AudioLevelMeter.Peak(block);

            Assert.InRange(rms, 0f, 1f);
            Assert.InRange(peak, 0f, 1f);
            Assert.False(float.IsNaN(rms));
            Assert.False(float.IsNaN(peak));
        }
    }

    // Regel 1: Ein lauterer Block ergibt einen höheren oder gleichen Pegel als ein leiserer, die Skala ist monoton.
    [Fact]
    public void Lauterer_Block_ergibt_keinen_kleineren_Pegel()
    {
        var leise = Konstant(328);
        var mittel = Konstant(3277);
        var laut = Konstant(32767);

        Assert.True(AudioLevelMeter.Peak(leise) <= AudioLevelMeter.Peak(mittel));
        Assert.True(AudioLevelMeter.Peak(mittel) <= AudioLevelMeter.Peak(laut));
        Assert.True(AudioLevelMeter.Rms(leise) <= AudioLevelMeter.Rms(mittel));
        Assert.True(AudioLevelMeter.Rms(mittel) <= AudioLevelMeter.Rms(laut));
    }

    // Vertrag: FloorDb ist minus 60 dB.
    [Fact]
    public void FloorDb_ist_minus_60()
    {
        Assert.Equal(-60f, AudioLevelMeter.FloorDb);
    }
}
