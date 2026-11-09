using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using Talk.App.Dictation;
using Talk.App.Processing;
using Talk.App.Settings;
using Talk.Core.Audio;
using Talk.Core.History;
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
                LanguageCodes.SwissStandard => "Du sprichst Mundart, Talk schreibt Schweizer Hochdeutsch.",
                LanguageCodes.SwissDialect => "Talk versucht, in Mundart zu schreiben. Das ist noch ein Versuch und macht mehr Fehler.",
                _ => string.Empty,
            };
            SwissHintText.Text = swissHint;
            SwissHintText.Visibility = swissHint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            DialectPanel.Visibility = s.Language == LanguageCodes.SwissDialect ? Visibility.Visible : Visibility.Collapsed;

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
                : s.LocalOnly ? "Solange \"Nur lokal\" an ist, wird nicht geglättet."
                : !_coordinator.Speech.HasKey ? "Es ist kein Groq Schlüssel hinterlegt. Bis dahin wird nicht geglättet."
                : string.Empty;
            SmoothingHintText.Text = hint;
            SmoothingHintText.Visibility = hint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            RenderPerApp(s.SmoothingPerApp ?? AppSmoothingMap.Empty);
            LocalOnlyBox.IsChecked = s.LocalOnly;
            HistoryBox.IsChecked = s.HistoryEnabled;
            KeyStateText.Text = _coordinator.Speech.HasKey ? "Schlüssel hinterlegt: ja" : "Schlüssel hinterlegt: nein";
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
            Text = string.Equals(display, name, StringComparison.OrdinalIgnoreCase) ? display : $"{display} ({name})",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 16, 0),
        });

        ComboBox box = new() { Tag = name };
        foreach ((string label, SmoothingLevel? level) in PerAppChoices) box.Items.Add(new ComboBoxItem { Content = label, Tag = level });

        box.SelectionChanged += OnPerAppChanged;
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        PerAppPanel.Children.Add(row);
        _perAppBoxes[name] = box;
    }

    private static readonly (string Label, SmoothingLevel? Level)[] PerAppChoices =
    [
        ("Wie Standard", null),
        ("Aus", SmoothingLevel.Off),
        ("Nur Fehler beheben", SmoothingLevel.Correct),
        ("Sätze glätten", SmoothingLevel.Smooth),
        ("Als Nachricht ausformulieren", SmoothingLevel.Message),
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

        StatusText.Text = saved
            ? $"Gespeichert um {DateTime.Now:HH:mm:ss}."
            : "Die Änderung gilt, ließ sich aber nicht speichern.";
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
            DeviceHintText.Text = "Kein Aufnahmegerät gefunden";
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else if (microphoneId is not null && chosen is null)
        {
            DeviceHintText.Text = "Das gewählte Mikrofon ist nicht angeschlossen. Talk nimmt solange das Standardgerät.";
            DeviceHintText.Visibility = Visibility.Visible;
        }
        else if ((DeviceBox.SelectedItem as AudioDeviceInfo)?.LikelyBluetoothHandsFree == true)
        {
            DeviceHintText.Text = "Bluetooth Freisprechprofil erkannt, Aufnahmequalität kann schlecht sein";
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
            "Der Verlauf wird ab jetzt nicht mehr gespeichert. Soll der schon gespeicherte Verlauf auch gelöscht werden?\n\nJa: alles löschen. Nein: die Datei bleibt liegen, bis du im Verlauf auf Alles löschen klickst.",
            "Talk Verlauf",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            await _coordinator.History.ClearAsync(CancellationToken.None);
            if (_closed) return;
            StatusText.Text = "Der gespeicherte Verlauf ist gelöscht.";
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
        }
        catch (Exception)
        {
            if (_closed) return;
            StatusText.Text = "Eine Datei des Verlaufs ließ sich nicht löschen.";
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
        AutostartHintText.Text = "Der Eintrag zeigt auf eine andere Kopie von Talk. Haken entfernen und neu setzen, damit diese Fassung startet.";
        AutostartHintText.Visibility = elsewhere ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;

        bool wanted = AutostartBox.IsChecked == true;
        bool ok = Autostart.Set(wanted, Autostart.CurrentExePath);
        StatusText.Text = ok
            ? (wanted ? "Talk startet ab jetzt mit Windows." : "Talk startet nicht mehr mit Windows.")
            : "Der Autostart ließ sich nicht ändern.";
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
            WordList.Private => (
                "Für Namen von Menschen, Ärzte, Einrichtungen und Gesundheit. Bleibt immer auf diesem Rechner, geht nie an Groq.",
                "Richtiges Wort",
                "So schreibt Talk es bisher falsch (freiwillig, mehrere mit Komma)"),
            WordList.Swiss => (
                "Eigene Schweizer Wörter. Gilt nur bei Schweizerdeutsch und Mundart: Talk ersetzt das Wort aus Deutschland durch das Schweizer.",
                "Schweizer Wort",
                "Wort aus Deutschland, das ersetzt werden soll (mehrere mit Komma)"),
            _ => (
                "Für Marken, Firmen, Orte und Fachwörter, keine Namen von Menschen. Geht auch als Hinweis an Groq, wenn Groq eingeschaltet ist.",
                "Richtiges Wort",
                "So schreibt Talk es bisher falsch (freiwillig, mehrere mit Komma)"),
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
            ShowWordStatus("Bitte auch das Wort aus Deutschland eintragen, das Talk ersetzen soll.", warn: true);
            WordVariantsBox.Focus();
            return;
        }

        WordListEditResult result = _wordStore.Add(_wordList, term, variants);

        string message = result.Change switch
        {
            WordListChange.Added => $"\u201E{term}\u201C steht jetzt in der Liste.",
            WordListChange.Extended => $"\u201E{result.StoredTerm}\u201C hat neue Schreibweisen dazubekommen.",
            WordListChange.Unchanged => $"Steht schon als \u201E{result.StoredTerm}\u201C in der Liste. Zum Ändern erst entfernen.",
            _ => result.Problem ?? "Das ließ sich nicht hinzufügen.",
        };

        // Kleingeschriebene Wörter sind in der Schweizer Liste der Normalfall (parkieren), dort kein Hinweis.
        if (result.Change == WordListChange.Added && _wordList != WordList.Swiss && WordListEditor.Hint(term) is string hint) message += " " + hint;

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
                WordListChange.Removed => $"\u201E{term}\u201C ist entfernt.",
                WordListChange.NotFound => $"\u201E{term}\u201C stand nicht mehr in der Liste.",
                _ => result.Problem ?? "Das ließ sich nicht entfernen.",
            },
            result.Change == WordListChange.Invalid);
        RenderWordEntries();
    }

    private void ResetRemoveButtons()
    {
        _removeReset.Stop();
        foreach (Button other in WordEntriesPanel.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<Button>())) other.Content = RemoveText;
    }

    private const string RemoveText = "Entfernen";
    private const string ConfirmRemoveText = "Wirklich entfernen?";

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
                text.Inlines.Add(new System.Windows.Documents.Run("  statt " + string.Join(", ", entry.Aliases)) { Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush") });
            }

            Button remove = new() { Content = RemoveText, Tag = entry.Term };
            remove.Click += OnWordRemoveClick;
            Grid.SetColumn(remove, 1);

            row.Children.Add(text);
            row.Children.Add(remove);
            WordEntriesPanel.Children.Add(row);
        }

        // Eine Liste, die nicht als UTF-8 gespeichert ist, ist nicht leer: Das sagt der Text dann ausdrücklich.
        WordEmptyText.Text = read is null ? WordListStore.NotUtf8Message : "In dieser Liste stehen noch keine Wörter.";
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
}
