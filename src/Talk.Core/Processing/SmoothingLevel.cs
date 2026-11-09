// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Processing;

/// <summary>
/// Stufe der optionalen Glättung durch ein Sprachmodell (Playbook Abschnitt 21). Standard ist aus.
/// Für jede Stufe gilt: nichts hinzufügen, nichts weglassen, Sprache des Texts beibehalten.
/// </summary>
public enum SmoothingLevel
{
    /// <summary>
    /// Keine Glättung, der erkannte Text bleibt wie er ist.
    /// </summary>
    Off,

    /// <summary>
    /// Nur Fehler beheben: Satzzeichen, Großschreibung, Grammatik. Die Wortwahl bleibt.
    /// </summary>
    Correct,

    /// <summary>
    /// Sätze glätten: Satzbau so, wie man schreibt. Inhalt und Ton bleiben.
    /// </summary>
    Smooth,

    /// <summary>
    /// Als Nachricht: ausformuliert wie eine Mail, ohne Anrede oder Gruß zu erfinden.
    /// </summary>
    Message
}
