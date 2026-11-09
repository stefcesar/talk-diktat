// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace Talk.Core.History;

/// <summary>
/// Was im Verlauf bleibt (Playbook Abschnitt 18, Teil 7b): höchstens 30 Tage, höchstens 500 Einträge, der neueste zuerst.
/// </summary>
public static class HistoryRetention
{
    /// <summary>Obergrenze der Einträge, wenn keine angegeben wird.</summary>
    public const int DefaultMaxEntries = 500;

    /// <summary>Höchstalter eines Eintrags, wenn keines angegeben wird.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(30);

    /// <summary>
    /// Liefert die Einträge, die bleiben: ohne leere Texte, ohne Einträge, die älter sind als das Höchstalter, der neueste zuerst,
    /// höchstens <paramref name="maxEntries"/>. Ein Eintrag, der genau so alt ist wie das Höchstalter, bleibt. Einträge mit gleicher
    /// Zeit behalten ihre Reihenfolge.
    /// </summary>
    public static IReadOnlyList<HistoryEntry> Prune(
        IEnumerable<HistoryEntry?>? entries,
        DateTimeOffset now,
        TimeSpan? maxAge = null,
        int maxEntries = DefaultMaxEntries)
    {
        if (entries is null || maxEntries <= 0) return [];

        DateTimeOffset oldest = now - (maxAge ?? DefaultMaxAge);
        return entries
            .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Text) && e.At >= oldest)
            .Select(e => e!)
            .OrderByDescending(e => e.At)
            .Take(maxEntries)
            .ToList();
    }
}

/// <summary>
/// Wandelt den Verlauf in Bytes und zurück (JSON in UTF 8 mit Versionsnummer). Die Verschlüsselung liegt außerhalb, in der App.
/// </summary>
public static class HistoryCodec
{
    /// <summary>Die Version des Formats.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Die Einträge als Bytes.
    /// </summary>
    public static byte[] Serialize(IReadOnlyList<HistoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return JsonSerializer.SerializeToUtf8Bytes(new HistoryFile(FormatVersion, [.. entries]));
    }

    /// <summary>
    /// Die Einträge aus den Bytes, oder null, wenn die Bytes kein Verlauf dieser Version sind. Wirft nie.
    /// </summary>
    public static IReadOnlyList<HistoryEntry>? Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            HistoryFile? file = JsonSerializer.Deserialize<HistoryFile>(data);
            if (file is null || file.Version != FormatVersion || file.Entries is null) return null;
            return file.Entries.Where(e => e is not null && e.Text is not null && e.Engine is not null).ToList();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed record HistoryFile(int Version, List<HistoryEntry> Entries);
}
