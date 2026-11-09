namespace Talk.Core.Processing;

/// <summary>Art eines Textstücks.</summary>
internal enum TextTokenKind
{
    /// <summary>Folge aus Buchstaben und Ziffern.</summary>
    Word,

    /// <summary>Folge aus Leerraum.</summary>
    Space,

    /// <summary>Ein einzelnes anderes Zeichen (Satzzeichen, Klammer, Anführungszeichen).</summary>
    Punct,
}

/// <summary>Ein Textstück aus der Zerlegung.</summary>
internal readonly record struct TextToken(TextTokenKind Kind, string Text)
{
    public bool IsWord => Kind == TextTokenKind.Word;

    public bool IsSpace => Kind == TextTokenKind.Space;

    public bool IsPunct(char c) => Kind == TextTokenKind.Punct && Text.Length == 1 && Text[0] == c;

    public bool IsPunctIn(string chars) => Kind == TextTokenKind.Punct && Text.Length == 1 && chars.Contains(Text[0]);
}

/// <summary>
/// Zerlegt einen Text verlustfrei in Wörter, Leerraum und einzelne andere Zeichen. Zusammengesetzt ergibt die Liste wieder den Text.
/// </summary>
internal static class TextTokenizer
{
    public static List<TextToken> Tokenize(string text)
    {
        List<TextToken> tokens = [];
        int i = 0;

        while (i < text.Length)
        {
            int start = i;
            char c = text[i];

            if (char.IsLetterOrDigit(c))
            {
                while (i < text.Length && char.IsLetterOrDigit(text[i])) i++;
                tokens.Add(new TextToken(TextTokenKind.Word, text[start..i]));
            }
            else if (char.IsWhiteSpace(c))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                tokens.Add(new TextToken(TextTokenKind.Space, text[start..i]));
            }
            else
            {
                i++;
                tokens.Add(new TextToken(TextTokenKind.Punct, text[start..i]));
            }
        }

        return tokens;
    }

    public static string Join(List<TextToken> tokens, int start, int end)
    {
        System.Text.StringBuilder sb = new();
        for (int i = start; i < end; i++) sb.Append(tokens[i].Text);
        return sb.ToString();
    }

    public static string Join(List<TextToken> tokens) => Join(tokens, 0, tokens.Count);

    /// <summary>Die Wörter eines Texts in Reihenfolge.</summary>
    public static string[] Words(string text) =>
        Tokenize(text).Where(t => t.IsWord).Select(t => t.Text).ToArray();
}
