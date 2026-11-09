// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Windows;

using Talk.App.Processing;
using Talk.Core.Processing;

using Button = System.Windows.Controls.Button;

namespace Talk.App.Ui;

/// <summary>
/// Übernimmt aus einem Text des Verlaufs ein falsch geschriebenes Wort ins Wörterbuch (Playbook Abschnitt 27): Das gewählte Wort wird
/// die Variante, das eingetragene die richtige Schreibweise. Der Text bleibt nur in diesem Fenster und im Arbeitsspeicher; der Eintrag
/// im Verlauf ändert sich nicht.
/// </summary>
public partial class WordFixDialog : Window
{
    // Satzzeichen am Rand eines Worts gehören nicht zur Wahl ("Cesareti," wird "Cesareti"); Bindestrich und Apostroph im Wort bleiben.
    private static readonly char[] EdgePunctuation = ['.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '«', '»', '„', '“', '”', '‚', '‘', '’', '…'];

    private readonly WordListStore _store;
    private readonly List<Button> _wordButtons = [];
    private int _first = -1;
    private int _last = -1;

    /// <param name="text">Der Text aus dem Verlauf.</param>
    /// <param name="store">Die Wörterlisten; null für die echten. Die Bilder der Diagnose geben eigene mit.</param>
    internal WordFixDialog(string text, WordListStore? store = null)
    {
        _store = store ?? new WordListStore();
        InitializeComponent();
        WindowTheme.Apply(this);
        MaxHeight = Math.Min(SystemParameters.WorkArea.Height, 860);

        foreach (string raw in (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string word = raw.Trim(EdgePunctuation);
            if (word.Length == 0) continue;

            Button button = new() { Content = word, Tag = _wordButtons.Count, Margin = new Thickness(0, 0, 8, 8) };
            button.Click += OnWordClick;
            _wordButtons.Add(button);
            WordsPanel.Children.Add(button);
        }
    }

    // Ein Klick wählt das Wort. Liegt es direkt neben der Wahl, kommt es dazu; sonst beginnt die Wahl neu.
    private void OnWordClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index }) return;

        if (_first >= 0 && index == _first - 1) _first = index;
        else if (_first >= 0 && index == _last + 1) _last = index;
        else _first = _last = index;

        for (int i = 0; i < _wordButtons.Count; i++)
        {
            bool selected = i >= _first && i <= _last;
            _wordButtons[i].Style = selected ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button));
        }

        CorrectBox.Text = Selected();
        CorrectBox.Focus();
        CorrectBox.SelectAll();
        StatusText.Text = string.Empty;
    }

    private string Selected() =>
        _first < 0 ? string.Empty : string.Join(" ", _wordButtons.Skip(_first).Take(_last - _first + 1).Select(b => (string)b.Content));

    private void OnRememberClick(object sender, RoutedEventArgs e)
    {
        string wrong = Selected();
        string correct = CorrectBox.Text.Trim();
        if (wrong.Length == 0)
        {
            ShowStatus("Bitte zuerst oben das falsche Wort anklicken.", warn: true);
            return;
        }

        WordList list = ListPrivate.IsChecked == true ? WordList.Private : WordList.General;
        WordListEditResult result = _store.Add(list, correct, [wrong]);
        bool same = VocabularyCorrector.Key(wrong) == VocabularyCorrector.Key(correct);

        string message = result.Change switch
        {
            WordListChange.Added when same => $"„{correct}“ steht jetzt im Wörterbuch.",
            WordListChange.Added => $"Gemerkt: Aus „{wrong}“ wird ab dem nächsten Diktat „{correct}“.",
            WordListChange.Extended => $"Gemerkt: Aus „{wrong}“ wird ab dem nächsten Diktat „{result.StoredTerm}“.",
            WordListChange.Unchanged => $"Steht schon als „{result.StoredTerm}“ im Wörterbuch. Zum Ändern in den Einstellungen erst entfernen.",
            _ => result.Problem ?? "Das ließ sich nicht merken.",
        };
        if (result.Change == WordListChange.Added && WordListEditor.Hint(correct) is string hint) message += " " + hint;

        ShowStatus(message, result.Change == WordListChange.Invalid);
    }

    private void ShowStatus(string message, bool warn)
    {
        StatusText.Text = message;
        StatusText.Style = (Style)FindResource(warn ? "WarnText" : "HintText");
        StatusText.Margin = new Thickness(0, 16, 0, 0);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
