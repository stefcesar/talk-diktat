// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Audio;
using Xunit;

namespace Talk.Core.Tests.Audio;

/// <summary>
/// Prüft die Standardumsetzungen der Schnittstelle <see cref="IVoiceActivityDetector"/> ausschließlich gegen
/// Regel 7 der reinen Logik in PLAYBOOK.md, Abschnitt 14. Dazu dient eine Testfassung der Schnittstelle,
/// die feste Abschnitte liefert und nur Options und DetectSegments selbst umsetzt. ContainsSpeech und TrimSilence
/// kommen aus der Schnittstelle und werden deshalb über eine Variable vom Typ der Schnittstelle aufgerufen.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class VoiceActivityDetectorDefaultsTests
{
    private const int Abtastrate = 16000;

    /// <summary>Testfassung der Schnittstelle: liefert immer dieselben festen Abschnitte, egal welcher Clip kommt.</summary>
    private sealed class FesteAbschnitteDetector : IVoiceActivityDetector
    {
        private readonly IReadOnlyList<SpeechSegment> _abschnitte;

        public FesteAbschnitteDetector(VadOptions options, params SpeechSegment[] abschnitte)
        {
            Options = options;
            _abschnitte = abschnitte;
        }

        public VadOptions Options { get; }

        public int Aufrufe { get; private set; }

        public IReadOnlyList<SpeechSegment> DetectSegments(AudioClip clip)
        {
            Aufrufe++;
            return _abschnitte;
        }
    }

    private static SpeechSegment Abschnitt(int anfang, int ende) => new(anfang, ende);

    /// <summary>Clip mit aufsteigenden Werten (Wert gleich Index), damit ein Ausschnitt eindeutig prüfbar ist.</summary>
    private static AudioClip ClipMitIndexwerten(int anzahl, int abtastrate = Abtastrate)
    {
        var werte = new short[anzahl];
        for (var i = 0; i < anzahl; i++)
        {
            werte[i] = (short)i;
        }

        return new AudioClip(werte, abtastrate);
    }

    // Regel 7: ContainsSpeech ist true, wenn DetectSegments mindestens einen Abschnitt liefert.
    [Fact]
    public void ContainsSpeech_ist_true_bei_mindestens_einem_Abschnitt()
    {
        IVoiceActivityDetector einer = new FesteAbschnitteDetector(new VadOptions(), Abschnitt(8000, 16000));
        IVoiceActivityDetector mehrere = new FesteAbschnitteDetector(
            new VadOptions(), Abschnitt(4000, 8000), Abschnitt(20000, 24000));
        var clip = ClipMitIndexwerten(32000);

        Assert.True(einer.ContainsSpeech(clip));
        Assert.True(mehrere.ContainsSpeech(clip));
    }

    // Regel 7: ContainsSpeech ist false, wenn DetectSegments keinen Abschnitt liefert.
    [Fact]
    public void ContainsSpeech_ist_false_ohne_Abschnitte()
    {
        IVoiceActivityDetector detector = new FesteAbschnitteDetector(new VadOptions());
        var clip = ClipMitIndexwerten(32000);

        Assert.False(detector.ContainsSpeech(clip));
    }

    // Regel 7: ContainsSpeech fragt DetectSegments und entscheidet nur danach, nicht nach dem Clip.
    [Fact]
    public void ContainsSpeech_entscheidet_nach_DetectSegments_nicht_nach_dem_Clip()
    {
        var mitAbschnitt = new FesteAbschnitteDetector(new VadOptions(), Abschnitt(0, 4000));
        var ohneAbschnitt = new FesteAbschnitteDetector(new VadOptions());
        var stille = new AudioClip(new short[32000]);

        Assert.True(((IVoiceActivityDetector)mitAbschnitt).ContainsSpeech(stille));
        Assert.False(((IVoiceActivityDetector)ohneAbschnitt).ContainsSpeech(stille));
        Assert.Equal(1, mitAbschnitt.Aufrufe);
        Assert.Equal(1, ohneAbschnitt.Aufrufe);
    }

    // Regel 7: TrimSilence entspricht SilenceTrimmer.Trim mit den Abschnitten aus DetectSegments und den Options der Umsetzung.
    [Fact]
    public void TrimSilence_entspricht_SilenceTrimmer_Trim_mit_den_Options_der_Umsetzung()
    {
        var optionen = new VadOptions(PaddingMs: 200);
        var abschnitte = new[] { Abschnitt(8000, 16000) };
        IVoiceActivityDetector detector = new FesteAbschnitteDetector(optionen, abschnitte);
        var clip = ClipMitIndexwerten(32000);

        var ergebnis = detector.TrimSilence(clip);
        var erwartet = SilenceTrimmer.Trim(clip, abschnitte, optionen);

        Assert.Equal(erwartet.Samples, ergebnis.Samples);
        Assert.Equal(erwartet.SampleRate, ergebnis.SampleRate);
        // Zur Sicherheit auch gegen Regel 6 direkt: 8000 minus 3200 bis 16000 plus 3200.
        Assert.Equal(clip.Samples[4800..19200], ergebnis.Samples);
    }

    // Regel 7: TrimSilence nutzt die Options der jeweiligen Umsetzung. Gleiche Abschnitte, anderer Rand, anderes Ergebnis.
    [Fact]
    public void TrimSilence_nutzt_die_Options_der_jeweiligen_Umsetzung()
    {
        var abschnitte = new[] { Abschnitt(8000, 16000) };
        var ohneRand = new VadOptions(PaddingMs: 0);
        var mitRand = new VadOptions(PaddingMs: 100);
        IVoiceActivityDetector detectorOhneRand = new FesteAbschnitteDetector(ohneRand, abschnitte);
        IVoiceActivityDetector detectorMitRand = new FesteAbschnitteDetector(mitRand, abschnitte);
        var clip = ClipMitIndexwerten(32000);

        var ergebnisOhneRand = detectorOhneRand.TrimSilence(clip);
        var ergebnisMitRand = detectorMitRand.TrimSilence(clip);

        Assert.Equal(SilenceTrimmer.Trim(clip, abschnitte, ohneRand).Samples, ergebnisOhneRand.Samples);
        Assert.Equal(SilenceTrimmer.Trim(clip, abschnitte, mitRand).Samples, ergebnisMitRand.Samples);
        Assert.Equal(8000, ergebnisOhneRand.Samples.Length);
        // 100 ms sind 1600 Abtastwerte je Seite, also 8000 plus 3200.
        Assert.Equal(11200, ergebnisMitRand.Samples.Length);
    }

    // Regel 7: TrimSilence mit mehreren Abschnitten behält die Pausen dazwischen, wie SilenceTrimmer.Trim (Regel 6).
    [Fact]
    public void TrimSilence_mit_mehreren_Abschnitten_entspricht_SilenceTrimmer_Trim()
    {
        var optionen = new VadOptions();
        var abschnitte = new[] { Abschnitt(4000, 8000), Abschnitt(20000, 24000) };
        IVoiceActivityDetector detector = new FesteAbschnitteDetector(optionen, abschnitte);
        var clip = ClipMitIndexwerten(32000);

        var ergebnis = detector.TrimSilence(clip);

        Assert.Equal(SilenceTrimmer.Trim(clip, abschnitte, optionen).Samples, ergebnis.Samples);
        Assert.Equal(26400, ergebnis.Samples.Length);
    }

    // Regel 7: TrimSilence ohne Abschnitte liefert wie SilenceTrimmer.Trim einen leeren Clip mit gleicher Abtastrate.
    [Theory]
    [InlineData(16000)]
    [InlineData(8000)]
    public void TrimSilence_ohne_Abschnitte_liefert_leeren_Clip_mit_gleicher_Abtastrate(int abtastrate)
    {
        IVoiceActivityDetector detector = new FesteAbschnitteDetector(new VadOptions());
        var clip = ClipMitIndexwerten(16000, abtastrate);

        var ergebnis = detector.TrimSilence(clip);
        var erwartet = SilenceTrimmer.Trim(clip, Array.Empty<SpeechSegment>(), new VadOptions());

        Assert.Empty(ergebnis.Samples);
        Assert.Equal(abtastrate, ergebnis.SampleRate);
        Assert.Equal(erwartet.Samples, ergebnis.Samples);
        Assert.Equal(erwartet.SampleRate, ergebnis.SampleRate);
    }

    // Regel 7: Die Testfassung liefert ihre Options unverändert; die Standardumsetzungen greifen genau darauf zu.
    [Fact]
    public void Options_der_Testfassung_bleiben_erhalten()
    {
        var optionen = new VadOptions(Threshold: 0.7f, MinSpeechMs: 100, MinSilenceMs: 150, PaddingMs: 50);
        IVoiceActivityDetector detector = new FesteAbschnitteDetector(optionen, Abschnitt(0, 4000));

        Assert.Same(optionen, detector.Options);
        Assert.Equal(0.7f, detector.Options.Threshold);
        Assert.Equal(50, detector.Options.PaddingMs);
    }
}
