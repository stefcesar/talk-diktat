// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;
using System.Text.Json;

namespace Talk.App.Diagnostics;

/// <summary>
/// Feste Pfade der App unter <c>%LOCALAPPDATA%\Talk</c>: Einstellungen, Protokoll und Modelle.
/// Diese Klasse liest nur. Einstellungen schreiben kommt in Schritt 7.
/// </summary>
/// <remarks>
/// <para>
/// Der Modellordner steht in <c>settings.json</c> unter dem Schlüssel <c>modelsDirectory</c>, damit die großen Modelle
/// auch auf einem anderen Laufwerk liegen können. Fehlt die Datei, der Schlüssel oder ist die Datei
/// unlesbar, gilt der Standardordner <c>%LOCALAPPDATA%\Talk\models</c>. Ein Lesefehler führt nie zu einer Ausnahme.
/// </para>
/// <para>
/// Zwei weitere Schlüssel gehören zur lokalen Erkennung: <c>whisperModel</c> nennt den Dateinamen des Modells im Modellordner,
/// <c>whisperRuntime</c> bevorzugt mit <c>vulkan</c> oder <c>cpu</c> eine Laufzeit (erzwingen kann sie nur der Schalter der Befehlszeile). Beide sind freiwillig.
/// </para>
/// </remarks>
internal static class TalkPaths
{
    private const string ModelsDirectoryKey = "modelsDirectory";
    private const string WhisperModelKey = "whisperModel";
    private const string SwissWhisperModelKey = "whisperModelSwiss";
    private const string DefaultSwissWhisperModelFileName = "ggml-large-v3-turbo-swiss-f16.bin";
    private const string WhisperRuntimeKey = "whisperRuntime";
    private const string HistoryEnabledKey = "historyEnabled";
    private const string VadModelFileName = "ggml-silero-v6.2.0.bin";
    private const string DefaultWhisperModelFileName = "ggml-large-v3-turbo-q5_0.bin";
    private const string RuntimeVulkan = "vulkan";
    private const string RuntimeCpu = "cpu";

    /// <summary>Hauptordner der App: <c>%LOCALAPPDATA%\Talk</c>.</summary>
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Talk");

    /// <summary>Die Einstellungsdatei <c>settings.json</c> im Hauptordner.</summary>
    public static string SettingsPath { get; } = Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>Der Protokollordner <c>logs</c> im Hauptordner.</summary>
    public static string LogDirectory { get; } = Path.Combine(AppDataDirectory, "logs");

    /// <summary>Die Protokolldatei <c>talk.log</c>, in die <see cref="AppLog"/> schreibt.</summary>
    public static string LogPath { get; } = Path.Combine(LogDirectory, "talk.log");

    /// <summary>Der Merkzettel <c>lautstaerke.json</c> der gesenkten Programme (Playbook Abschnitt 25), nur solange gesenkt ist.</summary>
    public static string DuckingJournalPath { get; } = Path.Combine(AppDataDirectory, "lautstaerke.json");

    /// <summary>Die verschlüsselte Verlaufsdatei <c>verlauf.bin</c> im Hauptordner.</summary>
    public static string HistoryPath { get; } = Path.Combine(AppDataDirectory, "verlauf.bin");

    /// <summary>
    /// Ob der Verlauf auf der Festplatte gespeichert wird (Schlüssel <c>historyEnabled</c>, wahr oder falsch). Fehlt der Schlüssel
    /// oder ist er unlesbar, wird gespeichert. Mit falsch bleibt der Verlauf nur im Arbeitsspeicher.
    /// </summary>
    public static bool HistoryEnabled => ReadSettingBool(HistoryEnabledKey) ?? true;

    /// <summary>Der Modellordner, wenn die Einstellungen keinen anderen nennen: <c>models</c> im Hauptordner.</summary>
    public static string DefaultModelsDirectory { get; } = Path.Combine(AppDataDirectory, "models");

    /// <summary>
    /// Der Modellordner aus <see cref="SettingsPath"/> (Schlüssel <c>modelsDirectory</c>) oder <see cref="DefaultModelsDirectory"/>.
    /// Wird bei jedem Zugriff frisch gelesen, damit eine geänderte Einstellung ohne Neustart gilt; die Datei ist winzig.
    /// </summary>
    public static string ModelsDirectory => ReadModelsDirectory() ?? DefaultModelsDirectory;

    /// <summary>Pfad der Modelldatei für die Stilleerkennung (Silero VAD im ggml Format) im Modellordner.</summary>
    public static string VadModelPath => Path.Combine(ModelsDirectory, VadModelFileName);

    /// <summary>
    /// Pfad der Modelldatei für die lokale Erkennung (Whisper im ggml Format) im Modellordner. Der Dateiname kommt aus
    /// <see cref="SettingsPath"/> (Schlüssel <c>whisperModel</c>), sonst gilt <c>ggml-large-v3-turbo-q5_0.bin</c>.
    /// Ein Wert mit Ordneranteil oder unerlaubten Zeichen zählt nicht, das Modell liegt immer im Modellordner.
    /// Ob die Datei da ist, prüft der Erkenner; nachgeladen wird nie.
    /// </summary>
    public static string WhisperModelPath => Path.Combine(ModelsDirectory, ValidFileName(WhisperModelOverride) ?? ReadWhisperModelFileName() ?? DefaultWhisperModelFileName);

    /// <summary>
    /// Pfad des Schweizer Modells für Schweizerdeutsch und Mundart (Playbook Abschnitt 26) im Modellordner. Der Dateiname kommt aus
    /// <see cref="SettingsPath"/> (Schlüssel <c>whisperModelSwiss</c>), sonst gilt <c>ggml-large-v3-turbo-swiss-f16.bin</c>.
    /// Mit <c>--model</c> gilt auch hier das genannte Modell, damit eine Messung genau ein Modell prüft. Fehlt die Datei, rechnet das
    /// normale Modell.
    /// </summary>
    public static string SwissWhisperModelPath => Path.Combine(
        ModelsDirectory,
        ValidFileName(WhisperModelOverride) ?? ValidFileName(ReadSettingString(SwissWhisperModelKey)) ?? DefaultSwissWhisperModelFileName);

    /// <summary>
    /// Dateiname eines Modells nur für diesen Lauf, gesetzt vom Schalter <c>--model</c> der Diagnose. Geht vor der Einstellung.
    /// Wie dort zählt nur ein reiner Dateiname; das Modell liegt immer im Modellordner.
    /// </summary>
    public static string? WhisperModelOverride { get; set; }

    /// <summary>
    /// Die erzwungene Laufzeit der lokalen Erkennung aus <see cref="SettingsPath"/> (Schlüssel <c>whisperRuntime</c>):
    /// <c>vulkan</c> oder <c>cpu</c> in Kleinbuchstaben. Fehlt der Schlüssel oder steht dort etwas anderes, kommt null zurück
    /// und es gilt die Standardreihenfolge.
    /// </summary>
    public static string? WhisperRuntimeSetting
    {
        get
        {
            string? value = ReadSettingString(WhisperRuntimeKey)?.ToLowerInvariant();
            return value is RuntimeVulkan or RuntimeCpu ? value : null;
        }
    }

    // Liest den Modellordner aus der Einstellungsdatei. Fehlt er oder ist er unbrauchbar, kommt null zurück.
    private static string? ReadModelsDirectory()
    {
        try
        {
            string? value = ReadSettingString(ModelsDirectoryKey);
            if (value is null) return null;

            string expanded = Environment.ExpandEnvironmentVariables(value);
            return Path.IsPathRooted(expanded)
                ? Path.GetFullPath(expanded)
                : Path.GetFullPath(Path.Combine(AppDataDirectory, expanded));
        }
        catch
        {
            // Ein unbrauchbarer Pfad ist kein Grund zum Abbruch: der Standardordner gilt.
            return null;
        }
    }

    // Liest den Dateinamen des lokalen Modells. Nur ein reiner Dateiname zählt, damit die Einstellung nie aus dem Modellordner hinausführt.
    private static string? ReadWhisperModelFileName() => ValidFileName(ReadSettingString(WhisperModelKey));

    // Nur ein reiner Dateiname zählt. Alles mit Ordneranteil oder unerlaubten Zeichen ergibt null.
    private static string? ValidFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        if (value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal))
        {
            return null;
        }

        return value;
    }

    // Liest einen Wahrheitswert aus der Einstellungsdatei. Jeder Fehler (Datei fehlt, kein gültiges JSON, falscher Typ) ergibt null.
    private static bool? ReadSettingBool(string key)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;

            using FileStream stream = File.OpenRead(SettingsPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty(key, out JsonElement element)) return null;

            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    // Liest einen Textwert aus der Einstellungsdatei, ohne Leerzeichen an den Rändern.
    // Jeder Fehler (Datei fehlt, kein gültiges JSON, falscher Typ, leerer Wert) ergibt null.
    private static string? ReadSettingString(string key)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;

            using FileStream stream = File.OpenRead(SettingsPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty(key, out JsonElement element)) return null;
            if (element.ValueKind != JsonValueKind.String) return null;

            string? value = element.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            // Unlesbare Einstellungen sind kein Grund zum Abbruch: der Standardwert gilt.
            return null;
        }
    }
}
