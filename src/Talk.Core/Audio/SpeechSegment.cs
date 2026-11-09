namespace Talk.Core.Audio;

/// <summary>
/// Ein Abschnitt mit Sprache innerhalb eines Clips, in Abtastwerten gezählt (Anfang einschließlich, Ende ausschließlich).
/// </summary>
/// <param name="StartSample">Erster Abtastwert des Abschnitts.</param>
/// <param name="EndSample">Erster Abtastwert nach dem Abschnitt.</param>
public readonly record struct SpeechSegment(int StartSample, int EndSample)
{
    /// <summary>Länge in Abtastwerten.</summary>
    public int Length => EndSample - StartSample;
}
