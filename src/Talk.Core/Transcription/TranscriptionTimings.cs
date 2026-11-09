// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Transcription;

/// <summary>
/// Gemessene Dauer der Stufen einer Erkennung in Millisekunden.
/// </summary>
/// <param name="EncodeMs">Umwandeln des Clips in das Übertragungsformat (lokal 0).</param>
/// <param name="UploadMs">Vom Absenden bis die Audiodaten vollständig übergeben sind (lokal 0).</param>
/// <param name="WaitMs">Vom Ende des Uploads bis die Antwort vollständig da ist (lokal die reine Rechenzeit).</param>
/// <param name="TotalMs">Gesamtdauer einschließlich Auswertung der Antwort.</param>
public sealed record TranscriptionTimings(int EncodeMs, int UploadMs, int WaitMs, int TotalMs);
