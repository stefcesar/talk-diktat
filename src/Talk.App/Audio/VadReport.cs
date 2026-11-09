using System.Diagnostics;
using System.Globalization;

using Talk.Core.Audio;

namespace Talk.App.Audio;

/// <summary>
/// Ergebnis eines Laufs der Stilleerkennung über einen Clip, als Zahlen für Protokoll und Anzeige.
/// Die Protokollzeile <c>vad | result</c> entsteht nur hier, damit Testfenster und Befehlszeile dieselbe Form liefern.
/// </summary>
/// <param name="ClipMs">Dauer des Clips in Millisekunden.</param>
/// <param name="SegmentCount">Anzahl der gefundenen Sprachabschnitte nach dem Zusammenführen.</param>
/// <param name="SpeechMs">Gesamte Sprachdauer in Millisekunden.</param>
/// <param name="TrimmedMs">Dauer des zugeschnittenen Clips in Millisekunden, mit Rand.</param>
/// <param name="DurationMs">Dauer des Erkennungslaufs in Millisekunden.</param>
internal sealed record VadReport(long ClipMs, int SegmentCount, long SpeechMs, long TrimmedMs, long DurationMs)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>True, wenn mindestens ein Sprachabschnitt gefunden wurde.</summary>
    public bool HasSpeech => SegmentCount > 0;

    /// <summary>Das Detail der Protokollzeile <c>vad | result</c>: nur Zahlen und feste Wörter.</summary>
    public string LogDetail =>
        $"speech={(HasSpeech ? "ja" : "nein")} segments={SegmentCount} speech_ms={SpeechMs} trimmed_ms={TrimmedMs} duration_ms={DurationMs}";

    /// <summary>Die Zeile fürs Testfenster, zum Beispiel "Dauer 3,2 s, Sprache: ja, zugeschnitten 2,4 s".</summary>
    public string DisplayText => HasSpeech
        ? $"Dauer {FormatSeconds(ClipMs)}, Sprache: ja, zugeschnitten {FormatSeconds(TrimmedMs)}"
        : $"Dauer {FormatSeconds(ClipMs)}, Sprache: nein";

    /// <summary>
    /// Lässt die Erkennung laufen, misst die Zeit und rechnet Sprachdauer und zugeschnittene Dauer mit <see cref="SilenceTrimmer"/>.
    /// Der Clip bleibt unverändert, es wird nichts geschrieben.
    /// </summary>
    /// <param name="detector">Die Stilleerkennung.</param>
    /// <param name="clip">Der zu prüfende Clip.</param>
    public static VadReport Run(IVoiceActivityDetector detector, AudioClip clip)
    {
        VadReport report = RunAndTrim(detector, clip, out AudioClip trimmed);

        // Hier zählt nur die Dauer. Die Kopie des Ausschnitts wird sofort wieder genullt, der Clip des Aufrufers bleibt unberührt.
        if (!ReferenceEquals(trimmed.Samples, clip.Samples)) Array.Clear(trimmed.Samples);
        return report;
    }

    /// <summary>
    /// Wie <see cref="Run"/>, liefert aber zusätzlich den zugeschnittenen Clip für die Erkennung. Die Stilleerkennung läuft nur einmal.
    /// Der zugeschnittene Clip ist eine eigene Kopie im Arbeitsspeicher (ohne Sprache leer); wer ihn bekommt, nullt ihn nach Gebrauch.
    /// </summary>
    /// <param name="detector">Die Stilleerkennung.</param>
    /// <param name="clip">Der zu prüfende Clip. Er bleibt unverändert.</param>
    /// <param name="trimmed">Der Ausschnitt vom ersten bis zum letzten Sprachabschnitt mit Rand, siehe <see cref="SilenceTrimmer.Trim"/>.</param>
    public static VadReport RunAndTrim(IVoiceActivityDetector detector, AudioClip clip, out AudioClip trimmed)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(clip);

        Stopwatch watch = Stopwatch.StartNew();
        IReadOnlyList<SpeechSegment> segments = detector.DetectSegments(clip);
        watch.Stop();

        long speechMs = RoundMs(SilenceTrimmer.SpeechDuration(segments, clip.SampleRate));
        trimmed = SilenceTrimmer.Trim(clip, segments, detector.Options);

        return new VadReport(RoundMs(clip.Duration), segments.Count, speechMs, RoundMs(trimmed.Duration), watch.ElapsedMilliseconds);
    }

    /// <summary>Millisekunden als Sekunden mit einer Nachkommastelle und deutschem Komma, zum Beispiel "3,2 s".</summary>
    public static string FormatSeconds(long ms)
    {
        return (ms / 1000.0).ToString("0.0", German) + " s";
    }

    private static long RoundMs(TimeSpan time) => (long)Math.Round(time.TotalMilliseconds);
}
