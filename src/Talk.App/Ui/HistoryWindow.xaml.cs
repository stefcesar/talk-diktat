using System.Globalization;
using System.Windows;

using Talk.App.History;
using Talk.Core.History;

namespace Talk.App.Ui;

/// <summary>
/// Zeigt die letzten Diktate aus dem Verlauf, neuestes zuerst, mit "Kopieren" und "Wort korrigieren" je Eintrag und "Alles löschen".
/// Die Texte erscheinen nur in diesem Fenster. Alles geht mit einzelnen Klicks.
/// </summary>
public partial class HistoryWindow : Window
{
    private const int ShownEntries = 200;

    private readonly IHistoryStore _history;
    private bool _closed;

    // Das letzte "Alles löschen" ließ eine Datei liegen; die Meldung bleibt, bis ein Löschen gelingt.
    private bool _clearFailed;

    private readonly Dictionary<System.Windows.Controls.Button, System.Windows.Threading.DispatcherTimer> _resetTimers = [];

    internal HistoryWindow(IHistoryStore history)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        InitializeComponent();
        WindowTheme.Apply(this);
        Loaded += async (_, _) => await ReloadAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<HistoryEntry> entries;
        bool readFailed = false;
        try
        {
            entries = await _history.GetRecentAsync(ShownEntries, CancellationToken.None);
        }
        catch (Exception)
        {
            entries = [];
            readFailed = true;
        }

        if (_closed) return;

        CultureInfo german = CultureInfo.GetCultureInfo("de-DE");
        EntriesList.ItemsSource = entries
            .Select((e, i) => new Row(e.At.LocalDateTime.ToString("dddd, d. MMMM, HH:mm", german), e.Text, i == 0, i == entries.Count - 1))
            .ToList();
        EntriesScroll.ScrollToTop();

        // Störungstexte sagen nur, was in jedem Fall stimmt: keine Ursache, kein Rat, kein Versprechen (Entscheid 66).
        EncryptedHistoryStore? saved = _history as EncryptedHistoryStore;

        // In der Mitte steht genau eins: die Liste, der leere Zustand oder der Fehler beim Anzeigen.
        EntriesScroll.Visibility = entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = entries.Count == 0 && !readFailed ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = readFailed ? Visibility.Visible : Visibility.Collapsed;
        SummaryText.Text = readFailed ? "Anzeige gestört" : entries.Count switch
        {
            0 => "0 Einträge",
            1 => "1 Eintrag",
            // Das Fenster holt höchstens ShownEntries; der Verlauf kann mehr haben.
            ShownEntries => $"Die neuesten {ShownEntries} Einträge, neuester zuerst",
            _ => $"{entries.Count} Einträge, neuester zuerst",
        };

        // Ein gescheitertes Löschen bleibt stehen, bis ein Löschen gelingt.
        MessageText.Text = _clearFailed ? "Eine Datei des Verlaufs ließ sich nicht löschen." : string.Empty;
        MessageText.Visibility = _clearFailed ? Visibility.Visible : Visibility.Collapsed;

        // Der Fußtext sagt, was gilt: gespeichert, nur im Arbeitsspeicher, oder zuletzt ein Fehler auf der Festplatte.
        FooterText.Text = saved switch
        {
            { Persisting: true, LastWriteFailed: true } =>
                "Achtung: Zuletzt ist ein Schreiben oder Löschen auf der Festplatte gescheitert.",
            { Persisting: true } =>
                "Der Verlauf liegt verschlüsselt auf diesem Rechner und ist nur mit deinem Windows Konto lesbar. Einträge verschwinden nach 30 Tagen von selbst.",
            _ =>
                "Das Speichern ist abgestellt. Der Verlauf steht nur im Arbeitsspeicher und ist nach dem Beenden von Talk weg.",
        };
        FooterText.Foreground = saved is { Persisting: true, LastWriteFailed: true }
            ? (System.Windows.Media.Brush)FindResource("WarnBrush")
            : (System.Windows.Media.Brush)FindResource("TextMutedBrush");
    }

    // Öffnet den Dialog, mit dem ein falsch geschriebenes Wort dieses Texts ins Wörterbuch kommt (Playbook Abschnitt 27).
    private void OnFixClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string text } || text.Length == 0) return;
        new WordFixDialog(text) { Owner = this }.ShowDialog();
    }

    // Kopiert den Text des Eintrags in die Zwischenablage. Das ist der ausdrückliche Wunsch des Nutzers, deshalb ohne Ausschlussformate.
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string text } button || text.Length == 0) return;

        try
        {
            System.Windows.Clipboard.SetText(text);
            ShowAtButton(button, "Kopiert");
        }
        catch (Exception)
        {
            ShowAtButton(button, "Nicht kopiert");
        }
    }

    // Die Rückmeldung steht am Knopf selbst, dort wo geklickt wurde, und verschwindet nach anderthalb Sekunden. Der Knopf hat eine feste
    // Mindestbreite, damit der Text daneben nicht springt. Ein Zeitgeber je Knopf: ein zweiter Klick beginnt die anderthalb Sekunden neu.
    private void ShowAtButton(System.Windows.Controls.Button button, string feedback)
    {
        button.Content = feedback;
        if (!_resetTimers.TryGetValue(button, out System.Windows.Threading.DispatcherTimer? reset))
        {
            reset = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            reset.Tick += (_, _) =>
            {
                reset.Stop();
                _resetTimers.Remove(button);
                button.Content = "Kopieren";
            };
            _resetTimers[button] = reset;
        }

        reset.Stop();
        reset.Start();
    }

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult answer = System.Windows.MessageBox.Show(
            this,
            "Alle Einträge im Verlauf löschen? Das lässt sich nicht rückgängig machen.",
            "Talk Verlauf",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        bool deleted = true;
        try
        {
            await _history.ClearAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            deleted = false;
        }

        if (_closed) return;
        _clearFailed = !deleted;
        await ReloadAsync();
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed record Row(string Stamp, string Text, bool IsNewest, bool IsLast);
}
