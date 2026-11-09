// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;

using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDataObject = System.Windows.DataObject;
using WpfIDataObject = System.Windows.IDataObject;

namespace Talk.App.Insertion;

/// <summary>Wie ein Einfügeversuch über die Zwischenablage ausging.</summary>
internal enum PasteResult
{
    /// <summary>Der Text lag in der Zwischenablage und Strg+V wurde gesendet.</summary>
    Pasted,

    /// <summary>Die Zwischenablage ließ sich nicht sichern oder setzen, oder Strg+V wurde nicht angenommen. Der Aufrufer tippt.</summary>
    Failed,

    /// <summary>Das Zielfenster war unmittelbar vor Strg+V nicht mehr vorn. Es wurde nichts gesendet.</summary>
    LostFocus
}

/// <summary>
/// Fügt Text über die Zwischenablage ein: alten Inhalt sichern, Text hineinlegen, Strg+V senden, alten Inhalt zurücklegen.
/// Gehört auf den UI Thread, weil die Zwischenablage einen STA Thread verlangt.
/// </summary>
/// <remarks>
/// Der diktierte Text trägt die drei Formate, die ihn aus dem Windows Verlauf und der Cloud heraushalten. Zurückgelegt wird nur,
/// wenn die Folgenummer von Windows noch dieselbe ist, also niemand inzwischen etwas anderes kopiert hat. Bestandteile des alten
/// Inhalts, die sich nicht lesen lassen, gehen beim Zurücklegen verloren (Playbook Abschnitt 8 Punkt 7 und Abschnitt 17).
/// </remarks>
internal sealed class ClipboardPaster
{
    private const string ExcludeFromMonitorsFormat = "ExcludeClipboardContentFromMonitorProcessing";
    private const string HistoryFormat = "CanIncludeInClipboardHistory";
    private const string CloudFormat = "CanUploadToCloudClipboard";
    private const int RestoreAttempts = 3;

    private static readonly TimeSpan RestoreDelay = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan RestoreRetryPause = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Das laufende Zurücklegen der Zwischenablage. Wer die App beendet, wartet vorher darauf.
    /// </summary>
    public Task PendingRestore { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Legt den Text in die Zwischenablage und sendet Strg+V an das Zielfenster. Nach jedem Ausgang außer
    /// <see cref="PasteResult.Pasted"/> ist der alte Inhalt schon wieder zurückgelegt oder nie angefasst worden.
    /// </summary>
    public async Task<PasteResult> PasteAsync(string text, nint target)
    {
        // Ein Zurücklegen aus dem vorigen Diktat zuerst abschließen, sonst würde es den neuen Text überschreiben.
        await PendingRestore;

        if (!TryBackup(out WpfDataObject? backup)) return PasteResult.Failed;

        uint before = InsertNative.GetClipboardSequenceNumber();
        if (!TrySetText(text))
        {
            // Das Setzen kann halb gelungen sein. Hat sich die Zwischenablage geändert, kommt der alte Inhalt sofort zurück.
            uint now = InsertNative.GetClipboardSequenceNumber();
            if (now != before) await RestoreAsync(backup, now);
            return PasteResult.Failed;
        }

        uint sequence = InsertNative.GetClipboardSequenceNumber();

        // Sichern und Setzen haben Zeit gekostet. Strg+V geht nur hinaus, wenn das Ziel jetzt noch vorn ist.
        if (InsertNative.GetForegroundWindow() != target)
        {
            await RestoreAsync(backup, sequence);
            return PasteResult.LostFocus;
        }

        if (!KeyboardTyper.SendPaste())
        {
            await RestoreAsync(backup, sequence);
            return PasteResult.Failed;
        }

        PendingRestore = RestoreLaterAsync(backup, sequence);
        return PasteResult.Pasted;
    }

    private static async Task RestoreLaterAsync(WpfDataObject? backup, uint sequence)
    {
        // Das Zielprogramm liest die Zwischenablage erst nach dem Tastendruck. Vorher zurücklegen hieße, den alten Inhalt einzufügen.
        await Task.Delay(RestoreDelay);
        await RestoreAsync(backup, sequence);
    }

    // Legt den alten Inhalt zurück, solange die Folgenummer noch die erwartete ist. Ist die Zwischenablage gerade von einem anderen
    // Programm belegt, folgen zwei weitere Versuche. Scheitern alle, bleibt der diktierte Text darin stehen.
    private static async Task RestoreAsync(WpfDataObject? backup, uint sequence)
    {
        for (int attempt = 1; attempt <= RestoreAttempts; attempt++)
        {
            try
            {
                if (InsertNative.GetClipboardSequenceNumber() != sequence) return;

                if (backup is null) WpfClipboard.Clear();
                else WpfClipboard.SetDataObject(backup, true);
                return;
            }
            catch
            {
                if (attempt < RestoreAttempts) await Task.Delay(RestoreRetryPause);
            }
        }
    }

    // Kopiert jeden Bestandteil des alten Inhalts in ein eigenes Objekt. backup ist null, wenn die Zwischenablage leer war.
    // False, wenn sich der Inhalt nicht lesen ließ: Dann wird die Zwischenablage nicht angefasst.
    private static bool TryBackup(out WpfDataObject? backup)
    {
        backup = null;
        try
        {
            WpfIDataObject? current = WpfClipboard.GetDataObject();
            string[] formats = current?.GetFormats(false) ?? [];
            if (current is null || formats.Length == 0) return true;

            WpfDataObject copy = new();
            int copied = 0;
            foreach (string format in formats)
            {
                try
                {
                    object? data = current.GetData(format, false);
                    if (data is null) continue;
                    copy.SetData(format, data, false);
                    copied++;
                }
                catch
                {
                    // Einzelne Bestandteile lassen sich nicht lesen (zum Beispiel verzögert gelieferte). Die übrigen werden gesichert.
                }
            }

            if (copied == 0) return false;
            backup = copy;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetText(string text)
    {
        try
        {
            WpfDataObject data = new();
            data.SetData(WpfDataFormats.UnicodeText, text);

            // Das erste Format wirkt durch seine bloße Anwesenheit, die beiden anderen tragen den Wert 0 als DWORD.
            data.SetData(ExcludeFromMonitorsFormat, new MemoryStream(new byte[4]));
            data.SetData(HistoryFormat, new MemoryStream(new byte[4]));
            data.SetData(CloudFormat, new MemoryStream(new byte[4]));

            WpfClipboard.SetDataObject(data, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
