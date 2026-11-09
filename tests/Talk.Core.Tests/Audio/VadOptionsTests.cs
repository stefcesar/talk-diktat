using Talk.Core.Audio;
using Xunit;

namespace Talk.Core.Tests.Audio;

/// <summary>
/// Prüft die Einstellungen der Stilleerkennung ausschließlich gegen Regel 3 der reinen Logik in PLAYBOOK.md, Abschnitt 14.
/// Die Tests kennen die Umsetzung nicht. Die Regel nennt nur "wirft", ohne Ausnahmetyp; erwartet wird die Familie
/// ArgumentException, so wie es der Vertrag von Schritt 2 (ArgumentOutOfRangeException) vorlebt.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class VadOptionsTests
{
    // Regel 3: Standardwerte sind 0,5, 250, 300, 200.
    [Fact]
    public void Standardwerte_sind_0_5_250_300_200()
    {
        var optionen = new VadOptions();

        Assert.Equal(0.5f, optionen.Threshold);
        Assert.Equal(250, optionen.MinSpeechMs);
        Assert.Equal(300, optionen.MinSilenceMs);
        Assert.Equal(200, optionen.PaddingMs);
    }

    // Regel 3: Die Standardwerte sind gültig, Validate wirft nicht.
    [Fact]
    public void Standardwerte_bestehen_Validate()
    {
        var optionen = new VadOptions();

        optionen.Validate();
    }

    // Regel 3: Validate wirft bei Schwelle kleiner gleich 0 oder größer gleich 1.
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.1f)]
    [InlineData(-1f)]
    [InlineData(1.5f)]
    [InlineData(100f)]
    public void Validate_wirft_bei_Schwelle_ausserhalb_von_0_bis_1(float schwelle)
    {
        var optionen = new VadOptions(Threshold: schwelle);

        Assert.ThrowsAny<ArgumentException>(() => optionen.Validate());
    }

    // Regel 3: Schwellen echt zwischen 0 und 1 sind gültig, auch nahe an den Grenzen.
    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.5f)]
    [InlineData(0.99f)]
    public void Validate_laesst_Schwellen_echt_zwischen_0_und_1_durch(float schwelle)
    {
        var optionen = new VadOptions(Threshold: schwelle);

        optionen.Validate();
    }

    // Regel 3: Validate wirft bei negativer Mindestsprachdauer.
    [Theory]
    [InlineData(-1)]
    [InlineData(-250)]
    public void Validate_wirft_bei_negativer_MinSpeechMs(int millisekunden)
    {
        var optionen = new VadOptions(MinSpeechMs: millisekunden);

        Assert.ThrowsAny<ArgumentException>(() => optionen.Validate());
    }

    // Regel 3: Validate wirft bei negativer Mindeststille.
    [Theory]
    [InlineData(-1)]
    [InlineData(-300)]
    public void Validate_wirft_bei_negativer_MinSilenceMs(int millisekunden)
    {
        var optionen = new VadOptions(MinSilenceMs: millisekunden);

        Assert.ThrowsAny<ArgumentException>(() => optionen.Validate());
    }

    // Regel 3: Validate wirft bei negativem Rand.
    [Theory]
    [InlineData(-1)]
    [InlineData(-200)]
    public void Validate_wirft_bei_negativer_PaddingMs(int millisekunden)
    {
        var optionen = new VadOptions(PaddingMs: millisekunden);

        Assert.ThrowsAny<ArgumentException>(() => optionen.Validate());
    }

    // Regel 3: Null Millisekunden sind nicht negativ und deshalb gültig.
    [Fact]
    public void Validate_laesst_null_Millisekunden_durch()
    {
        var optionen = new VadOptions(MinSpeechMs: 0, MinSilenceMs: 0, PaddingMs: 0);

        optionen.Validate();
    }

    // Regel 3: Validate wirft auch dann, wenn mehrere Werte zugleich ungültig sind.
    [Fact]
    public void Validate_wirft_bei_mehreren_ungueltigen_Werten()
    {
        var optionen = new VadOptions(Threshold: 0f, MinSpeechMs: -1, MinSilenceMs: -1, PaddingMs: -1);

        Assert.ThrowsAny<ArgumentException>(() => optionen.Validate());
    }
}
