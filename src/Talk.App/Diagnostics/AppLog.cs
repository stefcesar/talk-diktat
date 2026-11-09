// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;

namespace Talk.App.Diagnostics;

/// <summary>
/// Protokoll der App in %LOCALAPPDATA%\Talk\logs\talk.log. Eine Zeile je Ereignis im Format
/// "yyyy-MM-dd HH:mm:ss.fff | Kategorie | Ereignis | Detail". Threadsicher, Fehler werden verschluckt.
/// </summary>
/// <remarks>
/// Es werden nie Inhalte protokolliert: kein diktierter Text, kein Schlüssel, kein Tastencode einer fremden Taste.
/// Das Detail enthält nur feste Wörter, Zahlen oder Zustandsnamen.
/// </remarks>
internal static class AppLog
{
    private static readonly object Gate = new();

    // Die Pfade stehen an einer Stelle, in TalkPaths.
    private static readonly string LogDirectory = TalkPaths.LogDirectory;

    private static readonly string LogFile = TalkPaths.LogPath;

    // Ab dieser Größe wird das Protokoll zu talk.log.1 und ein neues beginnt. Es gibt genau eine alte Datei.
    private const long MaxBytes = 2 * 1024 * 1024;

    private static int _writesSinceCheck;

    /// <summary>
    /// Hängt eine Zeile an das Protokoll an.
    /// </summary>
    /// <param name="category">Kategorie, zum Beispiel "app", "hook", "hotkey", "caps", "hooktest".</param>
    /// <param name="eventName">Ereignis, zum Beispiel "start", "installed", "reinstalled", "removed".</param>
    /// <param name="detail">Optionales Detail aus festen Wörtern, Zahlen oder Zustandsnamen, zum Beispiel "grund=aufwachen".</param>
    public static void Write(string category, string eventName, string? detail = null)
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = detail is null
                ? $"{stamp} | {category} | {eventName}{Environment.NewLine}"
                : $"{stamp} | {category} | {eventName} | {detail}{Environment.NewLine}";

            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfLarge();
                File.AppendAllText(LogFile, line);
            }
        }
        catch
        {
            // Das Protokoll ist Komfort, kein Muss. Ein Schreibfehler darf die App nicht stören.
        }
    }

    /// <summary>Die alte Protokolldatei neben der aktuellen, oder der Pfad, an dem sie läge.</summary>
    public static string PreviousLogFile => LogFile + ".1";

    // Die Größe wird nur bei jedem hundertsten Schreiben geprüft, damit das Protokoll billig bleibt. Nur unter Gate aufrufen.
    private static void RotateIfLarge()
    {
        if (_writesSinceCheck++ % 100 != 0) return;

        FileInfo info = new(LogFile);
        if (!info.Exists || info.Length < MaxBytes) return;

        try
        {
            File.Move(LogFile, PreviousLogFile, overwrite: true);
        }
        catch
        {
            // Die Datei ist gerade von einem anderen Prozess offen. Dann bleibt sie, wie sie ist; die Zeile wird trotzdem geschrieben.
        }
    }
}
