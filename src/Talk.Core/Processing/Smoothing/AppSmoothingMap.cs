namespace Talk.Core.Processing.Smoothing;

/// <summary>
/// Die Stufe der Glättung je Zielprogramm (Playbook Abschnitt 23, Regeln 1 bis 5). Unveränderlich; zwei Listen sind gleich, wenn
/// sie dieselben Namen (ohne Groß und Klein) mit denselben Stufen enthalten.
/// </summary>
public sealed class AppSmoothingMap : IEquatable<AppSmoothingMap>
{
    /// <summary>Höchstzahl der Einträge.</summary>
    public const int MaxEntries = 50;

    /// <summary>Höchstlänge eines Programmnamens.</summary>
    public const int MaxNameLength = 64;

    private readonly KeyValuePair<string, SmoothingLevel>[] _entries;

    private AppSmoothingMap(KeyValuePair<string, SmoothingLevel>[] entries)
    {
        _entries = entries;
    }

    /// <summary>Die leere Liste.</summary>
    public static AppSmoothingMap Empty { get; } = new([]);

    /// <summary>Die Einträge, nach Namen sortiert (ohne Groß und Klein).</summary>
    public IReadOnlyList<KeyValuePair<string, SmoothingLevel>> Entries => _entries;

    /// <summary>Anzahl der Einträge.</summary>
    public int Count => _entries.Length;

    /// <summary>
    /// Der Name eines Programms für die Liste (Regel 1): ohne Leerraum am Rand und ohne ".exe" am Ende. Null, wenn er danach leer ist,
    /// länger als <see cref="MaxNameLength"/> oder ein Zeichen außer Buchstaben, Ziffern, Leerzeichen, Punkt, Unterstrich und
    /// Bindestrich enthält.
    /// </summary>
    public static string? NormalizeName(string? processName)
    {
        if (processName is null) return null;

        string name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4].TrimEnd();
        if (name.Length == 0 || name.Length > MaxNameLength) return null;

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c is not (' ' or '.' or '_' or '-')) return null;
        }

        return name;
    }

    /// <summary>
    /// Baut eine Liste (Regel 2): ungültige Namen und unbekannte Stufen fallen weg, bei gleichem Namen gewinnt der erste, höchstens
    /// die ersten <see cref="MaxEntries"/> gültigen Einträge. Null ergibt die leere Liste.
    /// </summary>
    public static AppSmoothingMap FromEntries(IEnumerable<KeyValuePair<string, SmoothingLevel>>? entries)
    {
        if (entries is null) return Empty;

        List<KeyValuePair<string, SmoothingLevel>> kept = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, SmoothingLevel> entry in entries)
        {
            if (kept.Count >= MaxEntries) break;

            string? name = NormalizeName(entry.Key);
            if (name is null || !Enum.IsDefined(entry.Value) || !seen.Add(name)) continue;
            kept.Add(new(name, entry.Value));
        }

        return Create(kept);
    }

    /// <summary>Die Stufe für das Programm oder null, wenn es keinen Eintrag hat.</summary>
    public SmoothingLevel? Get(string? processName)
    {
        string? name = NormalizeName(processName);
        if (name is null) return null;

        foreach (KeyValuePair<string, SmoothingLevel> entry in _entries)
        {
            if (string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)) return entry.Value;
        }

        return null;
    }

    /// <summary>
    /// Setzt oder entfernt einen Eintrag (Regel 3). Stufe null entfernt ihn. Ein ungültiger Name ändert nichts; bei
    /// <see cref="MaxEntries"/> Einträgen kommt kein neuer Name dazu.
    /// </summary>
    public AppSmoothingMap With(string? processName, SmoothingLevel? level)
    {
        string? name = NormalizeName(processName);
        if (name is null || (level is SmoothingLevel value && !Enum.IsDefined(value))) return this;

        List<KeyValuePair<string, SmoothingLevel>> entries = _entries
            .Where(e => !string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        bool existed = entries.Count != _entries.Length;

        if (level is SmoothingLevel newLevel)
        {
            if (!existed && entries.Count >= MaxEntries) return this;
            entries.Add(new(name, newLevel));
        }

        return Create(entries);
    }

    /// <summary>
    /// Überträgt nur die geänderten Einträge (Regel 3a): Ausgang ist <paramref name="basis"/>; jeder Name, dessen Stufe sich zwischen
    /// <paramref name="old"/> und <paramref name="requested"/> unterscheidet (auch neu oder entfernt), wird mit <see cref="With"/>
    /// auf den gewünschten Stand gebracht. Alle anderen Einträge der Basis bleiben. Null gilt als leere Liste.
    /// </summary>
    public static AppSmoothingMap MergeChanges(AppSmoothingMap? basis, AppSmoothingMap? old, AppSmoothingMap? requested)
    {
        AppSmoothingMap result = basis ?? Empty;
        AppSmoothingMap before = old ?? Empty;
        AppSmoothingMap after = requested ?? Empty;

        IEnumerable<string> names = before._entries.Select(e => e.Key).Concat(after._entries.Select(e => e.Key));
        foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SmoothingLevel? wanted = after.Get(name);
            if (before.Get(name) != wanted) result = result.With(name, wanted);
        }

        return result;
    }

    /// <summary>
    /// Die Stufe für ein Diktat (Regel 5): die Stufe des Programms aus der Liste, auch "Aus", sonst die Standardstufe.
    /// </summary>
    public static SmoothingLevel Resolve(SmoothingLevel standard, AppSmoothingMap? map, string? processName) =>
        map?.Get(processName) ?? standard;

    /// <inheritdoc />
    public bool Equals(AppSmoothingMap? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other._entries.Length != _entries.Length) return false;

        // Beide sind gleich sortiert, deshalb reicht der Vergleich Stelle für Stelle.
        for (int i = 0; i < _entries.Length; i++)
        {
            if (!string.Equals(_entries[i].Key, other._entries[i].Key, StringComparison.OrdinalIgnoreCase)) return false;
            if (_entries[i].Value != other._entries[i].Value) return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as AppSmoothingMap);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (KeyValuePair<string, SmoothingLevel> entry in _entries)
        {
            hash.Add(entry.Key, StringComparer.OrdinalIgnoreCase);
            hash.Add(entry.Value);
        }

        return hash.ToHashCode();
    }

    private static AppSmoothingMap Create(List<KeyValuePair<string, SmoothingLevel>> entries)
    {
        if (entries.Count == 0) return Empty;

        return new AppSmoothingMap(entries
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .ToArray());
    }
}

/// <summary>
/// Lesbare Namen bekannter Programme für das Einstellungsfenster (Playbook Abschnitt 23, Regel 6).
/// </summary>
public static class AppDisplayNames
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WINWORD"] = "Word",
        ["OUTLOOK"] = "Outlook",
        ["olk"] = "Outlook",
        ["EXCEL"] = "Excel",
        ["POWERPNT"] = "PowerPoint",
        ["chrome"] = "Chrome",
        ["msedge"] = "Edge",
        ["firefox"] = "Firefox",
        ["WhatsApp"] = "WhatsApp",
        ["WhatsApp.Root"] = "WhatsApp",
        ["claude"] = "Claude",
        ["notepad"] = "Editor",
        ["Telegram"] = "Telegram",
        ["ms-teams"] = "Teams",
        ["Signal"] = "Signal",
    };

    /// <summary>Der lesbare Name, sonst der Name nach <see cref="AppSmoothingMap.NormalizeName"/>, bei ungültigem Namen leer.</summary>
    public static string For(string? processName)
    {
        string? name = AppSmoothingMap.NormalizeName(processName);
        if (name is null) return string.Empty;
        return Known.TryGetValue(name, out string? display) ? display : name;
    }
}
