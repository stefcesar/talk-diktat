// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Audio;

/// <summary>
/// Warum eine Aufnahme nicht möglich war. Die App übersetzt das in eine verständliche Meldung im Overlay.
/// </summary>
public enum RecordingError
{
    /// <summary>Kein Aufnahmegerät vorhanden.</summary>
    NoDevice,

    /// <summary>Das Gerät wird gerade von einem anderen Programm exklusiv benutzt.</summary>
    DeviceInUse,

    /// <summary>Das Gerät wurde während der Aufnahme entfernt oder ungültig.</summary>
    DeviceLost,

    /// <summary>Das Gerät liefert ein Format, das nicht umgerechnet werden kann.</summary>
    UnsupportedFormat,

    /// <summary>Alles andere. Einzelheiten stehen im Protokoll, ohne Inhalte.</summary>
    Unknown,
}

/// <summary>Ausnahme einer fehlgeschlagenen Aufnahme mit dem Grund aus <see cref="RecordingError"/>.</summary>
public sealed class AudioRecorderException : Exception
{
    /// <summary>Erzeugt die Ausnahme mit Grund und Meldung.</summary>
    public AudioRecorderException(RecordingError error, string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
    }

    /// <summary>Der Grund.</summary>
    public RecordingError Error { get; }
}
