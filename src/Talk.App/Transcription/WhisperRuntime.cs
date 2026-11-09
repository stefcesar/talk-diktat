// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Whisper.net.LibraryLoader;

namespace Talk.App.Transcription;

/// <summary>
/// Legt fest, in welcher Reihenfolge Whisper.net seine nativen Laufzeiten versucht, und sagt, welche tatsächlich geladen wurde.
/// </summary>
/// <remarks>
/// <para>
/// Die Reihenfolge wirkt nur, wenn sie vor der ersten Factory gesetzt wird, auch vor der Factory der Stilleerkennung, denn beide
/// benutzen dieselbe geladene Bibliothek. <see cref="Configure"/> läuft deshalb ganz am Anfang des Programms. Dabei wird noch nichts
/// geladen; das passiert erst mit der ersten Factory.
/// </para>
/// <para>
/// Standard ist die Grafikkarte über Vulkan, danach die CPU. Lädt die Bibliothek einer Laufzeit nicht, nimmt Whisper.net die nächste.
/// Stürzt der native Code ab, gibt es keinen Rückfall; für diesen Fall lässt sich die CPU erzwingen.
/// </para>
/// <para>
/// Die Einstellungsdatei bevorzugt nur, die Befehlszeile erzwingt: Mit <c>--runtime vulkan</c> steht allein Vulkan in der Reihenfolge,
/// damit ein Vergleich nie still auf der CPU landet. Lädt Vulkan dann nicht, scheitert das Laden, statt auszuweichen.
/// </para>
/// </remarks>
internal static class WhisperRuntime
{
    /// <summary>Wort für die Grafikkarte über Vulkan, in Einstellungen, Befehlszeile und Protokoll.</summary>
    public const string Vulkan = "vulkan";

    /// <summary>Wort für die CPU, in Einstellungen, Befehlszeile und Protokoll.</summary>
    public const string Cpu = "cpu";

    /// <summary>Wort fürs Protokoll, solange keine Laufzeit geladen ist oder eine unerwartete geladen wurde.</summary>
    public const string Unknown = "unbekannt";

    /// <summary>
    /// Setzt die Reihenfolge der Laufzeiten. Der Schalter <c>--runtime</c> erzwingt: <c>vulkan</c> ergibt nur Vulkan ohne Rückfall,
    /// <c>cpu</c> ergibt CPU, CPU ohne AVX. Der Wert aus <c>settings.json</c> bevorzugt: <c>vulkan</c> ergibt Vulkan, CPU, CPU ohne AVX,
    /// <c>cpu</c> ergibt CPU, CPU ohne AVX. Ohne Vorgabe: Vulkan, CPU, CPU ohne AVX.
    /// Die Befehlszeile gewinnt vor der Einstellungsdatei; unbekannte Werte zählen nicht.
    /// </summary>
    /// <param name="setting">Der Wert aus <c>settings.json</c> (Schlüssel <c>whisperRuntime</c>) oder null.</param>
    /// <param name="cliOverride">Der Wert des Schalters <c>--runtime</c> oder null.</param>
    /// <returns>Die Vorgabe, die jetzt gilt: <see cref="Vulkan"/> oder <see cref="Cpu"/>.</returns>
    public static string Configure(string? setting, string? cliOverride)
    {
        string? forced = Normalize(cliOverride);
        string wanted = forced ?? Normalize(setting) ?? Vulkan;

        if (wanted == Cpu)
        {
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx];
        }
        else if (forced is not null)
        {
            // Von der Befehlszeile erzwungen: nur Vulkan, kein Rückfall auf die CPU.
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan];
        }
        else
        {
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx];
        }

        return wanted;
    }

    /// <summary>
    /// Die Laufzeit, die Whisper.net tatsächlich geladen hat: <see cref="Vulkan"/>, <see cref="Cpu"/> oder <see cref="Unknown"/>
    /// (noch nichts geladen oder eine Laufzeit, die Talk nicht ausliefert). Liest nur, setzt nie.
    /// </summary>
    /// <remarks>
    /// Das Wort nennt die geladene Bibliothek. Findet die Vulkan Bibliothek keine Grafikkarte, rechnet sie selbst auf der CPU
    /// und heißt hier trotzdem <c>vulkan</c>; das zeigt sich dann nur an der Rechenzeit.
    /// </remarks>
    public static string LoadedName => RuntimeOptions.LoadedLibrary switch
    {
        RuntimeLibrary.Vulkan => Vulkan,
        RuntimeLibrary.Cpu or RuntimeLibrary.CpuNoAvx => Cpu,
        _ => Unknown,
    };

    // "vulkan" oder "cpu", Groß und Klein und Randleerzeichen egal; alles andere ergibt null.
    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string word = value.Trim().ToLowerInvariant();
        return word is Vulkan or Cpu ? word : null;
    }
}
