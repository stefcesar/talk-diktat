// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;

using Talk.App.Diagnostics;
using Talk.Core.Processing;

namespace Talk.App.Processing;

/// <summary>
/// Die Wörter des Nutzers aus <c>%LOCALAPPDATA%\Talk</c>: <c>woerterbuch.txt</c>, <c>woerterbuch-lokal.txt</c>,
/// <c>fuellwoerter.txt</c> und <c>woerterbuch-ch.txt</c>. Diese Klasse liest nur. Die Dateien sind Nutzerdaten und liegen nie im Repository.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HintTerms"/> darf als Hinweis an jeden Erkenner gehen und enthält ausschließlich Schreibweisen aus
/// <c>woerterbuch.txt</c>. Das lokale Wörterbuch (Menschen, Einrichtungen, Gesundheit) wird nach der Erkennung auf dem Rechner
/// angewendet; als Hinweis (<see cref="LocalHintTerms"/>) geht es nur in einen Lauf, der erzwungen lokal ist.
/// </para>
/// <para>
/// <see cref="Refresh"/> liest eine Datei neu, wenn sich Änderungszeit oder Größe geändert haben. Eine fehlende
/// Datei gilt als leer; lässt sich eine Datei gerade nicht lesen, bleibt die zuletzt gelesene Liste. Es gibt nie eine Ausnahme. Im Protokoll stehen nur Zahlen, nie ein Wort aus den Listen.
/// </para>
/// </remarks>
internal sealed class UserWordLists
{
    internal const string VocabularyFileName = "woerterbuch.txt";
    internal const string LocalVocabularyFileName = "woerterbuch-lokal.txt";
    private const string FillersFileName = "fuellwoerter.txt";
    internal const string SwissFileName = "woerterbuch-ch.txt";

    private readonly string _directory;
    private const string MissingStamp = "fehlt";

    private string? _vocabularyStamp;
    private string? _localStamp;
    private string? _fillersStamp;
    private string? _swissStamp;

    public UserWordLists()
        : this(TalkPaths.AppDataDirectory)
    {
    }

    internal UserWordLists(string directory)
    {
        _directory = directory;
    }

    /// <summary>Zeilen aus <c>woerterbuch.txt</c>, im Format von <see cref="VocabularyEntry.Parse"/>.</summary>
    public IReadOnlyList<string> Vocabulary { get; private set; } = [];

    /// <summary>Zeilen aus <c>woerterbuch-lokal.txt</c> für die Nachbearbeitung auf dem Rechner.</summary>
    public IReadOnlyList<string> LocalVocabulary { get; private set; } = [];

    /// <summary>
    /// Eigene Helvetismen aus <c>woerterbuch-ch.txt</c> ("Schweizer Wort = Form | Form"), nur bei Schweizerdeutsch und Mundart
    /// (Playbook Abschnitt 26). Sie werden nur auf dem Rechner angewendet und gehen nie als Hinweis hinaus.
    /// </summary>
    public IReadOnlyList<string> SwissVocabulary { get; private set; } = [];

    /// <summary>Zusatzfüllwörter aus <c>fuellwoerter.txt</c>.</summary>
    public IReadOnlyList<string> ExtraFillers { get; private set; } = [];

    /// <summary>Die Schreibweisen aus <c>woerterbuch.txt</c> für den Hinweis an den Erkenner, in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<string> HintTerms { get; private set; } = [];

    /// <summary>
    /// Die Schreibweisen aus <c>woerterbuch-lokal.txt</c>. Sie dürfen nur dann in den Hinweis, wenn der Lauf erzwungen lokal ist
    /// (<c>forceLocal</c> der Erkennung), also nachweislich nichts den Rechner verlässt.
    /// </summary>
    public IReadOnlyList<string> LocalHintTerms { get; private set; } = [];

    /// <summary>
    /// Liest die Dateien, die sich seit dem letzten Aufruf geändert haben. Wirft nie.
    /// </summary>
    public void Refresh()
    {
        try
        {
            bool changed = false;
            changed |= RefreshFile(VocabularyFileName, ref _vocabularyStamp, lines => Vocabulary = lines);
            changed |= RefreshFile(LocalVocabularyFileName, ref _localStamp, lines => LocalVocabulary = lines);
            changed |= RefreshFile(FillersFileName, ref _fillersStamp, lines => ExtraFillers = lines);
            changed |= RefreshFile(SwissFileName, ref _swissStamp, lines => SwissVocabulary = lines);
            if (!changed) return;

            HintTerms = VocabularyEntry.Terms(Vocabulary);
            LocalHintTerms = VocabularyEntry.Terms(LocalVocabulary);
            AppLog.Write("words", "loaded", $"woerterbuch={Vocabulary.Count} lokal={LocalVocabulary.Count} fuellwoerter={ExtraFillers.Count} schweiz={SwissVocabulary.Count}");
        }
        catch
        {
            // Die Listen sind eine Hilfe. Ein Fehler hier darf kein Diktat aufhalten; es gelten die zuletzt gelesenen Listen.
        }
    }

    // Liest eine Datei neu, wenn sich ihre Kennung geändert hat. Lässt sie sich gerade nicht lesen (gesperrt vom Editor, vom
    // Virenscanner), bleiben die alte Liste und die alte Kennung, damit der nächste Aufruf es wieder versucht.
    private bool RefreshFile(string fileName, ref string? stamp, Action<IReadOnlyList<string>> assign)
    {
        string path = Path.Combine(_directory, fileName);
        string? current = Stamp(path);
        if (current is null || current == stamp) return false;

        IReadOnlyList<string> lines;
        if (current == MissingStamp)
        {
            lines = [];
        }
        else
        {
            try
            {
                lines = VocabularyEntry.ReadLines(File.ReadAllText(path));
            }
            catch
            {
                return false;
            }
        }

        assign(lines);
        stamp = current;
        return true;
    }

    // Änderungszeit und Größe als Kennung; fehlt die Datei, ein fester Wert; lässt sie sich nicht abfragen, null.
    private static string? Stamp(string path)
    {
        try
        {
            FileInfo info = new(path);
            return info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : MissingStamp;
        }
        catch
        {
            return null;
        }
    }
}
