// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;
using System.Windows.Threading;

using Talk.App.Audio;
using Talk.App.Diagnostics;
using Talk.App.Feedback;
using Talk.App.Input;
using Talk.App.Insertion;
using Talk.App.Processing;
using Talk.App.Transcription;
using Talk.Core;
using Talk.Core.Audio;
using Talk.Core.History;
using Talk.Core.Hotkey;
using Talk.Core.Insertion;
using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Processing.Swiss;
using Talk.Core.Settings;
using Talk.Core.Transcription;

namespace Talk.App.Dictation;

/// <summary>
/// Der Ablauf eines Diktats ohne Oberfläche: Hotkey, Aufnahme, Stilleerkennung, Erkennung, Nachbearbeitung, Verlauf und Einfügen.
/// Bis Schritt 7 stand das alles im Testfenster. Tray, Overlay und Fenster hören nur auf <see cref="Changed"/> und
/// <see cref="LevelChanged"/> und lesen die Eigenschaften; sie steuern nichts außer über die öffentlichen Methoden.
/// </summary>
/// <remarks>
/// <para>
/// Alles läuft auf dem UI Thread, auf dem die Klasse angelegt wurde; Ereignisse von Dienst und Rekorder wechseln per Dispatcher dorthin.
/// </para>
/// <para>
/// Audio bleibt im Arbeitsspeicher. Der ganze Clip wird genullt, sobald der Ausschnitt steht. Der Ausschnitt wird genullt bei Erfolg,
/// bei der nächsten Aufnahme, bei Abbruch und beim Beenden. Scheitert die Erkennung, bleibt er bis dahin erhalten und
/// <see cref="CanRetry"/> wird wahr, damit kein Diktat verloren geht.
/// </para>
/// <para>
/// Im Protokoll stehen nur Zahlen und feste Wörter. Gerätename, erkannter Text und Wörter des Nutzers erscheinen nur in den
/// Eigenschaften für die Oberfläche, der Schlüssel nirgends.
/// </para>
/// </remarks>
internal sealed class DictationCoordinator
{
    private const string VadMissingText = "VAD Modell fehlt";

    // Liegt das Modell da, lädt die Stilleerkennung aber trotzdem nicht (zum Beispiel ohne Microsoft Visual C++ Laufzeit), nennt die
    // Anzeige keine Ursache, die nicht sicher ist (Playbook Entscheid 66 und 70).
    private const string VadUnavailableText = "Stilleerkennung nicht verfügbar";
    private const string NoSpeechText = "Keine Sprache erkannt, nichts gesendet";
    private const string NoSttYetText = "Noch keine Erkennung";
    private const string LocalMissingText = "Lokales Modell fehlt";
    private const string LocalFailedText = "Lokales Modell lässt sich nicht laden";
    private const string NothingInsertedText = "Noch nichts eingefügt";

    // Ohne Stilleerkennung geht nichts an Groq. Die Anzeige nennt das Fehlen des Modells nur, wenn die Datei wirklich fehlt.
    private static string VadNothingSentText =>
        $"{(File.Exists(TalkPaths.VadModelPath) ? VadUnavailableText : VadMissingText)}, nichts gesendet";
    private const string DefaultLanguage = "de";

    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _refreshTimer;
    private readonly MmDeviceSource _deviceSource = new();
    private readonly WasapiAudioRecorder _recorder = new();
    private readonly Task<SileroVad?> _vadTask;
    private readonly TextInserter _inserter = new();

    // Nachbearbeitung ohne Netz und die Wörter des Nutzers aus dem Benutzerprofil.
    private readonly PostProcessor _postProcessor = new();
    private readonly UserWordLists _words = new();

    // Das Fenster, das beim Start der Aufnahme vorn war. Null, wenn keines vorn war.
    private TargetWindow? _target;

    private bool _disposed;

    // Merkt sich, dass das letzte Diktat gescheitert ist. Das Tray Symbol zeigt den Fehler in Ruhe, bis die nächste Aufnahme beginnt.
    private bool _trayError;

    // Zählt die Aufnahmen. Ein Ablauf, der zu einer älteren Aufnahme gehört, fasst Zustand und Anzeige nicht mehr an.
    private int _generation;

    // Der zugeschnittene Clip, solange die Erkennung läuft und nach einem Fehler bis zum nächsten Versuch. Nur im Arbeitsspeicher.
    private AudioClip? _pendingClip;
    private long _pendingAudioMs;

    // True, wenn der behaltene Clip keinen Sprachbefund hat (das VAD Modell fehlt). Er darf dann nie an Groq gehen, auch nicht bei "Erneut versuchen".
    private bool _pendingLocalOnly;

    // Gesetzt, solange eine Erkennung läuft.
    private CancellationTokenSource? _sttCts;

    // Messwerte des laufenden Diktats für die Zeile dictation | done: Uhr ab dem Stopp, Dauer der Aufnahme, der Stilleerkennung
    // und der Nachbearbeitung. Null bei "Erneut versuchen", dort gibt es keinen Stopp.
    private System.Diagnostics.Stopwatch? _sinceStop;
    private long _recordedMs;
    private long _vadMs;
    private long _postMs;
    private long _smoothMs;

    public DictationCoordinator(KeyboardHookService service, SpeechService speech, IHistoryStore history)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
        Speech = speech ?? throw new ArgumentNullException(nameof(speech));
        History = history ?? throw new ArgumentNullException(nameof(history));
        _dispatcher = Dispatcher.CurrentDispatcher;

        // Der Sekundentakt hält Anzeigen frisch, die sich ohne Ereignis ändern (Hook gesetzt oder nicht, Feststelltaste).
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => RaiseChanged();

        Service.ActionReceived += OnActionReceived;
        Service.NoticeReceived += OnNoticeReceived;
        Service.HookReinstalled += OnHookReinstalled;
        _recorder.LevelChanged += OnLevelChanged;

        // Das Modell lädt im Hintergrund. Fehlt es, bleibt die Stilleerkennung aus. Ohne Sprachbefund geht nichts an Groq:
        // Der ganze Clip geht dann nur an den lokalen Erkenner.
        _vadTask = Task.Run(CreateVad);
        _ = _vadTask.ContinueWith(
            finished => _dispatcher.InvokeAsync(() =>
            {
                if (!_disposed && finished.Status == TaskStatus.RanToCompletion && finished.Result is null)
                {
                    string reason = File.Exists(TalkPaths.VadModelPath) ? VadUnavailableText : VadMissingText;
                    ResultLine = $"{reason}, Aufnahmen gehen ohne Zuschnitt nur an die lokale Erkennung, nichts wird gesendet";
                    RaiseChanged();
                }
            }),
            TaskScheduler.Default);

        // Die Bedienhilfen vorladen, damit die Passwortprüfung schon beim ersten Diktat innerhalb ihres Zeitlimits antwortet.
        ForegroundWindowProbe.WarmUp();

        // Auch das lokale Modell lädt im Hintergrund, damit das erste lokale Diktat nicht auf das Laden wartet.
        _ = PreloadLocalModelAsync();

        _refreshTimer.Start();
    }

    /// <summary>Meldet, dass sich Zustand oder Anzeige geändert haben. Kommt auf dem UI Thread.</summary>
    public event Action? Changed;

    /// <summary>Meldet einen neuen Pegel (0 bis 100). Kommt auf dem UI Thread, höchstens alle 50 ms.</summary>
    public event Action<double>? LevelChanged;

    public KeyboardHookService Service { get; }

    public SpeechService Speech { get; }

    /// <summary>Der Verlauf. Jeder Text landet hier, bevor er eingefügt wird.</summary>
    public IHistoryStore History { get; }

    /// <summary>Das gewählte Mikrofon oder null für das Standardgerät. Gilt ab der nächsten Aufnahme.</summary>
    public string? DeviceId { get; set; }

    /// <summary>Sprachkürzel des Diktats: de, it, en, de-ch (Schweizerdeutsch) oder gsw (Mundart).</summary>
    public string Language { get; set; } = DefaultLanguage;

    /// <summary>Dialekt für Mundart: zh, be, bs oder mix (Playbook Abschnitt 26). Gilt nur bei Sprache gsw.</summary>
    public string Dialect { get; set; } = DictationSettings.DefaultDialect;

    /// <summary>Zusätzliche Begriffe fürs Wörterbuch, mit Komma getrennt (Feld im Diagnosefenster).</summary>
    public string VocabularyText { get; set; } = string.Empty;

    public string ResultLine { get; private set; } = "Noch keine Aufnahme";

    public string SttLine { get; private set; } = NoSttYetText;

    public string InsertLine { get; private set; } = NothingInsertedText;

    /// <summary>Der Text nach der Nachbearbeitung. Nur für die Oberfläche, nie im Protokoll.</summary>
    public string Transcript { get; private set; } = string.Empty;

    /// <summary>Der rohe Text, wenn er vom nachbearbeiteten abweicht, sonst leer.</summary>
    public string RawTranscript { get; private set; } = string.Empty;

    /// <summary>Der Text vor der Glättung, wenn die Glättung ihn geändert hat, sonst leer.</summary>
    public string UnsmoothedTranscript { get; private set; } = string.Empty;

    /// <summary>Wie die Glättung beim letzten Diktat ausging, als kurzer Satz für die Anzeige. Leer, wenn sie aus war.</summary>
    public string SmoothLine { get; private set; } = string.Empty;

    /// <summary>Glättung an oder aus. Mit an geht der Text über Groq ins Internet, außer bei "nur lokal" und im Terminal.</summary>
    public bool SmoothingEnabled { get; set; }

    /// <summary>Die Stufe der Glättung, wenn sie an ist.</summary>
    public SmoothingLevel Smoothing { get; set; } = SmoothingLevel.Smooth;

    /// <summary>Eigene Stufen je Zielprogramm (Playbook Abschnitt 23). Sie gelten nur, wenn die Glättung an ist.</summary>
    public AppSmoothingMap SmoothingPerApp { get; set; } = AppSmoothingMap.Empty;

    /// <summary>
    /// Die Programme, in die seit dem Start diktiert wurde, das neueste zuerst, höchstens <see cref="RecentAppsMax"/>, ohne Talk und
    /// ohne Terminals. Nur im Arbeitsspeicher, nie im Protokoll; das Einstellungsfenster bietet sie zur Wahl einer Stufe an.
    /// </summary>
    public IReadOnlyList<string> RecentApps => _recentApps;

    private const int RecentAppsMax = 10;
    private readonly List<string> _recentApps = [];

    // Merkt das Zielprogramm in der Liste der zuletzt benutzten (Playbook Abschnitt 23, Regel 10).
    private void RememberApp(TargetWindow? target)
    {
        if (target is null || TerminalDetector.IsTerminal(target.ProcessName, target.ClassName)) return;

        string? name = AppSmoothingMap.NormalizeName(target.ProcessName);
        if (name is null || string.Equals(name, "Talk", StringComparison.OrdinalIgnoreCase)) return;

        _recentApps.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        _recentApps.Insert(0, name);
        if (_recentApps.Count > RecentAppsMax) _recentApps.RemoveRange(RecentAppsMax, _recentApps.Count - RecentAppsMax);
    }

    public string LocalModelLine { get; private set; } = "Lokales Modell lädt";

    public bool LocalModelWarning { get; private set; }

    public string ReinstallLine { get; private set; } = "Letzte Hook Neusetzung: noch keine";

    public bool CanRetry { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public int CancelCount { get; private set; }

    /// <summary>
    /// Was Tray und Overlay zeigen. Läuft noch eine Erkennung, obwohl die Steuerung schon ruht (Erneut versuchen, Hook Neusetzung
    /// oder neue Einstellungen mitten in der Verarbeitung), gilt trotzdem Verarbeitung. Ein fehlender Hook zählt als Fehler.
    /// </summary>
    public TrayStatus Status
    {
        get
        {
            DictationState state = Service.Controller.State;
            if (state == DictationState.Idle && _sttCts is not null) state = DictationState.Processing;
            return TrayStatusRule.Select(state, _trayError || !Service.IsInstalled);
        }
    }

    /// <summary>
    /// Die Aufnahmegeräte, Standardgerät zuerst. Protokoll: nur Anzahl und Markierungen, nie Namen oder Kennungen.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> ListDevices()
    {
        IReadOnlyList<AudioDeviceInfo> devices = _deviceSource.ListCaptureDevices();
        AppLog.Write(
            "audio",
            "devices",
            $"count={devices.Count} default={ToWord(devices.Any(d => d.IsDefault))} bluetooth={devices.Count(d => d.LikelyBluetoothHandsFree)}");
        return devices;
    }

    /// <summary>
    /// Übergibt dem Dienst eine neue Steuerung mit diesen Werten. Die neue Steuerung steht auf Ruhe, deshalb wird eine gerade laufende
    /// Aufnahme verworfen.
    /// </summary>
    public void ApplyOptions(int doubleTapWindowMs, bool pushToTalk)
    {
        HotkeyOptions options = Service.Controller.Options with
        {
            DoubleTapWindowMs = doubleTapWindowMs,
            PushToTalkEnabled = pushToTalk,
        };

        Service.ReplaceController(new HotkeyController(options));
        if (_recorder.IsRecording) _ = CancelRecordingAsync("Abgebrochen wegen neuer Einstellungen, nichts ausgewertet");
        AppLog.Write("hotkey", "options", $"fenster={doubleTapWindowMs} ptt={(options.PushToTalkEnabled ? "an" : "aus")}");
        RaiseChanged();
    }

    /// <summary>
    /// Versucht die Erkennung mit dem behaltenen Clip noch einmal, mit den Einstellungen, die jetzt gelten.
    /// Ein Clip ohne Sprachbefund bleibt dabei beim lokalen Erkenner.
    /// </summary>
    public async Task RetryAsync()
    {
        AudioClip? clip = _pendingClip;
        if (clip is null || _sttCts is not null) return;

        // Ohne Stopp gibt es keine Gesamtzeit; die Zeile dictation | done trägt dann erneut=ja.
        _sinceStop = null;
        _vadMs = 0;
        _postMs = 0;
        _smoothMs = 0;

        try
        {
            await TranscribeAndShowAsync(clip, _pendingAudioMs, _pendingLocalOnly);
        }
        catch
        {
            // TranscribeAndShowAsync fängt alles selbst ab; hier darf trotzdem nie eine Ausnahme entkommen.
        }
    }

    /// <summary>
    /// Beendet den Ablauf: Hook lösen, laufende Erkennung beenden, Clip nullen, Rekorder und Stilleerkennung freigeben.
    /// Die Aufgabe endet erst, wenn die Zwischenablage zurückgelegt ist; sonst bliebe nach einem Einfügen kurz vor dem Beenden
    /// der diktierte Text darin. Den SpeechService gibt die App frei.
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _refreshTimer.Stop();
        Service.ActionReceived -= OnActionReceived;
        Service.NoticeReceived -= OnNoticeReceived;
        Service.HookReinstalled -= OnHookReinstalled;
        _recorder.LevelChanged -= OnLevelChanged;

        Service.Dispose();
        AppLog.Write("hook", "removed");

        CancelSpeech();

        // Eine laufende Aufnahme wird dabei verworfen. Lädt das Modell noch, wird es nach dem Laden freigegeben.
        _recorder.Dispose();
        if (_vadTask.IsCompleted)
        {
            if (_vadTask.Status == TaskStatus.RanToCompletion) _vadTask.Result?.Dispose();
        }
        else
        {
            _ = _vadTask.ContinueWith(
                finished =>
                {
                    if (finished.Status == TaskStatus.RanToCompletion) finished.Result?.Dispose();
                },
                TaskScheduler.Default);
        }

        try
        {
            await _inserter.PendingRestore;
        }
        catch
        {
            // Das Zurücklegen fängt seine Fehler selbst; hier darf trotzdem nichts das Beenden aufhalten.
        }
    }

    private void RaiseChanged()
    {
        if (!_disposed) Changed?.Invoke();
    }

    // Läuft auf dem Thread Pool. Fehlt das Modell: Protokoll vad | error | grund=modell_fehlt und null; die App lädt nie selbst nach.
    private static SileroVad? CreateVad()
    {
        try
        {
            return new SileroVad(new VadOptions(), TalkPaths.VadModelPath);
        }
        catch (FileNotFoundException)
        {
            AppLog.Write("vad", "error", "grund=modell_fehlt");
            return null;
        }
        catch (Exception)
        {
            AppLog.Write("vad", "error", "grund=unbekannt");
            return null;
        }
    }

    // Lädt das lokale Modell vorab und merkt sich seinen Zustand für die Anzeige. Es beginnt erst, wenn das VAD Modell fertig ist,
    // damit die native Bibliothek nicht von zwei Threads zugleich zum ersten Mal geladen wird.
    private async Task PreloadLocalModelAsync()
    {
        try
        {
            await _vadTask;
            if (_disposed) return;

            LocalModelState state = await Speech.PreloadLocalAsync();
            if (_disposed) return;

            (LocalModelLine, LocalModelWarning) = state switch
            {
                LocalModelState.Loaded => ($"Lokales Modell geladen ({RuntimeGerman(WhisperRuntime.LoadedName)})", false),
                LocalModelState.Missing => (LocalMissingText, true),
                LocalModelState.Failed => (LocalFailedText, true),
                _ => ("Kein lokaler Erkenner, nur Groq verfügbar", true),
            };
            RaiseChanged();
        }
        catch
        {
            // Das Vorladen ist nur eine Abkürzung. Scheitert es unerwartet, lädt das Modell beim ersten lokalen Diktat.
        }
    }

    // Die geladene Laufzeit als deutsches Wort für die Anzeige; im Protokoll steht das feste Wort aus WhisperRuntime.
    private static string RuntimeGerman(string runtime) => runtime switch
    {
        WhisperRuntime.Vulkan => "Grafikkarte über Vulkan",
        WhisperRuntime.Cpu => "CPU",
        _ => "Laufzeit unbekannt",
    };

    // Kommt aus dem Verbraucher Task, deshalb auf den UI Thread wechseln.
    private void OnActionReceived(HotkeyAction action)
    {
        _ = _dispatcher.InvokeAsync(() => HandleAction(action));
    }

    // Diagnose ohne Aktion: Umschalt plus Caps Lock wurde durchgelassen.
    private void OnNoticeReceived(HookNotice notice)
    {
        if (notice != HookNotice.ShiftCapsPassthrough) return;

        _ = _dispatcher.InvokeAsync(() =>
        {
            AppLog.Write("hotkey", "shift-caps-passthrough");
            RaiseChanged();
        });
    }

    // Nach einer Neusetzung steht die Steuerung auf Ruhe. Eine laufende Aufnahme wird verworfen, damit Zustand und Rekorder zusammenpassen.
    // Ein nach einem Fehler behaltener Clip bleibt dabei erhalten, sonst ginge er alle fünf Minuten durch den Wächter verloren.
    private void OnHookReinstalled(HookReinstallReason reason, bool installed)
    {
        _ = _dispatcher.InvokeAsync(() =>
        {
            string word = reason.ToWord();
            AppLog.Write("hook", "reinstalled", installed ? $"grund={word}" : $"grund={word} fehler={Service.LastInstallError}");
            string suffix = installed ? string.Empty : ", fehlgeschlagen";
            ReinstallLine = $"Letzte Hook Neusetzung: {DateTime.Now:HH:mm:ss} ({word}{suffix})";
            if (_recorder.IsRecording) _ = CancelRecordingAsync("Abgebrochen wegen Hook Neusetzung, nichts ausgewertet");
            RaiseChanged();
        });
    }

    // Pegel kommt vom Aufnahme Thread, höchstens alle 50 ms.
    private void OnLevelChanged(object? sender, float level)
    {
        _ = _dispatcher.InvokeAsync(
            () => SetLevel(Math.Clamp(level, 0f, 1f) * 100),
            DispatcherPriority.Background);
    }

    private void SetLevel(double level)
    {
        if (!_disposed) LevelChanged?.Invoke(level);
    }

    private void HandleAction(HotkeyAction action)
    {
        if (_disposed) return;

        switch (action)
        {
            case HotkeyAction.StartRecording:
                Beeper.Start();
                StartCount++;
                AppLog.Write("hotkey", "start");

                // Das Ziel für den Text merken, bevor irgendetwas anderes den Fokus bekommt.
                _target = ForegroundWindowProbe.Capture();
                RememberApp(_target);
                InsertLine = NothingInsertedText;

                // Eine neue Aufnahme beendet einen laufenden Versuch und verwirft einen behaltenen Clip.
                _generation++;
                _trayError = false;
                CancelSpeech();
                Transcript = string.Empty;
                RawTranscript = string.Empty;
                UnsmoothedTranscript = string.Empty;
                SmoothLine = string.Empty;
                SttLine = NoSttYetText;

                _ = StartRecordingAsync(DeviceId);

                // Verbindung zu Groq vorab aufbauen, ohne darauf zu warten. Passiert nur mit Schlüssel und nicht bei "nur lokal".
                _ = Speech.WarmUpAsync();
                break;

            case HotkeyAction.StopRecording:
                Beeper.Stop();
                StopCount++;
                AppLog.Write("hotkey", "stop");
                _sinceStop = System.Diagnostics.Stopwatch.StartNew();
                _recordedMs = 0;
                _vadMs = 0;
                _postMs = 0;
                _smoothMs = 0;
                _ = StopAndAnalyzeAsync();
                break;

            case HotkeyAction.Cancel:
                Beeper.Cancel();
                CancelCount++;
                AppLog.Write("hotkey", "cancel");
                CancelSpeech();
                _ = CancelRecordingAsync("Abgebrochen, nichts ausgewertet");
                break;

            default:
                return;
        }

        AppLog.Write("caps", "state", CapsLockState.LogDetail);
        RaiseChanged();
    }

    // Startet den Rekorder mit dem gewählten Gerät. Scheitert das, nennt die Anzeige den Grund und der Zustand geht zurück auf Ruhe.
    private async Task StartRecordingAsync(string? deviceId)
    {
        try
        {
            ResultLine = "Aufnahme läuft";
            try
            {
                await _recorder.StartAsync(deviceId, CancellationToken.None);
            }
            catch (Exception) when (deviceId is not null && !_disposed)
            {
                // Das gewählte Mikrofon ist nicht angeschlossen oder lässt sich nicht öffnen. Talk läuft ohne Fenster, deshalb
                // nimmt es für diese Aufnahme das Standardgerät, statt jedes Diktat scheitern zu lassen.
                AppLog.Write("audio", "fallback", "grund=mikrofon_nicht_verfuegbar");
                ResultLine = "Aufnahme läuft über das Standardgerät, das gewählte Mikrofon ist nicht verfügbar";
                RaiseChanged();
                await _recorder.StartAsync(null, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            if (_disposed) return;

            RecordingError error = (ex as AudioRecorderException)?.Error ?? RecordingError.Unknown;
            AppLog.Write("audio", "error", $"grund={error}");
            ResultLine = $"Aufnahme nicht möglich: {ToGerman(error)}";
            SetLevel(0);
            _trayError = true;
            Service.Controller.MarkProcessingFinished();
            Service.Controller.Reset();
            RaiseChanged();
        }
    }

    // Holt den Clip, lässt die Stilleerkennung im Hintergrund laufen, schickt den Ausschnitt mit Sprache an die Erkennung,
    // zeigt das Ergebnis und meldet erst danach die Verarbeitung fertig.
    private async Task StopAndAnalyzeAsync()
    {
        int generation = _generation;

        // Der ganze Clip aus dem Rekorder. Er wird genullt, sobald er nicht mehr gebraucht wird.
        AudioClip? clip = null;

        try
        {
            clip = await _recorder.StopAsync(CancellationToken.None);
            if (IsStale(generation)) return;

            SetLevel(0);
            long clipMs = RoundMs(clip.Duration);
            _recordedMs = clipMs;
            AppLog.Write("audio", "clip", $"ms={clipMs} samples={clip.Samples.Length} truncated={ToWord(_recorder.LastClipTruncated)}");

            SileroVad? vad = await _vadTask;
            if (IsStale(generation)) return;

            AudioClip toSend;
            bool forceLocal = false;
            if (vad is null)
            {
                // Ohne VAD Modell gibt es keinen Sprachbefund und keinen Zuschnitt. Deshalb geht nichts an Groq: Der ganze Clip geht nur
                // an den lokalen Erkenner, der nie etwas sendet. So bleibt das Diktat erhalten, ohne dass ungeprüftes Audio hinausgeht.
                ResultLine = $"Dauer {VadReport.FormatSeconds(clipMs)}, {VadNothingSentText}, Erkennung nur lokal";
                if (clip.Samples.Length == 0)
                {
                    SttLine = "Leere Aufnahme, nichts gesendet";
                    return;
                }

                // Derselbe Speicher gehört ab hier der Erkennung und wird dort genullt.
                toSend = clip;
                clip = null;
                forceLocal = true;
            }
            else
            {
                ResultLine = "Stilleerkennung läuft";
                RaiseChanged();
                AudioClip source = clip;
                System.Diagnostics.Stopwatch vadWatch = System.Diagnostics.Stopwatch.StartNew();
                (VadReport report, AudioClip trimmed) = await Task.Run(() =>
                {
                    VadReport result = VadReport.RunAndTrim(vad, source, out AudioClip cut);
                    return (result, cut);
                });

                // Der Ausschnitt ist eine eigene Kopie, der ganze Clip wird nicht mehr gebraucht.
                Array.Clear(clip.Samples);
                clip = null;

                if (IsStale(generation))
                {
                    Array.Clear(trimmed.Samples);
                    return;
                }

                _vadMs = vadWatch.ElapsedMilliseconds;
                AppLog.Write("vad", "result", report.LogDetail);
                ResultLine = report.DisplayText;

                if (!report.HasSpeech)
                {
                    // Ohne Sprache geht nichts an die Erkennung, weder an Groq noch lokal.
                    SttReport.WriteSkippedNoSpeech();
                    SttLine = NoSpeechText;
                    return;
                }

                toSend = trimmed;
            }

            await TranscribeAndShowAsync(toSend, RoundMs(toSend.Duration), forceLocal);
        }
        catch (AudioRecorderException ex)
        {
            if (_disposed) return;
            AppLog.Write("audio", "error", $"grund={ex.Error}");
            ResultLine = $"Aufnahme nicht möglich: {ToGerman(ex.Error)}";
            SetLevel(0);
            if (generation == _generation) _trayError = true;
            RaiseChanged();
        }
        catch (InvalidOperationException)
        {
            // Es lief keine Aufnahme, zum Beispiel weil der Start gescheitert war.
            if (_disposed) return;
            ResultLine = "Keine Aufnahme, nichts ausgewertet";
            SetLevel(0);
            RaiseChanged();
        }
        catch (Exception)
        {
            if (_disposed) return;
            AppLog.Write("vad", "error", "grund=unbekannt");
            ResultLine = "Auswertung fehlgeschlagen";
            SetLevel(0);
            if (generation == _generation) _trayError = true;
            RaiseChanged();
        }
        finally
        {
            if (clip is not null) Array.Clear(clip.Samples);

            if (!IsStale(generation))
            {
                Service.Controller.MarkProcessingFinished();
                RaiseChanged();
            }
        }
    }

    // Schickt den Clip an die Erkennung und zeigt Text, Erkenner und Zeiten. Bei Erfolg wird der Clip genullt. Scheitert die Erkennung,
    // bleibt er im Arbeitsspeicher und "Erneut versuchen" wird möglich. Sprache, Wörterbuch und "nur lokal" gelten so, wie sie jetzt eingestellt sind.
    // Mit forceLocal (Clip ohne Sprachbefund, das VAD Modell fehlt) läuft nur der lokale Erkenner, unabhängig vom Haken "nur lokal";
    // das merkt sich der behaltene Clip, damit auch "Erneut versuchen" nichts an Groq gibt.
    private async Task TranscribeAndShowAsync(AudioClip clip, long audioMs, bool forceLocal)
    {
        // Das Ziel dieser Aufnahme festhalten; eine neue Aufnahme überschreibt das Feld.
        TargetWindow? target = _target;

        CancellationTokenSource cts = new();
        _sttCts = cts;
        _pendingClip = clip;
        _pendingAudioMs = audioMs;
        _pendingLocalOnly = forceLocal;
        CanRetry = false;
        SttLine = "Erkennung läuft";
        RaiseChanged();

        try
        {
            string language = string.IsNullOrWhiteSpace(Language) ? DefaultLanguage : Language;

            // Der Hinweis an den Erkenner: zuerst die Begriffe aus dem Feld, dann die Schreibweisen aus woerterbuch.txt.
            // Das lokale Wörterbuch kommt nur in einen Lauf, der erzwungen lokal ist (siehe unten); angewendet wird es immer erst nach der Erkennung.
            // Auch aus dem Feld gehen nur die Schreibweisen hinaus, nie die Varianten rechts vom Gleichheitszeichen.
            _words.Refresh();
            IReadOnlyList<string> boxTerms = VocabularyPrompt.Parse(VocabularyText);

            // Läuft dieses Diktat sicher nur auf dem Rechner (Haken "nur lokal" oder kein Schlüssel hinterlegt),
            // darf auch das lokale Wörterbuch in den Hinweis, damit das Modell Namen gleich richtig hört. Der lokale Weg wird dann für
            // genau diesen Lauf erzwungen: So gehen die Namen auch dann nicht hinaus, wenn sich Haken oder Schlüssel inzwischen ändern.
            // Der Hinweis fasst nur rund 400 Zeichen, deshalb wechseln sich beide Listen ab; die Reihenfolge in der Datei ist die Rangfolge.
            // Ausnahme: Ein Clip ohne Sprachbefund (forceLocal, das VAD Modell fehlt) bekommt die lokale Liste nicht. Bei Stille gibt das
            // Modell gelegentlich Wörter aus dem Hinweis als Text aus, und dann stünde ein Name ungesprochen im Zielfenster.
            bool localHints = !forceLocal && (Speech.LocalOnly || !Speech.HasKey);
            bool localRun = forceLocal || localHints;
            IEnumerable<string> fileTerms = localHints ? Interleave(_words.LocalHintTerms, _words.HintTerms) : _words.HintTerms;
            // Bei Schweizerdeutsch und Mundart folgen wenige Schweizer Wörter, nach den eigenen, damit das Limit nichts Eigenes abschneidet.
            if (LanguageCodes.IsSwiss(language)) fileTerms = fileTerms.Concat(SwissWordLists.HintTerms);
            IReadOnlyList<string> vocabulary = VocabularyEntry.Terms(boxTerms).Concat(fileTerms).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Die Uhr läuft um den ganzen Aufruf, also auch über einen gescheiterten ersten Erkenner und den Wechsel auf den zweiten.
            System.Diagnostics.Stopwatch sttWatch = System.Diagnostics.Stopwatch.StartNew();
            TranscriptionResult result = await Speech.TranscribeAsync(clip, language, vocabulary, cts.Token, localRun);
            long sttMs = sttWatch.ElapsedMilliseconds;

            // Nach Beenden, Abbruch oder einer neuen Aufnahme ist der Clip schon genullt, das Ergebnis zählt nicht mehr.
            if (_disposed || cts.IsCancellationRequested) return;

            // Ins Protokoll gehen nur Zahlen und feste Wörter. Der Text steht nur in der Anzeige.
            SttReport.WriteResult(result, audioMs);

            // Soll gleich geglättet werden, geht der Text an Groq. Dann bleibt das lokale Wörterbuch (Menschen, Einrichtungen,
            // Gesundheit) bei der ersten Nachbearbeitung außen vor und greift erst nach der Glättung: So verlässt keine Schreibweise
            // aus dieser Liste den Rechner, die nicht ohnehin gesprochen und erkannt wurde.
            // Ein Lauf, der als lokal festgelegt ist (localRun), ein Text, den der Rechner erkannt hat (auch als Ersatz nach einem
            // Fehler von Groq), und ein Terminal als Ziel werden nie geglättet. Schalter und Stufe gelten, wie sie jetzt stehen.
            bool isTerminal = target is not null && TerminalDetector.IsTerminal(target.ProcessName, target.ClassName);
            // Eine eigene Stufe für das Zielprogramm geht der Standardstufe vor, auch "Aus" (Playbook Abschnitt 23, Regeln 5 und 11).
            // Mundart (Playbook Abschnitt 26): Whisper schreibt Hochdeutsch, erst das Sprachmodell schreibt in den Dialekt um, auch ohne
            // Schalter "Glätten" (SmoothingPlan). Wo nichts ins Netz darf (lokal, Terminal), bleibt Schweizer Hochdeutsch.
            SmoothingLevel? appLevel = SmoothingPerApp.Get(target?.ProcessName);
            SmoothingPlan plan = SmoothingPlan.For(SmoothingEnabled, Smoothing, appLevel, language, Dialect);
            bool dialectRewrite = plan.DialectRewrite;
            bool smoothingWanted = plan.Wanted;
            SmoothingLevel level = plan.Level;
            string smoothLanguage = plan.Language;
            SmoothingOutcome? blocked = SmoothingGate.Check(
                smoothingWanted,
                localRun,
                string.Equals(result.Engine, GroqTranscriber.EngineName, StringComparison.Ordinal),
                isTerminal,
                plan.OffForApp);
            bool willSmooth = blocked is null;

            // Nachbearbeitung auf dem Rechner. In Anzeige, Verlauf und Zielfenster geht nur der nachbearbeitete Text.
            string text = PostProcess(result, language, boxTerms, includeLocalVocabulary: !willSmooth, terminal: isTerminal);

            // Glättung per Sprachmodell (Playbook Abschnitt 21). Scheitert sie oder besteht die Antwort die Schutzregeln nicht,
            // bleibt der Text, wie er ist. Danach greift das Wörterbuch mit allen Listen, damit Schreibweisen sicher stimmen.
            UnsmoothedTranscript = string.Empty;
            SmoothLine = string.Empty;
            if (smoothingWanted && InsertText.IsInsertable(text))
            {
                SmoothingResult smooth;
                if (blocked is SmoothingOutcome reason)
                {
                    smooth = new SmoothingResult(text, reason, 0);
                }
                else
                {
                    SmoothLine = "Glättung läuft";
                    RaiseChanged();
                    try
                    {
                        smooth = await Speech.SmoothAsync(text, level, smoothLanguage, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        // Ein unerwarteter Fehler der Glättung ist kein Fehler der Erkennung: Der erkannte Text wird trotzdem eingefügt.
                        smooth = new SmoothingResult(text, SmoothingOutcome.ServiceError, 0);
                    }

                    if (_disposed || cts.IsCancellationRequested) return;
                }

                _smoothMs = smooth.Milliseconds;
                string before = text;
                System.Diagnostics.Stopwatch again = System.Diagnostics.Stopwatch.StartNew();
                if (smooth.Applied)
                {
                    // Umbrüche aus der Antwort werden zu Leerzeichen: Ein eingefügter Umbruch könnte in einem Terminal, das Talk
                    // nicht als solches erkennt, eine Zeile als Befehl ausführen.
                    text = ReapplyVocabulary(smooth.Text, language, boxTerms, dialectText: dialectRewrite);
                    string source = dialectRewrite ? "mundart" : appLevel is null ? "standard" : "programm";
                    AppLog.Write("smooth", "result", $"stufe={level.ToWord()} quelle={source} ms={smooth.Milliseconds} zeichen_ein={before.Length} zeichen_aus={text.Length}");
                    SmoothLine = dialectRewrite
                        ? $"In Mundart umgeschrieben über Groq, {smooth.Milliseconds} ms"
                        : $"Geglättet über Groq, {smooth.Milliseconds} ms";
                }
                else
                {
                    // Wurde das lokale Wörterbuch für die Glättung zurückgehalten, greift es jetzt.
                    if (willSmooth) text = ReapplyVocabulary(text, language, boxTerms, dialectText: false);

                    bool skipped = smooth.Outcome is SmoothingOutcome.Off or SmoothingOutcome.TooShort or SmoothingOutcome.TooLong
                        or SmoothingOutcome.NoKey or SmoothingOutcome.LocalOnly or SmoothingOutcome.Terminal or SmoothingOutcome.OffForApp;
                    string status = smooth.HttpStatus > 0 ? $" status={smooth.HttpStatus}" : string.Empty;
                    AppLog.Write("smooth", skipped ? "skipped" : "error", $"grund={smooth.Outcome.ToWord()}{status}");
                    SmoothLine = dialectRewrite
                        ? $"Nicht in Mundart umgeschrieben, Text in Schweizer Hochdeutsch: {ToGerman(smooth.Outcome)}"
                        : $"Nicht geglättet: {ToGerman(smooth.Outcome)}";
                }

                _postMs += again.ElapsedMilliseconds;
                if (smooth.Applied && !string.Equals(text, before, StringComparison.Ordinal)) UnsmoothedTranscript = before;
            }

            Transcript = text;
            RawTranscript = result.Text.Length > 0 && !string.Equals(result.Text, text, StringComparison.Ordinal) ? result.Text : string.Empty;

            string display = SttReport.DisplayText(result);
            if (forceLocal) display = $"{VadNothingSentText}. {display}";
            SttLine = result.Text.Length == 0 ? $"Kein Text erkannt. {display}"
                : text.Length == 0 ? $"Nach der Nachbearbeitung blieb kein Text übrig, nichts eingefügt. {display}"
                : display;
            DiscardPendingClip();
            _trayError = false;
            RaiseChanged();

            // Erst in den Verlauf, dann einfügen. Die Erkennung gilt bis dahin als laufend, Tray und Overlay zeigen weiter Verarbeitung.
            // Ein Fehler hier ist kein Fehler der Erkennung: Der Text steht schon in der Anzeige, deshalb eigene Behandlung.
            if (InsertText.IsInsertable(text))
            {
                try
                {
                    await History.AddAsync(new HistoryEntry(DateTimeOffset.Now, text, result.Engine, localRun), CancellationToken.None);
                    InsertOutcome outcome = await _inserter.InsertAsync(text, target, cts.Token, SentenceCase.RaisedStart(result.Text, text));

                    // Nach dem Beenden oder einer neuen Aufnahme gehört die Anzeige nicht mehr diesem Diktat.
                    if (_disposed || cts.IsCancellationRequested) return;
                    InsertLine = ToGerman(outcome);

                    // Eine Zeile je Diktat mit der Zeit jeder Stufe. aufnahme_ms ist die Länge der Aufnahme und zählt nicht zur Summe.
                    // gesamt_ms läuft vom Stopp bis zum eingefügten Text und enthält auch das Stoppen des Rekorders;
                    // das ist die Zahl, an der das Ziel von etwa einer Sekunde gemessen wird. Nur Zahlen und feste Wörter.
                    AppLog.Write(
                        "dictation",
                        "done",
                        $"aufnahme_ms={_recordedMs} vad_ms={_vadMs} erkennung_ms={sttMs} nachbearbeitung_ms={_postMs} glaettung_ms={_smoothMs} "
                        + $"einfuegen_ms={outcome.Milliseconds} gesamt_ms={_sinceStop?.ElapsedMilliseconds ?? 0} "
                        + $"weg={outcome.Result.ToWord()} erneut={ToWord(_sinceStop is null)}");
                }
                catch (Exception)
                {
                    if (_disposed) return;
                    AppLog.Write("insert", "error", "grund=unbekannt");
                    InsertLine = "Nicht eingefügt: unerwarteter Fehler. Der Text steht im Diagnosefenster.";
                }
            }
        }
        catch (OperationCanceledException) when (_disposed || cts.IsCancellationRequested)
        {
            // Beenden, Abbruch oder neue Aufnahme: Der Clip ist dort schon genullt worden.
        }
        catch (TranscriptionException ex)
        {
            ShowSpeechError(ex, cts);
        }
        catch (Exception)
        {
            // Alles Unerwartete zählt als gescheiterter Erkenner. Die Meldung der Ausnahme wird nicht protokolliert.
            ShowSpeechError(new TranscriptionException(TranscriptionError.EngineFailed, "Die Erkennung ist gescheitert."), cts);
        }
        finally
        {
            if (ReferenceEquals(_sttCts, cts)) _sttCts = null;
            cts.Dispose();
            RaiseChanged();
        }
    }

    // Mischt zwei Listen abwechselnd, beginnend mit der ersten; was bei der längeren übrig bleibt, hängt hinten an.
    private static IEnumerable<string> Interleave(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        int count = Math.Max(first.Count, second.Count);
        for (int i = 0; i < count; i++)
        {
            if (i < first.Count) yield return first[i];
            if (i < second.Count) yield return second[i];
        }
    }

    // Füllwörter entfernen, Wörterbuch anwenden, Leerraum aufräumen. Das Wörterbuch besteht aus dem Feld im Diagnosefenster und den beiden
    // Dateien des Nutzers. Protokoll nur mit Zahlen. Scheitert die Nachbearbeitung unerwartet, bleibt der rohe Text, damit nichts verloren geht.
    // Satzanfänge werden groß (Playbook Abschnitt 28), außer im Terminal: Dort zählt der Befehl so, wie er gesprochen wurde.
    private string PostProcess(TranscriptionResult result, string language, IReadOnlyList<string> boxTerms, bool includeLocalVocabulary, bool terminal)
    {
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string postLanguage = language == TranscriptionRequest.AutoLanguage ? result.DetectedLanguage ?? DefaultLanguage : language;
            IEnumerable<string> vocabulary = boxTerms.Concat(_words.Vocabulary);
            if (includeLocalVocabulary) vocabulary = vocabulary.Concat(_words.LocalVocabulary);
            PostProcessingOptions options = new(
                Vocabulary: vocabulary.ToList(),
                Language: postLanguage,
                ExtraFillers: _words.ExtraFillers,
                SwissVocabulary: _words.SwissVocabulary,
                CapitalizeSentences: !terminal);

            PostProcessingResult processed = _postProcessor.ProcessDetailed(result.Text, options);
            _postMs = watch.ElapsedMilliseconds;
            AppLog.Write(
                "post",
                "result",
                $"fuellwoerter={ToWord(processed.FillersChanged)} ersetzt={processed.Replacements} zeichen_ein={processed.CharsIn} zeichen_aus={processed.CharsOut} ms={watch.ElapsedMilliseconds}");
            return processed.Text;
        }
        catch (Exception)
        {
            AppLog.Write("post", "error", "grund=unbekannt");
            return result.Text;
        }
    }

    // Wendet das Wörterbuch mit allen drei Quellen an, ohne die Füllwörter noch einmal laufen zu lassen. Jeder Leerraum, auch ein
    // Zeilenumbruch, wird ein Leerzeichen. Kann den Text nie leeren. Bei Schweizerdeutsch und Mundart (Playbook Abschnitt 26) zuerst
    // die Helvetismen, außer der Text ist schon in Mundart umgeschrieben (dialectText), und am Ende immer ss statt ß.
    private string ReapplyVocabulary(string text, string language, IReadOnlyList<string> boxTerms, bool dialectText)
    {
        try
        {
            bool swiss = LanguageCodes.IsSwiss(language);
            string corrected = text;
            if (swiss && !dialectText) corrected = SwissText.Helvetisms(corrected, _words.SwissVocabulary.Concat(SwissWordLists.Helvetisms)).Text;

            List<string> lines = boxTerms.Concat(_words.Vocabulary).Concat(_words.LocalVocabulary).ToList();
            if (lines.Count > 0) corrected = VocabularyCorrector.Apply(corrected, VocabularyEntry.ParseAll(lines), true, language).Text;
            if (swiss) corrected = SwissText.Spelling(corrected);
            // Nach der Glättung (nie im Terminal) beginnt jeder Satz groß, auch wenn das Sprachmodell ihn klein gelassen hat.
            corrected = SentenceCase.Apply(TextCleanup.Clean(corrected));
            return corrected.Length > 0 ? corrected : WithoutBreaks(text);
        }
        catch (Exception)
        {
            return WithoutBreaks(text);
        }
    }

    // Auch im Rückfall gelangt kein Zeilenumbruch ins Zielfenster.
    private static string WithoutBreaks(string text) => text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

    // Der Grund, warum nicht geglättet wurde, als deutscher Satzteil für die Anzeige; im Protokoll steht das feste Wort.
    private static string ToGerman(SmoothingOutcome outcome) => outcome switch
    {
        SmoothingOutcome.TooShort => "der Text ist zu kurz",
        SmoothingOutcome.TooLong => "der Text ist zu lang",
        SmoothingOutcome.NoKey => "kein Groq Schlüssel hinterlegt",
        SmoothingOutcome.LocalOnly => "dieses Diktat bleibt auf dem Rechner",
        SmoothingOutcome.Terminal => "im Terminal bleibt der Text, wie er ist",
        SmoothingOutcome.Timeout => "Groq hat nicht rechtzeitig geantwortet",
        SmoothingOutcome.Network => "keine Verbindung zu Groq",
        SmoothingOutcome.Unauthorized => "Groq hat den Schlüssel abgelehnt",
        SmoothingOutcome.RateLimited => "das Limit bei Groq ist erreicht",
        SmoothingOutcome.Rejected => "die Antwort passte nicht zum Text",
        SmoothingOutcome.OffForApp => "für dieses Programm ausgeschaltet",
        _ => "Groq hat einen Fehler gemeldet",
    };

    // Fehler der Erkennung: Protokoll mit Grund und Kürzel, deutsche Meldung, Clip bleibt, "Erneut versuchen" wird möglich.
    private void ShowSpeechError(TranscriptionException ex, CancellationTokenSource cts)
    {
        if (_disposed || cts.IsCancellationRequested) return;

        SttReport.WriteError(ex);
        string message = $"{SttReport.GermanError(ex.Error)} Die Aufnahme bleibt im Arbeitsspeicher, Erneut versuchen ist möglich.";
        SttLine = _pendingLocalOnly ? $"{VadNothingSentText}. {message}" : message;
        CanRetry = _pendingClip is not null;
        _trayError = true;
        RaiseChanged();
    }

    // Beendet eine laufende Erkennung und nullt den Clip. Ohne beides folgenlos.
    private void CancelSpeech()
    {
        CancellationTokenSource? cts = _sttCts;
        _sttCts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
            // Die Quelle kann schon freigegeben sein, dann ist die Erkennung ohnehin zu Ende.
        }

        DiscardPendingClip();
    }

    // Überschreibt die Abtastwerte des behaltenen Clips mit Nullen und sperrt "Erneut versuchen".
    private void DiscardPendingClip()
    {
        AudioClip? clip = _pendingClip;
        _pendingClip = null;
        _pendingAudioMs = 0;
        _pendingLocalOnly = false;
        if (clip is not null) Array.Clear(clip.Samples);
        CanRetry = false;
    }

    // True, wenn der Ablauf beendet ist oder inzwischen eine neuere Aufnahme begonnen hat.
    private bool IsStale(int generation) => _disposed || generation != _generation;

    // Verwirft die Aufnahme ohne Auswertung. Ohne laufende Aufnahme folgenlos.
    private async Task CancelRecordingAsync(string message)
    {
        ResultLine = message;
        SetLevel(0);
        RaiseChanged();
        try
        {
            await _recorder.CancelAsync(CancellationToken.None);
        }
        catch
        {
            // Beim Abbruch ist nichts mehr auszuwerten; ein Fehler des Geräts ändert daran nichts.
        }
    }

    // Das Ergebnis des Einfügens als deutscher Satz für die Anzeige; im Protokoll stehen die festen Wörter.
    private static string ToGerman(InsertOutcome outcome)
    {
        string reason = outcome.Reason switch
        {
            InsertReason.NoTarget => "kein Zielfenster, beim Start war Talk selbst vorn",
            InsertReason.WindowGone => "das Zielfenster gibt es nicht mehr",
            InsertReason.HigherRights => "das Zielfenster hat höhere Rechte",
            InsertReason.PasswordField => "Passwortfeld",
            InsertReason.NotForeground => "das Zielfenster ließ sich nicht nach vorn holen",
            InsertReason.InputFailed => "die Eingabe ist gescheitert",
            InsertReason.Canceled => "abgebrochen",
            _ => "kein Text übrig",
        };

        return outcome.Result switch
        {
            InsertResult.Inserted => $"Eingefügt über die Zwischenablage, {outcome.Milliseconds} ms",
            InsertResult.Typed => outcome.IsTerminal ? $"Getippt (Terminal), {outcome.Milliseconds} ms" : $"Getippt, {outcome.Milliseconds} ms",
            InsertResult.Blocked => $"Absichtlich nicht eingefügt: {reason}. Der Text steht im Verlauf und im Diagnosefenster.",
            _ => $"Nicht eingefügt: {reason}. Der Text steht im Verlauf und im Diagnosefenster.",
        };
    }

    // Der Grund für die Anzeige als deutsches Wort; im Protokoll steht der Zustandsname aus RecordingError.
    private static string ToGerman(RecordingError error) => error switch
    {
        RecordingError.NoDevice => "kein Gerät",
        RecordingError.DeviceInUse => "Gerät belegt",
        RecordingError.DeviceLost => "Gerät getrennt",
        RecordingError.UnsupportedFormat => "Format nicht unterstützt",
        _ => "unbekannt",
    };

    private static string ToWord(bool value) => value ? "ja" : "nein";

    private static long RoundMs(TimeSpan time) => (long)Math.Round(time.TotalMilliseconds);
}
