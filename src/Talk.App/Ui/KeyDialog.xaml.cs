using System.Globalization;
using System.Windows;

using Talk.App.Diagnostics;
using Talk.App.Secrets;
using Talk.App.Transcription;
using Talk.Core.Secrets;
using Talk.Core.Transcription;

namespace Talk.App.Ui;

/// <summary>
/// Kleines Fenster für den Groq Schlüssel: verdecktes Eingabefeld, Einfügen aus der Zwischenablage, Speichern, Prüfen, Löschen.
/// Jeder Knopf braucht nur einen einzelnen Klick, damit Talk auch bedienbar bleibt, wenn jemand keine zwei Tasten gleichzeitig drücken kann.
/// </summary>
/// <remarks>
/// <para>
/// Der Schlüssel wird nie im Klartext angezeigt, nie protokolliert und nie in eine Datei geschrieben. Die Statuszeile nennt nur
/// feste Wörter und die Zeichenzahl. Gespeichert wird ausschließlich in der Windows Anmeldeinformationsverwaltung.
/// </para>
/// <para>
/// Die Zwischenablage wird nur beim Klick auf "Aus Zwischenablage einfügen" gelesen und nie beschrieben. Text mit Leerzeichen,
/// Zeilenumbrüchen oder Sonderzeichen wird abgewiesen: Ein Schlüssel sieht nicht so aus, und so kann kein versehentlich kopierter
/// Text gespeichert oder beim Prüfen an Groq geschickt werden.
/// </para>
/// <para>
/// Sieht der Text im Feld nicht wie ein Groq Schlüssel aus (beginnt nicht mit <c>gsk_</c> oder ist kürzer als 20 Zeichen), fragt der
/// Dialog vor "Speichern" und vor "Prüfen" mit Ja und Nein nach; bei Nein passiert nichts. Der Text selbst steht nie in der Rückfrage.
/// </para>
/// <para>
/// Typen, die Windows Forms gleichnamig mitbringt (Clipboard, MessageBox), werden voll ausgeschrieben.
/// </para>
/// </remarks>
public partial class KeyDialog : Window
{
    private const string NotAKeyText = "Das sieht nicht wie ein Schlüssel aus (Leerzeichen, Zeilenumbruch oder Sonderzeichen)";
    private const string UnusualKeyQuestion = "Der Text sieht nicht wie ein Groq Schlüssel aus. Trotzdem verwenden?";
    private const string DialogTitle = "Groq Schlüssel";

    // So sieht ein Groq Schlüssel aus: Er beginnt mit diesem Vorsatz und hat mindestens so viele Zeichen.
    private const string GroqKeyPrefix = "gsk_";
    private const int MinGroqKeyLength = 20;

    private readonly SpeechService _speech;
    private readonly CancellationTokenSource _closing = new();

    private bool _busy;
    private bool _closed;
    private bool _stored;

    internal KeyDialog(SpeechService speech)
    {
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        InitializeComponent();
        WindowTheme.Apply(this);

        LocalOnlyHintText.Visibility = _speech.LocalOnly ? Visibility.Visible : Visibility.Collapsed;
        RefreshState();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;

        // Eine laufende Prüfung endet, das Feld wird geleert.
        try
        {
            _closing.Cancel();
        }
        catch
        {
            // Beim Schließen nicht mehr wichtig.
        }

        _closing.Dispose();
        KeyBox.Clear();
    }

    // Liest Text aus der Zwischenablage, entfernt Leerzeichen und Zeilenumbrüche an den Rändern und setzt ihn in das verdeckte Feld.
    // Angezeigt wird nur die Zeichenzahl. Die Zwischenablage wird dabei nicht verändert.
    private void OnPasteClick(object sender, RoutedEventArgs e)
    {
        string? text;
        try
        {
            text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;
        }
        catch
        {
            // Ein anderes Programm hält die Zwischenablage gerade offen.
            StatusText.Text = "Die Zwischenablage ließ sich nicht lesen";
            return;
        }

        string trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            StatusText.Text = "Die Zwischenablage enthält keinen Text";
            return;
        }

        if (trimmed.Length > CredentialSecretStore.MaxSecretLength)
        {
            StatusText.Text = $"Der Text in der Zwischenablage ist zu lang ({Count(trimmed.Length)} Zeichen), nichts eingefügt";
            return;
        }

        if (!LooksLikeKey(trimmed))
        {
            StatusText.Text = $"{NotAKeyText}, nichts eingefügt";
            return;
        }

        KeyBox.Password = trimmed;
        StatusText.Text = $"Eingefügt: {Count(trimmed.Length)} Zeichen";
    }

    // Speichert den Inhalt des Felds in der Anmeldeinformationsverwaltung und leert das Feld.
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string value = KeyBox.Password.Trim();
        if (value.Length == 0)
        {
            StatusText.Text = "Das Feld ist leer, nichts gespeichert";
            return;
        }

        if (value.Length > CredentialSecretStore.MaxSecretLength || !LooksLikeKey(value))
        {
            StatusText.Text = $"{NotAKeyText}, nichts gespeichert";
            return;
        }

        // Kein Vorsatz gsk_ oder zu kurz: erst nachfragen. Bei Nein passiert nichts.
        if (!LooksLikeGroqKey(value) && !ConfirmUnusualKey()) return;

        try
        {
            _speech.Secrets.Set(SecretNames.GroqApiKey, value);
        }
        catch
        {
            // Die Meldung der Ausnahme wird nicht angezeigt und nicht protokolliert.
            StatusText.Text = "Speichern fehlgeschlagen";
            return;
        }

        AppLog.Write("key", "saved");
        KeyBox.Clear();
        RefreshState();
        StatusText.Text = "Gespeichert";
    }

    // Prüft den Inhalt des Felds, sonst den hinterlegten Schlüssel, mit einer Leseanfrage an Groq ohne Audio.
    private async void OnCheckClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        string candidate = KeyBox.Password.Trim();
        if (candidate.Length > 0 && !LooksLikeKey(candidate))
        {
            StatusText.Text = $"{NotAKeyText}, nichts geprüft";
            return;
        }

        bool fromField = candidate.Length > 0;

        // Nur für den Inhalt des Felds: kein Vorsatz gsk_ oder zu kurz, dann erst nachfragen. Bei Nein passiert nichts, es geht nichts ins Netz.
        // Der hinterlegte Schlüssel wird hier nicht gelesen und nicht beurteilt.
        if (fromField && !LooksLikeGroqKey(candidate) && !ConfirmUnusualKey()) return;

        _busy = true;
        UpdateButtons();
        StatusText.Text = "Prüfung läuft";

        try
        {
            KeyCheckResult? result = await _speech.CheckKeyAsync(fromField ? candidate : null, _closing.Token);
            if (_closed) return;

            StatusText.Text = result switch
            {
                null => "Nur lokal ist eingeschaltet, es wurde nichts gesendet",
                KeyCheckResult.Missing => SttReport.KeyCheckGerman(KeyCheckResult.Missing),
                KeyCheckResult value when fromField => $"{SttReport.KeyCheckGerman(value)} (Inhalt des Felds, noch nicht gespeichert)",
                KeyCheckResult value => $"{SttReport.KeyCheckGerman(value)} (hinterlegter Schlüssel)",
            };
        }
        catch (OperationCanceledException)
        {
            // Das Fenster wurde geschlossen.
        }
        catch
        {
            if (!_closed) StatusText.Text = "Prüfung fehlgeschlagen";
        }
        finally
        {
            _busy = false;
            if (!_closed) UpdateButtons();
        }
    }

    // Entfernt den hinterlegten Schlüssel nach einer Rückfrage.
    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult answer = System.Windows.MessageBox.Show(
            this,
            "Den hinterlegten Schlüssel wirklich löschen?",
            DialogTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            _speech.Secrets.Remove(SecretNames.GroqApiKey);
        }
        catch
        {
            StatusText.Text = "Löschen fehlgeschlagen";
            RefreshState();
            return;
        }

        AppLog.Write("key", "removed");
        RefreshState();
        StatusText.Text = "Gelöscht";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_closed) UpdateButtons();
    }

    // Liest neu, ob ein Schlüssel hinterlegt ist (nur ob, nie den Wert), und gleicht Zeile und Knöpfe an.
    private void RefreshState()
    {
        _stored = _speech.HasKey;
        StoredText.Text = _stored ? "Schlüssel hinterlegt: ja" : "Schlüssel hinterlegt: nein";
        UpdateButtons();
    }

    // Knöpfe an den Stand angleichen, ohne die Ablage zu lesen. Die Länge kommt aus dem verdeckten Feld, nicht aus einer Klartextkopie.
    private void UpdateButtons()
    {
        PasteButton.IsEnabled = !_busy;
        SaveButton.IsEnabled = !_busy && KeyBox.SecurePassword.Length > 0;
        CheckButton.IsEnabled = !_busy && !_speech.LocalOnly;
        DeleteButton.IsEnabled = !_busy && _stored;
    }

    // Ein Schlüssel besteht aus sichtbaren ASCII Zeichen ohne Leerzeichen. Alles andere ist vermutlich versehentlich kopierter Text.
    private static bool LooksLikeKey(string value)
    {
        foreach (char c in value)
        {
            if (c < '!' || c > '~') return false;
        }

        return value.Length > 0;
    }

    // Ein Groq Schlüssel beginnt mit gsk_ und hat mindestens 20 Zeichen. Alles andere ist nicht verboten, braucht aber eine Rückfrage.
    private static bool LooksLikeGroqKey(string value)
    {
        return value.Length >= MinGroqKeyLength && value.StartsWith(GroqKeyPrefix, StringComparison.Ordinal);
    }

    // Rückfrage mit Ja und Nein, Vorgabe Nein. Die Rückfrage nennt nur den festen Satz, nie den Text aus dem Feld.
    private bool ConfirmUnusualKey()
    {
        MessageBoxResult answer = System.Windows.MessageBox.Show(
            this,
            UnusualKeyQuestion,
            DialogTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
