namespace Talk.Core.Audio;

/// <summary>
/// Einstellungen der Stilleerkennung. Die Werte kommen später aus den Einstellungen der App.
/// </summary>
/// <param name="Threshold">Schwelle der Sprachwahrscheinlichkeit, ab der ein Abschnitt als Sprache gilt (0 bis 1). Standard 0,5.</param>
/// <param name="MinSpeechMs">Kürzeste Sprachdauer in Millisekunden, damit eine Aufnahme überhaupt als Sprache zählt. Standard 250 ms.</param>
/// <param name="MinSilenceMs">Kürzeste Stille in Millisekunden, die zwei Sprachabschnitte trennt. Standard 300 ms.</param>
/// <param name="PaddingMs">Rand in Millisekunden, der vor und nach jedem Sprachabschnitt stehen bleibt, damit nichts abgeschnitten klingt. Standard 200 ms.</param>
public sealed record VadOptions(
    float Threshold = 0.5f,
    int MinSpeechMs = 250,
    int MinSilenceMs = 300,
    int PaddingMs = 200)
{
    /// <summary>Wirft bei unbrauchbaren Werten.</summary>
    public void Validate()
    {
        if (Threshold <= 0f || Threshold >= 1f) throw new ArgumentOutOfRangeException(nameof(Threshold), "Die Schwelle muss zwischen 0 und 1 liegen.");
        if (MinSpeechMs < 0) throw new ArgumentOutOfRangeException(nameof(MinSpeechMs), "MinSpeechMs darf nicht negativ sein.");
        if (MinSilenceMs < 0) throw new ArgumentOutOfRangeException(nameof(MinSilenceMs), "MinSilenceMs darf nicht negativ sein.");
        if (PaddingMs < 0) throw new ArgumentOutOfRangeException(nameof(PaddingMs), "PaddingMs darf nicht negativ sein.");
    }
}
