// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using Talk.App.Dictation;
using Talk.App.Processing;
using Talk.App.Settings;
using Talk.Core.Audio;
using Talk.Core.History;
using Talk.Core.Localization;
using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Settings;
using Talk.Core.Transcription;

using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Talk.App.Ui;

/// <summary>
/// Das Einstellungsfenster: Mikrofon, Sprache, Push to Talk, Doppeltipp Fenster, Töne, Glättung, Wörterbuch, "nur lokal", Verlauf
/// speichern und der Schlüsseldialog. Jede Änderung geht sofort über <see cref="AppSettingsService.Apply"/> und wird gespeichert.
/// </summary>
/// <remarks>
/// Alles geht mit einzelnen Klicks; das Doppeltipp Fenster ist eine Wahl aus festen Werten. Der Gerätename erscheint nur hier,
/// nie im Protokoll.
/// </remarks>
public partial class SettingsWindow : Window
{
    private readonly AppSettingsService _settings;
    private readonly DictationCoordinator _coordinator;

    // Solange das Fenster seine Steuerelemente selbst setzt, lösen deren Ereignisse kein Speichern aus.
    private bool _rendering = true;
    private bool _closed;

    // Wörterbuch (Playbook Abschnitt 27): die gewählte Liste und der Zugriff auf die Dateien.
    private readonly WordListStore _wordStore;
    private WordList _wordList = WordList.Private;

    // "Wirklich entfernen?" gilt nur kurz; danach steht wieder "Entfernen", damit ein späterer Fehlklick nichts entfernt.
    private readonly System.Windows.Threading.DispatcherTimer _removeReset = new() { Interval = TimeSpan.FromSeconds(5) };

    // Die Sprache der Oberfläche beim Öffnen. Alle Texte dieses Fensters bleiben in ihr, auch nach einer neuen Wahl, damit das
    // Fenster nie gemischt spricht; es wechselt beim nächsten Öffnen (Playbook Abschnitt 29, Regel 11). Nur der Hinweis unter der
    // Auswahl steht in der neuen Sprache.
    private readonly string _lang = Lang.Current;
    private readonly CultureInfo _culture = Lang.Culture;

    /// <param name="settings">Die Einstellungen.</param>
    /// <param name="coordinator">Der Ablauf.</param>
    /// <param name="words">Die Wörterlisten; null für die echten in <c>%LOCALAPPDATA%\Talk</c>. Die Bilder der Diagnose geben eigene mit.</param>
    internal SettingsWindow(AppSettingsService settings, DictationCoordinator coordinator, WordListStore? words = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _wordStore = words ?? new WordListStore();
        InitializeComponent();
        WindowTheme.Apply(this);
        FillUiLanguages();
        // Höchstens 860 hoch und nie höher als der Arbeitsbereich; der Rest wird gerollt. So deckt das Fenster nicht den ganzen Bildschirm.
        MaxHeight = Math.Min(SystemParameters.WorkArea.Height, 860);

        _settings.Changed += Render;
        LoadDevices();
        Render();

        // Erst nach InitializeComponent wählen: Das Ereignis braucht die Steuerelemente darunter. Vorgewählt ist "Privat", damit ein
        // Personenname nicht aus Versehen als Hinweis an Groq geht (wie im Verlauf, Playbook Abschnitt 27).
        WordListPrivate.IsChecked = true;
        _removeReset.Tick += (_, _) => ResetRemoveButtons();

        // Diktiert der Nutzer bei offenem Fenster in ein neues Programm, kommt es unten in der Liste dazu.
        _coordinator.Changed += OnCoordinatorChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;
        _removeReset.Stop();
        _settings.Changed -= Render;
        _coordinator.Changed -= OnCoordinatorChanged;
    }

    // Überträgt die geltenden Einstellungen in die Steuerelemente.
    private void Render()
    {
        if (_closed) return;

        _rendering = true;
        try
        {
            DictationSettings s = _settings.Current;
            foreach (object item in LanguageBox.Items)
            {
                if (item is ComboBoxItem { Tag: string tag } && tag == s.Language) LanguageBox.SelectedItem = item;
            }

            foreach (object item in DialectBox.Items)
            {
                if (item is ComboBoxItem { Tag: string dialectTag } && dialectTag == s.Dialect) DialectBox.SelectedItem = item;
            }

            // Schweizerdeutsch und Mundart: ein Satz, was Talk schreibt; die Dialektwahl nur bei Mundart.
            string swissHint = s.Language switch
            {
                LanguageCodes.SwissStandard => T("settings.swiss.standard"),
                LanguageCodes.SwissDialect => T("settings.swiss.dialect"),
                _ => string.Empty,
            };
            SwissHintText.Text = swissHint;
            SwissHintText.Visibility = swissHint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            DialectPanel.Visibility = s.Language == LanguageCodes.SwissDialect ? Visibility.Visible : Visibility.Collapsed;

            // Vorauswahl ist die geltende Sprache der Oberfläche. Das Setzen hier speichert nichts, weil _rendering gesetzt ist.
            UiLanguageBox.SelectedItem = UiLanguageBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == Lang.Current);

            PushToTalkBox.IsChecked = s.PushToTalk;
            RenderDoubleTap(s.DoubleTapWindowMs);
            SoundsBox.IsChecked = s.SoundsEnabled;
            OverlayBox.IsChecked = s.OverlayEnabled;
            DuckingBox.IsChecked = s.DuckingEnabled;
            SmoothingBox.IsChecked = s.SmoothingEnabled;
            foreach (object item in SmoothingLevelBox.Items)
            {
                if (item is ComboBoxItem { Tag: string levelTag } && levelTag == s.Smoothing.ToString()) SmoothingLevelBox.SelectedItem = item;
            }

            // Die Glättung braucht einen Schlüssel und läuft nie bei "Nur lokal". Der Hinweis sagt, warum sie gerade nichts tut.
            string hint = !s.SmoothingEnabled ? string.Empty
                : s.LocalOnly ? T("settings.smoothing.local_only")
                : !_coordinator.Speech.HasKey ? T("settings.smoothing.no_key")
                : string.Empty;
            SmoothingHintText.Text = hint;
            SmoothingHintText.Visibility = hint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            RenderPerApp(s.SmoothingPerApp ?? AppSmoothingMap.Empty);
            LocalOnlyBox.IsChecked = s.LocalOnly;
            HistoryBox.IsChecked = s.HistoryEnabled;
            KeyStateText.Text = T(_coordinator.Speech.HasKey ? "key.stored.yes" : "key.stored.no");
            SelectDevice(s.MicrophoneId);
            RenderAutostart();
        }
        finally
        {
            _rendering = false;
        }
    }

    // Eine Zeile je Programm: beim Öffnen gespeicherte zuerst, dann die, in die seit dem Start diktiert wurde (Playbook Abschnitt 23,
    // Regel 12). Die Zeilen bleiben, wo sie sind: Neue Programme kommen unten dazu, und vorhandene Zeilen werden nie neu gebaut,
    // nur ihre Auswahl wird nachgeführt. Ein Neubau mitten in einem Klick hatte Klicks im Fenster verschluckt (Abschnitt 23, Handtest).
    private void RenderPerApp(AppSmoothingMap map)
    {
        foreach (string name in map.Entries.Select(e => e.Key).Concat(_coordinator.RecentApps))
        {
            if (!_perAppBoxes.ContainsKey(name)) AddPerAppRow(name);
        }

        bool before = _rendering;
        _rendering = true;
        try
        {
            foreach ((string name, ComboBox box) in _perAppBoxes)
            {
                SmoothingLevel? current = map.Get(name);
                box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Tag as SmoothingLevel?) == current);
            }
        }
        finally
        {
            _rendering = before;
        }

        PerAppEmptyText.Visibility = _perAppBoxes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private readonly Dictionary<string, ComboBox> _perAppBoxes = new(StringComparer.OrdinalIgnoreCase);

    // Nimmt neue Programme aus dem Ablauf auf, ohne vorhandene Zeilen anzufassen. Der Ablauf meldet sich auch im Sekundentakt;
    // ohne neues Programm passiert dann nichts.
    private void OnCoordinatorChanged()
    {
        if (_closed || _coordinator.RecentApps.All(_perAppBoxes.ContainsKey)) return;
        RenderPerApp(_settings.Current.SmoothingPerApp ?? AppSmoothingMap.Empty);
    }

    // Baut eine Zeile; die Auswahl setzt danach RenderPerApp. Das Ereignis hängt erst dran, wenn die Liste gefüllt ist.
    private void AddPerAppRow(string name)
    {
        string display = AppDisplayNames.For(name);

        // Name links, Auswahl rechts in fester Breite, damit alle Auswahllisten untereinander stehen.
        Grid row = new() { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        row.Children.Add(new TextBlock
        {
            Text = string.Equals(display, name, StringComparison.OrdinalIgnoreCase) ? display : F("common.name_with_detail", display, name),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 16, 0),
        });

        ComboBox box = new() { Tag = name };
        foreach ((string key, SmoothingLevel? level) in PerAppChoices) box.Items.Add(new ComboBoxItem { Content = T(key), Tag = level });

        box.SelectionChanged += OnPerAppChanged;
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        PerAppPanel.Children.Add(row);
        _perAppBoxes[name] = box;
    }

    private static readonly (string Key, SmoothingLevel? Level)[] PerAppChoices =
    [
        ("settings.level.default", null),
        ("settings.level.off", SmoothingLevel.Off),
        ("settings.level.correct", SmoothingLevel.Correct),
        ("settings.level.smooth", SmoothingLevel.Smooth),
        ("settings.level.message", SmoothingLevel.Message),
    ];

    private void OnPerAppChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rendering || sender is not ComboBox { Tag: string name } box || box.SelectedItem is not ComboBoxItem item) return;

        AppSmoothingMap map = _settings.Current.SmoothingPerApp ?? AppSmoothingMap.Empty;
        Save(_settings.Current with { SmoothingPerApp = map.With(name, item.Tag as SmoothingLevel?) });
    }

    private void Save(DictationSettings settings)
    {
        bool saved = _settings.Apply(settings);
        if (_closed) return;

        // Uhrzeit in der Kultur der Sprache (Regel 8); de-DE ergibt wie bisher 14:05:09.
        StatusText.Text = saved
            ? F("settings.saved", DateTime.Now.ToString("T", _culture))
            : T("settings.save_failed");
        StatusText.Foreground = saved ? (System.Windows.Media.Brush)FindResource("TextMutedBrush") : (System.Windows.Media.Brush)FindResource("WarnBrush");

        // Hinweise, die von mehreren Werten abhängen (Glättung), neu rechnen, auch wenn sich sonst nichts geändert hat.
        Render();
    }

    // Liest die Einstellungen aus den Steuerelementen. Das Doppeltipp Fenster hat eigene Knöpfe, und das Mikrofon ändert sich nur,
    // wenn der Nutzer es selbst wählt: Sonst würde ein gerade nicht angeschlossenes Mikrofon bei jedem anderen Klick vergessen.
    private DictationSettings FromControls() => _settings.Current with
    {
        Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? DictationSettings.DefaultLanguage,
        Dialect = (DialectBox.SelectedItem as ComboBoxItem)?.Tag as string ?? _settings.Current.Dialect,
        PushToTalk = PushToTalkBox.IsChecked == true,
        SoundsEnabled = SoundsBox.IsChecked == true,
        OverlayEnabled = OverlayBox.IsChecked == true,
        DuckingEnabled = DuckingBox.IsChecked == true,
        SmoothingEnabled = SmoothingBox.IsChecked == true,
        Smoothing = (SmoothingLevelBox.SelectedItem as ComboBoxItem)?.Tag is string level && Enum.TryParse(level, out SmoothingLevel parsed)
            ? parsed
            : _settings.Current.Smoothing,
        LocalOnly = LocalOnlyBox.IsChecked == true,
        HistoryEnabled = HistoryBox.IsChecked == true,
    };

    // Das Standardgerät wird als null gespeichert, damit Talk einem neuen Standardgerät folgt.
    private string? SelectedMicrophoneId() => DeviceBox.SelectedItem is AudioDeviceInfo { IsDefault: false } device ? device.Id : null;

    private void LoadDevices()
    {
        bool before = _rendering;
        _rendering = true;
        try
        {
            DeviceBox.ItemsSource = _coordinator.ListDevices();
            SelectDevice(_settings.Current.MicrophoneId);
        }
        finally
        {
            _rendering = before;
        }
    }

    private void SelectDevice(string? microphoneId)
    {
        IReadOnlyList<AudioDeviceInfo> devices = DeviceBox.ItemsSource as IReadOnlyList<AudioDeviceInfo> ?? [];
        AudioDeviceInfo? chosen = devices.FirstOrDefault(d => string.Equals(d.Id, microphoneId, StringComparison.OrdinalIgnoreCase));
        DeviceBox.SelectedItem = chosen ?? devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();

        if (devices.Count == 0)
        {
            DeviceHintText.Text = T("settings.device.none");
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else if (microphoneId is not null && chosen is null)
        {
            DeviceHintText.Text = T("settings.device.missing");
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else if ((DeviceBox.SelectedItem as AudioDeviceInfo)?.LikelyBluetoothHandsFree == true)
        {
            DeviceHintText.Text = T("settings.device.bluetooth");
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else
        {
            DeviceHintText.Visibility = Visibility.Collapsed;
        }
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_rendering) Save(FromControls());
    }

    private void OnDeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_rendering) Save(_settings.Current with { MicrophoneId = SelectedMicrophoneId() });
    }

    private void OnReloadDevicesClick(object sender, RoutedEventArgs e)
    {
        LoadDevices();
    }

    // Beim Abstellen fragt Talk, ob der schon gespeicherte Verlauf auch gelöscht werden soll.
    private async void OnHistoryUnchecked(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;

        Save(FromControls());

        MessageBoxResult answer = System.Windows.MessageBox.Show(
            this,
            T("settings.history_off.confirm"),
            T("history.title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            await _coordinator.History.ClearAsync(CancellationToken.None);
            if (_closed) return;
            StatusText.Text = T("settings.history.deleted");
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
        }
        catch (Exception)
        {
            if (_closed) return;
            StatusText.Text = T("history.clear_failed");
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("WarnBrush");
        }
    }

    // Das Doppeltipp Fenster als Wahl aus festen Werten, je ein Klick (DESIGN.md). Steht in settings.json ein anderer Wert, kommt er als
    // eigene Wahl dazu, damit nichts still verändert wird. Eine Wahl verschwindet nie, solange das Fenster offen ist: So wird die Reihe
    // nie mitten in einem Klick neu gebaut (das hatte früher Klicks verschluckt, Abschnitt 23, Regel 12), sondern nur, wenn der Wert
    // von außen kommt.
    private void RenderDoubleTap(int current)
    {
        int[] values = [.. DoubleTapChoices
            .Concat(DoubleTapPanel.Children.OfType<RadioButton>().Select(b => (int)b.Tag))
            .Append(current)
            .Distinct()
            .Order()];
        if (!DoubleTapPanel.Children.OfType<RadioButton>().Select(b => (int)b.Tag).SequenceEqual(values))
        {
            DoubleTapPanel.Children.Clear();
            foreach (int value in values)
            {
                RadioButton choice = new()
                {
                    Content = value.ToString(CultureInfo.InvariantCulture),
                    Tag = value,
                    GroupName = "DoubleTap",
                    Style = (Style)FindResource("ChoiceButton"),
                };
                choice.Checked += OnDoubleTapChecked;
                DoubleTapPanel.Children.Add(choice);
            }
        }

        foreach (RadioButton choice in DoubleTapPanel.Children.OfType<RadioButton>()) choice.IsChecked = (int)choice.Tag == current;
    }

    private static readonly int[] DoubleTapChoices = [250, 350, 500, 750];

    private void OnDoubleTapChecked(object sender, RoutedEventArgs e)
    {
        if (_rendering || sender is not RadioButton { Tag: int value }) return;
        Save(_settings.Current with { DoubleTapWindowMs = value });
    }

    // Der Autostart steht nicht in settings.json, sondern in der Registrierung von Windows. Der Haken zeigt, was dort wirklich steht.
    private void RenderAutostart()
    {
        bool enabled = Autostart.IsEnabled();
        AutostartBox.IsChecked = enabled;

        string? exe = Autostart.CurrentExePath;
        bool elsewhere = enabled && exe is not null && !Autostart.PointsTo(exe);
        AutostartHintText.Text = T("settings.autostart.elsewhere");
        AutostartHintText.Visibility = elsewhere ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;

        bool wanted = AutostartBox.IsChecked == true;
        bool ok = Autostart.Set(wanted, Autostart.CurrentExePath);
        StatusText.Text = ok
            ? T(wanted ? "settings.autostart.on" : "settings.autostart.off")
            : T("settings.autostart.failed");
        StatusText.Foreground = ok ? (System.Windows.Media.Brush)FindResource("TextMutedBrush") : (System.Windows.Media.Brush)FindResource("WarnBrush");

        _rendering = true;
        try
        {
            RenderAutostart();
        }
        finally
        {
            _rendering = false;
        }
    }

    private void OnKeyDialogClick(object sender, RoutedEventArgs e)
    {
        KeyDialog dialog = new(_coordinator.Speech) { Owner = this };
        dialog.ShowDialog();
        if (!_closed) Render();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // Wörterbuch: Wechsel der Liste. Beschriftung und Erklärung passen sich an, die Einträge der Liste werden gezeigt.
    private void OnWordListChecked(object sender, RoutedEventArgs e)
    {
        _wordList = sender == WordListPrivate ? WordList.Private : sender == WordListSwiss ? WordList.Swiss : WordList.General;
        WordStatusText.Visibility = Visibility.Collapsed;

        (WordListHintText.Text, WordTermLabel.Text, WordVariantsLabel.Text) = _wordList switch
        {
            WordList.Private => (T("settings.words.hint.private"), T("settings.words.term"), T("settings.words.variants")),
            WordList.Swiss => (T("settings.words.hint.swiss"), T("settings.words.term.swiss"), T("settings.words.variants.swiss")),
            _ => (T("settings.words.hint.general"), T("settings.words.term"), T("settings.words.variants")),
        };

        RenderWordEntries();
    }

    private void OnWordAddClick(object sender, RoutedEventArgs e)
    {
        string term = WordTermBox.Text.Trim();
        IReadOnlyList<string> variants = WordListEditor.SplitVariants(WordVariantsBox.Text);

        // In der Schweizer Liste wirkt ein Eintrag nur mit dem Wort aus Deutschland, das ersetzt werden soll.
        if (_wordList == WordList.Swiss && term.Length > 0 && variants.Count == 0)
        {
            ShowWordStatus(T("settings.words.swiss_variant_missing"), warn: true);
            WordVariantsBox.Focus();
            return;
        }

        WordListEditResult result = _wordStore.Add(_wordList, term, variants);

        string message = result.Change switch
        {
            WordListChange.Added => F("settings.words.added", term),
            WordListChange.Extended => F("settings.words.extended", result.StoredTerm),
            WordListChange.Unchanged => F("settings.words.unchanged", result.StoredTerm),
            _ => result.Problem is { } problem ? F(problem.Key, problem.Args) : T("settings.words.add_failed"),
        };

        // Kleingeschriebene Wörter sind in der Schweizer Liste der Normalfall (parkieren), dort kein Hinweis.
        if (result.Change == WordListChange.Added && _wordList != WordList.Swiss && WordListEditor.Hint(term) is { } hint)
        {
            message += " " + F(hint.Key, hint.Args);
        }

        if (result.Change != WordListChange.Invalid)
        {
            WordTermBox.Text = string.Empty;
            WordVariantsBox.Text = string.Empty;
        }

        ShowWordStatus(message, result.Change == WordListChange.Invalid);
        RenderWordEntries();
        WordTermBox.Focus();
    }

    // Entfernen braucht zwei Klicks: Der erste macht aus dem Knopf "Wirklich entfernen?", erst der zweite entfernt. Ein Fehlklick mit
    // der Bildschirmtastatur passiert leicht. Der Knopf wird dabei nur umbeschriftet, nicht neu gebaut (Abschnitt 23, Regel 12).
    private void OnWordRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string term } button) return;

        if (!Equals(button.Content, ConfirmRemoveText))
        {
            ResetRemoveButtons();
            button.Content = ConfirmRemoveText;
            _removeReset.Start();
            return;
        }

        _removeReset.Stop();

        WordListEditResult result = _wordStore.Remove(_wordList, term);
        ShowWordStatus(
            result.Change switch
            {
                WordListChange.Removed => F("settings.words.removed", term),
                WordListChange.NotFound => F("settings.words.not_found", term),
                _ => result.Problem is { } problem ? F(problem.Key, problem.Args) : T("settings.words.remove_failed"),
            },
            result.Change == WordListChange.Invalid);
        RenderWordEntries();
    }

    private void ResetRemoveButtons()
    {
        _removeReset.Stop();
        foreach (Button other in WordEntriesPanel.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<Button>())) other.Content = RemoveText;
    }

    private string RemoveText => T("settings.words.remove");
    private string ConfirmRemoveText => T("settings.words.remove.confirm");

    // Eine Zeile je Eintrag der gewählten Liste: Wort, dahinter die falschen Schreibweisen, rechts "Entfernen".
    private void RenderWordEntries()
    {
        WordEntriesPanel.Children.Clear();
        IReadOnlyList<VocabularyEntry>? read = _wordStore.Entries(_wordList);
        IReadOnlyList<VocabularyEntry> entries = read ?? [];
        foreach (VocabularyEntry entry in entries)
        {
            Grid row = new() { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock text = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            text.Inlines.Add(new System.Windows.Documents.Run(entry.Term) { FontWeight = FontWeights.SemiBold });
            if (entry.Aliases.Count > 0)
            {
                text.Inlines.Add(new System.Windows.Documents.Run("  " + F("settings.words.instead", string.Join(", ", entry.Aliases))) { Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush") });
            }

            Button remove = new() { Content = RemoveText, Tag = entry.Term };
            remove.Click += OnWordRemoveClick;
            Grid.SetColumn(remove, 1);

            row.Children.Add(text);
            row.Children.Add(remove);
            WordEntriesPanel.Children.Add(row);
        }

        // Eine Liste, die nicht als UTF-8 gespeichert ist, ist nicht leer: Das sagt der Text dann ausdrücklich.
        WordEmptyText.Text = read is null ? F(WordListStore.NotUtf8Message.Key, WordListStore.NotUtf8Message.Args) : T("settings.words.empty");
        WordEmptyText.Style = (Style)FindResource(read is null ? "WarnText" : "HintText");
        WordEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Rollt zum Abschnitt Wörterbuch. Für die Bilder der Diagnose.</summary>
    internal void ShowWordSection()
    {
        FrameworkElement last = WordEmptyText.Visibility == Visibility.Visible ? WordEmptyText : WordEntriesPanel;
        last.BringIntoView(new Rect(0, 0, 1, Math.Max(1, last.ActualHeight)));
    }

    private void ShowWordStatus(string message, bool warn)
    {
        WordStatusText.Text = message;
        WordStatusText.Style = (Style)FindResource(warn ? "WarnText" : "HintText");
        WordStatusText.Margin = new Thickness(0, 8, 0, 0);
        WordStatusText.Visibility = Visibility.Visible;
    }

    // Die Liste der Oberflächensprachen mit dem Namen in der eigenen Sprache (Regel 10): Deutsch, English, Italiano.
    private void FillUiLanguages()
    {
        foreach (string code in UiLanguages.All) UiLanguageBox.Items.Add(new ComboBoxItem { Content = UiLanguages.NativeName(code), Tag = code });
    }

    // Nur eine Wahl des Nutzers setzt UiLanguage (Regel 11). Die Vorauswahl in Render läuft mit _rendering und kommt hier nicht durch;
    // FromControls übernimmt UiLanguage unverändert aus den geltenden Einstellungen. Der Hinweis steht in der neu gewählten Sprache.
    private void OnUiLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rendering || UiLanguageBox.SelectedItem is not ComboBoxItem { Tag: string code }) return;

        Save(_settings.Current with { UiLanguage = code });
        if (_closed) return;

        UiLanguageHintText.Text = UiText.Get("settings.ui_language.hint", UiLanguages.Resolve(code, CultureInfo.CurrentUICulture.Name));
        UiLanguageHintText.Visibility = Visibility.Visible;
    }

    // Texte dieses Fensters in der Sprache beim Öffnen.
    private string T(string key) => UiText.Get(key, _lang);

    private string F(string key, params string?[] args) => UiText.Format(key, _lang, args);
}
