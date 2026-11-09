// Die Windows Desktop SDK nimmt System.IO aus den impliziten Usings heraus, deshalb hier ausdrücklich.
using System.IO;
using System.Text;

namespace Talk.App.Diagnostics;

/// <summary>
/// Schreibt Text in die Standardausgabe des Prozesses: UTF-8 ohne BOM, mit Zeilenende.
/// Nur für die Diagnose per <c>--stttest</c>; dort ist das der einzige Ort, an den erkannter Text geht. Ins Protokoll geht er nie.
/// </summary>
/// <remarks>
/// Talk ist eine Fenster App und hat von sich aus keine Konsole. Ein Ziel gibt es nur, wenn der Aufrufer die Standardausgabe umleitet
/// (in eine Datei oder in sein eigenes Programm). Ohne Ziel passiert nichts.
/// </remarks>
internal static class StandardOutput
{
    /// <summary>
    /// Schreibt den Text mit Zeilenende.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <returns>True, wenn das Schreiben ohne Fehler durchlief.</returns>
    public static bool WriteLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            using Stream output = Console.OpenStandardOutput();
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text + Environment.NewLine);
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
            return true;
        }
        catch (Exception)
        {
            // Die Standardausgabe ist zu oder fehlt.
            return false;
        }
    }
}
