// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Transcription;

/// <summary>
/// Ein Abschnitt des erkannten Texts mit den Kennzahlen des Erkenners. Die Kennzahlen dienen dem Filter gegen erfundene Sätze.
/// </summary>
/// <param name="StartSeconds">Beginn im Clip in Sekunden.</param>
/// <param name="EndSeconds">Ende im Clip in Sekunden.</param>
/// <param name="Text">Text des Abschnitts.</param>
/// <param name="AvgLogProb">Mittlere logarithmische Wahrscheinlichkeit der Wörter (0 ist sicher, stark negativ ist unsicher) oder null.</param>
/// <param name="NoSpeechProb">Wahrscheinlichkeit, dass der Abschnitt keine Sprache enthält (0 bis 1), oder null.</param>
/// <param name="CompressionRatio">Kompressionsverhältnis des Texts; hohe Werte deuten auf Wiederholungsschleifen, oder null.</param>
public sealed record TranscriptionSegment(
    double StartSeconds,
    double EndSeconds,
    string Text,
    double? AvgLogProb = null,
    double? NoSpeechProb = null,
    double? CompressionRatio = null);
