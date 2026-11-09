using System.Globalization;
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

using NAudio.Wave;

using Talk.App.Audio;
using Talk.App.Dictation;
using Talk.App.Diagnostics;
using Talk.App.History;
using Talk.App.Feedback;
using Talk.App.Input;
using Talk.App.Processing;
using Talk.App.Insertion;
using Talk.App.Secrets;
using Talk.App.Settings;
using Talk.App.Transcription;
using Talk.App.Ui;
using Talk.Core;
using Talk.Core.Audio;
using Talk.Core.Diagnostics;
using Talk.Core.History;
using Talk.Core.Hotkey;
using Talk.Core.Insertion;
using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Processing.Swiss;
using Talk.Core.Settings;
using Talk.Core.Transcription;

namespace Talk.App;

/// <summary>
/// Einstiegspunkt der App. Achtzehn Wege:
/// <c>--selftest</c> protokolliert Start und Selbsttest und endet ohne Hook (Exit 0);
/// <c>--hooktest N</c> setzt den Hook ohne Fenster für N Sekunden und protokolliert alle Aktionen (Exit 0, ohne Aufnahme);
/// <c>--traytest N</c> zeigt ohne Fenster und ohne Hook N Sekunden lang das Tray Symbol und wechselt durch alle vier Zustände (Exit 0, bei Fehler 2);
/// <c>--inserttest "Text" --pid P [--delay N]</c> wartet N Sekunden (Standard 3) und fügt den Prüftext in das Fenster ein, das dann vorn ist,
/// aber nur, wenn es zum Prozess P gehört; sonst wird nichts eingegeben (Exit 2);
/// der Text steht nie im Protokoll (Exit 0 eingefügt oder getippt, 3 nur Verlauf oder gesperrt, 2 bei Fehler);
/// <c>--audiotest N</c> nimmt ohne Fenster N Sekunden vom Standardgerät auf, protokolliert Geräte, Pegel, Clip und Stilleerkennung (Exit 0, bei Aufnahmefehler 2);
/// <c>--vadtest Pfad</c> liest eine WAV Datei nur für die Diagnose und lässt die Stilleerkennung laufen (Exit 0 bei Sprache, 3 ohne Sprache, 2 bei Fehler);
/// <c>--secrettest</c> prüft die Schlüsselablage mit dem eigenen Eintrag "Talk:SelfTest" und erfundenen Werten (Exit 0, bei Fehler 2);
/// <c>--overlaytest N [--shots Ordner]</c> zeigt N Sekunden lang das Overlay in allen Zuständen und prüft, dass es nie den Fokus nimmt (Exit 0, sonst 2);
/// <c>--autostarttest</c> prüft Setzen und Entfernen des Autostart Eintrags an einem eigenen Prüfschlüssel (Exit 0, sonst 2);
/// <c>--crashtest</c> löst absichtlich einen unerwarteten Fehler aus und prüft damit das saubere Ende (Exit 1);
/// <c>--logcheck</c> durchsucht das Protokoll nach Zeilen mit Inhalten statt fester Wörter und Zahlen (Exit 0 ohne Fund, 3 mit, 2 bei Fehler);
/// <c>--fallbacktest</c> prüft, dass nach einem gescheiterten Start mit unbekanntem Mikrofon das Standardgerät aufnimmt (Exit 0, sonst 2);
/// <c>--settingstest</c> prüft Lesen und Speichern der Einstellungen mit einer eigenen Prüfdatei (Exit 0, bei Fehler 2);
/// <c>--historytest</c> prüft den verschlüsselten Verlauf mit einer eigenen Prüfdatei und erfundenen Texten (Exit 0, bei Fehler 2);
/// <c>--smoothtest "Text" [--level fehler|saetze|nachricht] [--lang de|it|en]</c> glättet einen Prüftext über Groq und schreibt ihn
/// nur in die Standardausgabe (Exit 0 geglättet, 3 ohne Schlüssel, zu kurz, zu lang oder "Nur lokal", 2 bei Fehler);
/// <c>--keycheck</c> prüft den hinterlegten Groq Schlüssel, ohne ihn anzuzeigen (Exit 0 gültig, 3 abgelehnt oder fehlt, 2 keine Verbindung);
/// <c>--stttest Pfad [--local] [--lang de|it|en|auto] [--vocab "a, b"] [--runtime cpu|vulkan] [--model Datei] [--repeat N]</c> liest eine WAV Datei nur
/// für die Diagnose, schneidet mit der Stilleerkennung zu, erkennt den Text und schreibt ihn nur in die Standardausgabe (Exit 0 mit Text,
/// 3 ohne Sprache oder ohne Text, 2 bei Fehler). <c>--local</c> erkennt nur lokal, nichts geht ins Internet. <c>--runtime</c> erzwingt für
/// diesen Lauf die Laufzeit der lokalen Erkennung. <c>--repeat N</c> (1 bis 5) erkennt denselben Ausschnitt N mal im selben Prozess,
/// je Lauf eine Zeile <c>stt | result</c>, damit sich die Zeit mit geladenem Modell messen lässt;
/// ohne Argumente startet Talk im Tray, ohne Fenster: Hook, Aufnahme, Stilleerkennung, Erkennung, Nachbearbeitung, Einfügen und Verlauf;
/// Einstellungen, Verlauf und Diagnose öffnen sich über das Tray Menü.
/// Argumente ohne bekannten Schalter beenden die App ohne Fenster und ohne Hook (Exit 2).
/// Vor allem anderen wird die Reihenfolge der Whisper Laufzeiten gesetzt (<see cref="WhisperRuntime.Configure"/>).
/// Typen aus System.Windows werden voll ausgeschrieben, weil Windows Forms gleichnamige Typen mitbringt.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SelfTestSwitch = "--selftest";
    private const string HookTestSwitch = "--hooktest";
    private const string PushToTalkSwitch = "--ptt";
    private const string AudioTestSwitch = "--audiotest";
    private const string TrayTestSwitch = "--traytest";
    private const string InsertTestSwitch = "--inserttest";
    private const string DelaySwitch = "--delay";
    private const string TargetPidSwitch = "--pid";
    private const string VadTestSwitch = "--vadtest";
    private const string SecretTestSwitch = "--secrettest";
    private const string HistoryTestSwitch = "--historytest";
    private const string SettingsTestSwitch = "--settingstest";
    private const string WordListTestSwitch = "--wordlisttest";
    private const string OverlayTestSwitch = "--overlaytest";
    private const string LogCheckSwitch = "--logcheck";
    private const string AutostartTestSwitch = "--autostarttest";
    private const string CrashTestSwitch = "--crashtest";
    private const string ShotsSwitch = "--shots";
    private const string FallbackTestSwitch = "--fallbacktest";
    private const string UiShotsSwitch = "--uishots";
    private const string KeyCheckSwitch = "--keycheck";
    private const string SmoothTestSwitch = "--smoothtest";
    private const string LevelSwitch = "--level";
    private const string SttTestSwitch = "--stttest";
    private const string LocalSwitch = "--local";
    private const string LanguageSwitch = "--lang";
    private const string VocabularySwitch = "--vocab";
    private const string RuntimeSwitch = "--runtime";
    private const string ModelSwitch = "--model";
    private const string RepeatSwitch = "--repeat";
    private const string PostSwitch = "--post";
    private const string DialectSwitch = "--dialect";
    private const int SttRepeatDefault = 1;
    private const int SttRepeatMax = 5;
    private const int HookTestDefaultSeconds = 8;
    private const int HookTestMaxSeconds = 600;
    private const int AudioTestDefaultSeconds = 3;
    private const int AudioTestMaxSeconds = 60;
    private const int TrayTestDefaultSeconds = 8;
    private const int TrayTestMaxSeconds = 120;
    private const int InsertTestDefaultDelaySeconds = 3;
    private const int InsertTestMaxDelaySeconds = 30;
    private const int DiagnosticErrorExitCode = 2;

    // Exit 3 heißt bei allen Diagnosen "gelaufen, aber ohne Ergebnis": keine Sprache, kein Text, Schlüssel abgelehnt oder nicht hinterlegt.
    private const int NoResultExitCode = 3;

    // Name des eigenen Prüfeintrags in der Schlüsselablage ("Talk:SelfTest"). Der Eintrag des Groq Schlüssels wird von --secrettest nie angefasst.
    private const string SelfTestSecretName = "SelfTest";
    private const string DefaultLanguage = "de";

    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LevelLogInterval = TimeSpan.FromMilliseconds(500);

    private const string SingleInstanceName = "Local\\Talk.SingleInstance";

    private KeyboardHookService? _service;
    private Mutex? _singleInstance;
    private DictationCoordinator? _coordinator;
    private AppSettingsService? _settings;
    private EncryptedHistoryStore? _history;
    private TrayIcon? _tray;
    private HotkeyTestWindow? _diagnosticsWindow;
    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private OverlayWindow? _overlay;
    private AudioDucker? _ducker;
    private bool _quitting;
    private int _crashing;
    private bool _headless;
    private SpeechService? _speech;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        string[] args = e.Args ?? [];

        // Vor dem ersten Fenster: Ist in Windows "Animationen anzeigen" aus, entfällt jede Bewegung der Bedienelemente (DESIGN.md).
        WindowTheme.ApplyMotionPreference(this);

        // Zuerst die Reihenfolge der Whisper Laufzeiten, vor jeder Factory und auch vor der Factory der Stilleerkennung, weil beide
        // dieselbe geladene Bibliothek benutzen. Standard ist Vulkan vor CPU; "whisperRuntime" in settings.json bevorzugt eine Laufzeit,
        // "--runtime cpu|vulkan" erzwingt sie (vulkan dann ohne Rückfall auf die CPU), die Befehlszeile gewinnt. Hier wird nur die
        // Reihenfolge gesetzt und noch nichts geladen.
        TryGetSwitchValue(args, RuntimeSwitch, out string? runtimeOverride);
        WhisperRuntime.Configure(TalkPaths.WhisperRuntimeSetting, runtimeOverride);

        // "--model Datei" wählt für diesen einen Lauf ein anderes Modell aus dem Modellordner, ohne die Einstellungen anzufassen.
        // Gedacht für Messungen mit --stttest.
        if (TryGetSwitchValue(args, ModelSwitch, out string? modelOverride)) TalkPaths.WhisperModelOverride = modelOverride;

        base.OnStartup(e);

        // Unerwartete Fehler: festes Wort ins Protokoll (nie die Meldung, sie könnte Inhalte tragen), aufräumen, beenden.
        // Weiterlaufen in einem unbekannten Zustand wäre gefährlicher als ein sauberes Ende, weil der Hook Caps Lock hält.
        DispatcherUnhandledException += (_, crash) =>
        {
            crash.Handled = true;
            CrashExit("oberflaeche");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, _) => CrashExit("hintergrund");

        // Klicks im Tray Menü kommen über Windows Forms und liefen sonst an den Fängern vorbei in dessen eigenen Fehlerdialog,
        // der die Meldung der Ausnahme zeigt. Das muss vor dem ersten Windows Forms Objekt gesetzt sein.
        System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, _) => CrashExit("menue");
        _headless = args.Length > 0;
        TaskScheduler.UnobservedTaskException += (_, lost) =>
        {
            // Eine vergessene Aufgabe ist kein Grund zum Beenden, aber sie soll auffallen.
            lost.SetObserved();
            AppLog.Write("app", "crash", "art=aufgabe beendet=nein");
        };
        // Es wird nur die Anzahl protokolliert, nie der Inhalt der Argumente. Die Prozessnummer ordnet die Zeilen einem Lauf zu,
        // wenn mehrere Läufe zugleich in dieselbe Datei schreiben.
        AppLog.Write("app", "start", $"version={TalkInfo.Version} args={args.Length} pid={Environment.ProcessId}");

        if (HasSwitch(args, SelfTestSwitch))
        {
            AppLog.Write("app", "selftest", "ok");
            Shutdown(0);
            return;
        }

        if (TryGetSeconds(args, HookTestSwitch, HookTestDefaultSeconds, HookTestMaxSeconds, out int hookSeconds))
        {
            // "--ptt" schaltet im Hook Test Push to Talk ein, damit sich das Halten der Taste automatisch prüfen lässt.
            StartHookTest(hookSeconds, HasSwitch(args, PushToTalkSwitch));
            return;
        }

        if (TryGetSeconds(args, TrayTestSwitch, TrayTestDefaultSeconds, TrayTestMaxSeconds, out int traySeconds))
        {
            StartTrayTest(traySeconds);
            return;
        }

        if (TryGetSwitchValue(args, InsertTestSwitch, out string? insertText))
        {
            int delay = TryGetSeconds(args, DelaySwitch, InsertTestDefaultDelaySeconds, InsertTestMaxDelaySeconds, out int parsedDelay)
                ? parsedDelay
                : InsertTestDefaultDelaySeconds;
            // Ohne die Prozessnummer des erwarteten Ziels läuft der Test nicht: Der Prüftext darf nie in ein zufälliges Fenster gehen.
            int targetPid = TryGetSeconds(args, TargetPidSwitch, 0, int.MaxValue, out int parsedPid) ? parsedPid : 0;
            StartInsertTest(insertText, delay, targetPid);
            return;
        }

        if (TryGetSeconds(args, AudioTestSwitch, AudioTestDefaultSeconds, AudioTestMaxSeconds, out int audioSeconds))
        {
            StartAudioTest(audioSeconds);
            return;
        }

        if (TryGetSwitchValue(args, VadTestSwitch, out string? wavPath))
        {
            StartVadTest(wavPath);
            return;
        }

        if (TryGetSeconds(args, OverlayTestSwitch, TrayTestDefaultSeconds, TrayTestMaxSeconds, out int overlaySeconds))
        {
            TryGetSwitchValue(args, ShotsSwitch, out string? shotsDirectory);
            StartOverlayTest(overlaySeconds, shotsDirectory);
            return;
        }

        if (TryGetSwitchValue(args, UiShotsSwitch, out string? uiShotsDirectory))
        {
            // "--uishots Ordner": Bilder der Fenster nach DESIGN.md mit erfundenen Texten, ohne Hook und ohne echte Daten. Ohne Ordner Exit 2.
            AppLog.Write("uishots", "begin");
            if (string.IsNullOrWhiteSpace(uiShotsDirectory))
            {
                AppLog.Write("uishots", "end", $"exit={DiagnosticErrorExitCode}");
                Shutdown(DiagnosticErrorExitCode);
                return;
            }

            // Was auch passiert, am Ende steht immer Shutdown, sonst liefe der Prozess ohne Fenster und ohne Exit weiter.
            _ = Dispatcher.InvokeAsync(async () =>
            {
                int exitCode = DiagnosticErrorExitCode;
                try
                {
                    exitCode = await UiShots.RunAsync(uiShotsDirectory);
                }
                catch (Exception)
                {
                    exitCode = DiagnosticErrorExitCode;
                }
                finally
                {
                    AppLog.Write("uishots", "end", $"exit={exitCode}");
                    Shutdown(exitCode);
                }
            });
            return;
        }

        if (HasSwitch(args, CrashTestSwitch))
        {
            // Löst absichtlich einen unerwarteten Fehler auf dem UI Thread aus, ohne Hook und ohne Fenster. Erwartet: Exit 1 und die Zeile
            // app | crash | art=oberflaeche beendet=ja, aber nirgends die Meldung der Ausnahme.
            AppLog.Write("crashtest", "begin");
            _ = Dispatcher.BeginInvoke(() => throw new InvalidOperationException("Meldung, die nie im Protokoll stehen darf: äöü, geheim!"));
            return;
        }

        if (HasSwitch(args, AutostartTestSwitch))
        {
            AppLog.Write("autostarttest", "begin");
            RunDiagnostic("autostarttest", () => Task.FromResult(RunAutostartTest()));
            return;
        }

        if (HasSwitch(args, LogCheckSwitch))
        {
            RunDiagnostic("logcheck", () => Task.FromResult(RunLogCheck()));
            return;
        }

        if (HasSwitch(args, FallbackTestSwitch))
        {
            AppLog.Write("fallbacktest", "begin");
            RunDiagnostic("fallbacktest", RunFallbackTestAsync);
            return;
        }

        if (HasSwitch(args, SettingsTestSwitch))
        {
            AppLog.Write("settingstest", "begin");
            RunDiagnostic("settingstest", () => Task.FromResult(RunSettingsTest()));
            return;
        }

        if (HasSwitch(args, WordListTestSwitch))
        {
            AppLog.Write("wordlisttest", "begin");
            RunDiagnostic("wordlisttest", () => Task.FromResult(RunWordListTest()));
            return;
        }

        if (HasSwitch(args, HistoryTestSwitch))
        {
            AppLog.Write("historytest", "begin");
            RunDiagnostic("historytest", RunHistoryTestAsync);
            return;
        }

        if (HasSwitch(args, SecretTestSwitch))
        {
            StartSecretTest();
            return;
        }

        if (TryGetSwitchValue(args, SmoothTestSwitch, out string? smoothText))
        {
            TryGetSwitchValue(args, LevelSwitch, out string? smoothLevel);
            TryGetSwitchValue(args, LanguageSwitch, out string? smoothLanguage);
            TryGetSwitchValue(args, DialectSwitch, out string? smoothDialect);
            AppLog.Write("smoothtest", "begin");
            RunDiagnostic("smoothtest", () => RunSmoothTestAsync(smoothText, smoothLevel, smoothLanguage, smoothDialect));
            return;
        }

        if (HasSwitch(args, KeyCheckSwitch))
        {
            StartKeyCheck();
            return;
        }

        if (TryGetSwitchValue(args, SttTestSwitch, out string? sttPath))
        {
            TryGetSwitchValue(args, LanguageSwitch, out string? language);
            TryGetSwitchValue(args, VocabularySwitch, out string? vocabulary);

            // "--repeat N": fehlt der Schalter oder die Zahl, läuft die Erkennung einmal; mehr als fünf Läufe gibt es nicht.
            int repeat = TryGetSeconds(args, RepeatSwitch, SttRepeatDefault, SttRepeatMax, out int parsedRepeat)
                ? parsedRepeat
                : SttRepeatDefault;

            // "--post" und "--level": danach dieselbe Nachbearbeitung und Glättung wie beim Diktat, ohne die Wörterlisten des Nutzers
            // (Playbook Abschnitt 26, Messung). "--dialect" gilt für Mundart.
            TryGetSwitchValue(args, LevelSwitch, out string? sttLevel);
            TryGetSwitchValue(args, DialectSwitch, out string? sttDialect);

            // Eine unbekannte Stufe beendet den Test, bevor Audio an Groq geht.
            try
            {
                _ = ParseLevel(sttLevel);
            }
            catch (ArgumentException)
            {
                AppLog.Write("stttest", "args", "grund=stufe");
                Shutdown(DiagnosticErrorExitCode);
                return;
            }

            SttPipeline pipeline = new(HasSwitch(args, PostSwitch) || sttLevel is not null || sttDialect is not null, sttLevel, sttDialect);

            StartSttTest(sttPath, HasSwitch(args, LocalSwitch), language, vocabulary, repeat, pipeline);
            return;
        }

        if (args.Length > 0)
        {
            // Argumente, aber kein bekannter Schalter (Tippfehler oder falsch übergebene Liste): Die App endet, statt das Testfenster mit
            // aktivem Hook zu öffnen und dort zu warten. Protokolliert wird nur das feste Wort, nie der Inhalt der Argumente.
            AppLog.Write("app", "args", "unbekannt");
            Shutdown(DiagnosticErrorExitCode);
            return;
        }

        StartTray();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _service?.Dispose();
        _service = null;
        _speech?.Dispose();
        _speech = null;
        base.OnExit(e);
    }

    // Der normale Start: Talk läuft ohne Fenster im Tray. Einstellungen, Verlauf und Diagnose öffnen sich über das Tray Menü.
    private void StartTray()
    {
        // Talk läuft nur einmal: Zwei Hooks auf derselben Taste und zwei Schreiber am selben Verlauf würden sich stören.
        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out bool first);
        if (!first)
        {
            AppLog.Write("app", "start", "abgelehnt=laeuft_schon");
            System.Windows.MessageBox.Show("Talk läuft schon. Schau im Tray neben der Uhr nach dem Symbol.", "Talk");
            Shutdown(0);
            return;
        }

        // Das Tray Menü kommt aus Windows Forms. Damit es sich in einer WPF App wie gewohnt schließt, braucht es diese Brücke.
        System.Windows.Forms.Integration.WindowsFormsHost.EnableWindowsFormsInterop();

        DictationSettings settings = AppSettingsService.Load(TalkPaths.SettingsPath, out bool settingsReadFailed);

        KeyboardHookService service = new(new HotkeyController(new HotkeyOptions(
            DoubleTapWindowMs: settings.DoubleTapWindowMs,
            PushToTalkEnabled: settings.PushToTalk)));
        _service = service;
        service.Start();
        LogInstalled(service);

        // Die Erkennung gehört der App und wird beim Beenden freigegeben; der Ablauf benutzt sie nur.
        SpeechService speech = SpeechService.CreateDefault();
        speech.LocalOnly = settings.LocalOnly;
        speech.LocalLanguage = settings.Language;
        _speech = speech;

        // Der Verlauf liegt verschlüsselt im Benutzerprofil. Ist das Speichern abgestellt, bleibt er nur im Arbeitsspeicher.
        EncryptedHistoryStore history = new(TalkPaths.HistoryPath, settings.HistoryEnabled);
        _history = history;

        // Der Ablauf des Diktats ohne Oberfläche. Tray und Fenster hören nur zu.
        DictationCoordinator coordinator = new(service, speech, history);
        _coordinator = coordinator;
        AppSettingsService appSettings = new(coordinator, history, settings, TalkPaths.SettingsPath);
        _settings = appSettings;
        if (settingsReadFailed) appSettings.MarkLoadFailed();

        // Andere Programme während der Aufnahme leiser (Playbook Abschnitt 25). Zuerst zurücksetzen, was ein hartes Ende leise
        // zurückgelassen hat; danach folgt die Lautstärke dem Zustand des Ablaufs und dem Schalter.
        AudioDucker ducker = new(TalkPaths.DuckingJournalPath);
        _ducker = ducker;
        ducker.RestorePending();
        bool ducked = false;
        void FollowDucking()
        {
            bool wanted = appSettings.Current.DuckingEnabled && coordinator.Status == TrayStatus.Recording;
            if (wanted == ducked) return;
            ducked = wanted;
            if (wanted) ducker.Lower();
            else ducker.Restore();
        }

        coordinator.Changed += FollowDucking;
        appSettings.Changed += FollowDucking;

        // Auch beim Abmelden oder Herunterfahren soll der letzte Eintrag auf der Festplatte stehen. Windows lässt dafür nur kurz Zeit.
        SessionEnding += (_, _) =>
        {
            ducker.RestoreAndWait(TimeSpan.FromSeconds(1));
            history.FlushAsync().Wait(TimeSpan.FromSeconds(2));
        };

        // Das Tray Symbol ist Anzeige und Menü. Lässt es sich nicht anlegen, steht das mit festem Wort im Protokoll; dann öffnet sich
        // das Diagnosefenster, und es zu schließen beendet Talk, weil es sonst keinen Weg dorthin gäbe.
        TrayIcon? tray = TryCreateTray();
        _tray = tray;
        if (tray is null)
        {
            StartOverlay(coordinator, appSettings);
            ShowDiagnostics();
            if (_diagnosticsWindow is not null) _diagnosticsWindow.Closed += (_, _) => _ = QuitAsync();
            return;
        }

        tray.SetMenu(new TrayMenuActions(
            CopyLast: () => _ = CopyLastAsync(),
            ShowHistory: ShowHistory,
            ShowSettings: ShowSettings,
            ShowDiagnostics: ShowDiagnostics,
            ToggleLocalOnly: () => appSettings.Apply(appSettings.Current with { LocalOnly = !appSettings.Current.LocalOnly }),
            ToggleSmoothing: () => appSettings.Apply(appSettings.Current with { SmoothingEnabled = !appSettings.Current.SmoothingEnabled }),
            Quit: () => _ = QuitAsync()));
        tray.SetLocalOnly(settings.LocalOnly);
        tray.SetSmoothing(settings.SmoothingEnabled);
        tray.Show(coordinator.Status);
        coordinator.Changed += () => tray.Show(coordinator.Status);
        appSettings.Changed += () =>
        {
            tray.SetLocalOnly(appSettings.Current.LocalOnly);
            tray.SetSmoothing(appSettings.Current.SmoothingEnabled);
        };

        StartOverlay(coordinator, appSettings);

        tray.ShowHint("Talk läuft", "Caps Lock doppelt tippen startet das Diktat. Ein Klick auf dieses Symbol öffnet das Menü.");
    }

    // Das Overlay folgt nur Zustand und Pegel des Ablaufs. Lässt es sich nicht anlegen, läuft Talk ohne Overlay weiter.
    private void StartOverlay(DictationCoordinator coordinator, AppSettingsService settings)
    {
        try
        {
            OverlayWindow overlay = new() { Enabled = settings.Current.OverlayEnabled };
            _overlay = overlay;
            coordinator.Changed += () => overlay.Present(coordinator.Status);
            coordinator.LevelChanged += overlay.SetLevel;
            settings.Changed += () => overlay.Enabled = settings.Current.OverlayEnabled;
        }
        catch (Exception)
        {
            AppLog.Write("overlay", "error", "grund=anlegen");
        }
    }

    // Öffnet ein Fenster höchstens einmal; ist es schon offen, kommt es nach vorn.
    private static T ShowSingle<T>(ref T? window, Func<T> create)
        where T : System.Windows.Window
    {
        if (window is null)
        {
            window = create();
            window.Show();
        }
        else
        {
            if (window.WindowState == System.Windows.WindowState.Minimized) window.WindowState = System.Windows.WindowState.Normal;
            window.Activate();
        }

        return window;
    }

    private void ShowDiagnostics()
    {
        if (_coordinator is not { } coordinator || _settings is not { } settings || _quitting) return;
        bool isNew = _diagnosticsWindow is null;
        HotkeyTestWindow window = ShowSingle(ref _diagnosticsWindow, () => new HotkeyTestWindow(coordinator, settings));
        if (isNew) window.Closed += (_, _) => _diagnosticsWindow = null;
    }

    private void ShowSettings()
    {
        if (_coordinator is not { } coordinator || _settings is not { } settings || _quitting) return;
        bool isNew = _settingsWindow is null;
        SettingsWindow window = ShowSingle(ref _settingsWindow, () => new SettingsWindow(settings, coordinator));
        if (isNew) window.Closed += (_, _) => _settingsWindow = null;
    }

    private void ShowHistory()
    {
        if (_history is not { } history || _quitting) return;
        bool isNew = _historyWindow is null;
        HistoryWindow window = ShowSingle(ref _historyWindow, () => new HistoryWindow(history));
        if (isNew) window.Closed += (_, _) => _historyWindow = null;
    }

    // Legt den letzten Text aus dem Verlauf in die Zwischenablage. Das ist der ausdrückliche Wunsch des Nutzers, deshalb ohne Ausschlussformate.
    private async Task CopyLastAsync()
    {
        if (_history is null || _quitting) return;

        try
        {
            IReadOnlyList<HistoryEntry> last = await _history.GetRecentAsync(1, CancellationToken.None);
            if (last.Count == 0)
            {
                _tray?.ShowHint("Talk", "Der Verlauf ist noch leer.");
                return;
            }

            System.Windows.Clipboard.SetText(last[0].Text);
            _tray?.ShowHint("Talk", "Der letzte Text liegt in der Zwischenablage.");
        }
        catch (Exception)
        {
            _tray?.ShowHint("Talk", "Kopieren nicht möglich, die Zwischenablage ist gerade belegt.");
        }
    }

    // Beendet Talk: Fenster schließen, Tray Symbol entfernen, Ablauf abschließen (wartet auf die Zwischenablage), Verlauf fertig schreiben.
    private async Task QuitAsync()
    {
        if (_quitting) return;
        _quitting = true;

        try
        {
            _diagnosticsWindow?.Close();
            _settingsWindow?.Close();
            _historyWindow?.Close();
            _overlay?.Close();
            _tray?.Dispose();

            if (_coordinator is not null) await _coordinator.ShutdownAsync();
            if (_history is not null) await _history.FlushAsync();
        }
        catch (Exception)
        {
            // Beim Beenden darf nichts den Prozess festhalten. Die Meldung der Ausnahme wird nicht protokolliert.
            AppLog.Write("app", "quit", "fehler");
        }
        finally
        {
            // Die Lautstärke anderer Programme kommt auch dann zurück, wenn oben etwas scheitert.
            _ducker?.RestoreAndWait(TimeSpan.FromSeconds(1));
        }

        Shutdown(0);
    }

    private static TrayIcon? TryCreateTray()
    {
        try
        {
            return new TrayIcon();
        }
        catch (Exception)
        {
            AppLog.Write("tray", "error", "grund=symbol");
            return null;
        }
    }

    private void StartHookTest(int seconds, bool pushToTalk)
    {
        KeyboardHookService service = CreateService(pushToTalk);
        _service = service;
        service.ActionReceived += action => HandleHookTestAction(service, action);
        service.NoticeReceived += HandleHookTestNotice;
        service.HookReinstalled += (reason, installed) => AppLog.Write(
            "hook",
            "reinstalled",
            installed ? $"grund={reason.ToWord()}" : $"grund={reason.ToWord()} fehler={service.LastInstallError}");

        service.Start();
        LogInstalled(service);
        HotkeyOptions options = service.Controller.Options;
        AppLog.Write("hotkey", "options", $"fenster={options.DoubleTapWindowMs} ptt={(options.PushToTalkEnabled ? "an" : "aus")}");
        AppLog.Write("hooktest", "begin", $"sekunden={seconds}");

        DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            service.Dispose();
            _service = null;
            AppLog.Write("hook", "removed");
            AppLog.Write("hooktest", "end");
            Shutdown(0);
        };
        timer.Start();
    }

    // Tray Test ohne Fenster und ohne Hook: Das Symbol erscheint und zeigt nacheinander Ruhe, Aufnahme, Verarbeitung und Fehler,
    // jeden Zustand gleich lang. Protokoll: je Wechsel tray | state, am Ende traytest | end. Fehlt ein Symbol, endet der Test mit Exit 2.
    private void StartTrayTest(int seconds)
    {
        AppLog.Write("traytest", "begin", $"sekunden={seconds}");

        TrayIcon tray;
        try
        {
            tray = new TrayIcon();
        }
        catch (Exception)
        {
            AppLog.Write("traytest", "end", $"exit={DiagnosticErrorExitCode}");
            Shutdown(DiagnosticErrorExitCode);
            return;
        }

        TrayStatus[] order = [TrayStatus.Idle, TrayStatus.Recording, TrayStatus.Processing, TrayStatus.Error];
        int index = 0;
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(seconds * 1000.0 / order.Length) };
        timer.Tick += (_, _) =>
        {
            index++;
            if (index < order.Length)
            {
                tray.Show(order[index]);
                return;
            }

            timer.Stop();
            tray.Dispose();
            AppLog.Write("traytest", "end", "exit=0");
            Shutdown(0);
        };
        timer.Start();
    }

    // Einfügetest ohne Fenster und ohne Hook: wartet, nimmt dann das Fenster, das vorn ist, als Ziel und fügt den Prüftext ein,
    // aber nur, wenn es zum Prozess mit der übergebenen Nummer gehört. So landet der Prüftext nie in einem zufälligen Fenster.
    // Läuft auf dem UI Thread, weil die Zwischenablage ihn verlangt. Vor dem Ende wird gewartet, bis die Zwischenablage zurückgelegt ist.
    // Protokoll: inserttest | begin, insert | result (ohne Text), inserttest | end. Ein leerer Prüftext ist ein Fehler (Exit 2).
    private async void StartInsertTest(string? text, int delaySeconds, int targetPid)
    {
        AppLog.Write("inserttest", "begin", $"sekunden={delaySeconds}");

        int exitCode = DiagnosticErrorExitCode;
        try
        {
            if (InsertText.IsInsertable(text) && targetPid > 0)
            {
                ForegroundWindowProbe.WarmUp();
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                // Gehört das Fenster vorn nicht zum erwarteten Prozess, wird nichts eingegeben (Exit 2).
                TargetWindow? target = ForegroundWindowProbe.Capture();
                if (target is null || ForegroundWindowProbe.ProcessIdOf(target.Handle) != (uint)targetPid)
                {
                    AppLog.Write("inserttest", "abort", "grund=falsches_ziel");
                    AppLog.Write("inserttest", "end", $"exit={DiagnosticErrorExitCode}");
                    Shutdown(DiagnosticErrorExitCode);
                    return;
                }

                TextInserter inserter = new();
                InsertOutcome outcome = await inserter.InsertAsync(text!, target, CancellationToken.None);
                await inserter.PendingRestore;
                exitCode = outcome.Result is InsertResult.Inserted or InsertResult.Typed ? 0 : NoResultExitCode;
            }
        }
        catch (Exception)
        {
            exitCode = DiagnosticErrorExitCode;
        }

        AppLog.Write("inserttest", "end", $"exit={exitCode}");
        Shutdown(exitCode);
    }

    // Overlay Test ohne Hook und ohne Mikrofon: Das Overlay zeigt nacheinander Aufnahme (mit laufendem Pegel), Verarbeitung, Fehler
    // und Ruhe, jeden Zustand gleich lang. Dabei wird alle 50 ms geprüft, dass das Overlay nie das Fenster vorn ist und dass sein
    // Fenster die Stile "nie aktivieren" und "Klicks gehen hindurch" trägt. Mit --shots Ordner wird je Zustand ein Bild der Pille
    // geschrieben (aus dem Fenster selbst gezeichnet, kein Bildschirmfoto). Exit 0, bei einem Verstoß oder Fehler 2.
    private void StartOverlayTest(int seconds, string? shotsDirectory)
    {
        AppLog.Write("overlaytest", "begin", $"sekunden={seconds}");

        OverlayWindow overlay;
        try
        {
            overlay = new OverlayWindow();
        }
        catch (Exception)
        {
            AppLog.Write("overlaytest", "end", $"exit={DiagnosticErrorExitCode}");
            Shutdown(DiagnosticErrorExitCode);
            return;
        }

        TrayStatus[] order = [TrayStatus.Recording, TrayStatus.Processing, TrayStatus.Error, TrayStatus.Idle];
        int ticksPerState = Math.Max(4, seconds * 1000 / order.Length / 50);
        int tick = 0;
        int violations = 0;
        int shown = 0;
        bool hiddenInIdle = false;
        bool failed = false;

        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            try
            {
                int index = tick / ticksPerState;
                if (index >= order.Length)
                {
                    timer.Stop();
                    overlay.Close();
                    // Sichtbar in genau den drei Zuständen Aufnahme, Verarbeitung und Fehler, verborgen in Ruhe.
                    bool ok = !failed && violations == 0 && shown == 3 && hiddenInIdle;
                    AppLog.Write("overlay", "selftest", ok ? $"ok sichtbar={shown}" : $"fehler verstoesse={violations} sichtbar={shown} ruhe_verborgen={(hiddenInIdle ? "ja" : "nein")}");
                    int exitCode = ok ? 0 : DiagnosticErrorExitCode;
                    AppLog.Write("overlaytest", "end", $"exit={exitCode}");
                    Shutdown(exitCode);
                    return;
                }

                TrayStatus status = order[index];
                overlay.Present(status);
                if (status == TrayStatus.Recording) overlay.SetLevel(50 + (50 * Math.Sin(tick / 3.0)));

                if (tick % ticksPerState == ticksPerState / 2)
                {
                    if (status == TrayStatus.Idle) hiddenInIdle = !overlay.IsVisible;
                    else if (overlay.IsVisible) shown++;
                    if (!string.IsNullOrWhiteSpace(shotsDirectory) && status != TrayStatus.Idle) SaveOverlayShot(overlay, shotsDirectory, status);
                }

                // Das Overlay darf nie das Fenster vorn sein, und sein Fenster muss die Stile gegen Aktivierung und Klicks tragen.
                if (overlay.Handle != 0)
                {
                    if (Insertion.InsertNative.GetForegroundWindow() == overlay.Handle) violations++;
                    const int noActivateAndTransparent = 0x08000000 | 0x00000020;
                    if ((Insertion.InsertNative.GetWindowLong(overlay.Handle, -20) & noActivateAndTransparent) != noActivateAndTransparent) violations++;
                }

                tick++;
            }
            catch (Exception)
            {
                failed = true;
                tick = order.Length * ticksPerState;
            }
        };
        timer.Start();
    }

    // Schreibt ein Bild der Pille für die Sichtprüfung. Der Ordner kommt aus der Befehlszeile und steht nie im Protokoll.
    private static void SaveOverlayShot(OverlayWindow overlay, string directory, TrayStatus status)
    {
        Directory.CreateDirectory(directory);
        System.Windows.Media.Imaging.PngBitmapEncoder encoder = new();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(overlay.Render()));
        using FileStream stream = File.Create(Path.Combine(directory, $"overlay-{status.ToWord()}.png"));
        encoder.Save(stream);
    }

    // Prüft den Rückfall auf das Standardgerät: Der Start mit einer erfundenen Mikrofonkennung muss scheitern, der Start mit dem
    // Standardgerät direkt danach muss gelingen und eine halbe Sekunde aufnehmen. Der Clip bleibt im Arbeitsspeicher und wird genullt.
    // Protokoll: audio | fallbacktest | ok oder fehler mit der Nummer des Schritts. Exit 0 oder 2.
    private static async Task<int> RunFallbackTestAsync()
    {
        int step = 0;
        try
        {
            using WasapiAudioRecorder recorder = new();

            step = 1;
            bool rejected = false;
            try
            {
                await recorder.StartAsync("{talk-selftest-kein-geraet}", CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                rejected = true;
            }

            if (!rejected)
            {
                await recorder.CancelAsync(CancellationToken.None).ConfigureAwait(false);
                AppLog.Write("audio", "fallbacktest", $"fehler schritt={step}");
                return DiagnosticErrorExitCode;
            }

            step = 2;
            await recorder.StartAsync(null, CancellationToken.None).ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);
            AudioClip clip = await recorder.StopAsync(CancellationToken.None).ConfigureAwait(false);
            int samples = clip.Samples.Length;
            Array.Clear(clip.Samples);
            if (samples == 0)
            {
                AppLog.Write("audio", "fallbacktest", $"fehler schritt={step}");
                return DiagnosticErrorExitCode;
            }

            AppLog.Write("audio", "fallbacktest", $"ok samples={samples}");
            return 0;
        }
        catch (Exception)
        {
            AppLog.Write("audio", "fallbacktest", $"fehler schritt={step}");
            return DiagnosticErrorExitCode;
        }
    }

    // Audio Test ohne Fenster und ohne Hook.
    private void StartAudioTest(int seconds)
    {
        AppLog.Write("audiotest", "begin", $"sekunden={seconds}");
        RunDiagnostic("audiotest", () => RunAudioTestAsync(seconds));
    }

    // VAD Test ohne Fenster, ohne Hook und ohne Mikrofon.
    private void StartVadTest(string? wavPath)
    {
        AppLog.Write("vadtest", "begin");
        RunDiagnostic("vadtest", () => Task.FromResult(RunVadTest(wavPath)));
    }

    // Diagnose ohne Fenster: die Arbeit läuft auf dem Thread Pool, der UI Thread wartet nur auf das Ende und beendet die App mit dem Exitcode.
    private void RunDiagnostic(string category, Func<Task<int>> work)
    {
        _ = Task.Run(work).ContinueWith(
            finished =>
            {
                int exitCode = finished.Status == TaskStatus.RanToCompletion ? finished.Result : DiagnosticErrorExitCode;
                _ = Dispatcher.InvokeAsync(() =>
                {
                    AppLog.Write(category, "end", $"exit={exitCode}");
                    Shutdown(exitCode);
                });
            },
            TaskScheduler.Default);
    }

    // Geräteliste, Aufnahme vom Standardgerät, alle 500 ms der Pegel, danach Clip und Stilleerkennung. Nur Zahlen und feste Wörter im Protokoll.
    private static async Task<int> RunAudioTestAsync(int seconds)
    {
        try
        {
            MmDeviceSource deviceSource = new();
            IReadOnlyList<AudioDeviceInfo> devices = deviceSource.ListCaptureDevices();
            bool hasDefault = devices.Any(d => d.IsDefault);
            int bluetoothCount = devices.Count(d => d.LikelyBluetoothHandsFree);
            AppLog.Write("audio", "devices", $"count={devices.Count} default={ToWord(hasDefault)} bluetooth={bluetoothCount}");

            using WasapiAudioRecorder recorder = new();
            await recorder.StartAsync(null, CancellationToken.None).ConfigureAwait(false);

            int levelLines = Math.Max(1, (int)(seconds * 1000 / LevelLogInterval.TotalMilliseconds));
            for (int i = 0; i < levelLines; i++)
            {
                await Task.Delay(LevelLogInterval).ConfigureAwait(false);
                AppLog.Write(
                    "audio",
                    "level",
                    $"rms={recorder.LastRms.ToString("F2", CultureInfo.InvariantCulture)} peak={recorder.LastPeak.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            AudioClip clip = await recorder.StopAsync(CancellationToken.None).ConfigureAwait(false);
            long clipMs = (long)Math.Round(clip.Duration.TotalMilliseconds);
            AppLog.Write("audio", "clip", $"ms={clipMs} samples={clip.Samples.Length} truncated={ToWord(recorder.LastClipTruncated)}");

            // Die Stilleerkennung gehört zum Audio Test, ihr Fehlen aber nicht zum Aufnahmeteil: Exit bleibt 0, die Zeile steht trotzdem im Protokoll.
            LogVadForClip(clip);

            return 0;
        }
        catch (AudioRecorderException ex)
        {
            // Der Grund ist ein fester Zustandsname aus RecordingError, nie freier Text.
            AppLog.Write("audio", "error", $"grund={ex.Error}");
            return DiagnosticErrorExitCode;
        }
        catch (Exception)
        {
            AppLog.Write("audio", "error", $"grund={RecordingError.Unknown}");
            return DiagnosticErrorExitCode;
        }
    }

    // Stilleerkennung über den aufgenommenen Clip mit Standardoptionen. Fehlt das Modell, steht das im Protokoll; nachgeladen wird nie.
    private static void LogVadForClip(AudioClip clip)
    {
        try
        {
            using SileroVad vad = new(new VadOptions(), TalkPaths.VadModelPath);
            AppLog.Write("vad", "result", VadReport.Run(vad, clip).LogDetail);
        }
        catch (FileNotFoundException)
        {
            AppLog.Write("vad", "error", "grund=modell_fehlt");
        }
        catch (Exception)
        {
            AppLog.Write("vad", "error", "grund=unbekannt");
        }
    }

    // Liest die WAV Datei, lässt die Stilleerkennung laufen und protokolliert dieselbe Ergebniszeile wie der Audio Test.
    // Exit 0 bei Sprache, 3 ohne Sprache, 2 bei Fehler (Datei fehlt, Format passt nicht, Modell fehlt). Es wird nie etwas geschrieben.
    private static int RunVadTest(string? wavPath)
    {
        DiagnosticInput? input = LoadAndDetect(wavPath);
        if (input is null) return DiagnosticErrorExitCode;

        // Hier zählt nur das Ergebnis der Stilleerkennung, der Ausschnitt wird nicht gebraucht.
        Array.Clear(input.Trimmed.Samples);
        return input.Report.HasSpeech ? 0 : NoResultExitCode;
    }

    // Gemeinsamer Anfang von --vadtest und --stttest: WAV Datei lesen (vad | input), Stilleerkennung laufen lassen (vad | result) und den
    // Ausschnitt mit Sprache liefern. Bei einem Fehler steht vad | error im Protokoll und es kommt null zurück. Der ganze Clip wird danach
    // genullt; den Ausschnitt nullt der Aufrufer. Es wird nie etwas geschrieben.
    private static DiagnosticInput? LoadAndDetect(string? wavPath)
    {
        if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
        {
            AppLog.Write("vad", "error", "grund=datei_fehlt");
            return null;
        }

        AudioClip? clip = TryReadWaveForDiagnostics(wavPath, out string readError);
        if (clip is null)
        {
            AppLog.Write("vad", "error", $"grund={readError}");
            return null;
        }

        AppLog.Write("vad", "input", $"ms={RoundMs(clip.Duration)} samples={clip.Samples.Length}");

        try
        {
            using SileroVad vad = new(new VadOptions(), TalkPaths.VadModelPath);
            VadReport report = VadReport.RunAndTrim(vad, clip, out AudioClip trimmed);
            AppLog.Write("vad", "result", report.LogDetail);
            return new DiagnosticInput(report, trimmed);
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
        finally
        {
            Array.Clear(clip.Samples);
        }
    }

    // Räumt nach einem unerwarteten Fehler auf, so gut es geht, und beendet Talk mit Exit 1: Tray Symbol und Overlay weg,
    // Hook gelöst. Jeder Schritt ist einzeln abgesichert, damit ein zweiter Fehler das Beenden nicht aufhält.
    private void CrashExit(string kind)
    {
        if (Interlocked.Exchange(ref _crashing, 1) != 0) return;

        AppLog.Write("app", "crash", $"art={kind} beendet=ja");

        // Notnagel: Hängt das Aufräumen (geladene Grafikbibliothek, laufende Aufnahme), endet der Prozess nach fünf Sekunden hart.
        bool cleaned = false;
        Thread watchdog = new(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
            if (!Volatile.Read(ref cleaned)) System.Diagnostics.Process.GetCurrentProcess().Kill();
        })
        { IsBackground = true };
        watchdog.Start();

        try { _tray?.Dispose(); } catch { }
        try { _overlay?.Close(); } catch { }
        try { _service?.Dispose(); } catch { }
        try { _ducker?.RestoreAndWait(TimeSpan.FromSeconds(1)); } catch { }
        try { _history?.FlushAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        Volatile.Write(ref cleaned, true);

        // Talk verschwindet sonst stumm aus dem Tray, und Caps Lock schaltet wieder die Großschreibung. Ein fester Satz sagt es,
        // ohne den Text der Ausnahme. Bei den Prüfschaltern gibt es keine Meldung, dort zählt nur der Exitcode.
        if (!_headless)
        {
            try
            {
                MessageBoxW(0, "Talk ist auf einen unerwarteten Fehler gestoßen und wurde beendet. Bitte starte Talk neu.", "Talk", 0x00000010);
            }
            catch
            {
                // Dann endet Talk ohne Meldung.
            }
        }

        Environment.Exit(1);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    // Prüft Setzen, Lesen und Entfernen des Autostart Eintrags an einem eigenen Prüfschlüssel (HKCU\Software\Talk\SelfTest).
    // Der echte Schlüssel, aus dem Windows Programme startet, wird dabei nie angefasst. Exit 0 oder 2.
    private static int RunAutostartTest()
    {
        const string testKey = @"Software\Talk\SelfTest";
        const string testValue = "TalkSelfTest";
        const string fakeExe = @"X:\Pruefordner mit Leerzeichen\Talk.exe";
        int step = 0;

        try
        {
            step = 1;
            if (!Autostart.Set(false, null, testKey, testValue) || Autostart.IsEnabled(testKey, testValue)) return AutostartTestFailed(step);

            step = 2;
            if (!Autostart.Set(true, fakeExe, testKey, testValue)) return AutostartTestFailed(step);
            if (!Autostart.IsEnabled(testKey, testValue) || !Autostart.PointsTo(fakeExe, testKey, testValue)) return AutostartTestFailed(step);
            if (Autostart.PointsTo(@"X:\Anderswo\Talk.exe", testKey, testValue)) return AutostartTestFailed(step);

            step = 3;
            if (Autostart.Set(true, null, testKey, testValue)) return AutostartTestFailed(step);

            step = 4;
            if (!Autostart.Set(false, null, testKey, testValue) || Autostart.IsEnabled(testKey, testValue)) return AutostartTestFailed(step);

            AppLog.Write("autostart", "selftest", "ok");
            return 0;
        }
        catch (Exception)
        {
            return AutostartTestFailed(step);
        }
        finally
        {
            try
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testKey, throwOnMissingSubKey: false);

                // Den Elternschlüssel hat der Test mit angelegt. Ist er danach leer, verschwindet er auch.
                using Microsoft.Win32.RegistryKey? parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Talk");
                bool empty = parent is not null && parent.SubKeyCount == 0 && parent.ValueCount == 0;
                parent?.Dispose();
                if (empty) Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(@"Software\Talk", throwOnMissingSubKey: false);
            }
            catch
            {
                // Aufräumen nach bestem Bemühen.
            }
        }
    }

    private static int AutostartTestFailed(int step)
    {
        AppLog.Write("autostart", "selftest", $"fehler schritt={step}");
        return DiagnosticErrorExitCode;
    }

    // Durchsucht das Protokoll (und die eine alte Datei daneben) nach Zeilen, die nicht nur aus Zeitstempel, festen Wörtern und
    // Zahlen bestehen. So fällt auf, wenn je ein diktierter Text, ein Pfad, ein Gerätename oder eine Fehlermeldung hineingeraten ist.
    // Auffällige Zeilen werden nur gezählt, nie ausgegeben. Protokoll: log | check | zeilen=N auffaellig=M. Exit 0 ohne Fund, 3 mit, 2 bei Fehler.
    private static int RunLogCheck()
    {
        try
        {
            int lines = 0;
            int dirty = 0;
            int legacy = 0;
            foreach (string path in new[] { AppLog.PreviousLogFile, TalkPaths.LogPath })
            {
                if (!File.Exists(path)) continue;

                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new(stream);
                while (reader.ReadLine() is { } line)
                {
                    lines++;
                    if (LogLineRule.IsClean(line)) continue;

                    // Zeilen im Format der ersten Tage zählen eigens: anderes Format, aber kein Inhalt.
                    if (LogLineRule.IsLegacy(line)) legacy++;
                    else dirty++;
                }
            }

            AppLog.Write("log", "check", $"zeilen={lines} auffaellig={dirty} altformat={legacy}");
            return dirty == 0 ? 0 : NoResultExitCode;
        }
        catch (Exception)
        {
            AppLog.Write("log", "check", "fehler");
            return DiagnosticErrorExitCode;
        }
    }

    // Prüft Lesen und Speichern der Einstellungen mit einer eigenen Prüfdatei; die echte settings.json wird nie angefasst.
    // Schritte: fehlende Datei ergibt Standardwerte, Speichern und Zurücklesen, fremde Schlüssel bleiben erhalten, eine Datei mit
    // Tippfehler wird nicht überschrieben, eine gesperrte Datei wird als Lesefehler gemeldet und nicht überschrieben, ein Wert
    // außerhalb des erlaubten Bereichs wird begrenzt. Protokoll: settings | selftest | ok oder fehler mit der Nummer des Schritts.
    private static int RunSettingsTest()
    {
        string path = Path.Combine(TalkPaths.AppDataDirectory, "settings-selftest.json");
        int step = 0;

        try
        {
            foreach (string leftover in new[] { path, path + ".tmp" })
            {
                if (File.Exists(leftover)) File.Delete(leftover);
            }

            // Schritt 1: Fehlt die Datei, gelten die Standardwerte, und das ist kein Lesefehler.
            step = 1;
            if (AppSettingsService.Load(path, out bool failed) != new DictationSettings() || failed) return SettingsTestFailed(step);

            // Schritt 2: Speichern und zurücklesen.
            step = 2;
            DictationSettings wanted = new("{pruefgeraet}", "it", 500, true, true, false, false);
            if (!AppSettingsService.Save(path, wanted)) return SettingsTestFailed(step);
            if (AppSettingsService.Load(path, out failed) != wanted || failed) return SettingsTestFailed(step);

            // Schritt 3: Fremde Schlüssel wie der Modellordner bleiben beim Speichern erhalten.
            step = 3;
            File.WriteAllText(path, "{\n  \"modelsDirectory\": \"X:\\\\Pruefmodelle\",\n  \"whisperRuntime\": \"vulkan\"\n}");
            if (!AppSettingsService.Save(path, wanted with { Language = "en" })) return SettingsTestFailed(step);
            using (System.Text.Json.JsonDocument saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
            {
                System.Text.Json.JsonElement root = saved.RootElement;
                if (root.GetProperty("modelsDirectory").GetString() != "X:\\Pruefmodelle"
                    || root.GetProperty("whisperRuntime").GetString() != "vulkan"
                    || root.GetProperty("language").GetString() != "en")
                {
                    return SettingsTestFailed(step);
                }
            }

            // Schritt 4: Eine Datei mit Tippfehler (schließende Klammer fehlt) ergibt Standardwerte und wird nicht überschrieben.
            step = 4;
            string broken = "{\n  \"modelsDirectory\": \"X:\\\\Pruefmodelle\"\n";
            File.WriteAllText(path, broken);
            if (AppSettingsService.Load(path, out failed) != new DictationSettings() || failed) return SettingsTestFailed(step);
            if (AppSettingsService.Save(path, wanted)) return SettingsTestFailed(step);
            if (File.ReadAllText(path) != broken || File.Exists(path + ".tmp")) return SettingsTestFailed(step);

            // Schritt 5: Eine gerade gesperrte Datei ist ein Lesefehler, Speichern scheitert, die Datei bleibt, wie sie war.
            step = 5;
            File.Delete(path);
            if (!AppSettingsService.Save(path, wanted)) return SettingsTestFailed(step);
            string before = File.ReadAllText(path);
            using (FileStream hold = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                AppSettingsService.Load(path, out failed);
                if (!failed) return SettingsTestFailed(step);
                if (AppSettingsService.Save(path, new DictationSettings())) return SettingsTestFailed(step);
            }

            if (File.ReadAllText(path) != before || AppSettingsService.Load(path, out failed) != wanted) return SettingsTestFailed(step);

            // Schritt 6: Ein Wert außerhalb des erlaubten Bereichs wird beim Lesen begrenzt.
            step = 6;
            File.WriteAllText(path, "{ \"doubleTapMs\": 99999, \"language\": \"xx\" }");
            DictationSettings limited = AppSettingsService.Load(path, out failed);
            if (limited.DoubleTapWindowMs != DictationSettings.MaxDoubleTapMs || limited.Language != DictationSettings.DefaultLanguage) return SettingsTestFailed(step);

            AppLog.Write("settings", "selftest", "ok");
            return 0;
        }
        catch (Exception)
        {
            // Die Meldung der Ausnahme wird nicht protokolliert, nur der Schritt.
            return SettingsTestFailed(step);
        }
        finally
        {
            foreach (string leftover in new[] { path, path + ".tmp" })
            {
                try
                {
                    if (File.Exists(leftover)) File.Delete(leftover);
                }
                catch
                {
                    // Aufräumen nach bestem Bemühen; ein Fehler steht dann schon im Protokoll.
                }
            }
        }
    }

    // Prüft das Schreiben der Wörterlisten (Playbook Abschnitt 27) in einem eigenen Prüfordner mit erfundenen Wörtern; die echten Listen
    // werden nie angefasst. Schritte: neue Datei mit Kopfzeile, Hinzufügen, Ergänzen, Sicherung des Tages vorhanden, Datei ohne BOM und
    // mit CRLF, eine Zeile von Hand bleibt beim nächsten Hinzufügen erhalten, Entfernen, ungültige Eingabe ändert nichts, gesperrte Datei
    // ergibt einen Fehler ohne Änderung, eine Datei in ANSI bleibt Byte für Byte und ohne Zwischendatei. Danach wird der Prüfordner entfernt. Protokoll: wordlist | selftest | ok oder fehler mit Schritt.
    private static int RunWordListTest()
    {
        string directory = Path.Combine(TalkPaths.AppDataDirectory, "woerter-selftest");
        int step = 0;
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
            WordListStore store = new(directory);
            string path = Path.Combine(directory, WordListStore.FileName(WordList.General));

            step = 1;
            if (store.Add(WordList.General, "Zentrix", ["Zentriks"]).Change != WordListChange.Added) return WordListTestFailed(step);
            string text = File.ReadAllText(path);
            if (!text.StartsWith("# ", StringComparison.Ordinal) || !text.EndsWith("Zentrix = Zentriks\r\n", StringComparison.Ordinal)) return WordListTestFailed(step);

            step = 2;
            if (store.Add(WordList.General, "zentrix", ["Zentrigs"]).Change != WordListChange.Extended) return WordListTestFailed(step);
            if (!File.ReadAllText(path).Contains("Zentrix = Zentriks | Zentrigs\r\n", StringComparison.Ordinal)) return WordListTestFailed(step);

            step = 3;
            if (!File.Exists($"{path}.sicherung-{DateTime.Now:yyyy-MM-dd}")) return WordListTestFailed(step);

            step = 4;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return WordListTestFailed(step);

            step = 5;
            File.AppendAllText(path, "# von Hand\r\nKorvaline\r\n");
            if (store.Add(WordList.General, "Fantasia", null).Change != WordListChange.Added) return WordListTestFailed(step);
            text = File.ReadAllText(path);
            if (!text.Contains("# von Hand\r\nKorvaline\r\nFantasia\r\n", StringComparison.Ordinal)) return WordListTestFailed(step);

            step = 6;
            if (store.Remove(WordList.General, "Korvaline").Change != WordListChange.Removed) return WordListTestFailed(step);
            if ((store.Entries(WordList.General) ?? []).Any(e => e.Term == "Korvaline") || !File.ReadAllText(path).Contains("# von Hand", StringComparison.Ordinal)) return WordListTestFailed(step);

            step = 7;
            string before = File.ReadAllText(path);
            if (store.Add(WordList.General, "A = B", null).Change != WordListChange.Invalid || File.ReadAllText(path) != before) return WordListTestFailed(step);

            step = 8;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                if (store.Add(WordList.General, "Gesperrt", null).Change != WordListChange.Invalid) return WordListTestFailed(step);
            }

            if (File.ReadAllText(path) != before) return WordListTestFailed(step);

            step = 9;
            string ansiPath = Path.Combine(directory, WordListStore.FileName(WordList.Private));
            byte[] ansi = [0x4D, 0xFC, 0x6C, 0x6C, 0x65, 0x72, 0x0D, 0x0A];
            File.WriteAllBytes(ansiPath, ansi);
            if (store.Add(WordList.Private, "Zentrix", null).Change != WordListChange.Invalid) return WordListTestFailed(step);
            if (!File.ReadAllBytes(ansiPath).SequenceEqual(ansi) || File.Exists(ansiPath + ".neu")) return WordListTestFailed(step);

            Directory.Delete(directory, recursive: true);
            AppLog.Write("wordlist", "selftest", "ok");
            return 0;
        }
        catch (Exception)
        {
            return WordListTestFailed(step);
        }
    }

    private static int WordListTestFailed(int step)
    {
        AppLog.Write("wordlist", "selftest", $"fehler schritt={step}");
        return DiagnosticErrorExitCode;
    }

    private static int SettingsTestFailed(int step)
    {
        AppLog.Write("settings", "selftest", $"fehler schritt={step}");
        return DiagnosticErrorExitCode;
    }

    // Prüft den verschlüsselten Verlauf mit einer eigenen Prüfdatei und erfundenen Texten; der echte Verlauf wird nie angefasst.
    // Schritte: schreiben und mit einer zweiten Instanz zurücklesen, der Klartext steht nicht in der Datei, ein alter Eintrag fällt
    // heraus, eine beschädigte Datei ergibt einen leeren Verlauf und wird beim nächsten Eintrag beiseitegelegt, Löschen entfernt alle
    // Dateien, abgestelltes Speichern schreibt keine Datei, eine gesperrte Datei bleibt unangetastet und wird später zusammengeführt.
    // Protokoll: history | selftest | ok oder fehler mit der Nummer des Schritts. Exit 0 oder 2.
    private static async Task<int> RunHistoryTestAsync()
    {
        string path = Path.Combine(TalkPaths.AppDataDirectory, "verlauf-selftest.bin");
        int step = 0;

        try
        {
            foreach (string leftover in HistoryTestFiles(path))
            {
                File.Delete(leftover);
            }

            // Schritt 1: zwei Einträge schreiben, mit einer zweiten Instanz zurücklesen, neuester zuerst.
            step = 1;
            string marker = "talk_selftest_" + Guid.NewGuid().ToString("N");
            EncryptedHistoryStore first = new(path);
            await first.AddAsync(new HistoryEntry(DateTimeOffset.Now.AddMinutes(-1), marker + " eins", "test", true), CancellationToken.None);
            await first.AddAsync(new HistoryEntry(DateTimeOffset.Now, marker + " zwei mit Umlaut ä", "test", false), CancellationToken.None);
            await first.FlushAsync();

            IReadOnlyList<HistoryEntry> read = await new EncryptedHistoryStore(path).GetRecentAsync(10, CancellationToken.None);
            if (read.Count != 2 || read[0].Text != marker + " zwei mit Umlaut ä" || read[1].Text != marker + " eins" || read[1].LocalOnly != true)
            {
                return HistoryTestFailed(step);
            }

            // Schritt 2: Der Klartext steht nicht in der Datei, weder als UTF 8 noch als UTF 16.
            step = 2;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(marker)) >= 0
                || bytes.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes(marker)) >= 0)
            {
                return HistoryTestFailed(step);
            }

            // Schritt 3: Ein Eintrag, der älter ist als 30 Tage, fällt beim Schreiben heraus.
            step = 3;
            EncryptedHistoryStore aging = new(path);
            await aging.AddAsync(new HistoryEntry(DateTimeOffset.Now.AddDays(-31), marker + " alt", "test", false), CancellationToken.None);
            await aging.FlushAsync();
            read = await new EncryptedHistoryStore(path).GetRecentAsync(10, CancellationToken.None);
            if (read.Count != 2 || read.Any(e => e.Text.EndsWith(" alt", StringComparison.Ordinal))) return HistoryTestFailed(step);

            // Schritt 4: Eine beschädigte Datei ergibt einen leeren Verlauf; beim nächsten Eintrag wird sie beiseitegelegt.
            step = 4;
            File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);
            EncryptedHistoryStore broken = new(path);
            if ((await broken.GetRecentAsync(10, CancellationToken.None)).Count != 0) return HistoryTestFailed(step);
            await broken.AddAsync(new HistoryEntry(DateTimeOffset.Now, marker + " neu", "test", false), CancellationToken.None);
            await broken.FlushAsync();
            if (HistoryTestFiles(path).Count(f => f.Contains(".unlesbar", StringComparison.Ordinal)) != 1) return HistoryTestFailed(step);
            read = await new EncryptedHistoryStore(path).GetRecentAsync(10, CancellationToken.None);
            if (read.Count != 1 || read[0].Text != marker + " neu") return HistoryTestFailed(step);

            // Schritt 5: Löschen leert den Verlauf und entfernt alle Dateien, auch die beiseitegelegte.
            step = 5;
            await broken.ClearAsync(CancellationToken.None);
            if (HistoryTestFiles(path).Count != 0 || (await broken.GetRecentAsync(10, CancellationToken.None)).Count != 0) return HistoryTestFailed(step);

            // Schritt 6: Ist das Speichern abgestellt, entsteht keine Datei, der Eintrag steht nur im Arbeitsspeicher.
            step = 6;
            EncryptedHistoryStore memoryOnly = new(path, persist: false);
            await memoryOnly.AddAsync(new HistoryEntry(DateTimeOffset.Now, marker + " fluechtig", "test", false), CancellationToken.None);
            await memoryOnly.FlushAsync();
            if (HistoryTestFiles(path).Count != 0 || (await memoryOnly.GetRecentAsync(10, CancellationToken.None)).Count != 1) return HistoryTestFailed(step);

            // Schritt 7: Eine gerade gesperrte Datei wird nicht beiseitegelegt und nicht überschrieben; nach der Freigabe wird zusammengeführt.
            step = 7;
            EncryptedHistoryStore seed = new(path);
            await seed.AddAsync(new HistoryEntry(DateTimeOffset.Now.AddMinutes(-5), marker + " vorher", "test", false), CancellationToken.None);
            await seed.FlushAsync();
            EncryptedHistoryStore locked;
            using (FileStream hold = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                locked = new EncryptedHistoryStore(path);
                await locked.AddAsync(new HistoryEntry(DateTimeOffset.Now, marker + " waehrend", "test", false), CancellationToken.None);
                await locked.FlushAsync();
                if (!locked.LastWriteFailed) return HistoryTestFailed(step);
            }

            await locked.AddAsync(new HistoryEntry(DateTimeOffset.Now, marker + " danach", "test", false), CancellationToken.None);
            await locked.FlushAsync();
            read = await new EncryptedHistoryStore(path).GetRecentAsync(10, CancellationToken.None);
            if (read.Count != 3 || HistoryTestFiles(path).Any(f => f.Contains(".unlesbar", StringComparison.Ordinal))) return HistoryTestFailed(step);

            AppLog.Write("history", "selftest", "ok");
            return 0;
        }
        catch (Exception)
        {
            // Die Meldung der Ausnahme wird nicht protokolliert, nur der Schritt.
            return HistoryTestFailed(step);
        }
        finally
        {
            foreach (string leftover in HistoryTestFiles(path))
            {
                try
                {
                    File.Delete(leftover);
                }
                catch
                {
                    // Aufräumen nach bestem Bemühen; ein Fehler steht dann schon im Protokoll.
                }
            }
        }
    }

    // Alle Dateien, die zur Prüfdatei gehören: sie selbst, die Zwischendatei und beiseitegelegte unlesbare Dateien.
    private static List<string> HistoryTestFiles(string path) =>
        [.. Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")];

    private static int HistoryTestFailed(int step)
    {
        AppLog.Write("history", "selftest", $"fehler schritt={step}");
        return DiagnosticErrorExitCode;
    }

    // Prüfung der Schlüsselablage ohne Fenster. Fasst nur den eigenen Eintrag "Talk:SelfTest" an.
    private void StartSecretTest()
    {
        AppLog.Write("secrettest", "begin");
        RunDiagnostic("secrettest", () => Task.FromResult(RunSecretTest()));
    }

    // Schreibt den Eintrag "Talk:SelfTest" mit erfundenen Werten, liest ihn zurück, vergleicht, überschreibt, prüft die Grenzen, entfernt ihn
    // und prüft, dass er weg ist. Protokoll: secret | selftest | ok oder fehler mit der Nummer des Schritts. Die Werte selbst stehen nirgends.
    // Exit 0 oder 2. Der Eintrag des Groq Schlüssels wird nie gelesen, geschrieben oder entfernt.
    private static int RunSecretTest()
    {
        CredentialSecretStore store = new();
        int step = 0;

        try
        {
            // Schritt 1: Entfernen wirft nicht, auch wenn kein Eintrag da ist; danach ist nichts zu lesen.
            step = 1;
            store.Remove(SelfTestSecretName);
            if (store.Get(SelfTestSecretName) is not null) return SecretTestFailed(step);

            // Schritt 2: Schreiben und zurücklesen.
            step = 2;
            string first = "talk_selftest_" + Guid.NewGuid().ToString("N");
            store.Set(SelfTestSecretName, first);
            if (!string.Equals(store.Get(SelfTestSecretName), first, StringComparison.Ordinal)) return SecretTestFailed(step);

            // Schritt 3: Überschreiben mit einem Wert der größten erlaubten Länge (40 mal 32 Zeichen sind 1.280).
            step = 3;
            string longest = string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString("N"), CredentialSecretStore.MaxSecretLength / 32));
            store.Set(SelfTestSecretName, longest);
            if (longest.Length != CredentialSecretStore.MaxSecretLength
                || !string.Equals(store.Get(SelfTestSecretName), longest, StringComparison.Ordinal))
            {
                return SecretTestFailed(step);
            }

            // Schritt 4: Ein leerer und ein zu langer Wert werden abgewiesen, der Eintrag bleibt dabei unverändert.
            step = 4;
            if (!SetIsRejected(store, string.Empty) || !SetIsRejected(store, longest + "x")) return SecretTestFailed(step);
            if (!string.Equals(store.Get(SelfTestSecretName), longest, StringComparison.Ordinal)) return SecretTestFailed(step);

            // Schritt 5: Entfernen, danach ist der Eintrag weg.
            step = 5;
            store.Remove(SelfTestSecretName);
            if (store.Get(SelfTestSecretName) is not null) return SecretTestFailed(step);

            // Schritt 6: Noch einmal entfernen wirft nicht.
            step = 6;
            store.Remove(SelfTestSecretName);

            AppLog.Write("secret", "selftest", "ok");
            return 0;
        }
        catch (Exception)
        {
            // Die Meldung der Ausnahme wird nicht protokolliert, nur der Schritt.
            return SecretTestFailed(step);
        }
        finally
        {
            try
            {
                store.Remove(SelfTestSecretName);
            }
            catch
            {
                // Aufräumen nach bestem Bemühen; ein Fehler steht dann schon im Protokoll.
            }
        }
    }

    private static int SecretTestFailed(int step)
    {
        AppLog.Write("secret", "selftest", $"fehler schritt={step}");
        return DiagnosticErrorExitCode;
    }

    // True, wenn die Ablage den Wert mit einer ArgumentException abweist.
    private static bool SetIsRejected(CredentialSecretStore store, string value)
    {
        try
        {
            store.Set(SelfTestSecretName, value);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // Glättet einen Prüftext über Groq und schreibt das Ergebnis nur in die Standardausgabe, nie ins Protokoll. Der Prüftext kommt
    // aus der Befehlszeile; dort nie echten Inhalt verwenden. "--level fehler|saetze|nachricht" wählt die Stufe (Standard saetze).
    // Protokoll: smooth | result oder skipped oder error, nur mit festen Wörtern und Zahlen.
    // Exit 0 geglättet, 3 nicht geglättet ohne Fehler (kein Schlüssel, zu kurz), 2 bei Fehler.
    private static async Task<int> RunSmoothTestAsync(string? text, string? levelWord, string? language, string? dialect)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return DiagnosticErrorExitCode;

            SmoothingLevel level = (levelWord ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "fehler" => SmoothingLevel.Correct,
                "nachricht" => SmoothingLevel.Message,
                _ => SmoothingLevel.Smooth,
            };

            using SpeechService speech = SpeechService.CreateDefault();

            // Auch der Prüfschalter hält sich an "Nur lokal" aus den Einstellungen: Dann wird nichts gesendet.
            // Sind die Einstellungen nicht lesbar (gesperrt oder kein gültiges JSON), gilt die vorsichtige Annahme "Nur lokal".
            bool smoothLocalOnly = true;
            try
            {
                string? settingsJson = File.Exists(TalkPaths.SettingsPath) ? File.ReadAllText(TalkPaths.SettingsPath) : null;
                if (DictationSettingsJson.IsReadable(settingsJson)) smoothLocalOnly = DictationSettingsJson.Read(settingsJson).LocalOnly;
            }
            catch (Exception)
            {
                // Bleibt bei "Nur lokal".
            }

            speech.LocalOnly = smoothLocalOnly;
            // Mundart mit "--dialect zh|be|bs|mix" (Playbook Abschnitt 26).
            string smoothLanguage = NormalizeLanguage(language);
            if (LanguageCodes.IsDialect(smoothLanguage)) smoothLanguage = LanguageCodes.DialectTag(dialect);
            SmoothingResult result = await speech.SmoothAsync(text, level, smoothLanguage, CancellationToken.None).ConfigureAwait(false);

            if (result.Applied)
            {
                AppLog.Write("smooth", "result", $"stufe={level.ToWord()} ms={result.Milliseconds} zeichen_ein={text.Length} zeichen_aus={result.Text.Length}");
                StandardOutput.WriteLine(result.Text);
                return 0;
            }

            bool skipped = result.Outcome is SmoothingOutcome.TooShort or SmoothingOutcome.TooLong or SmoothingOutcome.NoKey or SmoothingOutcome.LocalOnly;
            string status = result.HttpStatus > 0 ? $" status={result.HttpStatus}" : string.Empty;
            AppLog.Write("smooth", skipped ? "skipped" : "error", $"grund={result.Outcome.ToWord()}{status}");
            return skipped ? NoResultExitCode : DiagnosticErrorExitCode;
        }
        catch (Exception)
        {
            AppLog.Write("smooth", "error", "grund=unbekannt");
            return DiagnosticErrorExitCode;
        }
    }

    // Prüfung des hinterlegten Groq Schlüssels ohne Fenster. Der Schlüssel wird nur vom Programm gelesen, nie angezeigt oder protokolliert.
    private void StartKeyCheck()
    {
        AppLog.Write("keycheck", "begin");
        RunDiagnostic("keycheck", RunKeyCheckAsync);
    }

    // Protokoll key | check | ergebnis=gueltig/ungueltig/keine_verbindung/fehlt (geschrieben von SpeechService).
    // Exit 0 gültig, 3 abgelehnt oder nicht hinterlegt, 2 keine Verbindung.
    private static async Task<int> RunKeyCheckAsync()
    {
        try
        {
            using SpeechService speech = SpeechService.CreateDefault();
            KeyCheckResult? result = await speech.CheckKeyAsync(null, CancellationToken.None).ConfigureAwait(false);
            return result switch
            {
                KeyCheckResult.Valid => 0,
                KeyCheckResult.Invalid or KeyCheckResult.Missing => NoResultExitCode,
                _ => DiagnosticErrorExitCode,
            };
        }
        catch (Exception)
        {
            return DiagnosticErrorExitCode;
        }
    }

    // Erkennungstest ohne Fenster, ohne Hook und ohne Mikrofon.
    private void StartSttTest(string? wavPath, bool localOnly, string? language, string? vocabulary, int repeat, SttPipeline pipeline)
    {
        AppLog.Write("stttest", "begin");
        RunDiagnostic("stttest", () => RunSttTestAsync(wavPath, localOnly, language, vocabulary, repeat, pipeline));
    }

    // Stufe aus der Befehlszeile: fehler, saetze, nachricht oder aus; fehlt der Wert, null. Unbekannt wirft, damit ein Tippfehler nicht
    // still eine andere Stufe misst.
    private static SmoothingLevel? ParseLevel(string? word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" => null,
        "fehler" => SmoothingLevel.Correct,
        "saetze" => SmoothingLevel.Smooth,
        "nachricht" => SmoothingLevel.Message,
        "aus" => SmoothingLevel.Off,
        _ => throw new ArgumentException("Unbekannte Stufe."),
    };

    // Was --stttest nach der Erkennung noch tut: Nachbearbeitung (Post), Glättung mit Stufe (Level: fehler, saetze, nachricht) und
    // der Dialekt für Mundart.
    private sealed record SttPipeline(bool Post, string? Level, string? Dialect);

    // Nachbearbeitung und Glättung wie beim Diktat, ohne Wörterlisten des Nutzers. Geglättet wird nur ein Text, den Groq erkannt hat,
    // nie bei "--local". Bei Mundart wird immer umgeschrieben, ohne Stufe mit Stufe Fehler. Scheitert die Glättung, bleibt der Text.
    private static async Task<string> RunSttPipelineAsync(SpeechService speech, TranscriptionResult result, string language, bool localOnly, SttPipeline pipeline)
    {
        string text = new PostProcessor().ProcessDetailed(result.Text, new PostProcessingOptions(Language: language, CapitalizeSentences: true)).Text;

        SmoothingLevel? level = ParseLevel(pipeline.Level);
        SmoothingPlan plan = SmoothingPlan.For(level is not null && level != SmoothingLevel.Off, level ?? SmoothingLevel.Off, null, language, pipeline.Dialect);
        bool online = !localOnly && string.Equals(result.Engine, GroqTranscriber.EngineName, StringComparison.Ordinal);
        if (!online || !plan.Wanted || plan.OffForApp || !InsertText.IsInsertable(text)) return text;

        bool dialect = plan.DialectRewrite;
        SmoothingResult smooth = await speech.SmoothAsync(text, plan.Level, plan.Language, CancellationToken.None).ConfigureAwait(false);
        AppLog.Write("stttest", "smooth", $"ergebnis={smooth.Outcome.ToWord()} ms={smooth.Milliseconds}");
        if (!smooth.Applied) return text;

        string smoothed = smooth.Text;
        if (LanguageCodes.IsSwiss(language) && !dialect) smoothed = SwissText.Helvetisms(smoothed, SwissWordLists.Helvetisms).Text;
        if (LanguageCodes.IsSwiss(language)) smoothed = SwissText.Spelling(smoothed);
        return SentenceCase.Apply(TextCleanup.Clean(smoothed));
    }

    // Liest die WAV Datei wie --vadtest, schneidet mit der Stilleerkennung zu und erkennt den Ausschnitt über dieselbe Verdrahtung wie das
    // Fenster. Ohne Sprache geht nichts an die Erkennung (stt | skipped, Exit 3). Der erkannte Text geht nur in die Standardausgabe, nie ins
    // Protokoll. Exit 0 mit Text, 3 bei leerem Text, 2 bei Fehler (stt | error). Mit localOnly geht nichts ins Internet.
    // Mit repeat größer 1 läuft dieselbe Erkennung mehrmals im selben Prozess, je Lauf eine Zeile stt | result; so zeigt sich die Zeit mit
    // geladenem Modell. Text und Exitcode stammen vom letzten Lauf. Nach dem ersten lokalen Lauf schreibt der SpeechService einmal
    // stt | local | geladen mit Ladezeit und Laufzeit.
    private static async Task<int> RunSttTestAsync(string? wavPath, bool localOnly, string? language, string? vocabulary, int repeat, SttPipeline pipeline)
    {
        DiagnosticInput? input = LoadAndDetect(wavPath);
        if (input is null) return DiagnosticErrorExitCode;

        AudioClip clip = input.Trimmed;
        try
        {
            if (!input.Report.HasSpeech)
            {
                SttReport.WriteSkippedNoSpeech();
                return NoResultExitCode;
            }

            using SpeechService speech = SpeechService.CreateDefault();
            speech.LocalOnly = localOnly;

            // Wie im Fenster beim Start der Aufnahme: Verbindung vorab aufbauen. Passiert nur mit Schlüssel und nicht bei "nur lokal",
            // und hier erst, wenn feststeht, dass es Sprache gibt.
            await speech.WarmUpAsync().ConfigureAwait(false);

            string normalizedLanguage = NormalizeLanguage(language);
            IReadOnlyList<string> terms = VocabularyPrompt.Parse(vocabulary);
            long audioMs = RoundMs(clip.Duration);
            int runs = Math.Clamp(repeat, SttRepeatDefault, SttRepeatMax);
            TranscriptionResult? result = null;

            for (int run = 0; run < runs; run++)
            {
                result = await speech
                    .TranscribeAsync(clip, normalizedLanguage, terms, CancellationToken.None)
                    .ConfigureAwait(false);

                // Je Lauf eine Zeile, nur Zahlen und feste Wörter.
                SttReport.WriteResult(result, audioMs);
            }

            if (result is null || string.IsNullOrWhiteSpace(result.Text)) return NoResultExitCode;

            // Der einzige Ort, an den der erkannte Text hier geht, und nur der Text des letzten Laufs.
            // Fehlt die Standardausgabe, sagt der Exitcode trotzdem, dass Text erkannt wurde.
            string output = pipeline.Post ? await RunSttPipelineAsync(speech, result, normalizedLanguage, localOnly, pipeline).ConfigureAwait(false) : result.Text;
            StandardOutput.WriteLine(output);
            return 0;
        }
        catch (TranscriptionException ex)
        {
            SttReport.WriteError(ex);
            return DiagnosticErrorExitCode;
        }
        catch (Exception)
        {
            // Alles Unerwartete zählt als gescheiterter Erkenner. Die Meldung der Ausnahme wird nicht protokolliert.
            SttReport.WriteError(new TranscriptionException(TranscriptionError.EngineFailed, "Die Erkennung ist gescheitert."));
            return DiagnosticErrorExitCode;
        }
        finally
        {
            Array.Clear(clip.Samples);
        }
    }

    // "--lang de|it|en|de-ch|gsw|auto": fehlt der Wert oder ist er kein Sprachkürzel aus Buchstaben, gilt Deutsch.
    private static string NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultLanguage;

        string word = value.Trim().ToLowerInvariant();
        if (word == TranscriptionRequest.AutoLanguage) return TranscriptionRequest.AutoLanguage;
        if (DictationSettings.Languages.Contains(word)) return word;

        string sanitized = SttReport.SanitizeLanguage(word);
        return sanitized == SttReport.Unknown ? DefaultLanguage : sanitized;
    }

    // Ergebnis des gemeinsamen Anfangs von --vadtest und --stttest: die Zahlen der Stilleerkennung und der Ausschnitt mit Sprache.
    private sealed record DiagnosticInput(VadReport Report, AudioClip Trimmed);

    // Nur für die Diagnose per --vadtest: liest eine fertige WAV Datei mit dem NAudio WaveFileReader. Beim Diktat wird dieser Weg nie benutzt,
    // dort kommt Audio nur aus dem Mikrofon und bleibt im Arbeitsspeicher. Verlangt PCM, 16 Bit, 16000 Hz, mono; sonst null mit Grund "format".
    // Ein Lesefehler ergibt null mit Grund "lesen". Es wird nichts geschrieben.
    private static AudioClip? TryReadWaveForDiagnostics(string path, out string error)
    {
        try
        {
            using WaveFileReader reader = new(path);
            WaveFormat format = reader.WaveFormat;
            if (format.Encoding != WaveFormatEncoding.Pcm
                || format.BitsPerSample != 16
                || format.SampleRate != AudioClip.DefaultSampleRate
                || format.Channels != 1
                || reader.Length > int.MaxValue)
            {
                error = "format";
                return null;
            }

            byte[] bytes = new byte[reader.Length];
            int read = 0;
            while (read < bytes.Length)
            {
                int count = reader.Read(bytes, read, bytes.Length - read);
                if (count <= 0) break;
                read += count;
            }

            // Ein halbes Abtastwort am Ende wird verworfen.
            int evenBytes = read - (read % sizeof(short));
            short[] samples = MemoryMarshal.Cast<byte, short>(bytes.AsSpan(0, evenBytes)).ToArray();
            error = string.Empty;
            return new AudioClip(samples, AudioClip.DefaultSampleRate);
        }
        catch (IOException)
        {
            error = "lesen";
            return null;
        }
        catch (Exception)
        {
            // NAudio meldet eine Datei, die keine WAV Datei ist, als FormatException; alles andere ist hier ebenfalls ein Formatfehler.
            error = "format";
            return null;
        }
    }

    // Im Hook Test gibt es kein Fenster und keine Aufnahme: Töne und Protokoll reichen, die Verarbeitung endet nach 500 ms von selbst.
    private static void HandleHookTestAction(KeyboardHookService service, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.StartRecording:
                Beeper.Start();
                AppLog.Write("hotkey", "start");
                break;

            case HotkeyAction.StopRecording:
                Beeper.Stop();
                AppLog.Write("hotkey", "stop");
                _ = Task.Delay(ProcessingDelay).ContinueWith(
                    _ => service.Controller.MarkProcessingFinished(),
                    TaskScheduler.Default);
                break;

            case HotkeyAction.Cancel:
                Beeper.Cancel();
                AppLog.Write("hotkey", "cancel");
                break;

            default:
                return;
        }

        AppLog.Write("caps", "state", CapsLockState.LogDetail);
    }

    // Diagnose ohne Aktion: Umschalt plus Caps Lock wurde durchgelassen, die Großschreibung schaltet wie gewohnt.
    private static void HandleHookTestNotice(HookNotice notice)
    {
        if (notice == HookNotice.ShiftCapsPassthrough) AppLog.Write("hotkey", "shift-caps-passthrough");
    }

    private static KeyboardHookService CreateService(bool pushToTalk = false)
    {
        // Die Standardwerte kommen aus HotkeyOptions. Einstellungen speichern ist Schritt 7.
        return new KeyboardHookService(new HotkeyController(new HotkeyOptions(PushToTalkEnabled: pushToTalk)));
    }

    private static void LogInstalled(KeyboardHookService service)
    {
        if (service.IsInstalled) AppLog.Write("hook", "installed");
        else AppLog.Write("hook", "failed", $"fehler={service.LastInstallError}");

        AppLog.Write("caps", "state", CapsLockState.LogDetail);
    }

    private static string ToWord(bool value) => value ? "ja" : "nein";

    private static long RoundMs(TimeSpan time) => (long)Math.Round(time.TotalMilliseconds);

    private static bool HasSwitch(string[] args, string name)
    {
        return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }

    // "--hooktest 8" oder "--audiotest 3": die Zahl ist die Laufzeit in Sekunden. Fehlt sie oder ist sie unbrauchbar, gilt der Standard;
    // über der Obergrenze wird gekappt.
    private static bool TryGetSeconds(string[] args, string switchName, int defaultSeconds, int maxSeconds, out int seconds)
    {
        seconds = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], switchName, StringComparison.OrdinalIgnoreCase)) continue;

            seconds = defaultSeconds;
            if (i + 1 < args.Length
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && parsed > 0)
            {
                seconds = Math.Min(parsed, maxSeconds);
            }

            return true;
        }

        return false;
    }

    // "--vadtest Pfad": true, wenn der Schalter da ist; der Wert ist das nächste Argument, sofern es kein weiterer Schalter ist, sonst null.
    private static bool TryGetSwitchValue(string[] args, string switchName, out string? value)
    {
        value = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], switchName, StringComparison.OrdinalIgnoreCase)) continue;

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[i + 1];
            }

            return true;
        }

        return false;
    }
}
