using NAudio.CoreAudioApi;

using Talk.Core.Audio;

namespace Talk.App.Audio;

/// <summary>
/// Liefert die aktiven Aufnahmegeräte von Windows über die Core Audio Schnittstelle (NAudio <see cref="MMDeviceEnumerator"/>).
/// </summary>
/// <remarks>
/// Das Standardgerät für Kommunikation (sonst das für die Konsole) steht in der Liste zuerst und trägt <see cref="AudioDeviceInfo.IsDefault"/>.
/// <see cref="AudioDeviceInfo.LikelyBluetoothHandsFree"/> ist true, wenn der Gerätename "Hands-Free", "Freisprech" oder "HFP" enthält
/// (Groß und Kleinschreibung egal); solche Profile nehmen in schlechter Qualität auf.
/// Jeder Aufruf öffnet und schließt seinen eigenen Enumerator, alle Geräteobjekte werden nach dem Lesen freigegeben.
/// Fehler (keine Geräte, COM Fehler) ergeben eine leere Liste oder null, nie eine Ausnahme nach außen.
/// </remarks>
internal sealed class MmDeviceSource : IAudioDeviceSource
{
    private const string FallbackName = "Aufnahmegerät";

    private static readonly string[] HandsFreeMarkers = ["Hands-Free", "Freisprech", "HFP"];

    /// <inheritdoc />
    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
    {
        try
        {
            using MMDeviceEnumerator enumerator = new();
            string? defaultId = GetDefaultId(enumerator);

            List<AudioDeviceInfo> result = [];
            using MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (MMDevice device in devices)
            {
                using (device)
                {
                    AudioDeviceInfo? info = Describe(device, defaultId);
                    if (info is not null) result.Add(info);
                }
            }

            // Das Standardgerät steht immer vorn.
            int defaultIndex = result.FindIndex(d => d.IsDefault);
            if (defaultIndex > 0)
            {
                AudioDeviceInfo defaultDevice = result[defaultIndex];
                result.RemoveAt(defaultIndex);
                result.Insert(0, defaultDevice);
            }

            return result;
        }
        catch
        {
            // Ohne Geräte oder bei einem COM Fehler gibt es einfach keine Auswahl.
            return [];
        }
    }

    /// <inheritdoc />
    public AudioDeviceInfo? GetDefaultDevice()
    {
        try
        {
            using MMDeviceEnumerator enumerator = new();
            using MMDevice? device = TryGetDefault(enumerator);
            return device is null ? null : Describe(device, device.ID);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Liefert das Standardgerät für Aufnahme: zuerst die Rolle Kommunikation, sonst Konsole, sonst null.
    /// Der Aufrufer gibt das Gerät frei.
    /// </summary>
    internal static MMDevice? TryGetDefault(MMDeviceEnumerator enumerator)
    {
        if (enumerator.TryGetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications, out MMDevice? device) && device is not null)
        {
            return device;
        }

        if (enumerator.TryGetDefaultAudioEndpoint(DataFlow.Capture, Role.Console, out device) && device is not null)
        {
            return device;
        }

        return null;
    }

    private static string? GetDefaultId(MMDeviceEnumerator enumerator)
    {
        try
        {
            using MMDevice? device = TryGetDefault(enumerator);
            return device?.ID;
        }
        catch
        {
            return null;
        }
    }

    // Baut die Beschreibung eines Geräts. Ein Gerät ohne lesbare Kennung wird übersprungen.
    private static AudioDeviceInfo? Describe(MMDevice device, string? defaultId)
    {
        string id;
        try
        {
            id = device.ID;
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(id)) return null;

        string name;
        try
        {
            name = device.FriendlyName;
            if (string.IsNullOrWhiteSpace(name)) name = FallbackName;
        }
        catch
        {
            // Der Anzeigename kommt aus dem Eigenschaftenspeicher und kann bei einzelnen Geräten fehlen.
            name = FallbackName;
        }

        bool isDefault = defaultId is not null && string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase);
        bool handsFree = HandsFreeMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));

        return new AudioDeviceInfo(id, name, isDefault, handsFree);
    }
}
