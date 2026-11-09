using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Talk.Core.Audio;

namespace Talk.Core.Transcription;

/// <summary>
/// Verpackt einen Clip im Arbeitsspeicher als WAV (PCM 16 Bit, mono) für die Übertragung. Es wird nie eine Datei geschrieben.
/// </summary>
public static class WavEncoder
{
    /// <summary>Länge des Vorspanns in Byte.</summary>
    public const int HeaderSize = 44;

    /// <summary>
    /// Liefert den Clip als WAV: 44 Byte Vorspann, danach die Abtastwerte als 16 Bit in Little Endian.
    /// </summary>
    public static byte[] Encode(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        int dataBytes = checked(clip.Samples.Length * 2);
        byte[] wav = new byte[HeaderSize + dataBytes];
        Span<byte> s = wav;

        "RIFF"u8.CopyTo(s);
        BinaryPrimitives.WriteInt32LittleEndian(s[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(s[8..]);
        "fmt "u8.CopyTo(s[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(s[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(s[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(s[24..], clip.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(s[28..], clip.SampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(s[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(s[34..], 16);
        "data"u8.CopyTo(s[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(s[40..], dataBytes);

        Span<byte> data = s[HeaderSize..];
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.AsBytes(clip.Samples.AsSpan()).CopyTo(data);
        }
        else
        {
            for (int i = 0; i < clip.Samples.Length; i++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data[(i * 2)..], clip.Samples[i]);
            }
        }

        return wav;
    }
}
