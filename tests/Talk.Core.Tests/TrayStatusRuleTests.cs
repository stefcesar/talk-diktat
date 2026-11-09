using Xunit;

namespace Talk.Core.Tests;

/// <summary>
/// Prüft, welches Tray Symbol zu Zustand und Fehlermerker gehört.
/// </summary>
public sealed class TrayStatusRuleTests
{
    [Theory]
    [InlineData(DictationState.Idle, false, TrayStatus.Idle)]
    [InlineData(DictationState.Idle, true, TrayStatus.Error)]
    [InlineData(DictationState.Recording, false, TrayStatus.Recording)]
    [InlineData(DictationState.Recording, true, TrayStatus.Recording)]
    [InlineData(DictationState.Processing, false, TrayStatus.Processing)]
    [InlineData(DictationState.Processing, true, TrayStatus.Processing)]
    public void Select_zeigt_den_Fehler_nur_in_Ruhe(DictationState state, bool hasError, TrayStatus expected)
    {
        Assert.Equal(expected, TrayStatusRule.Select(state, hasError));
    }

    [Theory]
    [InlineData(TrayStatus.Idle, "ruhe")]
    [InlineData(TrayStatus.Recording, "aufnahme")]
    [InlineData(TrayStatus.Processing, "verarbeitung")]
    [InlineData(TrayStatus.Error, "fehler")]
    public void ToWord_liefert_das_feste_Wort(TrayStatus status, string expected)
    {
        Assert.Equal(expected, status.ToWord());
    }

    [Fact]
    public void ToWord_ist_fuer_jeden_Wert_verschieden()
    {
        TrayStatus[] all = Enum.GetValues<TrayStatus>();
        Assert.Equal(all.Length, all.Select(s => s.ToWord()).Distinct().Count());
    }
}
