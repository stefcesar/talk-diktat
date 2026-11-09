// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Processing;

/// <summary>
/// Einstellungen für die Nachbearbeitung des erkannten Texts.
/// </summary>
/// <param name="RemoveFillers">Füllwörter wie "ähm" entfernen. Standard an.</param>
/// <param name="Smoothing">Stufe der Glättung. Standard aus.</param>
/// <param name="Vocabulary">Wörterbuch für Ersetzungen nach der Erkennung, je Eintrag "Schreibweise" oder "Schreibweise = Variante 1 | Variante 2", oder null.</param>
/// <param name="Language">Sprache des Texts für Füllwörter und Wörterbuch ("de", "en", "it" oder ein Name). Unbekannt entfernt nur die gemeinsamen Zögerlaute.</param>
/// <param name="ExtraFillers">Zusätzliche Füllwörter des Nutzers, die an jeder Stelle entfallen, oder null.</param>
/// <param name="FuzzyVocabulary">Auch ähnlich geschriebene Wörter auf die richtige Schreibweise bringen. Standard an.</param>
/// <param name="CapitalizeSentences">Den ersten Buchstaben jedes Satzes groß schreiben (<see cref="SentenceCase"/>). Standard aus; die App schaltet es für jedes Diktat ein, außer im Terminal.</param>
/// <param name="SwissVocabulary">Eigene Helvetismen des Nutzers (<c>woerterbuch-ch.txt</c>) im Format "Schweizer Wort = Form | Form", oder null. Gilt nur bei Schweizerdeutsch und Mundart.</param>
public sealed record PostProcessingOptions(
    bool RemoveFillers = true,
    SmoothingLevel Smoothing = SmoothingLevel.Off,
    IReadOnlyList<string>? Vocabulary = null,
    string Language = "de",
    IReadOnlyList<string>? ExtraFillers = null,
    bool FuzzyVocabulary = true,
    IReadOnlyList<string>? SwissVocabulary = null,
    bool CapitalizeSentences = false);
