using System.Runtime.InteropServices;

using NAudio.CoreAudioApi;
using NAudio.Wave;

using Talk.Core.Audio;

namespace Talk.App.Audio;

/// <summary>
/// Nimmt das Mikrofon über WASAPI im geteilten Modus auf, direkt als 16 kHz, 16 Bit, mono (Windows rechnet selbst um).
/// Alle Abtastwerte bleiben im Arbeitsspeicher in einem <see cref="AudioClipBuilder"/> (Obergrenze fünf Minuten). Es wird nie eine Datei geschrieben.
/// </summary>
/// <remarks>
/// <para>
/// Ablauf: <see cref="StartAsync"/> wählt das Gerät (Kennung oder Standardgerät für Kommunikation, sonst Konsole), baut den
/// <see cref="WasapiRecorder"/> und startet ihn. <see cref="StopAsync"/> hält an, wartet auf das Ende der Aufnahme (höchstens zwei Sekunden),
/// liefert den Clip und leert den Sammler. <see cref="CancelAsync"/> verwirft alles und überschreibt den Speicher mit Nullen.
/// </para>
/// <para>
/// <see cref="LevelChanged"/> kommt höchstens alle 50 ms und wird auf dem Aufnahme Thread ausgelöst; Abnehmer wechseln selbst auf den
/// UI Thread. <see cref="LastRms"/> und <see cref="LastPeak"/> halten den Pegel des letzten Blocks für Protokoll und Anzeige.
/// </para>
/// <para>
/// Fehler kommen als <see cref="AudioRecorderException"/>: Gerät belegt (<see cref="RecordingError.DeviceInUse"/>), Gerät weg
/// (<see cref="RecordingError.DeviceLost"/>), Format nicht möglich (<see cref="RecordingError.UnsupportedFormat"/>), kein Gerät
/// (<see cref="RecordingError.NoDevice"/>), sonst <see cref="RecordingError.Unknown"/>; die Originalausnahme hängt als innere Ausnahme an.
/// Bricht die Aufnahme von selbst ab (zum Beispiel Gerät abgezogen), merkt sich der Rekorder den Grund und <see cref="StopAsync"/> wirft ihn.
/// </para>
/// <para>
/// Der Rekorder wird absichtlich ohne <see cref="SynchronizationContext"/> gebaut: NAudio würde sonst das Ende der Aufnahme über den
/// Dispatcher melden, und ein wartender UI Thread käme nie an die Reihe. Start, Stopp und Abbruch laufen deshalb auf dem Thread Pool.
/// </para>
/// </remarks>
internal sealed class WasapiAudioRecorder : IAudioRecorder, IDisposable
{
    private const int SampleRate = 16000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const int BufferMilliseconds = 50;
    private const int LevelIntervalMs = 50;
    private const int StartingPollMs = 5;

    private static readonly TimeSpan MaxClipDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartingTimeout = TimeSpan.FromSeconds(2);

    // Nullen für Pakete, die Windows als Stille kennzeichnet: deren Inhalt darf nicht gelesen werden.
    private static readonly short[] Silence = new short[SampleRate / 10];

    private readonly AudioClipBuilder _builder = new(SampleRate, MaxClipDuration);
    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    private volatile Session? _session;
    private volatile bool _isRecording;
    private volatile bool _disposed;
    private volatile float _lastRms;
    private volatile float _lastPeak;
    private bool _lastClipTruncated;

    // Nur auf dem Aufnahme Thread benutzt.
    private long _lastLevelTicks;

    /// <inheritdoc />
    public event EventHandler<float>? LevelChanged;

    /// <inheritdoc />
    public bool IsRecording => _isRecording;

    /// <summary>Effektivwert (0 bis 1) des letzten Blocks, für Protokoll und Anzeige. 0 ohne Aufnahme.</summary>
    public float LastRms => _lastRms;

    /// <summary>Spitzenwert (0 bis 1) des letzten Blocks, für Protokoll und Anzeige. 0 ohne Aufnahme.</summary>
    public float LastPeak => _lastPeak;

    /// <summary>True, wenn der zuletzt mit <see cref="StopAsync"/> gelieferte Clip an der Obergrenze abgeschnitten wurde.</summary>
    public bool LastClipTruncated => Volatile.Read(ref _lastClipTruncated);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Wenn schon eine Aufnahme läuft.</exception>
    /// <exception cref="AudioRecorderException">Wenn kein Gerät da ist, das Gerät belegt ist oder das Format nicht geht.</exception>
    public async Task StartAsync(string? deviceId, CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isRecording) throw new InvalidOperationException("Es läuft bereits eine Aufnahme.");

            // Auf dem Thread Pool gibt es keinen SynchronizationContext, siehe Klassenkommentar.
            await Task.Run(() => StartCore(deviceId), ct).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Wenn keine Aufnahme läuft.</exception>
    /// <exception cref="AudioRecorderException">Wenn die Aufnahme zwischendurch abgebrochen ist, zum Beispiel weil das Gerät weg ist.</exception>
    public async Task<AudioClip> StopAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Session? session = _session;
            if (!_isRecording || session is null) throw new InvalidOperationException("Es läuft keine Aufnahme.");

            // Aufräumen läuft immer bis zum Ende, auch wenn das Abbruchsignal kommt; sonst bliebe das Gerät offen.
            return await Task.Run(() => StopCoreAsync(session, keepClip: true), CancellationToken.None).ConfigureAwait(false)
                ?? throw new AudioRecorderException(RecordingError.Unknown, "Die Aufnahme hat keinen Clip geliefert.");
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed) return;

            Session? session = _session;
            if (!_isRecording || session is null)
            {
                // Ohne Aufnahme folgenlos; der Sammler ist dann ohnehin leer.
                lock (_gate) _builder.Clear();
                return;
            }

            try
            {
                await Task.Run(() => StopCoreAsync(session, keepClip: false), CancellationToken.None).ConfigureAwait(false);
            }
            catch (AudioRecorderException)
            {
                // Ein Abbruch soll nicht melden, dass das Gerät weg war: alles ist ohnehin verworfen.
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>Hält eine laufende Aufnahme an, verwirft alles und gibt Rekorder und Gerät frei. Wirft nie.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Session? session = _session;
        _session = null;
        _isRecording = false;

        if (session is not null)
        {
            WasapiRecorder recorder = session.Recorder;

            // Zuerst die Handler abhängen, damit nach dem Nullen kein Paket mehr in den Sammler läuft.
            recorder.DataAvailable -= OnDataAvailable;
            recorder.RecordingStopped -= session.OnRecordingStopped;

            try
            {
                recorder.StopRecording();
            }
            catch
            {
                // Beim Beenden nicht mehr wichtig.
            }

            // Begrenzt auf das Ende des Aufnahme Threads warten, damit das letzte Paket nicht nach dem Nullen eintrifft.
            long startedWaiting = Environment.TickCount64;
            while (recorder.CaptureState != CaptureState.Stopped
                && Environment.TickCount64 - startedWaiting < StopTimeout.TotalMilliseconds)
            {
                Thread.Sleep(StartingPollMs);
            }

            ReleaseSession(session);
        }

        lock (_gate) _builder.Clear();
        _lastRms = 0f;
        _lastPeak = 0f;

        // Die Semaphore wird absichtlich nicht freigegeben: Ein noch laufendes StopAsync oder CancelAsync gibt sie in seinem
        // finally zurück, und ein Dispose der Semaphore würde dort eine ObjectDisposedException auslösen. Sie hält keine
        // Betriebssystem Ressourcen, der Speicherbereiniger räumt sie auf.
    }

    // Läuft auf dem Thread Pool: Gerät wählen, Rekorder bauen, Format prüfen, starten.
    private void StartCore(string? deviceId)
    {
        MMDevice device = ResolveDevice(deviceId);
        WasapiRecorder recorder;

        try
        {
            recorder = BuildRecorder(device);
        }
        catch (Exception ex)
        {
            device.Dispose();
            throw Map(ex);
        }

        if (!IsExpectedFormat(recorder.WaveFormat))
        {
            recorder.Dispose();
            device.Dispose();
            throw new AudioRecorderException(
                RecordingError.UnsupportedFormat,
                "Das Gerät liefert nicht 16 kHz, 16 Bit, mono.");
        }

        lock (_gate) _builder.Clear();
        Volatile.Write(ref _lastClipTruncated, false);
        _lastRms = 0f;
        _lastPeak = 0f;
        _lastLevelTicks = 0;

        Session session = new(recorder, device);
        recorder.DataAvailable += OnDataAvailable;
        recorder.RecordingStopped += session.OnRecordingStopped;

        try
        {
            // Der Audio Client wird hier synchron eingerichtet; Gerät belegt und Format Fehler kommen an dieser Stelle.
            recorder.StartRecording();
        }
        catch (Exception ex)
        {
            recorder.DataAvailable -= OnDataAvailable;
            recorder.RecordingStopped -= session.OnRecordingStopped;
            ReleaseSession(session);
            throw Map(ex);
        }

        _session = session;
        _isRecording = true;
    }

    // Läuft auf dem Thread Pool: anhalten, auf das Ende warten, Clip bauen oder verwerfen, alles freigeben.
    private async Task<AudioClip?> StopCoreAsync(Session session, bool keepClip)
    {
        WasapiRecorder recorder = session.Recorder;

        // Ein Stopp während des Anlaufens würde vom Aufnahme Thread überschrieben; deshalb kurz warten, bis er läuft.
        long startedWaiting = Environment.TickCount64;
        while (recorder.CaptureState == CaptureState.Starting
            && Environment.TickCount64 - startedWaiting < StartingTimeout.TotalMilliseconds)
        {
            await Task.Delay(StartingPollMs).ConfigureAwait(false);
        }

        try
        {
            recorder.StopRecording();
        }
        catch
        {
            // Ein Gerät, das schon weg ist, meldet das über RecordingStopped; der Zustand wird unten geprüft.
        }

        // Auf das Ende warten: RecordingStopped oder Zustand Stopped, höchstens zwei Sekunden.
        Task finished = session.Stopped.Task;
        using (CancellationTokenSource timeout = new(StopTimeout))
        {
            while (!finished.IsCompleted && recorder.CaptureState != CaptureState.Stopped && !timeout.IsCancellationRequested)
            {
                await Task.WhenAny(finished, Task.Delay(StartingPollMs, timeout.Token)).ConfigureAwait(false);
            }
        }

        Exception? failure = session.Failure;
        AudioClip? clip = null;

        lock (_gate)
        {
            if (keepClip && failure is null)
            {
                clip = _builder.Build();
                Volatile.Write(ref _lastClipTruncated, _builder.Truncated);
            }

            _builder.Clear();
        }

        recorder.DataAvailable -= OnDataAvailable;
        recorder.RecordingStopped -= session.OnRecordingStopped;

        _session = null;
        _isRecording = false;
        _lastRms = 0f;
        _lastPeak = 0f;

        ReleaseSession(session);

        if (failure is not null && keepClip)
        {
            throw Map(failure, defaultError: RecordingError.DeviceLost);
        }

        return clip;
    }

    // Gerät nach Kennung, sonst Standardgerät für Kommunikation, sonst Konsole. Der Aufrufer gibt das Gerät frei.
    private static MMDevice ResolveDevice(string? deviceId)
    {
        MMDeviceEnumerator enumerator;
        try
        {
            enumerator = new MMDeviceEnumerator();
        }
        catch (Exception ex)
        {
            throw Map(ex);
        }

        using (enumerator)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                try
                {
                    return enumerator.GetDevice(deviceId);
                }
                catch (Exception ex)
                {
                    throw new AudioRecorderException(RecordingError.NoDevice, "Das gewählte Aufnahmegerät wurde nicht gefunden.", ex);
                }
            }

            MMDevice? device;
            try
            {
                device = MmDeviceSource.TryGetDefault(enumerator);
            }
            catch (Exception ex)
            {
                throw Map(ex);
            }

            return device ?? throw new AudioRecorderException(RecordingError.NoDevice, "Es ist kein Aufnahmegerät vorhanden.");
        }
    }

    // Baut den Rekorder ohne SynchronizationContext, damit RecordingStopped nie über den Dispatcher läuft.
    private static WasapiRecorder BuildRecorder(MMDevice device)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithFormat(new WaveFormat(SampleRate, BitsPerSample, Channels))
                .WithBufferLength(BufferMilliseconds)
                .Build();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static bool IsExpectedFormat(WaveFormat? format)
    {
        return format is not null
            && format.Encoding == WaveFormatEncoding.Pcm
            && format.SampleRate == SampleRate
            && format.BitsPerSample == BitsPerSample
            && format.Channels == Channels;
    }

    // Übersetzt eine Ausnahme von NAudio in den Grund des Vertrags. Die Originalausnahme hängt als innere Ausnahme an.
    private static AudioRecorderException Map(Exception ex, RecordingError defaultError = RecordingError.Unknown)
    {
        return ex switch
        {
            AudioRecorderException ready => ready,
            AudioDeviceInUseException => new AudioRecorderException(RecordingError.DeviceInUse, "Das Aufnahmegerät wird von einem anderen Programm benutzt.", ex),
            AudioDeviceDisconnectedException => new AudioRecorderException(RecordingError.DeviceLost, "Das Aufnahmegerät wurde entfernt.", ex),
            AudioFormatNotSupportedException => new AudioRecorderException(RecordingError.UnsupportedFormat, "Das Aufnahmegerät unterstützt 16 kHz, 16 Bit, mono nicht.", ex),
            _ => new AudioRecorderException(defaultError, "Die Aufnahme ist fehlgeschlagen.", ex),
        };
    }

    // Gibt Rekorder und Gerät frei. Der Rekorder beendet dabei seinen Aufnahme Thread; das Gerät gibt NAudio nicht selbst frei.
    private static void ReleaseSession(Session session)
    {
        try
        {
            session.Recorder.Dispose();
        }
        catch
        {
            // Ein Gerät, das schon weg ist, darf das Aufräumen nicht stören.
        }

        try
        {
            session.Device.Dispose();
        }
        catch
        {
            // Siehe oben.
        }
    }

    // Aufnahme Thread: Bytes als 16 Bit Werte lesen, sammeln, Pegel rechnen. Der Span gilt nur in diesem Aufruf. Kein Warten, kein Protokoll.
    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        if (buffer.Length < sizeof(short)) return;

        ReadOnlySpan<short> samples = MemoryMarshal.Cast<byte, short>(buffer);
        if ((flags & AudioClientBufferFlags.Silent) == AudioClientBufferFlags.Silent)
        {
            AppendSilence(samples.Length);
            _lastRms = 0f;
            _lastPeak = 0f;
        }
        else
        {
            lock (_gate) _builder.Append(samples);
            _lastRms = AudioLevelMeter.Rms(samples);
            _lastPeak = AudioLevelMeter.Peak(samples);
        }

        long now = Environment.TickCount64;
        if (now - _lastLevelTicks < LevelIntervalMs) return;
        _lastLevelTicks = now;

        EventHandler<float>? handler = LevelChanged;
        if (handler is null) return;

        try
        {
            handler(this, _lastRms);
        }
        catch
        {
            // Ein Fehler im Abnehmer darf die Aufnahme nicht anhalten.
        }
    }

    private void AppendSilence(int count)
    {
        lock (_gate)
        {
            while (count > 0)
            {
                int take = Math.Min(count, Silence.Length);
                _builder.Append(Silence.AsSpan(0, take));
                count -= take;
            }
        }
    }

    // Eine laufende Aufnahme: Rekorder, Gerät, Ende Signal und ein gemerkter Fehler. Je Start ein neues Objekt,
    // damit ein spätes Ereignis einer alten Aufnahme nie eine neue betrifft.
    private sealed class Session
    {
        private Exception? _failure;

        public Session(WasapiRecorder recorder, MMDevice device)
        {
            Recorder = recorder;
            Device = device;
            Stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public WasapiRecorder Recorder { get; }

        public MMDevice Device { get; }

        public TaskCompletionSource Stopped { get; }

        /// <summary>Die Ausnahme, mit der die Aufnahme von selbst endete, sonst null.</summary>
        public Exception? Failure => Volatile.Read(ref _failure);

        // Aufnahme Thread (oder bei einem Fehler im Audio Client der NAudio Thread): Fehler merken, Ende signalisieren.
        public void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception is not null) Volatile.Write(ref _failure, e.Exception);
            Stopped.TrySetResult();
        }
    }
}
