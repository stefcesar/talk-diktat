namespace Talk.Core.Audio;

/// <summary>
/// Rechnet aus einem Block von Abtastwerten einen Pegel zwischen 0 und 1 für die Anzeige.
/// Reine Rechnung, ohne Zustand, deshalb automatisch testbar.
/// </summary>
public static class AudioLevelMeter
{
    /// <summary>Leisester Pegel, der noch angezeigt wird, in Dezibel unter Vollausschlag. Alles darunter ist 0.</summary>
    public const float FloorDb = -60f;

    /// <summary>
    /// Effektivwert (RMS) des Blocks in Dezibel unter Vollausschlag, als Pegel von 0 (leiser als <see cref="FloorDb"/>) bis 1 (Vollausschlag).
    /// Ein leerer Block ergibt 0.
    /// </summary>
    public static float Rms(ReadOnlySpan<short> samples)
    {
        if (samples.IsEmpty) return 0f;

        double sum = 0;
        foreach (short s in samples)
        {
            double v = s / 32768.0;
            sum += v * v;
        }

        double rms = Math.Sqrt(sum / samples.Length);
        return ToLevel(rms);
    }

    /// <summary>Spitzenwert des Blocks als Pegel von 0 bis 1, nach derselben Dezibel Skala wie <see cref="Rms"/>.</summary>
    public static float Peak(ReadOnlySpan<short> samples)
    {
        if (samples.IsEmpty) return 0f;

        int max = 0;
        foreach (short s in samples)
        {
            int a = Math.Abs((int)s);
            if (a > max) max = a;
        }

        // Vollausschlag in beide Richtungen (32767 und minus 32768) zählt als 1 (Regel 1).
        if (max >= short.MaxValue) return 1f;

        return ToLevel(max / 32768.0);
    }

    // Lineare Amplitude (0 bis 1) in Dezibel, dann auf die Skala 0 bis 1 zwischen FloorDb und 0 dB abbilden.
    private static float ToLevel(double amplitude)
    {
        if (amplitude <= 0) return 0f;

        double db = 20.0 * Math.Log10(amplitude);
        if (db <= FloorDb) return 0f;
        if (db >= 0) return 1f;

        return (float)((db - FloorDb) / -FloorDb);
    }
}
