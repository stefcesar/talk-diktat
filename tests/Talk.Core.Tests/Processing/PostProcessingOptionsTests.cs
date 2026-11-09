using Talk.Core.Processing;
using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Prüft die Einstellungen der Nachbearbeitung ausschließlich gegen PLAYBOOK.md, Abschnitt 16, "Vertrag":
/// PostProcessingOptions(RemoveFillers = true, Smoothing = Off, Vocabulary = null, Language = "de", ExtraFillers = null, FuzzyVocabulary = true).
/// </summary>
public sealed class PostProcessingOptionsTests
{
    // Vertrag: die Vorgaben ohne jede Angabe.
    [Fact]
    public void Vorgaben_ohne_Angabe()
    {
        PostProcessingOptions optionen = new();

        Assert.True(optionen.RemoveFillers);
        Assert.Equal(SmoothingLevel.Off, optionen.Smoothing);
        Assert.Null(optionen.Vocabulary);
        Assert.Equal("de", optionen.Language);
        Assert.Null(optionen.ExtraFillers);
        Assert.True(optionen.FuzzyVocabulary);
    }

    // Vertrag: Reihenfolge der Angaben wie im Playbook.
    [Fact]
    public void Angaben_in_der_Reihenfolge_des_Vertrags()
    {
        string[] woerterbuch = ["beKEY = Bikey"];
        string[] zusatz = ["sozusagen"];

        PostProcessingOptions optionen = new(false, SmoothingLevel.Off, woerterbuch, "it", zusatz, false);

        Assert.False(optionen.RemoveFillers);
        Assert.Equal(SmoothingLevel.Off, optionen.Smoothing);
        Assert.Same(woerterbuch, optionen.Vocabulary);
        Assert.Equal("it", optionen.Language);
        Assert.Same(zusatz, optionen.ExtraFillers);
        Assert.False(optionen.FuzzyVocabulary);
    }

    // Vertrag: Jede Angabe lässt sich einzeln setzen, die übrigen behalten ihre Vorgabe.
    [Fact]
    public void Einzelne_Angabe_laesst_die_uebrigen_bei_der_Vorgabe()
    {
        PostProcessingOptions optionen = new(Language: "en");

        Assert.Equal("en", optionen.Language);
        Assert.True(optionen.RemoveFillers);
        Assert.Equal(SmoothingLevel.Off, optionen.Smoothing);
        Assert.Null(optionen.Vocabulary);
        Assert.Null(optionen.ExtraFillers);
        Assert.True(optionen.FuzzyVocabulary);
    }
}
