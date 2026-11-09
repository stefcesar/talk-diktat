// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Transcription;

/// <summary>
/// Ergebnis einer Erkennung.
/// </summary>
/// <param name="Text">Erkannter Text nach dem Filter gegen erfundene Sätze, noch ohne Nachbearbeitung.</param>
/// <param name="DetectedLanguage">Vom Erkenner gemeldete Sprache oder null, wenn unbekannt.</param>
/// <param name="Elapsed">Gemessene Dauer der Erkennung.</param>
/// <param name="Engine">Name des Erkenners, der das Ergebnis geliefert hat.</param>
/// <param name="Segments">Die behaltenen Abschnitte mit Kennzahlen oder null.</param>
/// <param name="Timings">Dauer der einzelnen Stufen oder null.</param>
/// <param name="RateLimit">Limit Angaben des Dienstes oder null.</param>
/// <param name="FilteredSegments">Anzahl der als erfunden verworfenen Abschnitte.</param>
/// <param name="FallbackReason">Gesetzt, wenn der zweite Erkenner einspringen musste: der Grund, warum der erste ausfiel.</param>
public sealed record TranscriptionResult(
    string Text,
    string? DetectedLanguage,
    TimeSpan Elapsed,
    string Engine,
    IReadOnlyList<TranscriptionSegment>? Segments = null,
    TranscriptionTimings? Timings = null,
    RateLimitInfo? RateLimit = null,
    int FilteredSegments = 0,
    TranscriptionError? FallbackReason = null);
