namespace Talk.Core.History;

/// <summary>
/// Verwahrt die letzten Diktate, damit nie Text verloren geht.
/// Umsetzungen: im Arbeitsspeicher (<see cref="MemoryHistoryStore"/>) und verschlüsselt auf der Festplatte (in der App).
/// </summary>
public interface IHistoryStore
{
    /// <summary>
    /// Legt einen Eintrag im Verlauf ab.
    /// </summary>
    /// <param name="entry">Der neue Eintrag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    Task AddAsync(HistoryEntry entry, CancellationToken ct);

    /// <summary>
    /// Liefert die jüngsten Einträge, neueste zuerst.
    /// </summary>
    /// <param name="count">Höchstzahl der Einträge.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die jüngsten Einträge.</returns>
    Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct);

    /// <summary>
    /// Löscht alle Einträge.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    Task ClearAsync(CancellationToken ct);
}
