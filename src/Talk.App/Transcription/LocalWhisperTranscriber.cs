// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Diagnostics;
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;

using Talk.Core.Audio;
using Talk.Core.Transcription;

using Whisper.net;

namespace Talk.App.Transcription;

/// <summary>
/// Lokale Erkennung über Whisper.net mit einem ggml Modell aus dem Modellordner. Rechnet auf der Grafikkarte über Vulkan,
/// sonst auf der CPU (siehe <see cref="WhisperRuntime"/>). Nichts geht ins Netz, nichts wird auf die Festplatte geschrieben.
/// </summary>
/// <remarks>
/// <para>
/// Fehlt die Modelldatei, kommt <see cref="TranscriptionError.ModelMissing"/>. Die App lädt nie selbst nach; die Downloader Klassen
/// von Whisper.net werden nicht benutzt.
/// </para>
/// <para>
/// Das Modell wird beim ersten Gebrauch oder vorab über <see cref="EnsureLoadedAsync"/> geladen und bleibt bis <see cref="Dispose"/>
/// geladen. Braucht ein Lauf das andere Modell (Schweizer Modell für Schweizerdeutsch und Mundart, Playbook Abschnitt 26), wird
/// das geladene unter der Sperre freigegeben und das andere geladen; es liegt immer nur eines im Speicher. Lässt sich das Schweizer
/// Modell nicht laden, rechnet für den Rest der Sitzung das normale. Je Lauf entsteht ein eigener Prozessor, der vor dem Modell
/// wieder freigegeben wird.
/// </para>
/// <para>
/// Laden, Erkennen und Freigeben laufen streng nacheinander über eine Sperre, weil paralleler Betrieb im nativen Code abstürzt.
/// Die Arbeit läuft auf dem Thread Pool, der Aufrufer wird nie blockiert.
/// </para>
/// <para>
/// Diese Klasse protokolliert nichts. Die Ladezeit holt sich der Aufrufer genau einmal über <see cref="TryTakeLoadReport"/>.
/// Erkannter Text steht nur im Ergebnis und in keiner Meldung einer Ausnahme.
/// </para>
/// </remarks>
internal sealed class LocalWhisperTranscriber : ITranscriber, IDisposable
{
    /// <summary>Name dieses Erkenners in Ergebnissen und im Protokoll.</summary>
    public const string EngineName = "lokal";

    /// <summary>Die einzige Abtastrate, die Whisper verarbeitet.</summary>
    public const int RequiredSampleRate = 16000;

    // 16 Bit Werte werden durch 32768 geteilt, damit minus 32768 genau minus 1 ergibt.
    private const float SampleScale = 32768f;

    // Zahl der Threads einer Rechnung: die halbe Zahl der logischen Prozessoren, mindestens 4, höchstens 8.
    // Ohne Angabe nähme Whisper höchstens 4; auf der CPU Laufzeit bestimmt die Zahl die Rechenzeit.
    private const int MinThreads = 4;
    private const int MaxThreads = 8;

    // So lange wartet Dispose höchstens auf laufende Arbeit. Danach bleibt das Modell bis zum Prozessende geladen,
    // denn Freigeben während der Rechnung würde im nativen Code abstürzen.
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);

    private readonly string _modelPath;
    private readonly string? _swissModelPath;
    private readonly HallucinationFilterOptions _filter;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Nur unter der Sperre gelesen und geschrieben: das geladene Modell und der Pfad, aus dem es stammt.
    private WhisperFactory? _factory;
    private string? _loadedPath;

    // Ohne Sperre gelesen, darum volatile: die eingestellte Sprache fürs Vorladen, ob das Schweizer Modell geladen ist, ob es sich in
    // dieser Sitzung nicht laden ließ, und ob überhaupt ein Modell geladen ist.
    private volatile string? _preferredLanguage;
    private volatile bool _loadedSwiss;
    private volatile bool _swissBlocked;
    private volatile bool _isLoaded;
    private int _swissFailureReportPending;

    private long _loadMs;
    private int _loadReportPending;
    private int _disposed;

    /// <summary>
    /// Merkt sich Modellpfad und Filter. Geladen wird hier noch nichts.
    /// </summary>
    /// <param name="modelPath">Pfad der ggml Modelldatei, in der App <see cref="Diagnostics.TalkPaths.WhisperModelPath"/>.</param>
    /// <param name="filter">Schwellen des Filters gegen erfundene Sätze oder null für die Standardwerte.</param>
    /// <param name="swissModelPath">
    /// Pfad des Schweizer Modells (Playbook Abschnitt 26) oder null. Es gilt für Schweizerdeutsch und Mundart, solange die Datei da ist;
    /// fehlt sie, rechnet das normale Modell. Geladen ist immer nur eines; ein Wechsel der Sprache lädt beim nächsten Lauf um.
    /// </param>
    public LocalWhisperTranscriber(string modelPath, HallucinationFilterOptions? filter = null, string? swissModelPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        _modelPath = modelPath;
        _swissModelPath = string.IsNullOrWhiteSpace(swissModelPath) ? null : swissModelPath;
        _filter = filter ?? new HallucinationFilterOptions();
    }

    /// <summary>
    /// Die eingestellte Sprache, damit das Vorladen gleich das passende Modell lädt (bei Schweizerdeutsch und Mundart das Schweizer).
    /// </summary>
    public string? PreferredLanguage
    {
        get => _preferredLanguage;
        set => _preferredLanguage = value;
    }

    /// <summary>True, wenn das zuletzt geladene Modell das Schweizer ist. Nur fürs Protokoll.</summary>
    public bool LoadedSwiss => _loadedSwiss;

    /// <summary>True, wenn gerade ein Modell geladen ist.</summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>True, wenn die Sprache ein anderes Modell braucht als das geladene. Ohne geladenes Modell false.</summary>
    /// <param name="language">Sprachkürzel.</param>
    public bool NeedsOtherModel(string? language) => _isLoaded && _loadedSwiss != UsesSwiss(language);

    /// <summary>
    /// Liefert genau einmal true, nachdem sich das Schweizer Modell nicht laden ließ und das normale eingesprungen ist. Der Aufrufer
    /// schreibt damit eine Protokollzeile.
    /// </summary>
    public bool TryTakeSwissFailureReport() => Interlocked.Exchange(ref _swissFailureReportPending, 0) != 0;

    /// <inheritdoc />
    public string Name => EngineName;

    /// <summary>True, wenn die Modelldatei vorhanden ist. Geprüft wird bei jedem Zugriff frisch.</summary>
    public bool ModelFileExists => File.Exists(_modelPath);

    /// <summary>
    /// Lädt das Modell, falls es noch nicht geladen ist, und lässt es geladen. Gedacht für das Vorladen im Hintergrund,
    /// damit das erste Diktat nicht auf das Laden wartet. Läuft auf dem Thread Pool.
    /// </summary>
    /// <param name="ct">Abbruchsignal. Es wirkt nur, solange auf die Sperre gewartet wird; das Laden selbst lässt sich nicht abbrechen.</param>
    /// <exception cref="TranscriptionException">
    /// <see cref="TranscriptionError.ModelMissing"/>, wenn die Modelldatei fehlt; <see cref="TranscriptionError.EngineFailed"/>,
    /// wenn das Laden scheitert.
    /// </exception>
    /// <exception cref="OperationCanceledException">Bei Abbruch durch den Aufrufer.</exception>
    /// <exception cref="ObjectDisposedException">Nach <see cref="Dispose"/>.</exception>
    public Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        return Task.Run(
            async () =>
            {
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    LoadUnderGate(ModelPathFor(_preferredLanguage));
                }
                finally
                {
                    _gate.Release();
                }
            },
            ct);
    }

    /// <summary>
    /// Liefert genau einmal die gemessene Ladezeit des Modells, sobald es geladen ist. Der Aufrufer schreibt damit die Protokollzeile
    /// <c>stt | local | geladen</c>. Vor dem Laden und nach dem ersten erfolgreichen Abholen kommt false.
    /// </summary>
    /// <param name="loadMs">Dauer des Ladens in Millisekunden.</param>
    /// <returns>True beim ersten Aufruf nach dem Laden.</returns>
    public bool TryTakeLoadReport(out long loadMs)
    {
        if (Interlocked.Exchange(ref _loadReportPending, 0) == 0)
        {
            loadMs = 0;
            return false;
        }

        loadMs = Interlocked.Read(ref _loadMs);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ein leerer Clip ergibt ein leeres Ergebnis ohne Lauf und ohne Laden. Die Gesamtzeit im Ergebnis enthält das Warten auf die Sperre
    /// und, beim ersten Gebrauch ohne Vorladen, das Laden des Modells; die reine Rechenzeit steht einzeln in den Stufen.
    /// </remarks>
    /// <exception cref="ArgumentException">Wenn der Clip nicht 16 kHz hat.</exception>
    /// <exception cref="TranscriptionException">
    /// <see cref="TranscriptionError.ModelMissing"/>, wenn die Modelldatei fehlt; <see cref="TranscriptionError.EngineFailed"/>
    /// bei jedem anderen Fehler, mit der auslösenden Ausnahme als innerer Ausnahme.
    /// </exception>
    /// <exception cref="OperationCanceledException">Bei Abbruch durch den Aufrufer.</exception>
    /// <exception cref="ObjectDisposedException">Nach <see cref="Dispose"/>.</exception>
    public async Task<TranscriptionResult> TranscribeAsync(AudioClip clip, TranscriptionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);

        if (clip.SampleRate != RequiredSampleRate)
        {
            throw new ArgumentException($"Die lokale Erkennung erwartet {RequiredSampleRate} Hz.", nameof(clip));
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        // Ein leerer Clip wird nie gerechnet.
        if (clip.Samples.Length == 0)
        {
            return new TranscriptionResult(string.Empty, null, TimeSpan.Zero, EngineName, [], new TranscriptionTimings(0, 0, 0, 0));
        }

        Stopwatch total = Stopwatch.StartNew();

        // Laden und Rechnen blockieren ihren Thread, deshalb läuft alles Weitere auf dem Thread Pool.
        // Ein Abbruch vor dem Beginn geht unverändert an den Aufrufer.
        return await Task.Run(() => RunAsync(clip, request, total, ct), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Wartet auf laufende Arbeit (höchstens 5 Sekunden) und gibt das Modell frei. Mehrfacher Aufruf ist folgenlos, wirft nie.
    /// Läuft die Arbeit länger, bleibt das Modell bis zum Prozessende geladen, statt mitten in der Rechnung freigegeben zu werden.
    /// </summary>
    public void Dispose()
    {
        // Ab hier lädt niemand mehr: Wer die Sperre nach diesem Aufruf bekommt, sieht das Kennzeichen und hört auf.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        bool held = false;
        try
        {
            held = _gate.Wait(DisposeWait);
            if (!held) return;

            WhisperFactory? factory = _factory;
            _factory = null;
            _isLoaded = false;
            factory?.Dispose();
        }
        catch
        {
            // Beim Freigeben nicht mehr wichtig.
        }
        finally
        {
            if (held)
            {
                try
                {
                    _gate.Release();
                }
                catch
                {
                    // Siehe oben.
                }
            }
        }
    }

    // Ein Lauf unter der Sperre: Modell laden falls nötig, Prozessor bauen, rechnen, Abschnitte sammeln und filtern.
    private async Task<TranscriptionResult> RunAsync(AudioClip clip, TranscriptionRequest request, Stopwatch total, CancellationToken ct)
    {
        // Ein Abbruch während des Wartens geht unverändert an den Aufrufer.
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            WhisperFactory factory = LoadUnderGate(ModelPathFor(request.Language));
            ct.ThrowIfCancellationRequested();

            // 16 Bit Werte als Gleitkommazahlen zwischen minus 1 und 1, so erwartet es Whisper. Die Kopie lebt nur im Arbeitsspeicher.
            float[] samples = ToFloat(clip.Samples);
            List<TranscriptionSegment> segments = [];
            string? language = null;
            Stopwatch compute = Stopwatch.StartNew();

            try
            {
                // Der Prozessor wird am Ende des Blocks freigegeben, also immer vor dem Modell.
                WhisperProcessor processor = BuildProcessor(factory, request);
                await using (processor.ConfigureAwait(false))
                {
                    await foreach (SegmentData data in processor.ProcessAsync(samples, ct).ConfigureAwait(false))
                    {
                        language ??= data.Language;
                        segments.Add(new TranscriptionSegment(
                            data.Start.TotalSeconds,
                            data.End.TotalSeconds,
                            data.Text ?? string.Empty,
                            AvgLogProb: null,
                            NoSpeechProb: data.NoSpeechProbability,
                            CompressionRatio: null));
                    }
                }
            }
            finally
            {
                Array.Clear(samples);
            }

            int computeMs = ToMs(compute.Elapsed);
            HallucinationFilterResult filtered = HallucinationFilter.Apply(segments, _filter);
            TimeSpan elapsed = total.Elapsed;

            return new TranscriptionResult(
                filtered.Text,
                string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant(),
                elapsed,
                EngineName,
                filtered.Kept,
                new TranscriptionTimings(0, 0, computeMs, Math.Max(computeMs, ToMs(elapsed))),
                null,
                filtered.FilteredCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TranscriptionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Die Meldung nennt weder Pfad noch Text. Die Ursache hängt als innere Ausnahme an.
            throw new TranscriptionException(TranscriptionError.EngineFailed, "Die lokale Erkennung ist gescheitert.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Das Modell für die Sprache: bei Schweizerdeutsch und Mundart das Schweizer, wenn seine Datei da ist, sonst das normale.
    private string ModelPathFor(string? language) => UsesSwiss(language) ? _swissModelPath! : _modelPath;

    private bool UsesSwiss(string? language) =>
        _swissModelPath is not null && !_swissBlocked && LanguageCodes.IsSwiss(language) && File.Exists(_swissModelPath);

    // Lädt das Modell aus dem Pfad, falls nötig; ein anderes geladenes Modell wird vorher freigegeben, damit nur eines Speicher belegt.
    // Nur unter der Sperre aufrufen, also nie während einer Rechnung. Misst die Ladezeit und merkt vor, dass sie einmal gemeldet wird.
    private WhisperFactory LoadUnderGate(string path)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_factory is not null && string.Equals(_loadedPath, path, StringComparison.OrdinalIgnoreCase)) return _factory;

        if (_factory is not null)
        {
            WhisperFactory old = _factory;
            _factory = null;
            _loadedPath = null;
            _isLoaded = false;
            old.Dispose();
        }

        if (!File.Exists(path))
        {
            // Der Pfad steht bewusst nicht in der Meldung.
            throw new TranscriptionException(TranscriptionError.ModelMissing, "Das lokale Modell fehlt. Die App lädt es nie selbst nach.");
        }

        try
        {
            Stopwatch watch = Stopwatch.StartNew();

            // Mit UseGpu rechnet die Vulkan Bibliothek auf der Grafikkarte; die CPU Bibliothek übergeht die Angabe.
            WhisperFactory factory = WhisperFactory.FromPath(path, WhisperFactoryOptions.Default with { UseGpu = true });

            // Erst die Angaben, dann die Meldung freigeben: Interlocked macht sie für TryTakeLoadReport sichtbar.
            _factory = factory;
            _loadedPath = path;
            _loadedSwiss = !string.Equals(path, _modelPath, StringComparison.OrdinalIgnoreCase);
            _isLoaded = true;
            Interlocked.Exchange(ref _loadMs, watch.ElapsedMilliseconds);
            Interlocked.Exchange(ref _loadReportPending, 1);
            return factory;
        }
        catch (Exception) when (!string.Equals(path, _modelPath, StringComparison.OrdinalIgnoreCase))
        {
            // Das Schweizer Modell ist kaputt oder passt nicht in den Speicher: für diese Sitzung sperren, das normale lädt.
            _swissBlocked = true;
            Interlocked.Exchange(ref _swissFailureReportPending, 1);
            return LoadUnderGate(_modelPath);
        }
        catch (Exception ex)
        {
            throw new TranscriptionException(TranscriptionError.EngineFailed, "Das lokale Modell lässt sich nicht laden.", ex);
        }
    }

    // Je Lauf ein eigener Prozessor: Sprache fest oder automatisch, Hinweistext aus dem Wörterbuch, Temperatur 0,
    // ohne Kontext aus früheren Läufen, mit Wahrscheinlichkeiten, Threads nach der Zahl der logischen Prozessoren.
    private static WhisperProcessor BuildProcessor(WhisperFactory factory, TranscriptionRequest request)
    {
        WhisperProcessorBuilder builder = factory.CreateBuilder();

        builder = request.AutoDetect
            ? builder.WithLanguageDetection()
            : builder.WithLanguage(request.WhisperLanguage);

        string? prompt = VocabularyPrompt.Build(request.Vocabulary);
        if (prompt is not null) builder = builder.WithPrompt(prompt);

        return builder
            .WithTemperature(0f)
            .WithNoContext()
            .WithProbabilities()
            .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, MinThreads, MaxThreads))
            .Build();
    }

    private static float[] ToFloat(short[] source)
    {
        float[] samples = new float[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            samples[i] = source[i] / SampleScale;
        }

        return samples;
    }

    private static int ToMs(TimeSpan time) => (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(time.TotalMilliseconds)));
}
