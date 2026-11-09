namespace Talk.Core.Audio;

/// <summary>
/// Liefert die verfügbaren Aufnahmegeräte. Die Umsetzung fragt Windows, die Steuerung sieht nur diese Liste.
/// </summary>
public interface IAudioDeviceSource
{
    /// <summary>Alle aktiven Aufnahmegeräte, das Standardgerät zuerst.</summary>
    IReadOnlyList<AudioDeviceInfo> ListCaptureDevices();

    /// <summary>Das Standardgerät von Windows oder null, wenn keines vorhanden ist.</summary>
    AudioDeviceInfo? GetDefaultDevice();
}
