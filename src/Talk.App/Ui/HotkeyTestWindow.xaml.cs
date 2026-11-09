// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using Talk.App.Dictation;
using Talk.App.Input;
using Talk.App.Settings;
using Talk.Core;
using Talk.Core.Audio;
using Talk.Core.Settings;

namespace Talk.App.Ui;

/// <summary>
/// Das Diagnosefenster: zeigt Zustand, Zähler, Feststelltaste, Hook, Pegel, Ergebnis der Stilleerkennung, den erkannten Text und
/// das Ergebnis des Einfügens, und erlaubt Mikrofon, Sprache, Wörterbuch, "nur lokal", Push to Talk und das Doppeltipp Fenster zu ändern.
/// </summary>
/// <remarks>
/// Das Fenster ist nur noch Anzeige und Eingabe. Der Ablauf des Diktats liegt in <see cref="DictationCoordinator"/>; das Fenster
/// hört auf dessen Ereignisse. Einstellungen ändert es nur über <see cref="AppSettingsService"/>, damit sie gespeichert werden und
/// im Einstellungsfenster gleich mitziehen. Schließen beendet Talk nicht; das macht das Tray Menü. Der Gerätename und der erkannte Text erscheinen ausschließlich hier, nie im Protokoll.
/// Typen, die Windows Forms gleichnamig mitbringt (Application, MessageBox), werden voll ausgeschrieben.
/// </remarks>
public partial class HotkeyTestWindow : Window
{
    private const string BluetoothHint = "Bluetooth Freisprechprofil erkannt, Aufnahmequalität kann schlecht sein";

    private readonly DictationCoordinator _coordinator;
    private readonly AppSettingsService _settings;
    private bool _ready;
    private bool _closed;

    internal HotkeyTestWindow(DictationCoordinator coordinator, AppSettingsService settings)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
        WindowTheme.Apply(this);

        // Das Fenster passt seine Höhe dem Inhalt an, aber nie über den Arbeitsbereich hinaus. Der Rest wird gerollt.
        MaxHeight = SystemParameters.WorkArea.Height;

        _coordinator.Changed += Render;
        _coordinator.LevelChanged += OnLevelChanged;

        _settings.Changed += RenderSettings;

        VocabularyBox.Text = _coordinator.VocabularyText;
        LoadDevices();
        RenderSettings();
        RefreshKeyState();
        _ready = true;

        Render();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;
        _coordinator.Changed -= Render;
        _coordinator.LevelChanged -= OnLevelChanged;
        _settings.Changed -= RenderSettings;
    }

    // Überträgt die geltenden Einstellungen in die Steuerelemente, ohne dass deren Ereignisse wieder speichern.
    private void RenderSettings()
    {
        if (_closed) return;

        bool before = _ready;
        _ready = false;
        try
        {
            DictationSettings s = _settings.Current;
            DoubleTapBox.Text = s.DoubleTapWindowMs.ToString(CultureInfo.InvariantCulture);
            PushToTalkBox.IsChecked = s.PushToTalk;
            LocalOnlyBox.IsChecked = s.LocalOnly;
            SelectLanguage(s.Language);
            SelectDevice(s.MicrophoneId);
        }
        finally
        {
            _ready = before;
        }
    }

    // Überträgt den Stand des Ablaufs in die Anzeige. Kommt bei jeder Änderung und im Sekundentakt.
    private void Render()
    {
        if (_closed) return;

        StateText.Text = _coordinator.Status switch
        {
            TrayStatus.Recording => "Aufnahme",
            TrayStatus.Processing => "Verarbeitung",
            TrayStatus.Error => "Fehler",
            _ => "Ruhe",
        };

        StartCountText.Text = _coordinator.StartCount.ToString(CultureInfo.InvariantCulture);
        StopCountText.Text = _coordinator.StopCount.ToString(CultureInfo.InvariantCulture);
        CancelCountText.Text = _coordinator.CancelCount.ToString(CultureInfo.InvariantCulture);

        CapsText.Text = CapsLockState.IsOn ? "Feststelltaste: an" : "Feststelltaste: aus";
        HookText.Text = _coordinator.Service.IsInstalled
            ? "Hook: aktiv"
            : $"Hook: nicht aktiv (Windows Fehler {_coordinator.Service.LastInstallError})";
        ReinstallText.Text = _coordinator.ReinstallLine;

        ResultText.Text = _coordinator.ResultLine;
        LocalModelText.Text = _coordinator.LocalModelLine;
        LocalModelText.Foreground = _coordinator.LocalModelWarning ? (System.Windows.Media.Brush)FindResource("WarnBrush") : (System.Windows.Media.Brush)FindResource("TextMutedBrush");

        if (!string.Equals(TranscriptBox.Text, _coordinator.Transcript, StringComparison.Ordinal)) TranscriptBox.Text = _coordinator.Transcript;
        bool hasRaw = _coordinator.RawTranscript.Length > 0;
        RawText.Text = hasRaw ? $"Roh: {_coordinator.RawTranscript}" : string.Empty;
        RawText.Visibility = hasRaw ? Visibility.Visible : Visibility.Collapsed;
        bool hasUnsmoothed = _coordinator.UnsmoothedTranscript.Length > 0;
        UnsmoothedText.Text = hasUnsmoothed ? $"Vor der Glättung: {_coordinator.UnsmoothedTranscript}" : string.Empty;
        UnsmoothedText.Visibility = hasUnsmoothed ? Visibility.Visible : Visibility.Collapsed;
        bool hasSmooth = _coordinator.SmoothLine.Length > 0;
        SmoothText.Text = _coordinator.SmoothLine;
        SmoothText.Visibility = hasSmooth ? Visibility.Visible : Visibility.Collapsed;
        SttText.Text = _coordinator.SttLine;
        InsertInfoText.Text = _coordinator.InsertLine;
        RetryButton.IsEnabled = _coordinator.CanRetry;
    }

    private void OnLevelChanged(double level)
    {
        if (!_closed) LevelBar.Value = level;
    }

    // Füllt die Geräteliste, Standardgerät zuerst. Gewählt ist das Mikrofon aus den Einstellungen, sonst das Standardgerät.
    private void LoadDevices()
    {
        bool before = _ready;
        _ready = false;
        try
        {
            DeviceBox.ItemsSource = _coordinator.ListDevices();
            SelectDevice(_settings.Current.MicrophoneId);
        }
        finally
        {
            _ready = before;
        }
    }

    private void SelectDevice(string? microphoneId)
    {
        IReadOnlyList<AudioDeviceInfo> devices = DeviceBox.ItemsSource as IReadOnlyList<AudioDeviceInfo> ?? [];
        DeviceBox.SelectedItem = devices.FirstOrDefault(d => string.Equals(d.Id, microphoneId, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(d => d.IsDefault)
            ?? devices.FirstOrDefault();
        UpdateDeviceHint();
    }

    private void UpdateDeviceHint()
    {
        AudioDeviceInfo? device = DeviceBox.SelectedItem as AudioDeviceInfo;
        if (device is null)
        {
            DeviceHintText.Text = "Kein Aufnahmegerät gefunden";
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else if (device.LikelyBluetoothHandsFree)
        {
            DeviceHintText.Text = BluetoothHint;
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else
        {
            DeviceHintText.Visibility = Visibility.Collapsed;
        }
    }

    // Übernimmt das gewählte Gerät in die Einstellungen. Das Standardgerät wird als null gespeichert, damit Talk einem neuen Standardgerät folgt.
    private void UpdateDevice()
    {
        UpdateDeviceHint();
        if (!_ready) return;

        string? id = DeviceBox.SelectedItem is AudioDeviceInfo { IsDefault: false } device ? device.Id : null;
        _settings.Apply(_settings.Current with { MicrophoneId = id });
    }

    private void SelectLanguage(string language)
    {
        foreach (object item in LanguageBox.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } && string.Equals(tag, language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                return;
            }
        }
    }

    private void OnDeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDevice();
    }

    private void OnReloadDevicesClick(object sender, RoutedEventArgs e)
    {
        LoadDevices();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && LanguageBox.SelectedItem is ComboBoxItem { Tag: string tag }) _settings.Apply(_settings.Current with { Language = tag });
    }

    private void OnVocabularyChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) _coordinator.VocabularyText = VocabularyBox.Text;
    }

    // Öffnet den Schlüsseldialog über diesem Fenster. Danach wird nur neu gelesen, ob ein Schlüssel hinterlegt ist.
    private void OnKeyDialogClick(object sender, RoutedEventArgs e)
    {
        KeyDialog dialog = new(_coordinator.Speech) { Owner = this };
        dialog.ShowDialog();
        if (!_closed) RefreshKeyState();
    }

    // Der Schalter wird je Erkennung einmal am Anfang gelesen: Mit Haken geht ab dann nichts mehr ins Internet (keine Erkennung über Groq,
    // kein Verbindungsaufbau, keine Schlüsselprüfung). Eine schon laufende Anfrage läuft zu Ende (Entscheid 37).
    private void OnLocalOnlyChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _settings.Apply(_settings.Current with { LocalOnly = LocalOnlyBox.IsChecked == true });
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        await _coordinator.RetryAsync();
    }

    // Zeigt nur, ob ein Schlüssel hinterlegt ist. Der Wert wird nie gelesen oder angezeigt.
    private void RefreshKeyState()
    {
        KeyStateText.Text = _coordinator.Speech.HasKey ? "Schlüssel hinterlegt: ja" : "Schlüssel hinterlegt: nein";
    }

    private void OnOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) ApplyOptions();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ApplyOptions();
    }

    // Öffnet den Verlauf als eigenes Fenster. Bis das Tray Menü da ist (Teil 7c), ist das der Weg dorthin.
    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        new HistoryWindow(_coordinator.History) { Owner = this }.Show();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyOptions()
    {
        if (!int.TryParse(DoubleTapBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int windowMs) || windowMs <= 0)
        {
            System.Windows.MessageBox.Show(this, "Bitte eine ganze Zahl größer als 0 eingeben.", "Doppeltipp Fenster");
            DoubleTapBox.Text = _settings.Current.DoubleTapWindowMs.ToString(CultureInfo.InvariantCulture);
            return;
        }

        _settings.Apply(_settings.Current with { DoubleTapWindowMs = windowMs, PushToTalk = PushToTalkBox.IsChecked == true });

        // Die Einstellungen begrenzen den Wert auf 150 bis 1000; das Feld zeigt, was wirklich gilt.
        RenderSettings();
    }
}
