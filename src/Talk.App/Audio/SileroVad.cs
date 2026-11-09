// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;

using Talk.Core.Audio;

using Whisper.net;

namespace Talk.App.Audio;

/// <summary>
/// Stilleerkennung über das in Whisper.net eingebaute Silero VAD mit dem ggml Modell aus <see cref="Diagnostics.TalkPaths.VadModelPath"/>.
/// Liefert die Sprachabschnitte eines Clips in Abtastwerten, bereits durch <see cref="SilenceTrimmer.Merge"/> zusammengeführt und gefiltert.
/// </summary>
/// <remarks>
/// <para>
/// Der Konstruktor prüft die Optionen und das Vorhandensein der Modelldatei. Fehlt sie, kommt eine <see cref="FileNotFoundException"/>
/// mit deutscher Meldung. Die App lädt nie selbst nach; die Downloader Klassen von Whisper.net werden nicht benutzt.
/// </para>
/// <para>
/// Der Rand um die Sprachabschnitte (<see cref="VadOptions.PaddingMs"/>) wird nicht an Silero übergeben, sondern erst beim Zuschneiden in
/// <see cref="SilenceTrimmer.Trim"/> angelegt, damit er nicht doppelt wirkt. Schwelle, Mindestsprache und Mindeststille gehen an Silero
/// und werden danach in <see cref="SilenceTrimmer.Merge"/> noch einmal nach den Regeln des Vertrags angewendet.
/// </para>
/// <para>
/// Der native Prozessor ist nicht threadsicher; <see cref="DetectSegments"/> läuft deshalb unter einer Sperre. Die Abtastwerte werden
/// nur im Arbeitsspeicher umgerechnet, es wird nie eine Datei geschrieben.
/// </para>
/// </remarks>
internal sealed class SileroVad : IVoiceActivityDetector, IDisposable
{
    /// <summary>Die einzige Abtastrate, die Silero verarbeitet.</summary>
    public const int RequiredSampleRate = 16000;

    // 16 Bit Werte werden durch 32768 geteilt, damit minus 32768 genau minus 1 ergibt.
    private const float SampleScale = 32768f;

    private readonly WhisperVadFactory _factory;
    private readonly WhisperVadProcessor _processor;
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>
    /// Lädt das Modell und baut den Prozessor mit Schwelle, Mindestsprache und Mindeststille aus den Optionen, ohne eigenen Rand.
    /// </summary>
    /// <param name="options">Einstellungen der Stilleerkennung; werden mit <see cref="VadOptions.Validate"/> geprüft.</param>
    /// <param name="modelPath">Pfad der ggml Modelldatei.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bei unbrauchbaren Optionen.</exception>
    /// <exception cref="FileNotFoundException">Wenn die Modelldatei fehlt.</exception>
    public SileroVad(VadOptions options, string modelPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        options.Validate();

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"VAD Modell fehlt: {modelPath}", modelPath);
        }

        Options = options;
        _factory = WhisperVadFactory.FromPath(modelPath);
        try
        {
            _processor = _factory.CreateBuilder()
                .WithThreshold(options.Threshold)
                .WithMinSpeechDuration(TimeSpan.FromMilliseconds(options.MinSpeechMs))
                .WithMinSilenceDuration(TimeSpan.FromMilliseconds(options.MinSilenceMs))
                .WithSpeechPadding(TimeSpan.Zero)
                .Build();
        }
        catch
        {
            _factory.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public VadOptions Options { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Wenn der Clip nicht 16 kHz hat.</exception>
    /// <exception cref="ObjectDisposedException">Nach <see cref="Dispose"/>.</exception>
    public IReadOnlyList<SpeechSegment> DetectSegments(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.SampleRate != RequiredSampleRate)
        {
            throw new ArgumentException($"Die Stilleerkennung erwartet {RequiredSampleRate} Hz.", nameof(clip));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        int length = clip.Samples.Length;
        if (length == 0) return [];

        // 16 Bit Werte als Gleitkommazahlen zwischen minus 1 und 1, so erwartet es Silero.
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            samples[i] = clip.Samples[i] / SampleScale;
        }

        IReadOnlyList<VadSegmentData> raw;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            raw = _processor.DetectSpeech(samples);
        }

        List<SpeechSegment> segments = new(raw.Count);
        foreach (VadSegmentData data in raw)
        {
            int start = ToSample(data.Start, length);
            int end = ToSample(data.End, length);
            if (end > start) segments.Add(new SpeechSegment(start, end));
        }

        // Silero liefert die Abschnitte aufsteigend; die Sortierung sichert die Voraussetzung von Merge trotzdem ab.
        segments.Sort((a, b) => a.StartSample.CompareTo(b.StartSample));
        return SilenceTrimmer.Merge(segments, Options, RequiredSampleRate);
    }

    /// <summary>Gibt Prozessor und Modell frei. Mehrfacher Aufruf ist folgenlos, wirft nie.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _processor.Dispose();
            }
            catch
            {
                // Beim Freigeben nicht mehr wichtig.
            }

            try
            {
                _factory.Dispose();
            }
            catch
            {
                // Siehe oben.
            }
        }
    }

    // Zeit in Abtastwerte: Sekunden mal 16000, gerundet und auf den Clip begrenzt.
    private static int ToSample(TimeSpan time, int clipLength)
    {
        double value = Math.Round(time.TotalSeconds * RequiredSampleRate);
        if (value <= 0) return 0;
        if (value >= clipLength) return clipLength;
        return (int)value;
    }
}
