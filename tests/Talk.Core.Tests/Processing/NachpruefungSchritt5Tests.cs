// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Insertion;
using Talk.Core.Processing;

using Xunit;

namespace Talk.Core.Tests.Processing;

/// <summary>
/// Regeln, die nach der Prüfung vom 11.10.2026 dazugekommen sind (Playbook Abschnitt 16, "Nach der Prüfung ergänzt").
/// </summary>
public sealed class NachpruefungSchritt5Tests
{
    [Theory]
    [InlineData("Ähm. Ich komme.", "Ich komme.")]
    [InlineData("Das stimmt. Hm. Weiter geht's.", "Das stimmt. Weiter geht's.")]
    [InlineData("Ja. Ähm? Nein.", "Ja. Nein.")]
    public void Zoegerlaut_als_eigener_Satz_nimmt_sein_Satzzeichen_mit(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    [Theory]
    [InlineData("Uh-huh, that works.", "en")]
    [InlineData("Mm-hmm, I agree.", "en")]
    [InlineData("Hm-hm, genau.", "de")]
    [InlineData("Die Tour hat 800 hm.", "de")]
    public void Zoegerlaut_mit_Bindestrich_oder_hinter_einer_Zahl_bleibt(string text, string sprache)
    {
        Assert.Equal(text, FillerRemover.Remove(text, sprache));
    }

    [Theory]
    [InlineData("Um 5 äh kommt er.", "Um 5 kommt er.")]
    [InlineData("Das kostet 20 ähm Euro.", "Das kostet 20 Euro.")]
    [InlineData("Ähm .NET ist gut.", ".NET ist gut.")]
    [InlineData("Gut. Äh .NET läuft.", "Gut. .NET läuft.")]
    public void Schutz_und_eigener_Satz_greifen_nicht_zu_weit(string text, string erwartet)
    {
        Assert.Equal(erwartet, FillerRemover.Remove(text, "de"));
    }

    [Theory]
    [InlineData("C#", "Ich schreibe C# Code.")]
    [InlineData("C#", "Vitamin C hilft.")]
    [InlineData(".NET", "das .NET SDK")]
    [InlineData("C++", "Ich mag C++ sehr.")]
    public void Schreibweise_mit_Sonderzeichen_am_Rand_wird_nicht_verdoppelt(string eintrag, string text)
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply(text, VocabularyEntry.ParseAll([eintrag]));

        Assert.Equal(text, ergebnis.Text);
        Assert.Equal(0, ergebnis.Replacements);
    }

    [Fact]
    public void Schreibweise_mit_Sonderzeichen_am_Rand_wird_trotzdem_berichtigt()
    {
        VocabularyCorrectionResult ergebnis = VocabularyCorrector.Apply("ich schreibe c# code.", VocabularyEntry.ParseAll(["C#"]));

        Assert.Equal("ich schreibe C# code.", ergebnis.Text);
        Assert.Equal(1, ergebnis.Replacements);
    }

    [Theory]
    [InlineData("Get-ChildItem", "Get-ChildItem ")]
    [InlineData("Rscript test.R", "rscript test.R ")]
    [InlineData("Echo hallo.", "echo hallo ")]
    public void ForTerminal_misst_das_erste_Wort_bis_zum_Leerraum(string text, string erwartet)
    {
        Assert.Equal(erwartet, InsertText.ForTerminal(text));
    }
}
