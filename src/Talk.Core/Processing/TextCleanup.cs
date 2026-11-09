// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Text;

namespace Talk.Core.Processing;

/// <summary>
/// Räumt Leerraum auf, ohne den Wortlaut zu ändern.
/// </summary>
public static class TextCleanup
{
    private const string ClosingMarks = ",.;:!?";

    /// <summary>
    /// Jede Folge von Leerraum wird ein Leerzeichen; Ränder ohne Leerraum; ein Leerzeichen vor Komma, Punkt, Semikolon,
    /// Doppelpunkt, Ausrufezeichen oder Fragezeichen entfällt, wenn das Zeichen am Ende steht oder Leerraum folgt
    /// (ein Punkt vor einem Buchstaben wie in ".NET" bleibt mit seinem Leerzeichen stehen). Enthält der Text keinen
    /// Buchstaben und keine Ziffer, kommt ein leerer Text zurück. Null ergibt leer.
    /// </summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        StringBuilder sb = new(text.Length);
        bool pendingSpace = false;
        bool anyContent = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && sb.Length > 0)
            {
                bool closes = ClosingMarks.Contains(c) && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]));
                if (!closes) sb.Append(' ');
            }

            pendingSpace = false;
            if (char.IsLetterOrDigit(c)) anyContent = true;
            sb.Append(c);
        }

        return anyContent ? sb.ToString() : string.Empty;
    }
}
