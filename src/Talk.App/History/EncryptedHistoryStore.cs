// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;
using System.Security.Cryptography;

using Talk.App.Diagnostics;
using Talk.Core.History;

namespace Talk.App.History;

/// <summary>
/// Der Verlauf der App. Mit <c>persist</c> liegt er als Datei im Benutzerprofil, verschlüsselt mit dem Windows Datenschutz (DPAPI):
/// Nur dieses Windows Konto auf diesem Rechner kann sie lesen. Ohne <c>persist</c> lebt er nur im Arbeitsspeicher; die Datei wird
/// dann weder gelesen noch geschrieben, <see cref="ClearAsync"/> räumt aber auch eine Datei von früher weg.
/// Einträge bleiben höchstens 30 Tage, höchstens 500 Stück.
/// </summary>
/// <remarks>
/// <para>
/// Ein neuer Eintrag steht sofort im Arbeitsspeicher; die Datei wird gleich danach im Hintergrund geschrieben, damit das Einfügen
/// nicht darauf wartet. <see cref="FlushAsync"/> wartet auf das Schreiben, die App ruft es vor dem Beenden.
/// </para>
/// <para>
/// Beim Laden werden zwei Fälle getrennt. Lässt sich die Datei nicht entschlüsseln oder hat sie ein fremdes Format, ist sie
/// unlesbar: Der Verlauf beginnt leer, und beim ersten neuen Eintrag wird die alte Datei mit der Endung <c>.unlesbar</c> und einem
/// Zeitstempel beiseitegelegt. Lässt sie sich nur gerade nicht öffnen (gesperrt, Lesefehler), bleibt sie unangetastet: Vor dem
/// nächsten Schreiben wird noch einmal gelesen und zusammengeführt, und solange das nicht gelingt, wird nichts geschrieben.
/// </para>
/// <para>
/// Im Protokoll stehen nur Anzahlen und feste Wörter, nie ein Text aus dem Verlauf.
/// </para>
/// </remarks>
internal sealed class EncryptedHistoryStore : IHistoryStore
{
    // Eine Datei über dieser Größe ist kein Verlauf von Talk (500 Einträge bleiben weit darunter).
    private const long MaxFileBytes = 20 * 1024 * 1024;

    // Bindet die Verschlüsselung an diesen Zweck: Andere Programme desselben Kontos müssten diesen Wert kennen.
    private static readonly byte[] Entropy = "Talk.Verlauf.v1"u8.ToArray();

    private readonly string _path;
    private volatile bool _persist;
    private readonly object _gate = new();
    private List<HistoryEntry> _entries = [];
    private Task _writes = Task.CompletedTask;

    // Nur in der Schreibkette und im Konstruktor angefasst.
    private bool _unreadable;
    private bool _needsMerge;

    public EncryptedHistoryStore(string path, bool persist = true)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _persist = persist;

        if (!persist)
        {
            AppLog.Write("history", "loaded", "eintraege=0 datei=abgestellt");
            return;
        }

        LoadResult loaded = TryLoad(out List<HistoryEntry> stored);
        switch (loaded)
        {
            case LoadResult.Ok:
                _entries = [.. HistoryRetention.Prune(stored, DateTimeOffset.Now)];
                AppLog.Write("history", "loaded", $"eintraege={_entries.Count} datei=ok");

                // Abgelaufene Einträge sollen auch von der Festplatte verschwinden, nicht erst beim nächsten Diktat.
                if (_entries.Count != stored.Count)
                {
                    lock (_gate)
                    {
                        _ = QueueWrite([.. _entries]);
                    }
                }

                break;

            case LoadResult.Missing:
                AppLog.Write("history", "loaded", "eintraege=0 datei=fehlt");
                break;

            case LoadResult.Unreadable:
                _unreadable = true;
                AppLog.Write("history", "loaded", "eintraege=0 datei=unlesbar");
                break;

            default:
                _needsMerge = true;
                AppLog.Write("history", "loaded", "eintraege=0 datei=lesefehler");
                break;
        }
    }

    private enum LoadResult
    {
        Ok,
        Missing,
        Unreadable,
        IoError
    }

    /// <summary>True, wenn der Verlauf auf der Festplatte gespeichert wird.</summary>
    public bool Persisting => _persist;

    /// <summary>
    /// Schaltet das Speichern an oder ab. Beim Anschalten wird eine vorhandene Datei vor dem ersten Schreiben gelesen und mit den
    /// Einträgen im Arbeitsspeicher zusammengeführt. Beim Abschalten bleibt die Datei liegen, bis <see cref="ClearAsync"/> sie löscht.
    /// </summary>
    public void SetPersisting(bool persist)
    {
        lock (_gate)
        {
            if (_persist == persist) return;
            _persist = persist;
            AppLog.Write("history", "mode", persist ? "speichern=an" : "speichern=aus");
            if (!persist) return;

            List<HistoryEntry> snapshot = [.. _entries];
            _writes = _writes.ContinueWith(
                _ =>
                {
                    _needsMerge = true;
                    return Write(snapshot);
                },
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// True, wenn das letzte Schreiben oder Löschen auf der Festplatte gescheitert ist. Die Einträge stehen dann nur im Arbeitsspeicher.
    /// </summary>
    public bool LastWriteFailed { get; private set; }

    public Task AddAsync(HistoryEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _entries.Insert(0, entry);
            _entries = [.. HistoryRetention.Prune(_entries, DateTimeOffset.Now)];
            if (_persist) _ = QueueWrite([.. _entries]);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            // Läuft Talk tagelang ohne Diktat, sollen abgelaufene Einträge trotzdem nicht mehr erscheinen.
            IReadOnlyList<HistoryEntry> recent = count <= 0
                ? []
                : HistoryRetention.Prune(_entries, DateTimeOffset.Now).Take(count).ToList();
            return Task.FromResult(recent);
        }
    }

    /// <summary>
    /// Löscht alle Einträge im Arbeitsspeicher und alle Dateien des Verlaufs: die Verlaufsdatei, eine angefangene Zwischendatei und
    /// beiseitegelegte unlesbare Dateien. Das gilt auch, wenn das Speichern abgestellt ist. Lässt sich eine Datei nicht löschen,
    /// kommt eine <see cref="IOException"/>.
    /// </summary>
    public async Task ClearAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Task<bool> queued;
        lock (_gate)
        {
            _entries = [];
            queued = QueueWrite(null);
        }

        if (!await queued.ConfigureAwait(false)) throw new IOException("Der Verlauf ließ sich auf der Festplatte nicht vollständig löschen.");
    }

    /// <summary>
    /// Wartet, bis alles Vorgemerkte auf der Festplatte steht. Wirft nie.
    /// </summary>
    public Task FlushAsync()
    {
        lock (_gate)
        {
            return _writes;
        }
    }

    // Reiht das Schreiben hinter die laufenden Schreibvorgänge. Mit null werden alle Dateien gelöscht. Nur unter _gate aufrufen.
    // Die Aufgabe liefert, ob es gelungen ist; sie scheitert nie.
    private Task<bool> QueueWrite(List<HistoryEntry>? snapshot)
    {
        Task<bool> next = _writes.ContinueWith(_ => snapshot is null ? DeleteFiles() : Write(snapshot), TaskScheduler.Default);
        _writes = next;
        return next;
    }

    private bool DeleteFiles()
    {
        bool ok = true;
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            List<string> files = [_path, _path + ".tmp"];
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                files.AddRange(Directory.GetFiles(directory, Path.GetFileName(_path) + ".unlesbar*"));
            }

            foreach (string file in files)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch (Exception)
                {
                    ok = false;
                }
            }
        }
        catch (Exception)
        {
            ok = false;
        }

        _unreadable = false;
        _needsMerge = false;
        LastWriteFailed = !ok;
        AppLog.Write("history", ok ? "cleared" : "error", ok ? null : "grund=loeschen");
        return ok;
    }

    private bool Write(List<HistoryEntry> snapshot)
    {
        try
        {
            if (_needsMerge)
            {
                // Beim Start ließ sich die Datei nicht öffnen. Bevor etwas überschrieben wird, noch einmal lesen und zusammenführen.
                switch (TryLoad(out List<HistoryEntry> stored))
                {
                    case LoadResult.Ok:
                        lock (_gate)
                        {
                            _entries = [.. HistoryRetention.Prune(_entries.Concat(stored).Distinct(), DateTimeOffset.Now)];
                            snapshot = [.. _entries];
                        }

                        break;

                    case LoadResult.Unreadable:
                        _unreadable = true;
                        break;

                    case LoadResult.IoError:
                        // Die Datei ist weiter nicht zu öffnen. Nichts überschreiben; die Einträge bleiben im Arbeitsspeicher.
                        LastWriteFailed = true;
                        AppLog.Write("history", "error", "grund=lesen");
                        return false;
                }

                _needsMerge = false;
            }

            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            if (_unreadable && File.Exists(_path))
            {
                // Der Zeitstempel verhindert, dass eine früher beiseitegelegte Datei überschrieben wird.
                File.Move(_path, $"{_path}.unlesbar-{DateTime.Now:yyyyMMdd-HHmmss}");
            }

            _unreadable = false;

            byte[] plain = HistoryCodec.Serialize(snapshot);
            byte[] cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Array.Clear(plain);

            // Erst daneben schreiben, dann tauschen: Ein Absturz mitten im Schreiben lässt die alte Datei ganz.
            string temp = _path + ".tmp";
            File.WriteAllBytes(temp, cipher);
            File.Move(temp, _path, overwrite: true);
            LastWriteFailed = false;
            AppLog.Write("history", "saved", $"eintraege={snapshot.Count}");
            return true;
        }
        catch (Exception)
        {
            // Der Text steht weiter im Arbeitsspeicher. Die Meldung der Ausnahme wird nicht protokolliert.
            LastWriteFailed = true;
            AppLog.Write("history", "error", "grund=schreiben");
            return false;
        }
    }

    // Liest und entschlüsselt die Datei. Unlesbar heißt: nicht zu entschlüsseln, fremdes Format oder viel zu groß.
    // Lesefehler heißt: Die Datei ließ sich gerade nicht öffnen oder lesen; dann darf sie nicht angetastet werden.
    private LoadResult TryLoad(out List<HistoryEntry> entries)
    {
        entries = [];

        byte[] cipher;
        try
        {
            FileInfo info = new(_path);
            if (!info.Exists) return LoadResult.Missing;
            if (info.Length > MaxFileBytes) return LoadResult.Unreadable;
            cipher = File.ReadAllBytes(_path);
        }
        catch (Exception)
        {
            return LoadResult.IoError;
        }

        try
        {
            byte[] plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            IReadOnlyList<HistoryEntry>? stored = HistoryCodec.Deserialize(plain);
            Array.Clear(plain);
            if (stored is null) return LoadResult.Unreadable;

            entries = [.. stored];
            return LoadResult.Ok;
        }
        catch (CryptographicException)
        {
            return LoadResult.Unreadable;
        }
        catch (Exception)
        {
            return LoadResult.IoError;
        }
    }
}
