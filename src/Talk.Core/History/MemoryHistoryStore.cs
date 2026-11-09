namespace Talk.Core.History;

/// <summary>
/// Verlauf nur im Arbeitsspeicher, für den Fall, dass das Speichern abgestellt ist. Hält höchstens <see cref="Capacity"/> Einträge,
/// der älteste fällt heraus. Nach dem Beenden von Talk ist er leer.
/// </summary>
public sealed class MemoryHistoryStore : IHistoryStore
{
    /// <summary>Obergrenze, wenn keine angegeben wird.</summary>
    public const int DefaultCapacity = 50;

    private readonly object _gate = new();
    private readonly LinkedList<HistoryEntry> _entries = new();

    /// <summary>
    /// Legt einen leeren Verlauf an.
    /// </summary>
    /// <param name="capacity">Höchstzahl der Einträge, mindestens 1.</param>
    public MemoryHistoryStore(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>Höchstzahl der Einträge.</summary>
    public int Capacity { get; }

    /// <inheritdoc />
    public Task AddAsync(HistoryEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > Capacity) _entries.RemoveLast();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<HistoryEntry> recent = count <= 0 ? [] : _entries.Take(count).ToList();
            return Task.FromResult(recent);
        }
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _entries.Clear();
        }

        return Task.CompletedTask;
    }
}
