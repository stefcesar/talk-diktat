using Talk.Core.Audio;
using Xunit;

namespace Talk.Core.Tests;

/// <summary>
/// Prüft Dauerberechnung und Eingabeprüfung von <see cref="AudioClip"/>.
/// </summary>
public sealed class AudioClipTests
{
    [Fact]
    public void Dauer_16000_Abtastwerte_bei_16000_Hz_ergibt_eine_Sekunde()
    {
        var clip = new AudioClip(new short[16000], 16000);

        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
    }

    [Fact]
    public void Dauer_8000_Abtastwerte_bei_16000_Hz_ergibt_eine_halbe_Sekunde()
    {
        var clip = new AudioClip(new short[8000], 16000);

        Assert.Equal(TimeSpan.FromSeconds(0.5), clip.Duration);
    }

    [Fact]
    public void Konstruktor_mit_Abtastrate_0_wirft_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioClip(new short[10], 0));
    }

    [Fact]
    public void Konstruktor_mit_Samples_null_wirft_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AudioClip(null!, 16000));
    }

    [Fact]
    public void Standardabtastrate_ist_16000_Hz()
    {
        var clip = new AudioClip(new short[16000]);

        Assert.Equal(16000, clip.SampleRate);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
    }
}
