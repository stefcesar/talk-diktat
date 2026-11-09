// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Audio;

/// <summary>
/// Ein Audioabschnitt im Arbeitsspeicher: PCM mit 16 Bit, ein Kanal (mono).
/// Audio wird nie auf die Festplatte geschrieben, dieser Typ lebt nur im Speicher.
/// </summary>
public sealed record AudioClip
{
    /// <summary>
    /// Standardabtastrate in Hertz. 16 kHz ist das Format, das die Erkenner erwarten.
    /// </summary>
    public const int DefaultSampleRate = 16000;

    /// <summary>
    /// Erzeugt einen Audioabschnitt.
    /// </summary>
    /// <param name="samples">Abtastwerte als PCM 16 Bit, mono.</param>
    /// <param name="sampleRate">Abtastrate in Hertz, Standard 16000.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="samples"/> null ist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="sampleRate"/> kleiner oder gleich 0 ist.</exception>
    public AudioClip(short[] samples, int sampleRate = DefaultSampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleRate),
                sampleRate,
                "Die Abtastrate muss größer als 0 sein.");
        }

        Samples = samples;
        SampleRate = sampleRate;
    }

    /// <summary>
    /// Abtastwerte als PCM 16 Bit, mono.
    /// </summary>
    public short[] Samples { get; }

    /// <summary>
    /// Abtastrate in Hertz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Dauer des Abschnitts, berechnet aus Anzahl der Abtastwerte geteilt durch die Abtastrate.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
