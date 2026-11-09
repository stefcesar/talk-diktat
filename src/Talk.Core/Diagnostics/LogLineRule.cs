using System.Text.RegularExpressions;

namespace Talk.Core.Diagnostics;

/// <summary>
/// Die Regel für eine saubere Protokollzeile (Playbook Schritt 8): Zeitstempel, Kategorie, Ereignis und ein Detail, das nur aus
/// Paaren <c>name=wert</c> besteht, davor höchstens ein einzelnes kleines Wort (zum Beispiel "ok", "fehler", "off").
/// Ein Wert hat höchstens 24 Zeichen aus Buchstaben ohne Umlaute, Ziffern, Unterstrich, Punkt und Bindestrich.
/// </summary>
/// <remarks>
/// Damit fallen auf: ein diktierter Satz (mehrere bloße Wörter, Satzzeichen, Umlaute), ein Pfad, ein Gerätename, eine
/// Fehlermeldung und ein Schlüssel (länger als 24 Zeichen). Nicht auffallen kann ein einzelnes kurzes Wort ohne Umlaut;
/// dass dort nie Inhalt steht, belegt der Code, nicht diese Regel.
/// </remarks>
public static partial class LogLineRule
{
    /// <summary>
    /// True, wenn die Zeile der Regel entspricht. Eine leere Zeile gilt als sauber.
    /// </summary>
    public static bool IsClean(string? line) => string.IsNullOrEmpty(line) || CleanLine().IsMatch(line);

    /// <summary>
    /// True für Zeilen im Format der ersten Tage (Zeitstempel ohne Millisekunden, ohne Kategorie), die trotzdem nur aus
    /// kurzen festen Wörtern und Paaren bestehen. Sie gelten nicht als sauber, sind aber auch kein Inhalt.
    /// </summary>
    public static bool IsLegacy(string? line) => !string.IsNullOrEmpty(line) && LegacyLine().IsMatch(line);

    [GeneratedRegex(
        @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \| [a-z_]+ \| [a-z_\-]+( \| ([a-z]{1,12}|([a-z]{1,12} )?[a-z_]{1,24}=[A-Za-z0-9_.\-]{1,24}( [a-z_]{1,24}=[A-Za-z0-9_.\-]{1,24})*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CleanLine();

    [GeneratedRegex(
        @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}( \| ([a-z]{1,12}|[a-z_]{1,24}=[A-Za-z0-9_.\-]{1,24})){1,3}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LegacyLine();
}