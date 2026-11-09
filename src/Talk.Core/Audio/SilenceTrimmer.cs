namespace Talk.Core.Audio;

/// <summary>
/// Reine Rechnung auf gefundenen Sprachabschnitten: Welche Abschnitte zählen, wie lang ist die Sprache insgesamt,
/// und welcher Ausschnitt des Clips bleibt übrig. Die Erkennung selbst (Silero) liefert nur die Abschnitte.
/// </summary>
public static class SilenceTrimmer
{
    /// <summary>
    /// Führt Abschnitte zusammen, deren Lücke kürzer als <see cref="VadOptions.MinSilenceMs"/> ist, und verwirft Abschnitte
    /// kürzer als <see cref="VadOptions.MinSpeechMs"/>. Erwartet Abschnitte in aufsteigender Reihenfolge ohne Überlappung.
    /// </summary>
    public static IReadOnlyList<SpeechSegment> Merge(IReadOnlyList<SpeechSegment> segments, VadOptions options, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(options);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

        int minGap = MsToSamples(options.MinSilenceMs, sampleRate);
        int minSpeech = MsToSamples(options.MinSpeechMs, sampleRate);

        List<SpeechSegment> merged = [];
        foreach (SpeechSegment s in segments)
        {
            if (s.EndSample <= s.StartSample) continue;

            if (merged.Count > 0 && s.StartSample - merged[^1].EndSample < minGap)
            {
                merged[^1] = new SpeechSegment(merged[^1].StartSample, Math.Max(merged[^1].EndSample, s.EndSample));
            }
            else
            {
                merged.Add(s);
            }
        }

        merged.RemoveAll(s => s.Length < minSpeech);
        return merged;
    }

    /// <summary>Gesamte Sprachdauer der Abschnitte.</summary>
    public static TimeSpan SpeechDuration(IReadOnlyList<SpeechSegment> segments, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

        long total = 0;
        foreach (SpeechSegment s in segments) total += Math.Max(0, s.Length);
        return TimeSpan.FromSeconds(total / (double)sampleRate);
    }

    /// <summary>
    /// Schneidet den Clip auf den Bereich vom ersten bis zum letzten Sprachabschnitt zu, mit <see cref="VadOptions.PaddingMs"/> Rand
    /// auf beiden Seiten. Stille zwischen den Abschnitten bleibt erhalten, damit der Satzrhythmus erhalten bleibt.
    /// Ohne Abschnitte kommt ein leerer Clip zurück.
    /// </summary>
    public static AudioClip Trim(AudioClip clip, IReadOnlyList<SpeechSegment> segments, VadOptions options)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(options);

        if (segments.Count == 0) return new AudioClip([], clip.SampleRate);

        int pad = MsToSamples(options.PaddingMs, clip.SampleRate);
        int start = Math.Max(0, segments[0].StartSample - pad);
        int end = Math.Min(clip.Samples.Length, segments[^1].EndSample + pad);
        if (end <= start) return new AudioClip([], clip.SampleRate);

        short[] cut = new short[end - start];
        Array.Copy(clip.Samples, start, cut, 0, cut.Length);
        return new AudioClip(cut, clip.SampleRate);
    }

    private static int MsToSamples(int ms, int sampleRate) => (int)Math.Round(ms / 1000.0 * sampleRate);
}
