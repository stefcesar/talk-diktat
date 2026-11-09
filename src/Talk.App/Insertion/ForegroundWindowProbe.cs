using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Text;

using Talk.Core.Insertion;

namespace Talk.App.Insertion;

/// <summary>
/// Merkt sich das Fenster, das vorn ist, und liest die Fakten, nach denen <see cref="InsertPlanner"/> den Weg entscheidet.
/// Liest nie Fenstertitel oder Inhalte, nur Kennung, Prozessname, Fensterklasse, Rechte und die Art des Felds mit Fokus.
/// </summary>
internal static class ForegroundWindowProbe
{
    private static readonly TimeSpan ForegroundTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ForegroundPoll = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan AutomationTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Das Fenster, das jetzt vorn ist, oder null, wenn keines vorn ist.
    /// </summary>
    public static TargetWindow? Capture()
    {
        nint handle = InsertNative.GetForegroundWindow();
        if (handle == 0) return null;

        InsertNative.GetWindowThreadProcessId(handle, out uint processId);
        return new TargetWindow(handle, ReadProcessName(processId), ReadClassName(handle));
    }

    /// <summary>
    /// Die Prozessnummer, zu der das Fenster gehört, oder 0.
    /// </summary>
    public static uint ProcessIdOf(nint handle)
    {
        InsertNative.GetWindowThreadProcessId(handle, out uint processId);
        return processId;
    }

    /// <summary>
    /// Die Fakten zum Ziel ohne das Passwortfeld. Das lässt sich erst lesen, wenn das Fenster vorn ist
    /// (<see cref="IsPasswordFocusedAsync"/>).
    /// </summary>
    public static InsertTargetFacts ReadFacts(TargetWindow? target)
    {
        if (target is null) return new InsertTargetFacts(false, false, false, false, false, false);

        bool exists = InsertNative.IsWindow(target.Handle);
        uint processId = 0;
        if (exists) InsertNative.GetWindowThreadProcessId(target.Handle, out processId);

        return new InsertTargetFacts(
            HasTarget: true,
            IsOwnWindow: exists && processId == (uint)Environment.ProcessId,
            Exists: exists,
            HigherRights: exists && HasHigherRights(processId),
            PasswordField: false,
            IsTerminal: TerminalDetector.IsTerminal(target.ProcessName, target.ClassName));
    }

    /// <summary>
    /// True, wenn das Fenster vorn ist. Ist es das nicht, wird es einmal nach vorn geholt und kurz gewartet.
    /// </summary>
    public static async Task<bool> EnsureForegroundAsync(nint handle)
    {
        if (InsertNative.GetForegroundWindow() == handle) return true;

        // Ein gesperrtes Fenster hat einen eigenen Dialog offen. Dorthin geht nichts.
        if (!InsertNative.IsWindowEnabled(handle)) return false;

        InsertNative.SetForegroundWindow(handle);
        Stopwatch waited = Stopwatch.StartNew();
        while (waited.Elapsed < ForegroundTimeout)
        {
            await Task.Delay(ForegroundPoll);
            if (InsertNative.GetForegroundWindow() == handle) return true;
        }

        return false;
    }

    /// <summary>
    /// True, wenn das Feld mit dem Fokus im Zielfenster ein Passwortfeld ist. Zuerst der Stil eines klassischen Eingabefelds,
    /// dann die Windows Bedienhilfen mit Zeitlimit. Ohne Antwort gilt das Feld nicht als Passwortfeld (Playbook Abschnitt 8 Punkt 6).
    /// </summary>
    public static async Task<bool> IsPasswordFocusedAsync(nint handle)
    {
        if (HasPasswordStyle(handle)) return true;

        try
        {
            // Die Bedienhilfen fragen den fremden Prozess. Antwortet er nicht, läuft die Abfrage im Hintergrund aus.
            Task<bool> query = Task.Run(() => AutomationElement.FocusedElement?.Current.IsPassword == true);
            Task finished = await Task.WhenAny(query, Task.Delay(AutomationTimeout));
            return finished == query && query.Status == TaskStatus.RanToCompletion && query.Result;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Die höchstens <see cref="ContextChars"/> Zeichen vor dem Cursor im Feld mit Fokus, bei markiertem Text vor der Markierung
    /// (Playbook Abschnitt 24, Regel 7). Leer, wenn der Cursor am Anfang steht. Null, wenn das Feld kein Textmuster hat, ein
    /// Passwortfeld ist, ein Fehler auftritt oder die Antwort nicht innerhalb des Zeitlimits kommt. Die Zeichen gehen nie ins
    /// Protokoll und werden nur für die eine Entscheidung gebraucht.
    /// </summary>
    public static async Task<string?> ReadTextBeforeCaretAsync(nint handle)
    {
        try
        {
            uint processId = ProcessIdOf(handle);
            Task<string?> query = Task.Run(() => ReadTextBeforeCaret(processId));
            Task finished = await Task.WhenAny(query, Task.Delay(ContextTimeout));
            return finished == query && query.Status == TaskStatus.RanToCompletion ? query.Result : null;
        }
        catch
        {
            return null;
        }
    }

    private const int ContextChars = 4;
    private static readonly TimeSpan ContextTimeout = TimeSpan.FromMilliseconds(150);

    private static string? ReadTextBeforeCaret(uint processId)
    {
        try
        {
            // Das Feld mit Fokus muss zum Zielprogramm gehören; sonst wäre es ein anderes Programm als das, in das eingefügt wird.
            AutomationElement? element = AutomationElement.FocusedElement;
            if (element is null || processId == 0 || element.Current.ProcessId != (int)processId || element.Current.IsPassword) return null;
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)) return null;

            TextPatternRange[] selection = ((TextPattern)pattern).GetSelection();
            if (selection.Length == 0) return null;

            // Ein leerer Bereich am Anfang der Markierung, dessen Anfang um höchstens vier Zeichen zurückgeschoben wird.
            TextPatternRange range = selection[0].Clone();
            range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -ContextChars);
            // Die Bedienhilfen zählen ein Emoji oder CR LF als ein Zeichen. Deshalb den ganzen Bereich lesen und hinten die letzten
            // vier Stellen nehmen; GetText(4) schnitte sonst gerade das Zeichen direkt vor dem Cursor ab.
            string text = range.GetText(-1) ?? string.Empty;
            return text.Length > ContextChars ? text[^ContextChars..] : text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lädt die Windows Bedienhilfen im Hintergrund vor. Der erste Aufruf dauert sonst leicht länger als das Zeitlimit,
    /// und gerade das erste Diktat nach dem Start bliebe ohne Passwortprüfung.
    /// </summary>
    public static void WarmUp()
    {
        _ = Task.Run(() =>
        {
            try
            {
                _ = AutomationElement.FocusedElement;
            }
            catch
            {
                // Nur eine Abkürzung. Scheitert sie, lädt die erste echte Abfrage.
            }
        });
    }

    private static bool HasPasswordStyle(nint handle)
    {
        uint threadId = InsertNative.GetWindowThreadProcessId(handle, out _);
        if (threadId == 0) return false;

        InsertNative.GUITHREADINFO info = new() { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<InsertNative.GUITHREADINFO>() };
        if (!InsertNative.GetGUIThreadInfo(threadId, ref info) || info.hwndFocus == 0) return false;

        // Eingabefelder heißen je nach Baukasten anders (Edit, WindowsForms10.EDIT, TEdit); allen gemeinsam ist "edit" im Klassennamen.
        if (!ReadClassName(info.hwndFocus).Contains("edit", StringComparison.OrdinalIgnoreCase)) return false;
        return (InsertNative.GetWindowLong(info.hwndFocus, InsertNative.GWL_STYLE) & InsertNative.ES_PASSWORD) != 0;
    }

    // Höhere Rechte: Der Zielprozess ist erhöht und Talk nicht. Lässt sich der Prozess oder sein Token nicht öffnen, gilt er als höher berechtigt.
    private static bool HasHigherRights(uint processId)
    {
        if (Environment.IsPrivilegedProcess) return false;

        nint process = InsertNative.OpenProcess(InsertNative.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0) return true;

        try
        {
            if (!InsertNative.OpenProcessToken(process, InsertNative.TOKEN_QUERY, out nint token)) return true;

            try
            {
                return !InsertNative.GetTokenInformation(token, InsertNative.TokenElevation, out int elevated, sizeof(int), out _)
                    || elevated != 0;
            }
            finally
            {
                InsertNative.CloseHandle(token);
            }
        }
        finally
        {
            InsertNative.CloseHandle(process);
        }
    }

    private static string ReadProcessName(uint processId)
    {
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadClassName(nint handle)
    {
        char[] buffer = new char[256];
        int length = InsertNative.GetClassNameW(handle, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }
}
