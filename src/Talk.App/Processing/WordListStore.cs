// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;
using System.Text;

using Talk.App.Diagnostics;
using Talk.Core.Processing;

namespace Talk.App.Processing;

/// <summary>Die Wörterlisten, die die App selbst ändern kann (Playbook Abschnitt 27).</summary>
internal enum WordList
{
    /// <summary><c>woerterbuch.txt</c>: geht auch als Hinweis an Groq.</summary>
    General,

    /// <summary><c>woerterbuch-lokal.txt</c>: Namen und Privates, bleibt auf dem Rechner.</summary>
    Private,

    /// <summary><c>woerterbuch-ch.txt</c>: eigene Helvetismen, nur bei Schweizerdeutsch und Mundart.</summary>
    Swiss
}

/// <summary>
/// Liest und ändert die Wörterlisten des Nutzers in <c>%LOCALAPPDATA%\Talk</c> für die Einstellungen und den Verlauf
/// (Playbook Abschnitt 27). Die Textarbeit macht <see cref="WordListEditor"/>; diese Klasse kümmert sich um die Datei.
/// </summary>
/// <remarks>
/// Vor jeder Änderung wird die Datei frisch gelesen, damit Änderungen von Hand im Editor nicht verloren gehen. Vor dem ersten
/// Schreiben eines Tages entsteht eine Sicherung <c>Name.sicherung-JJJJ-MM-TT</c> (eine vorhandene Sicherung desselben Tages bleibt).
/// Geschrieben wird über eine Zwischendatei, die die alte ersetzt, damit nie eine halbe Datei entsteht; UTF-8 ohne BOM. Im Protokoll
/// stehen nur feste Wörter und Zahlen, nie ein Wort aus den Listen. Wirft nie: Fehler kommen als Ergebnis zurück.
/// </remarks>
internal sealed class WordListStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    // Streng lesen: Eine Datei, die nicht UTF-8 ist (zum Beispiel im Editor als ANSI gespeichert), würde sonst still mit Ersatzzeichen
    // gelesen und beim Zurückschreiben jeder Umlaut zerstört. Sie wird dann nicht angefasst.
    private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const string NotUtf8Problem =
        "Die Liste ist nicht als UTF-8 gespeichert. Bitte die Datei im Editor öffnen und mit Codierung UTF-8 speichern; bis dahin ändert Talk sie nicht.";

    private readonly string _directory;

    public WordListStore()
        : this(TalkPaths.AppDataDirectory)
    {
    }

    internal WordListStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>Der Dateiname der Liste.</summary>
    public static string FileName(WordList list) => list switch
    {
        WordList.Private => UserWordLists.LocalVocabularyFileName,
        WordList.Swiss => UserWordLists.SwissFileName,
        _ => UserWordLists.VocabularyFileName,
    };

    /// <summary>
    /// Die Einträge der Liste; fehlt die Datei oder lässt sie sich gerade nicht lesen, eine leere Liste. Ist sie nicht als UTF-8
    /// gespeichert, null: Dann soll die Anzeige das sagen, statt eine leere Liste vorzutäuschen.
    /// </summary>
    public IReadOnlyList<VocabularyEntry>? Entries(WordList list)
    {
        try
        {
            string path = PathOf(list);
            return File.Exists(path) ? WordListEditor.Entries(File.ReadAllText(path, Utf8Strict)) : [];
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Die Meldung, wenn eine Liste nicht als UTF-8 gespeichert ist.</summary>
    public static string NotUtf8Message => NotUtf8Problem;

    /// <summary>Fügt ein Wort hinzu oder ergänzt seine Varianten. Fehlt die Datei, wird sie mit einer Kopfzeile angelegt.</summary>
    public WordListEditResult Add(WordList list, string? term, IEnumerable<string>? variants) =>
        Change(list, content => WordListEditor.Add(content ?? WordListEditor.NewFileHeader + "\r\n", term, variants));

    /// <summary>Entfernt ein Wort aus der Liste.</summary>
    public WordListEditResult Remove(WordList list, string? term) =>
        Change(list, content => content is null ? new WordListEditResult(string.Empty, WordListChange.NotFound) : WordListEditor.Remove(content, term));

    // Liest frisch (null, wenn die Datei fehlt), ändert, sichert und schreibt nur bei einer Änderung.
    private WordListEditResult Change(WordList list, Func<string?, WordListEditResult> edit)
    {
        string path = PathOf(list);
        try
        {
            string? content = File.Exists(path) ? File.ReadAllText(path, Utf8Strict) : null;
            WordListEditResult result = edit(content);
            if (result.Changed)
            {
                Directory.CreateDirectory(_directory);
                if (content is not null) Backup(path);
                WriteReplacing(path, result.Content);
            }

            AppLog.Write("words", "changed", $"liste={ListWord(list)} ergebnis={ChangeWord(result.Change)}");
            return result;
        }
        catch (DecoderFallbackException)
        {
            AppLog.Write("words", "error", $"liste={ListWord(list)} grund=codierung");
            return new WordListEditResult(string.Empty, WordListChange.Invalid, NotUtf8Problem);
        }
        catch
        {
            AppLog.Write("words", "error", $"liste={ListWord(list)} grund=datei");
            return new WordListEditResult(string.Empty, WordListChange.Invalid, "Die Liste ließ sich gerade nicht speichern. Sie ist gesperrt oder schreibgeschützt.");
        }
    }

    private string PathOf(WordList list) => Path.Combine(_directory, FileName(list));

    // Eine Sicherung je Tag, die erste des Tages gewinnt.
    private static void Backup(string path)
    {
        string backup = $"{path}.sicherung-{DateTime.Now:yyyy-MM-dd}";
        if (!File.Exists(backup)) File.Copy(path, backup);
    }

    // Erst in eine Zwischendatei, dann ersetzen: Bricht das Schreiben ab, bleibt die alte Datei ganz.
    private static void WriteReplacing(string path, string content)
    {
        string temp = path + ".neu";
        try
        {
            File.WriteAllText(temp, content, Utf8NoBom);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        catch
        {
            // Die Zwischendatei enthält die ganze Liste; sie bleibt nicht liegen.
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // Nicht mehr zu retten; der Fehler unten zählt.
            }

            throw;
        }
    }

    private static string ListWord(WordList list) => list switch
    {
        WordList.Private => "privat",
        WordList.Swiss => "schweiz",
        _ => "allgemein",
    };

    private static string ChangeWord(WordListChange change) => change switch
    {
        WordListChange.Added => "neu",
        WordListChange.Extended => "ergaenzt",
        WordListChange.Unchanged => "vorhanden",
        WordListChange.Removed => "entfernt",
        WordListChange.NotFound => "fehlt",
        _ => "ungueltig",
    };
}
