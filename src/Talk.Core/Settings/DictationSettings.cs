using System.Text.Json;
using System.Text.Json.Nodes;

using Talk.Core.Processing;
using Talk.Core.Processing.Smoothing;

namespace Talk.Core.Settings;

/// <summary>
/// Die Einstellungen, die der Nutzer in Talk ändern kann (Playbook Abschnitt 18, Teil 7c).
/// </summary>
/// <param name="MicrophoneId">Kennung des Mikrofons oder null für das Standardgerät.</param>
/// <param name="Language">Sprachkürzel des Diktats: de, it, en, de-ch (Schweizerdeutsch) oder gsw (Mundart).</param>
/// <param name="DoubleTapWindowMs">Höchstabstand der beiden Tipps auf Caps Lock in Millisekunden.</param>
/// <param name="PushToTalk">Aufnahme durch Halten der Taste.</param>
/// <param name="LocalOnly">Nichts geht ins Internet.</param>
/// <param name="SoundsEnabled">Töne bei Start, Stopp und Abbruch.</param>
/// <param name="HistoryEnabled">Der Verlauf wird auf der Festplatte gespeichert.</param>
/// <param name="OverlayEnabled">Das Overlay zeigt während Aufnahme und Verarbeitung den Zustand.</param>
/// <param name="SmoothingEnabled">Der Text wird vor dem Einfügen von einem Sprachmodell geglättet. Geht über Groq ins Internet.</param>
/// <param name="Smoothing">Die Stufe der Glättung, wenn sie an ist. Nie <see cref="SmoothingLevel.Off"/>.</param>
/// <param name="SmoothingPerApp">Eigene Stufen je Zielprogramm (Playbook Abschnitt 23). Null gilt wie die leere Liste.</param>
/// <param name="DuckingEnabled">Andere Programme werden während der Aufnahme leiser (Playbook Abschnitt 25).</param>
/// <param name="Dialect">Dialekt für Mundart (Playbook Abschnitt 26): zh, be, bs oder mix (gemischt). Gilt nur bei Sprache gsw.</param>
public sealed record DictationSettings(
    string? MicrophoneId = null,
    string Language = DictationSettings.DefaultLanguage,
    int DoubleTapWindowMs = DictationSettings.DefaultDoubleTapMs,
    bool PushToTalk = false,
    bool LocalOnly = false,
    bool SoundsEnabled = true,
    bool HistoryEnabled = true,
    bool OverlayEnabled = true,
    bool SmoothingEnabled = false,
    SmoothingLevel Smoothing = SmoothingLevel.Smooth,
    AppSmoothingMap? SmoothingPerApp = null,
    bool DuckingEnabled = false,
    string Dialect = DictationSettings.DefaultDialect)
{
    /// <summary>Eigene Stufen je Zielprogramm, nie null: Null aus dem Konstruktor wird die leere Liste, damit Gleichheit stimmt.</summary>
    public AppSmoothingMap SmoothingPerApp { get; init; } = SmoothingPerApp ?? AppSmoothingMap.Empty;

    /// <summary>Sprache, wenn keine oder eine unbekannte eingestellt ist.</summary>
    public const string DefaultLanguage = "de";

    /// <summary>Doppeltipp Fenster, wenn keines eingestellt ist.</summary>
    public const int DefaultDoubleTapMs = 350;

    /// <summary>Kleinstes erlaubtes Doppeltipp Fenster.</summary>
    public const int MinDoubleTapMs = 150;

    /// <summary>Größtes erlaubtes Doppeltipp Fenster.</summary>
    public const int MaxDoubleTapMs = 1000;

    /// <summary>Die Sprachen, die sich einstellen lassen.</summary>
    public static IReadOnlyList<string> Languages { get; } = ["de", "it", "en", "de-ch", "gsw"];

    /// <summary>Dialekt, wenn keiner oder ein unbekannter eingestellt ist: gemischt.</summary>
    public const string DefaultDialect = "mix";

    /// <summary>Die Dialekte, die sich für Mundart einstellen lassen: Zürich, Bern, Basel, gemischt.</summary>
    public static IReadOnlyList<string> Dialects { get; } = ["zh", "be", "bs", "mix"];

    /// <summary>
    /// Bringt die Werte in den erlaubten Bereich: unbekannte Sprache wird Deutsch, unbekannter Dialekt wird gemischt, das Doppeltipp
    /// Fenster wird auf 150 bis 1000 ms begrenzt, eine leere Mikrofonkennung wird null.
    /// </summary>
    public DictationSettings Normalize()
    {
        string language = (Language ?? string.Empty).Trim().ToLowerInvariant();
        if (!Languages.Contains(language)) language = DefaultLanguage;

        string dialect = (Dialect ?? string.Empty).Trim().ToLowerInvariant();
        if (!Dialects.Contains(dialect)) dialect = DefaultDialect;

        string? microphone = string.IsNullOrWhiteSpace(MicrophoneId) ? null : MicrophoneId.Trim();

        return this with
        {
            MicrophoneId = microphone,
            Language = language,
            Dialect = dialect,
            DoubleTapWindowMs = Math.Clamp(DoubleTapWindowMs, MinDoubleTapMs, MaxDoubleTapMs),

            // An und aus regelt SmoothingEnabled. Die Stufe selbst ist nie "aus" und nie ein unbekannter Wert.
            Smoothing = Smoothing is SmoothingLevel.Correct or SmoothingLevel.Smooth or SmoothingLevel.Message ? Smoothing : SmoothingLevel.Smooth,
            SmoothingPerApp = SmoothingPerApp ?? AppSmoothingMap.Empty,
        };
    }
}

/// <summary>
/// Liest und schreibt die Einstellungen als JSON. Schlüssel, die diese Klasse nicht kennt (zum Beispiel der Modellordner),
/// bleiben beim Schreiben erhalten.
/// </summary>
public static class DictationSettingsJson
{
    private const string MicrophoneKey = "microphoneId";
    private const string LanguageKey = "language";
    private const string DoubleTapKey = "doubleTapMs";
    private const string PushToTalkKey = "pushToTalk";
    private const string LocalOnlyKey = "localOnly";
    private const string SoundsKey = "soundsEnabled";
    private const string HistoryKey = "historyEnabled";
    private const string OverlayKey = "overlayEnabled";
    private const string SmoothingEnabledKey = "smoothingEnabled";
    private const string SmoothingLevelKey = "smoothingLevel";
    private const string SmoothingPerAppKey = "smoothingPerApp";
    private const string DuckingKey = "duckingEnabled";
    private const string DialectKey = "dialect";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Die Einstellungen aus dem JSON Text. Fehlt ein Schlüssel oder hat er den falschen Typ, gilt dafür der Standardwert.
    /// Ist der Text leer oder kein JSON Objekt, gelten alle Standardwerte. Das Ergebnis ist immer normalisiert. Wirft nie.
    /// </summary>
    public static DictationSettings Read(string? json)
    {
        DictationSettings defaults = new();
        JsonObject? root = Parse(json);
        if (root is null) return defaults;

        return new DictationSettings(
            MicrophoneId: ReadString(root, MicrophoneKey),
            Language: ReadString(root, LanguageKey) ?? defaults.Language,
            DoubleTapWindowMs: ReadInt(root, DoubleTapKey) ?? defaults.DoubleTapWindowMs,
            PushToTalk: ReadBool(root, PushToTalkKey) ?? defaults.PushToTalk,
            LocalOnly: ReadBool(root, LocalOnlyKey) ?? defaults.LocalOnly,
            SoundsEnabled: ReadBool(root, SoundsKey) ?? defaults.SoundsEnabled,
            HistoryEnabled: ReadBool(root, HistoryKey) ?? defaults.HistoryEnabled,
            OverlayEnabled: ReadBool(root, OverlayKey) ?? defaults.OverlayEnabled,
            SmoothingEnabled: ReadBool(root, SmoothingEnabledKey) ?? defaults.SmoothingEnabled,
            Smoothing: ReadLevel(ReadString(root, SmoothingLevelKey)) ?? defaults.Smoothing,
            SmoothingPerApp: ReadPerApp(root[SmoothingPerAppKey]),
            DuckingEnabled: ReadBool(root, DuckingKey) ?? defaults.DuckingEnabled,
            Dialect: ReadString(root, DialectKey) ?? defaults.Dialect).Normalize();
    }

    /// <summary>
    /// True, wenn der Text leer ist oder ein JSON Objekt. Bei false steht in der Datei etwas, das diese Klasse nicht lesen kann
    /// (Tippfehler nach einer Bearbeitung von Hand, abgeschnitten); dann darf sie nicht überschrieben werden.
    /// </summary>
    public static bool IsReadable(string? json) => string.IsNullOrWhiteSpace(json) || Parse(json) is not null;

    /// <summary>
    /// Der neue JSON Text: die vorhandenen Schlüssel aus <paramref name="existingJson"/> bleiben, die Schlüssel der Einstellungen
    /// werden gesetzt. Ist der vorhandene Text leer, beginnt ein neues Objekt. Ist er nicht leer und kein JSON Objekt, kommt null:
    /// Sonst gingen fremde Schlüssel wie der Modellordner verloren, die sich mit einer kleinen Korrektur retten ließen.
    /// </summary>
    public static string? Merge(string? existingJson, DictationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsReadable(existingJson)) return null;

        DictationSettings normalized = settings.Normalize();
        JsonObject root = Parse(existingJson) ?? [];

        if (normalized.MicrophoneId is null) root.Remove(MicrophoneKey);
        else root[MicrophoneKey] = normalized.MicrophoneId;

        root[LanguageKey] = normalized.Language;
        root[DoubleTapKey] = normalized.DoubleTapWindowMs;
        root[PushToTalkKey] = normalized.PushToTalk;
        root[LocalOnlyKey] = normalized.LocalOnly;
        root[SoundsKey] = normalized.SoundsEnabled;
        root[HistoryKey] = normalized.HistoryEnabled;
        root[OverlayKey] = normalized.OverlayEnabled;
        root[SmoothingEnabledKey] = normalized.SmoothingEnabled;
        root[SmoothingLevelKey] = LevelWord(normalized.Smoothing);
        root[DuckingKey] = normalized.DuckingEnabled;
        root[DialectKey] = normalized.Dialect;

        AppSmoothingMap perApp = normalized.SmoothingPerApp ?? AppSmoothingMap.Empty;
        if (perApp.Count == 0)
        {
            root.Remove(SmoothingPerAppKey);
        }
        else
        {
            JsonObject apps = [];
            foreach (KeyValuePair<string, SmoothingLevel> entry in perApp.Entries) apps[entry.Key] = entry.Value.ToWord();
            root[SmoothingPerAppKey] = apps;
        }

        return root.ToJsonString(WriteOptions);
    }

    // Stufen je Programm (Playbook Abschnitt 23, Regel 8): Name auf Wort; alles andere wird übergangen.
    private static AppSmoothingMap ReadPerApp(JsonNode? node)
    {
        if (node is not JsonObject apps) return AppSmoothingMap.Empty;

        List<KeyValuePair<string, SmoothingLevel>> entries = [];
        foreach (KeyValuePair<string, JsonNode?> app in apps)
        {
            if (app.Value is not JsonValue value || !value.TryGetValue(out string? word)) continue;

            SmoothingLevel? level = (word ?? string.Empty).Trim().ToLowerInvariant() == "aus" ? SmoothingLevel.Off : ReadLevel(word);
            if (level is SmoothingLevel known) entries.Add(new(app.Key, known));
        }

        return AppSmoothingMap.FromEntries(entries);
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Die Stufe steht als Wort in der Datei: fehler, saetze oder nachricht.
    private static string LevelWord(SmoothingLevel level) => level switch
    {
        SmoothingLevel.Correct => "fehler",
        SmoothingLevel.Message => "nachricht",
        _ => "saetze",
    };

    private static SmoothingLevel? ReadLevel(string? word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "fehler" => SmoothingLevel.Correct,
        "saetze" => SmoothingLevel.Smooth,
        "nachricht" => SmoothingLevel.Message,
        _ => null,
    };

    private static string? ReadString(JsonObject root, string key) =>
        root[key] is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static int? ReadInt(JsonObject root, string key) =>
        root[key] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private static bool? ReadBool(JsonObject root, string key) =>
        root[key] is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;
}
