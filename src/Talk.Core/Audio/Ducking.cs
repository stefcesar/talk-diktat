using System.Text.Json;
using System.Text.Json.Nodes;

namespace Talk.Core.Audio;

/// <summary>
/// Eine Audiositzung eines Programms auf einem Wiedergabegerät (Playbook Abschnitt 25, Regel 1).
/// </summary>
/// <param name="Id">Die Kennung der Sitzung von Windows; enthält die Prozessnummer.</param>
/// <param name="ProcessId">Die Prozessnummer; 0 sind die Systemklänge.</param>
/// <param name="Volume">Die Lautstärke von 0 bis 1.</param>
/// <param name="GroupId">Die Gruppenkennung ohne Prozessnummer; bleibt beim Neustart des Programms gleich. Darf leer sein.</param>
public sealed record DuckSession(string Id, int ProcessId, float Volume, string? GroupId = null);

/// <summary>
/// Eine gesenkte Sitzung: die Lautstärke vorher, die, auf die Talk sie gesetzt hat, und die Gruppenkennung.
/// </summary>
public sealed record DuckEntry(string Id, float Original, float Target, string GroupId = "");

/// <summary>Ein Auftrag beim Zurücksetzen: Diese Sitzung bekommt diese Lautstärke, wegen dieses Eintrags.</summary>
public sealed record DuckSet(string SessionId, float Volume, DuckEntry Entry);

/// <summary>Der Plan fürs Zurücksetzen (Regel 3a).</summary>
/// <param name="Sets">Die Aufträge.</param>
/// <param name="Done">Einträge mit mindestens einer Treffersitzung; sie sind nach den Aufträgen erledigt.</param>
/// <param name="Remaining">Einträge ohne Treffer; sie bleiben offen.</param>
public sealed record DuckRestorePlan(IReadOnlyList<DuckSet> Sets, IReadOnlyList<DuckEntry> Done, IReadOnlyList<DuckEntry> Remaining);

/// <summary>
/// Die Regeln, nach denen Talk andere Programme während der Aufnahme leiser stellt (Playbook Abschnitt 25, Regeln 2, 3 und 3a).
/// </summary>
public static class DuckingRules
{
    /// <summary>Gesenkt wird auf diesen Anteil der Lautstärke.</summary>
    public const float Factor = 0.2f;

    /// <summary>Unterhalb dieser Lautstärke gilt eine Sitzung als stumm und wird nicht angefasst.</summary>
    public const float SilentBelow = 0.001f;

    /// <summary>So weit darf die jetzige Lautstärke vom gesetzten Wert abweichen, damit zurückgesetzt wird.</summary>
    public const float RestoreTolerance = 0.02f;

    /// <summary>
    /// Die Sitzungen, die gesenkt werden (Regel 2): nicht Systemklänge, nicht Talk selbst, mit Kennung, nicht stumm; bei gleicher
    /// Kennung zählt nur die erste. Null ergibt eine leere Liste.
    /// </summary>
    public static IReadOnlyList<DuckEntry> PlanLower(IEnumerable<DuckSession>? sessions, int ownProcessId)
    {
        if (sessions is null) return [];

        List<DuckEntry> plan = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (DuckSession session in sessions)
        {
            if (session is null || session.ProcessId == 0 || session.ProcessId == ownProcessId) continue;
            if (string.IsNullOrEmpty(session.Id) || !seen.Add(session.Id)) continue;

            float original = Clamp(session.Volume);
            if (original <= SilentBelow) continue;

            plan.Add(new DuckEntry(session.Id, original, original * Factor, session.GroupId ?? string.Empty));
        }

        return plan;
    }

    /// <summary>
    /// True, wenn die Sitzung zurückgesetzt wird (Regel 3): Die jetzige Lautstärke weicht höchstens <see cref="RestoreTolerance"/>
    /// vom gesetzten Wert ab. Sonst hat der Nutzer sie inzwischen selbst verändert.
    /// </summary>
    public static bool ShouldRestore(DuckEntry entry, float currentVolume)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Math.Abs(currentVolume - entry.Target) <= RestoreTolerance;
    }

    /// <summary>
    /// Plant das Zurücksetzen (Regel 3a): Treffer sind Sitzungen mit gleicher Kennung, ohne solche die mit gleicher, nicht leerer
    /// Gruppenkennung. Ohne Treffer bleibt der Eintrag offen; mit Treffern ist er erledigt, und jeder Treffer nach Regel 3 bekommt
    /// einen Auftrag. Eine Sitzung bekommt höchstens einen Auftrag.
    /// </summary>
    public static DuckRestorePlan PlanRestore(IEnumerable<DuckEntry>? entries, IEnumerable<DuckSession>? sessions)
    {
        List<DuckSession> all = sessions?.Where(s => s is not null && !string.IsNullOrEmpty(s.Id)).ToList() ?? [];
        List<DuckSet> sets = [];
        List<DuckEntry> done = [];
        List<DuckEntry> remaining = [];
        HashSet<string> assigned = new(StringComparer.Ordinal);

        // Erst alle Treffer über die Kennung, dann die über die Gruppe: Sonst könnte ein Eintrag über die Gruppe die Sitzung eines
        // anderen Eintrags bekommen, die über ihre eigene Kennung zurückgehört.
        List<DuckEntry> list = entries?.Where(e => e is not null).ToList() ?? [];
        List<DuckEntry> withoutId = [];
        foreach (DuckEntry entry in list)
        {
            List<DuckSession> hits = all.Where(s => string.Equals(s.Id, entry.Id, StringComparison.Ordinal)).ToList();
            if (hits.Count == 0)
            {
                withoutId.Add(entry);
                continue;
            }

            Assign(entry, hits);
        }

        foreach (DuckEntry entry in withoutId)
        {
            List<DuckSession> hits = string.IsNullOrEmpty(entry.GroupId)
                ? []
                : all.Where(s => string.Equals(s.GroupId, entry.GroupId, StringComparison.Ordinal)).ToList();
            if (hits.Count == 0)
            {
                remaining.Add(entry);
                continue;
            }

            Assign(entry, hits);
        }

        return new DuckRestorePlan(sets, done, remaining);

        void Assign(DuckEntry entry, List<DuckSession> hits)
        {
            done.Add(entry);
            foreach (DuckSession hit in hits)
            {
                if (ShouldRestore(entry, hit.Volume) && assigned.Add(hit.Id)) sets.Add(new DuckSet(hit.Id, entry.Original, entry));
            }
        }
    }

    internal static float Clamp(float value) => float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);
}

/// <summary>
/// Der Merkzettel der gesenkten Sitzungen (Playbook Abschnitt 25, Regel 4). Damit setzt Talk die Lautstärke auch nach einem harten
/// Ende beim nächsten Start zurück.
/// </summary>
public static class DuckingJournal
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Die Einträge als JSON Liste mit <c>id</c>, <c>original</c>, <c>target</c> und <c>group</c>.</summary>
    public static string Serialize(IEnumerable<DuckEntry>? entries)
    {
        JsonArray list = [];
        foreach (DuckEntry entry in entries ?? [])
        {
            if (entry is null) continue;
            list.Add(new JsonObject
            {
                ["id"] = entry.Id,
                ["original"] = entry.Original,
                ["target"] = entry.Target,
                ["group"] = entry.GroupId ?? string.Empty,
            });
        }

        return list.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Liest den Merkzettel. Wirft nie: Kein gültiges JSON oder keine Liste ergibt eine leere Liste; Einträge, die sich nicht lesen
    /// lassen (doppelte Schlüssel, kein Text als Kennung, keine Zahlen), fallen weg; eine fehlende Gruppenkennung wird leer; Zahlen
    /// werden auf 0 bis 1 begrenzt; bei gleicher Kennung gilt der erste Eintrag.
    /// </summary>
    public static IReadOnlyList<DuckEntry> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        JsonArray? list;
        try
        {
            list = JsonNode.Parse(text) as JsonArray;
        }
        catch (Exception)
        {
            return [];
        }

        if (list is null) return [];

        List<DuckEntry> entries = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < list.Count; i++)
        {
            // Ein Eintrag mit doppelten Schlüsseln wirft erst beim Zugriff. Er fällt dann weg, die übrigen bleiben.
            try
            {
                if (list[i] is not JsonObject item) continue;
                if (!TryString(item["id"], out string? id) || string.IsNullOrEmpty(id)) continue;
                if (!TryNumber(item["original"], out float original) || !TryNumber(item["target"], out float target)) continue;
                string group = TryString(item["group"], out string? g) && g is not null ? g : string.Empty;
                if (!seen.Add(id)) continue;

                entries.Add(new DuckEntry(id, DuckingRules.Clamp(original), DuckingRules.Clamp(target), group));
            }
            catch (Exception)
            {
                // Unlesbarer Eintrag.
            }
        }

        return entries;
    }

    private static bool TryString(JsonNode? node, out string? value)
    {
        value = null;
        return node is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryNumber(JsonNode? node, out float value)
    {
        value = 0f;
        if (node is not JsonValue v) return false;
        if (v.TryGetValue(out double number) && double.IsFinite(number))
        {
            value = (float)number;
            return true;
        }

        return false;
    }
}
