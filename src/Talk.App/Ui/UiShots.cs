using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Talk.App.Diagnostics;
using Talk.App.Dictation;
using Talk.App.History;
using Talk.App.Input;
using Talk.App.Processing;
using Talk.App.Settings;
using Talk.App.Transcription;
using Talk.Core.History;
using Talk.Core.Hotkey;
using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;
using Talk.Core.Secrets;
using Talk.Core.Settings;

namespace Talk.App.Ui;

/// <summary>
/// Schalter --uishots Ordner: zeichnet Musterbogen, Verlauf (gefüllt, leer, übervoll), Schlüsseldialog und Tray Menü als Bilder in den Ordner,
/// damit sich die Optik nach DESIGN.md ohne laufendes Talk prüfen lässt.
/// </summary>
/// <remarks>
/// Die Fenster stehen dafür kurz weit außerhalb des Bildschirms, ohne Aktivierung und ohne Taskleiste. Die Texte sind erfunden; echter
/// Verlauf, echte Einstellungen und der echte Schlüssel werden nie gelesen. Nur das Einstellungsfenster zeigt zwei Dinge aus Windows, nur
/// gelesen: die Namen der Mikrofone und, ob der Autostart eingetragen ist. Gezeichnet wird der Inhalt ohne Titelleiste, in doppelter Auflösung.
/// Der Ordner steht nie im Protokoll. Ergebnis: 0, wenn alle Bilder geschrieben sind, sonst 2.
/// </remarks>
internal static class UiShots
{
    private static readonly string[] SampleTexts =
    [
        "Ich bin morgen ab zehn im Büro und bringe die Unterlagen mit.",
        "Ciao Marco, ci vediamo stasera alle otto davanti al ristorante, perché prima non riesco.",
        "Das ist ein sehr langer Beispieltext, der zeigt, wie der Verlauf mit vielen Zeilen umgeht. Er geht über mehrere Zeilen, damit man sieht, "
            + "ob der Umbruch sauber sitzt, ob der Knopf daneben oben bleibt und ob die Abstände zwischen den Einträgen ruhig wirken.",
    ];

    public static async Task<int> RunAsync(string directory)
    {
        int written = 0;
        int failed = 0;

        async Task Shot(string name, Func<Window> create)
        {
            try
            {
                await SaveAsync(create(), Path.Combine(directory, name));
                written++;
            }
            catch (Exception)
            {
                failed++;
            }
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception)
        {
            AppLog.Write("uishots", "done", "bilder=0 fehler=1");
            return 2;
        }

        await Shot("muster.png", () => new ThemeSampleWindow());
        await Shot("verlauf.png", () => new HistoryWindow(Filled(SampleTexts.Length)));
        await Shot("verlauf-leer.png", () => new HistoryWindow(new MemoryHistoryStore()));
        await Shot("verlauf-voll.png", () => new HistoryWindow(Filled(40)));
        await Shot("verlauf-fehler.png", () => new HistoryWindow(new FailingHistoryStore()));

        using (SpeechService speech = new(new EmptySecretStore(), new HttpClient()))
        {
            await Shot("schluessel.png", () => new KeyDialog(speech));

            // Das Einstellungsfenster braucht einen echten Ablauf. Er bekommt erfundene Einstellungen mit abgestelltem Verlauf (so wird keine
            // Verlaufsdatei geschrieben) und eine Einstellungsdatei im Bildordner, die nur bei einem Klick geschrieben würde. Der Hook wird nie
            // gesetzt; die Zeile "hook | removed" beim Beenden ist deshalb ohne Bedeutung.
            DictationCoordinator? coordinator = null;
            try
            {
                using KeyboardHookService hook = new(new HotkeyController());
                EncryptedHistoryStore history = new(Path.Combine(directory, "verlauf-muster.bin"), persist: false);
                coordinator = new DictationCoordinator(hook, speech, history);
                DictationSettings sample = new(
                    HistoryEnabled: false,
                    SmoothingEnabled: true,
                    Smoothing: SmoothingLevel.Message,
                    SmoothingPerApp: AppSmoothingMap.Empty
                        .With("WhatsApp", SmoothingLevel.Message)
                        .With("WINWORD", SmoothingLevel.Smooth)
                        .With("WindowsTerminal", SmoothingLevel.Off));
                AppSettingsService settings = new(coordinator, history, sample, Path.Combine(directory, "settings-muster.json"));

                // Wörterbuch nur mit erfundenen Wörtern in einem eigenen Ordner im Bildordner, nie die echten Listen (Playbook Abschnitt 27).
                string wordsDirectory = Path.Combine(directory, "woerter-muster");
                Directory.CreateDirectory(wordsDirectory);
                File.WriteAllText(
                    Path.Combine(wordsDirectory, "woerterbuch.txt"),
                    "# Erfundene Musterwörter für die Bilder\r\nFantasia Brotladen = Fantasie Brotladen\r\nKorvaline = Korwaline | Korva Line\r\nZentrix\r\n");
                File.WriteAllText(
                    Path.Combine(wordsDirectory, "woerterbuch-lokal.txt"),
                    "# Erfundene Musterwörter für die Bilder\r\nDr. Varnholt = Dr. Farnholt | Doktor Varnholt\r\nMira Selent = Mira Zelent\r\nPraxis am Kornfeld = Praxis am Kornfelt\r\n");
                WordListStore words = new(wordsDirectory);

                await Shot("einstellungen.png", () => new SettingsWindow(settings, coordinator, words));
                await Shot("einstellungen-woerterbuch.png", () =>
                {
                    SettingsWindow window = new(settings, coordinator, words);
                    window.Loaded += (_, _) => window.ShowWordSection();
                    return window;
                });
                await Shot("wort-korrigieren.png", () => new WordFixDialog(SampleTexts[0], words));
            }
            catch (Exception)
            {
                failed++;
            }
            finally
            {
                if (coordinator is not null) await coordinator.ShutdownAsync();
            }
        }

        foreach ((string name, bool select) in new[] { ("tray-menue.png", false), ("tray-menue-auswahl.png", true) })
        {
            try
            {
                SaveMenu(Path.Combine(directory, name), select);
                written++;
            }
            catch (Exception)
            {
                failed++;
            }
        }

        AppLog.Write("uishots", "done", $"bilder={written} fehler={failed}");
        return failed == 0 ? 0 : 2;
    }

    private static MemoryHistoryStore Filled(int count)
    {
        MemoryHistoryStore store = new(Math.Max(count, 1));
        DateTimeOffset start = new(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(1));
        for (int i = 0; i < count; i++)
        {
            store.AddAsync(new HistoryEntry(start.AddMinutes(7 * i), SampleTexts[i % SampleTexts.Length], "local", false), CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        return store;
    }

    private static async Task SaveAsync(Window window, string path)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = 0;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Show();
        try
        {
            // Erst wenn alles geladen ist (der Verlauf lädt asynchron), steht das Fenster fertig da.
            await Task.Delay(400);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            // Das erste Kind des Fensters ist die Fläche des Inhalts mit dem Hintergrund, ohne Titelleiste.
            FrameworkElement client = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
            int width = (int)Math.Ceiling(client.ActualWidth);
            int height = (int)Math.Ceiling(client.ActualHeight);
            RenderTargetBitmap bitmap = new(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(client);

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }
        finally
        {
            window.Close();
        }
    }

    // Zeichnet das Tray Menü, ohne es zu zeigen: "Nur lokal" mit Haken, auf Wunsch der erste Eintrag wie unter der Maus.
    // Grenze: DrawToBitmap verschiebt bei einem nie gezeigten Menü den Inhalt um einige Punkte nach oben; Farben, Schrift, Haken und
    // Breite stimmen, die genaue Lage zeigt nur das echte Menü.
    private static void SaveMenu(string path, bool select)
    {
        TrayMenuActions none = new(() => { }, () => { }, () => { }, () => { }, () => { }, () => { }, () => { });
        using System.Windows.Forms.ContextMenuStrip menu = TrayMenu.Build(none, out System.Windows.Forms.ToolStripMenuItem localOnly, out _);
        localOnly.Checked = true;
        menu.CreateControl();
        System.Drawing.Size size = menu.GetPreferredSize(System.Drawing.Size.Empty);
        menu.Size = size;
        menu.PerformLayout();
        if (select) menu.Items[0].Select();

        using System.Drawing.Bitmap bitmap = new(size.Width, size.Height);
        menu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, size.Width, size.Height));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // Ein Verlauf, der sich nie lesen lässt, für das Bild des Fehlerzustands.
    private sealed class FailingHistoryStore : IHistoryStore
    {
        public Task AddAsync(HistoryEntry entry, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct) =>
            Task.FromException<IReadOnlyList<HistoryEntry>>(new IOException("Prüfbild"));

        public Task ClearAsync(CancellationToken ct) => Task.CompletedTask;
    }

    // Eine Schlüsselablage ohne Eintrag, damit der Schlüsseldialog nie die echte Ablage liest.
    private sealed class EmptySecretStore : ISecretStore
    {
        public string? Get(string name) => null;

        public void Set(string name, string value)
        {
        }

        public void Remove(string name)
        {
        }
    }
}
