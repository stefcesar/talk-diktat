// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Audio;

/// <summary>
/// Sammelt Abtastwerte (16 kHz, mono, PCM 16 Bit) im Arbeitsspeicher und baut daraus einen <see cref="AudioClip"/>.
/// Hält eine Obergrenze ein, damit eine vergessene Aufnahme nicht den Speicher füllt: Ist sie erreicht, werden weitere Werte verworfen
/// und <see cref="Truncated"/> wird true. Nicht threadsicher; der Aufrufer serialisiert die Aufrufe.
/// </summary>
public sealed class AudioClipBuilder
{
    /// <summary>Standard Obergrenze: fünf Minuten.</summary>
    public static readonly TimeSpan DefaultMaxDuration = TimeSpan.FromMinutes(5);

    private readonly int _sampleRate;
    private readonly int _maxSamples;
    private short[] _buffer;
    private int _count;

    /// <summary>Erzeugt einen Sammler für die angegebene Abtastrate und Obergrenze.</summary>
    public AudioClipBuilder(int sampleRate = 16000, TimeSpan? maxDuration = null)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), "Die Abtastrate muss größer als 0 sein.");
        TimeSpan max = maxDuration ?? DefaultMaxDuration;
        if (max <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxDuration), "Die Obergrenze muss größer als 0 sein.");

        _sampleRate = sampleRate;
        _maxSamples = checked((int)Math.Min(int.MaxValue, (long)Math.Ceiling(max.TotalSeconds * sampleRate)));
        _buffer = new short[Math.Min(_maxSamples, sampleRate * 10)];
    }

    /// <summary>Anzahl der gesammelten Abtastwerte.</summary>
    public int Count => _count;

    /// <summary>Bisherige Dauer.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_count / (double)_sampleRate);

    /// <summary>True, wenn die Obergrenze erreicht wurde und Werte verworfen wurden.</summary>
    public bool Truncated { get; private set; }

    /// <summary>Hängt Abtastwerte an. Über der Obergrenze wird abgeschnitten.</summary>
    public void Append(ReadOnlySpan<short> samples)
    {
        if (samples.IsEmpty) return;

        int room = _maxSamples - _count;
        if (room <= 0)
        {
            Truncated = true;
            return;
        }

        int take = Math.Min(room, samples.Length);
        if (take < samples.Length) Truncated = true;

        EnsureCapacity(_count + take);
        samples[..take].CopyTo(_buffer.AsSpan(_count));
        _count += take;
    }

    /// <summary>Baut den Clip aus den gesammelten Werten. Der Sammler bleibt unverändert.</summary>
    public AudioClip Build()
    {
        short[] samples = new short[_count];
        Array.Copy(_buffer, samples, _count);
        return new AudioClip(samples, _sampleRate);
    }

    /// <summary>Verwirft alles. Der Speicher wird mit Nullen überschrieben, damit Audio nicht liegen bleibt.</summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _count);
        _count = 0;
        Truncated = false;
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _buffer.Length) return;

        int newSize = Math.Max(needed, Math.Min(_maxSamples, _buffer.Length * 2));
        short[] bigger = new short[newSize];
        Array.Copy(_buffer, bigger, _count);
        Array.Clear(_buffer, 0, _count);
        _buffer = bigger;
    }
}
