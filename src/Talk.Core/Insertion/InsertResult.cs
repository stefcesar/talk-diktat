namespace Talk.Core.Insertion;

/// <summary>
/// Ergebnis eines Einfügeversuchs. Der Text steht in jedem Fall im Verlauf.
/// </summary>
public enum InsertResult
{
    /// <summary>
    /// Über die Zwischenablage eingefügt.
    /// </summary>
    Inserted,

    /// <summary>
    /// Über den Ersatzweg eingetippt (SendInput).
    /// </summary>
    Typed,

    /// <summary>
    /// Nicht eingefügt, nur im Verlauf abgelegt, zum Beispiel weil das Zielfenster weg ist.
    /// </summary>
    HistoryOnly,

    /// <summary>
    /// Absichtlich nicht eingefügt, zum Beispiel bei Passwortfeldern oder Fenstern mit höheren Rechten.
    /// </summary>
    Blocked
}
