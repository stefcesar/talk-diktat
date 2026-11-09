namespace Talk.Core.Audio;

/// <summary>
/// Ein Aufnahmegerät (Mikrofon), so wie die App es zur Auswahl anbietet.
/// </summary>
/// <param name="Id">Kennung des Geräts in Windows. Wird in den Einstellungen gespeichert.</param>
/// <param name="Name">Anzeigename für die Auswahl.</param>
/// <param name="IsDefault">True für das Standardgerät von Windows.</param>
/// <param name="LikelyBluetoothHandsFree">True, wenn der Name auf ein Bluetooth Freisprechprofil hindeutet. Dann warnt die App vor schlechter Aufnahmequalität.</param>
public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault, bool LikelyBluetoothHandsFree);
