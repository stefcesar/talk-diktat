// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;

using Talk.App.Diagnostics;
using Talk.App.Dictation;
using Talk.App.Feedback;
using Talk.App.History;
using Talk.Core.Hotkey;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Settings;

namespace Talk.App.Settings;

/// <summary>
/// Die eine Stelle für die Einstellungen des Nutzers: lesen aus <c>settings.json</c>, anwenden auf den laufenden Ablauf, speichern.
/// Einstellungsfenster, Diagnosefenster und Tray Menü ändern Einstellungen nur über <see cref="Apply"/> und hören auf
/// <see cref="Changed"/>. Gehört auf den UI Thread.
/// </summary>
/// <remarks>
/// Beim Speichern bleiben Schlüssel erhalten, die diese Klasse nicht kennt (Modellordner, Modell, Laufzeit). Im Protokoll stehen
/// nur feste Wörter: Die Kennung des Mikrofons gehört nicht ins Protokoll. Steht in der Datei etwas Unlesbares, wird sie nie
/// überschrieben; die Änderung gilt dann nur bis zum Beenden.
/// </remarks>
internal sealed class AppSettingsService
{
    private readonly DictationCoordinator _coordinator;
    private readonly EncryptedHistoryStore _history;
    private readonly string _path;
    private bool _loadFailed;

    public AppSettingsService(DictationCoordinator coordinator, EncryptedHistoryStore history, DictationSettings initial, string path)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _path = path ?? throw new ArgumentNullException(nameof(path));
        Current = (initial ?? new DictationSettings()).Normalize();
        ApplyToRuntime(Current);
    }

    /// <summary>Meldet, dass sich die Einstellungen geändert haben.</summary>
    public event Action? Changed;

    /// <summary>Die geltenden Einstellungen, immer normalisiert.</summary>
    public DictationSettings Current { get; private set; }

    /// <summary>
    /// Die Einstellungen aus der Datei. Fehlt sie oder ist ihr Inhalt kein JSON Objekt, gelten die Standardwerte.
    /// <paramref name="readFailed"/> ist wahr, wenn die Datei da ist, sich aber gerade nicht lesen ließ (gesperrt). Wirft nie.
    /// </summary>
    public static DictationSettings Load(string path, out bool readFailed)
    {
        readFailed = false;
        try
        {
            return DictationSettingsJson.Read(File.Exists(path) ? File.ReadAllText(path) : null);
        }
        catch (Exception)
        {
            readFailed = true;
            return new DictationSettings();
        }
    }

    /// <summary>
    /// Sagt dem Dienst, dass die Einstellungen beim Start nicht gelesen werden konnten. Vor dem ersten Speichern wird dann noch
    /// einmal gelesen, damit nicht Standardwerte über die Werte des Nutzers geschrieben werden.
    /// </summary>
    public void MarkLoadFailed() => _loadFailed = true;

    /// <summary>
    /// Wendet die Einstellungen an und speichert sie. Liefert false, wenn das Speichern gescheitert ist; angewendet sind sie dann
    /// trotzdem, bis Talk beendet wird.
    /// </summary>
    public bool Apply(DictationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        DictationSettings requested = settings.Normalize();
        if (requested == Current) return true;

        if (_loadFailed)
        {
            // Beim Start galten nur Standardwerte. Jetzt die Datei lesen und nur die Felder ändern, die der Nutzer gerade geändert hat.
            DictationSettings disk = Load(_path, out bool stillFailing);
            if (!stillFailing)
            {
                requested = Overlay(disk, Current, requested).Normalize();
                _loadFailed = false;
            }
        }

        Current = requested;
        ApplyToRuntime(requested);
        bool saved = !_loadFailed && Save(_path, requested);
        if (_loadFailed) AppLog.Write("settings", "error", "grund=lesen");
        Changed?.Invoke();
        return saved;
    }

    // Nimmt die Werte aus der Datei und darüber jedes Feld, das sich zwischen dem alten und dem gewünschten Stand geändert hat.
    private static DictationSettings Overlay(DictationSettings disk, DictationSettings old, DictationSettings requested) => disk with
    {
        MicrophoneId = requested.MicrophoneId != old.MicrophoneId ? requested.MicrophoneId : disk.MicrophoneId,
        Language = requested.Language != old.Language ? requested.Language : disk.Language,
        DoubleTapWindowMs = requested.DoubleTapWindowMs != old.DoubleTapWindowMs ? requested.DoubleTapWindowMs : disk.DoubleTapWindowMs,
        PushToTalk = requested.PushToTalk != old.PushToTalk ? requested.PushToTalk : disk.PushToTalk,
        LocalOnly = requested.LocalOnly != old.LocalOnly ? requested.LocalOnly : disk.LocalOnly,
        SoundsEnabled = requested.SoundsEnabled != old.SoundsEnabled ? requested.SoundsEnabled : disk.SoundsEnabled,
        HistoryEnabled = requested.HistoryEnabled != old.HistoryEnabled ? requested.HistoryEnabled : disk.HistoryEnabled,
        OverlayEnabled = requested.OverlayEnabled != old.OverlayEnabled ? requested.OverlayEnabled : disk.OverlayEnabled,
        SmoothingEnabled = requested.SmoothingEnabled != old.SmoothingEnabled ? requested.SmoothingEnabled : disk.SmoothingEnabled,
        Smoothing = requested.Smoothing != old.Smoothing ? requested.Smoothing : disk.Smoothing,
        DuckingEnabled = requested.DuckingEnabled != old.DuckingEnabled ? requested.DuckingEnabled : disk.DuckingEnabled,
        Dialect = requested.Dialect != old.Dialect ? requested.Dialect : disk.Dialect,
        // Bei der Liste je Programm nur die geänderten Einträge, sonst gingen Einträge aus der Datei verloren (Abschnitt 23, Regel 3a).
        SmoothingPerApp = AppSmoothingMap.MergeChanges(disk.SmoothingPerApp, old.SmoothingPerApp, requested.SmoothingPerApp),
    };

    private void ApplyToRuntime(DictationSettings settings)
    {
        _coordinator.DeviceId = settings.MicrophoneId;
        _coordinator.Language = settings.Language;
        _coordinator.Dialect = settings.Dialect;
        _coordinator.Speech.LocalLanguage = settings.Language;
        _coordinator.Speech.LocalOnly = settings.LocalOnly;
        Beeper.Enabled = settings.SoundsEnabled;
        _coordinator.SmoothingEnabled = settings.SmoothingEnabled;
        _coordinator.Smoothing = settings.Smoothing;
        _coordinator.SmoothingPerApp = settings.SmoothingPerApp ?? AppSmoothingMap.Empty;
        _history.SetPersisting(settings.HistoryEnabled);

        // Eine neue Steuerung verwirft eine laufende Aufnahme, deshalb nur bei einer echten Änderung.
        HotkeyOptions options = _coordinator.Service.Controller.Options;
        if (options.DoubleTapWindowMs != settings.DoubleTapWindowMs || options.PushToTalkEnabled != settings.PushToTalk)
        {
            _coordinator.ApplyOptions(settings.DoubleTapWindowMs, settings.PushToTalk);
        }
    }

    /// <summary>
    /// Schreibt die Einstellungen in die Datei und behält fremde Schlüssel. Liefert false, wenn die Datei etwas Unlesbares enthält
    /// (sie wird dann nicht angefasst) oder das Schreiben scheitert. Wirft nie.
    /// </summary>
    public static bool Save(string path, DictationSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string? existing = File.Exists(path) ? File.ReadAllText(path) : null;
            string? merged = DictationSettingsJson.Merge(existing, settings);
            if (merged is null)
            {
                // In der Datei steht etwas Unlesbares. Sie wird nicht überschrieben, sonst wäre der Modellordner darin verloren.
                AppLog.Write("settings", "error", "grund=datei_unlesbar");
                return false;
            }

            // Erst daneben schreiben, dann tauschen: Ein Absturz mitten im Schreiben lässt die alte Datei ganz.
            string temp = path + ".tmp";
            File.WriteAllText(temp, merged);
            File.Move(temp, path, overwrite: true);
            AppLog.Write("settings", "saved");
            return true;
        }
        catch (Exception)
        {
            AppLog.Write("settings", "error", "grund=schreiben");
            return false;
        }
    }
}
