// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Processing;

/// <summary>
/// Bearbeitet den erkannten Text ohne Netz nach: Füllwörter, Wörterbuch, Leerraum. Der Filter gegen erfundene Sätze läuft
/// schon in den Erkennern, die Glättung per Sprachmodell ist eine eigene Stufe. Umsetzung: <see cref="PostProcessor"/>.
/// </summary>
public interface IPostProcessor
{
    /// <summary>
    /// Wendet die Nachbearbeitung auf einen Text an.
    /// </summary>
    /// <param name="text">Roher Text aus der Erkennung.</param>
    /// <param name="options">Einstellungen für die Nachbearbeitung.</param>
    /// <returns>Der bearbeitete Text.</returns>
    string Process(string text, PostProcessingOptions options);
}
