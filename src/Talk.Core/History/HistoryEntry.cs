namespace Talk.Core.History;

/// <summary>
/// Ein Eintrag im Verlauf. Jeder Text landet zuerst hier, erst danach wird eingefügt.
/// </summary>
/// <param name="At">Zeitpunkt des Diktats.</param>
/// <param name="Text">Der diktierte Text nach der Nachbearbeitung.</param>
/// <param name="Engine">Name des Erkenners, der den Text geliefert hat.</param>
/// <param name="LocalOnly">True, wenn das Diktat erzwungen nur auf dem Rechner erkannt wurde. Gespeichert wird es wie jedes andere (Entscheid 50).</param>
public sealed record HistoryEntry(
    DateTimeOffset At,
    string Text,
    string Engine,
    bool LocalOnly);
