// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Text;
using Talk.Core.Audio;
using Talk.Core.Transcription;
using Xunit;

namespace Talk.Core.Tests.Transcription;

/// <summary>
/// Prüft die WAV Verpackung ausschließlich gegen Regel 1 der reinen Logik in PLAYBOOK.md, Abschnitt 15.
/// Die Tests kennen die Umsetzung nicht. Alle Zahlen im Vorspann stehen mit dem niederwertigen Byte zuerst.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class WavEncoderTests
{
    private static string Zeichen(byte[] bytes, int anfang) => Encoding.ASCII.GetString(bytes, anfang, 4);

    private static uint Zahl32(byte[] bytes, int anfang) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(anfang, 4));

    private static ushort Zahl16(byte[] bytes, int anfang) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(anfang, 2));

    /// <summary>Clip mit gut unterscheidbaren Werten (Wert gleich Index mal 3 minus 100).</summary>
    private static AudioClip ClipMitWerten(int anzahl, int abtastrate = 16000)
    {
        var werte = new short[anzahl];
        for (var i = 0; i < anzahl; i++)
        {
            werte[i] = (short)((i * 3) - 100);
        }

        return new AudioClip(werte, abtastrate);
    }

    // Regel 1 (Vertrag): Der Vorspann ist 44 Byte lang.
    [Fact]
    public void HeaderSize_ist_44()
    {
        Assert.Equal(44, WavEncoder.HeaderSize);
    }

    // Regel 1: Länge ist 44 plus zweimal die Zahl der Abtastwerte.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(1600)]
    [InlineData(16000)]
    public void Laenge_ist_44_plus_zweimal_Anzahl(int anzahl)
    {
        var wav = WavEncoder.Encode(ClipMitWerten(anzahl));

        Assert.Equal(44 + (2 * anzahl), wav.Length);
    }

    // Regel 1: Byte 0 bis 3 "RIFF", 8 bis 11 "WAVE", 12 bis 15 "fmt " mit Leerzeichen, 36 bis 39 "data".
    [Fact]
    public void Kennungen_stehen_an_den_festen_Stellen()
    {
        var wav = WavEncoder.Encode(ClipMitWerten(10));

        Assert.Equal("RIFF", Zeichen(wav, 0));
        Assert.Equal("WAVE", Zeichen(wav, 8));
        Assert.Equal("fmt ", Zeichen(wav, 12));
        Assert.Equal("data", Zeichen(wav, 36));
    }

    // Regel 1: Byte 4 bis 7 ist 36 plus Datenlänge, Byte 40 bis 43 ist die Datenlänge.
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1600)]
    public void Laengenfelder_nennen_36_plus_Datenlaenge_und_Datenlaenge(int anzahl)
    {
        var wav = WavEncoder.Encode(ClipMitWerten(anzahl));

        var datenlaenge = (uint)(2 * anzahl);
        Assert.Equal(36u + datenlaenge, Zahl32(wav, 4));
        Assert.Equal(datenlaenge, Zahl32(wav, 40));
    }

    // Regel 1: Byte 16 bis 19 die Zahl 16, 20 bis 21 die Zahl 1 (PCM), 22 bis 23 die Zahl 1 (mono),
    // 32 bis 33 die Zahl 2, 34 bis 35 die Zahl 16.
    [Fact]
    public void Feste_Formatfelder_stimmen()
    {
        var wav = WavEncoder.Encode(ClipMitWerten(10));

        Assert.Equal(16u, Zahl32(wav, 16));
        Assert.Equal((ushort)1, Zahl16(wav, 20));
        Assert.Equal((ushort)1, Zahl16(wav, 22));
        Assert.Equal((ushort)2, Zahl16(wav, 32));
        Assert.Equal((ushort)16, Zahl16(wav, 34));
    }

    // Regel 1: Byte 24 bis 27 die Abtastrate, 28 bis 31 Abtastrate mal 2.
    [Theory]
    [InlineData(16000)]
    [InlineData(8000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void Abtastrate_und_Byterate_stehen_im_Vorspann(int abtastrate)
    {
        var wav = WavEncoder.Encode(ClipMitWerten(10, abtastrate));

        Assert.Equal((uint)abtastrate, Zahl32(wav, 24));
        Assert.Equal((uint)(abtastrate * 2), Zahl32(wav, 28));
    }

    // Regel 1: Alle Zahlen niederwertiges Byte zuerst, geprüft an den rohen Bytes der Abtastrate 16000 (0x3E80).
    [Fact]
    public void Zahlen_im_Vorspann_haben_das_niederwertige_Byte_zuerst()
    {
        var wav = WavEncoder.Encode(ClipMitWerten(3));

        // Abtastrate 16000 ist 0x00003E80, Byterate 32000 ist 0x00007D00, Datenlänge 6, Gesamtfeld 42.
        Assert.Equal(new byte[] { 0x80, 0x3E, 0x00, 0x00 }, wav[24..28]);
        Assert.Equal(new byte[] { 0x00, 0x7D, 0x00, 0x00 }, wav[28..32]);
        Assert.Equal(new byte[] { 42, 0, 0, 0 }, wav[4..8]);
        Assert.Equal(new byte[] { 6, 0, 0, 0 }, wav[40..44]);
        Assert.Equal(new byte[] { 16, 0, 0, 0 }, wav[16..20]);
        Assert.Equal(new byte[] { 1, 0 }, wav[20..22]);
        Assert.Equal(new byte[] { 1, 0 }, wav[22..24]);
        Assert.Equal(new byte[] { 2, 0 }, wav[32..34]);
        Assert.Equal(new byte[] { 16, 0 }, wav[34..36]);
    }

    // Regel 1: Nach dem Vorspann folgen die Abtastwerte in Reihenfolge, 16 Bit, niederwertiges Byte zuerst.
    [Fact]
    public void Abtastwerte_folgen_in_Reihenfolge_mit_niederwertigem_Byte_zuerst()
    {
        var clip = new AudioClip(new short[] { 0x1234, -2, 0, short.MaxValue, short.MinValue, 1, -1 });

        var wav = WavEncoder.Encode(clip);

        var erwartet = new byte[]
        {
            0x34, 0x12, // 0x1234
            0xFE, 0xFF, // minus 2
            0x00, 0x00, // 0
            0xFF, 0x7F, // 32767
            0x00, 0x80, // minus 32768
            0x01, 0x00, // 1
            0xFF, 0xFF, // minus 1
        };
        Assert.Equal(erwartet, wav[44..]);
    }

    // Regel 1: Jeder Abtastwert lässt sich aus den Daten in derselben Reihenfolge zurücklesen.
    [Fact]
    public void Abtastwerte_lassen_sich_vollstaendig_zuruecklesen()
    {
        var clip = ClipMitWerten(1600);

        var wav = WavEncoder.Encode(clip);

        var gelesen = new short[clip.Samples.Length];
        for (var i = 0; i < gelesen.Length; i++)
        {
            gelesen[i] = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (2 * i), 2));
        }

        Assert.Equal(clip.Samples, gelesen);
    }

    // Regel 1: Vollständiger Vergleich aller Bytes für einen kleinen Clip mit 8000 Hz.
    [Fact]
    public void Ganzer_Inhalt_fuer_kleinen_Clip_stimmt_Byte_fuer_Byte()
    {
        var clip = new AudioClip(new short[] { 1, 258 }, 8000);

        var wav = WavEncoder.Encode(clip);

        var erwartet = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            40, 0, 0, 0, // 36 plus 4
            (byte)'W', (byte)'A', (byte)'V', (byte)'E',
            (byte)'f', (byte)'m', (byte)'t', (byte)' ',
            16, 0, 0, 0,
            1, 0,
            1, 0,
            0x40, 0x1F, 0, 0, // 8000
            0x80, 0x3E, 0, 0, // 16000
            2, 0,
            16, 0,
            (byte)'d', (byte)'a', (byte)'t', (byte)'a',
            4, 0, 0, 0,
            1, 0, // 1
            2, 1, // 258 ist 0x0102
        };
        Assert.Equal(erwartet, wav);
    }

    // Regel 1: Ein leerer Clip ergibt genau 44 Byte mit Datenlänge 0.
    [Fact]
    public void Leerer_Clip_ergibt_genau_44_Byte_mit_Datenlaenge_0()
    {
        var wav = WavEncoder.Encode(new AudioClip(Array.Empty<short>()));

        Assert.Equal(44, wav.Length);
        Assert.Equal("RIFF", Zeichen(wav, 0));
        Assert.Equal(36u, Zahl32(wav, 4));
        Assert.Equal("WAVE", Zeichen(wav, 8));
        Assert.Equal("fmt ", Zeichen(wav, 12));
        Assert.Equal("data", Zeichen(wav, 36));
        Assert.Equal(0u, Zahl32(wav, 40));
    }

    // Regel 1: Null wirft ArgumentNullException.
    [Fact]
    public void Null_wirft_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => WavEncoder.Encode(null!));
    }

    // Regel 1: Das Verpacken verändert die Abtastwerte des Clips nicht.
    [Fact]
    public void Clip_bleibt_unveraendert()
    {
        var clip = ClipMitWerten(50);
        var vorher = (short[])clip.Samples.Clone();

        _ = WavEncoder.Encode(clip);

        Assert.Equal(vorher, clip.Samples);
    }
}
