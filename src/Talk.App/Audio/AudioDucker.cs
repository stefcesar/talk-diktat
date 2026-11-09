// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.Diagnostics;
using System.IO;

using NAudio.CoreAudioApi;

using Talk.App.Diagnostics;
using Talk.Core.Audio;

namespace Talk.App.Audio;

/// <summary>
/// Stellt andere Programme während der Aufnahme leiser und danach zurück (Playbook Abschnitt 25, Regeln 6 bis 11).
/// </summary>
/// <remarks>
/// Gesenkt wird je Audiositzung, nicht die Gesamtlautstärke. Senken und Zurücksetzen laufen nacheinander in einer Warteschlange im
/// Hintergrund. Vor dem Senken steht der Merkzettel auf der Festplatte; Einträge, die sich nicht zurücksetzen ließen, bleiben darin
/// offen und werden später wieder versucht. Im Protokoll stehen nur Zahlen und feste Wörter, nie ein Programmname und nie eine
/// Kennung.
/// </remarks>
internal sealed class AudioDucker
{
    private readonly string _journalPath;
    private readonly object _gate = new();
    private Task _queue = Task.CompletedTask;

    // Beide nur in der Warteschlange gelesen und geschrieben: die gerade gesenkten und die offenen Einträge von früher.
    private List<DuckEntry> _lowered = [];
    private List<DuckEntry> _open = [];

    public AudioDucker(string journalPath)
    {
        _journalPath = journalPath ?? throw new ArgumentNullException(nameof(journalPath));
    }

    /// <summary>Senkt die anderen Programme, wenn noch nicht gesenkt ist (Regel 6).</summary>
    public void Lower() => Enqueue(LowerNow);

    /// <summary>Setzt die gesenkten und die offenen Programme zurück (Regel 7).</summary>
    public void Restore() => Enqueue(RestoreNow);

    /// <summary>Setzt nach einem harten Ende zurück, was der Merkzettel noch kennt (Regel 8).</summary>
    public void RestorePending() => Enqueue(() =>
    {
        if (!File.Exists(_journalPath)) return;

        // Lässt sich die Datei gerade nicht lesen (zum Beispiel vom Virenscanner gesperrt), bleibt sie für den nächsten Start liegen.
        IReadOnlyList<DuckEntry>? pending = ReadJournal();
        if (pending is null) return;

        AppLog.Write("ducking", "pending", $"sitzungen={pending.Count}");
        _open = [.. pending];
        RestoreNow();

        // Ein Merkzettel ohne lesbaren Eintrag wird gelöscht.
        if (_open.Count == 0 && _lowered.Count == 0) DeleteJournal();
    });

    /// <summary>
    /// Setzt zurück und wartet höchstens <paramref name="timeout"/> darauf. Für Beenden, Abmelden und den Absturzpfad.
    /// </summary>
    public void RestoreAndWait(TimeSpan timeout)
    {
        Restore();
        try
        {
            Task pending;
            lock (_gate) pending = _queue;
            pending.Wait(timeout);
        }
        catch
        {
            // Beim Beenden darf nichts den Prozess festhalten.
        }
    }

    private void Enqueue(Action action)
    {
        lock (_gate)
        {
            _queue = _queue.ContinueWith(_ =>
            {
                try
                {
                    action();
                }
                catch
                {
                    // Jeder Schritt meldet seinen Fehler selbst; die Warteschlange läuft weiter.
                }
            }, TaskScheduler.Default);
        }
    }

    private void LowerNow()
    {
        if (_lowered.Count > 0) return;

        // Offene Einträge von früher zuerst zurücksetzen, sonst würde ihre gesenkte Lautstärke als neue Ausgangslage gelten.
        if (_open.Count > 0) RestoreNow();

        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            List<DuckSession> sessions = ReadSessions();
            // Eine Sitzung, deren Eintrag noch offen ist, wird nicht neu gesenkt: Ihre gesenkte Lautstärke würde sonst als Original gelten.
            HashSet<string> openIds = new(_open.Select(e => e.Id), StringComparer.Ordinal);
            IReadOnlyList<DuckEntry> plan = DuckingRules.PlanLower(sessions.Where(s => !openIds.Contains(s.Id)), Environment.ProcessId);
            if (plan.Count == 0) return;

            // Erst der Merkzettel, dann senken: So bleibt nach einem harten Ende nichts leise, was Talk nicht kennt.
            if (!WriteJournal([.. plan, .. _open])) return;
            _lowered = [.. plan];

            Dictionary<string, DuckEntry> byId = plan.ToDictionary(e => e.Id, StringComparer.Ordinal);
            int lowered = 0;
            ForEachSession((id, control) =>
            {
                if (!byId.TryGetValue(id, out DuckEntry? entry)) return;
                control.SimpleAudioVolume.Volume = entry.Target;
                lowered++;
            });

            AppLog.Write("ducking", "lowered", $"sitzungen={lowered} ms={watch.ElapsedMilliseconds}");
        }
        catch (Exception)
        {
            AppLog.Write("ducking", "error", "grund=senken");
        }
    }

    private void RestoreNow()
    {
        List<DuckEntry> entries = [.. _lowered, .. _open];
        if (entries.Count == 0) return;

        Stopwatch watch = Stopwatch.StartNew();
        List<DuckEntry> stillOpen = [];
        int restored = 0;
        try
        {
            DuckRestorePlan plan = DuckingRules.PlanRestore(entries, ReadSessions());
            stillOpen.AddRange(plan.Remaining);

            // Jeder Auftrag einzeln: Ein Fehler bei einer Sitzung hält die übrigen nicht auf. Scheitert ein Auftrag oder findet er
            // seine Sitzung nicht mehr, bleibt sein Eintrag offen.
            Dictionary<string, DuckSet> bySession = plan.Sets.ToDictionary(s => s.SessionId, StringComparer.Ordinal);
            HashSet<string> applied = new(StringComparer.Ordinal);
            HashSet<DuckEntry> failed = [];
            ForEachSession((id, control) =>
            {
                if (!bySession.TryGetValue(id, out DuckSet? set) || applied.Contains(id)) return;
                try
                {
                    control.SimpleAudioVolume.Volume = set.Volume;
                    applied.Add(id);
                    restored++;
                }
                catch (Exception)
                {
                    failed.Add(set.Entry);
                }
            });

            foreach (DuckSet set in plan.Sets)
            {
                if (!applied.Contains(set.SessionId)) failed.Add(set.Entry);
            }

            stillOpen.AddRange(failed.Where(e => !stillOpen.Contains(e)));
            AppLog.Write("ducking", "restored", $"sitzungen={restored} offen={stillOpen.Count} ms={watch.ElapsedMilliseconds}");
        }
        catch (Exception)
        {
            // Die Sitzungen ließen sich nicht einmal lesen: Alles bleibt offen.
            stillOpen = entries;
            AppLog.Write("ducking", "error", "grund=zuruecksetzen");
        }

        _lowered = [];
        _open = stillOpen;
        if (_open.Count == 0) DeleteJournal();
        else WriteJournal(_open);
    }

    private static List<DuckSession> ReadSessions()
    {
        List<DuckSession> sessions = [];
        ForEachSession((id, control) =>
        {
            sessions.Add(new DuckSession(id, (int)control.GetProcessID, control.SimpleAudioVolume.Volume, control.GetSessionIdentifier));
        });
        return sessions;
    }

    // Geht über alle Sitzungen aller aktiven Wiedergabegeräte. Ein Fehler bei einem Gerät oder einer Sitzung überspringt nur diese;
    // nur wenn sich die Geräte gar nicht aufzählen lassen, kommt der Fehler heraus. Jedes COM Objekt wird nach dem Lesen freigegeben.
    private static void ForEachSession(Action<string, AudioSessionControl> visit)
    {
        using MMDeviceEnumerator enumerator = new();
        using MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (MMDevice device in devices)
        {
            using (device)
            {
                SessionCollection sessions;
                try
                {
                    AudioSessionManager manager = device.AudioSessionManager;
                    manager.RefreshSessions();
                    sessions = manager.Sessions;
                }
                catch (Exception)
                {
                    continue;
                }

                for (int i = 0; i < sessions.Count; i++)
                {
                    try
                    {
                        using AudioSessionControl control = sessions[i];
                        visit(control.GetSessionInstanceIdentifier ?? string.Empty, control);
                    }
                    catch (Exception)
                    {
                        // Diese Sitzung überspringen.
                    }
                }
            }
        }
    }

    // Null, wenn sich die Datei gerade nicht lesen lässt; dann darf sie nicht gelöscht werden.
    private IReadOnlyList<DuckEntry>? ReadJournal()
    {
        try
        {
            return File.Exists(_journalPath) ? DuckingJournal.Parse(File.ReadAllText(_journalPath)) : [];
        }
        catch (Exception)
        {
            AppLog.Write("ducking", "error", "grund=merkzettel");
            return null;
        }
    }

    private bool WriteJournal(IReadOnlyList<DuckEntry> entries)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_journalPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_journalPath, DuckingJournal.Serialize(entries));
            return true;
        }
        catch (Exception)
        {
            AppLog.Write("ducking", "error", "grund=merkzettel");
            return false;
        }
    }

    private void DeleteJournal()
    {
        try
        {
            if (File.Exists(_journalPath)) File.Delete(_journalPath);
        }
        catch (Exception)
        {
            AppLog.Write("ducking", "error", "grund=merkzettel");
        }
    }
}
