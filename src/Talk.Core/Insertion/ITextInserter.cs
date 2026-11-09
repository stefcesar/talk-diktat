namespace Talk.Core.Insertion;

/// <summary>
/// Fügt Text in das Zielfenster ein: zuerst über die Zwischenablage, sonst durch Tippen, sonst nur Verlauf.
/// Die Umsetzung kommt in Schritt 6.
/// </summary>
public interface ITextInserter
{
    /// <summary>
    /// Fügt den Text in das Zielfenster ein.
    /// </summary>
    /// <param name="text">Fertig nachbearbeiteter Text.</param>
    /// <param name="target">Das Fenster, das den Text erhalten soll, oder null, wenn es kein Ziel gibt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <param name="startRaised">True, wenn erst Talk den ersten Buchstaben groß gemacht hat (<see cref="Processing.SentenceCase.RaisedStart"/>);
    /// mitten im Satz wird er dann wieder klein.</param>
    /// <returns>Wie der Text angekommen ist, mit Grund und Zeit.</returns>
    Task<InsertOutcome> InsertAsync(string text, TargetWindow? target, CancellationToken ct, bool startRaised = false);
}
