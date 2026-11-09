namespace Talk.Core.Insertion;

/// <summary>
/// Warum ein Text nicht oder nur über den Ersatzweg angekommen ist. <see cref="None"/> heißt: kein besonderer Grund.
/// </summary>
public enum InsertReason
{
    /// <summary>Kein besonderer Grund.</summary>
    None,

    /// <summary>Es gab kein Zielfenster, oder das Ziel war ein Fenster von Talk selbst.</summary>
    NoTarget,

    /// <summary>Das Zielfenster gibt es nicht mehr.</summary>
    WindowGone,

    /// <summary>Das Zielfenster läuft mit höheren Rechten, oder seine Rechte ließen sich nicht lesen.</summary>
    HigherRights,

    /// <summary>Das Feld mit dem Fokus ist ein Passwortfeld.</summary>
    PasswordField,

    /// <summary>Das Zielfenster ließ sich nicht nach vorn holen.</summary>
    NotForeground,

    /// <summary>Zwischenablage und Tippen sind beide gescheitert.</summary>
    InputFailed,

    /// <summary>Das Diktat wurde abgebrochen, bevor die Eingabe begann.</summary>
    Canceled
}

/// <summary>
/// Was beim Einfügen über das Zielfenster bekannt ist. Die Werte liest die App aus Windows, die Regeln stehen in <see cref="InsertPlanner"/>.
/// </summary>
/// <param name="HasTarget">Beim Start der Aufnahme war ein Fenster vorn.</param>
/// <param name="IsOwnWindow">Das Fenster gehört zu Talk selbst.</param>
/// <param name="Exists">Das Fenster gibt es noch.</param>
/// <param name="HigherRights">Das Fenster läuft mit höheren Rechten als Talk, oder seine Rechte ließen sich nicht lesen.</param>
/// <param name="PasswordField">Das Feld mit dem Fokus ist ein Passwortfeld.</param>
/// <param name="IsTerminal">Das Fenster ist ein Terminal.</param>
public sealed record InsertTargetFacts(
    bool HasTarget,
    bool IsOwnWindow,
    bool Exists,
    bool HigherRights,
    bool PasswordField,
    bool IsTerminal);

/// <summary>
/// Der geplante Weg. <see cref="InsertResult.Inserted"/> heißt Zwischenablage, <see cref="InsertResult.Typed"/> heißt tippen;
/// bei <see cref="InsertResult.HistoryOnly"/> und <see cref="InsertResult.Blocked"/> wird nichts eingegeben.
/// </summary>
/// <param name="Way">Der Weg.</param>
/// <param name="Reason">Der Grund, wenn nichts eingegeben wird.</param>
public sealed record InsertPlan(InsertResult Way, InsertReason Reason);

/// <summary>
/// Ergebnis eines Einfügeversuchs mit Grund und Zeit. Der Text steht in jedem Fall im Verlauf.
/// </summary>
/// <param name="Result">Wie der Text angekommen ist.</param>
/// <param name="Reason">Der Grund, wenn er nicht oder nur über den Ersatzweg ankam.</param>
/// <param name="IsTerminal">Das Ziel war ein Terminal.</param>
/// <param name="Milliseconds">Dauer bis zur Eingabe, ohne das Zurücklegen der Zwischenablage.</param>
public sealed record InsertOutcome(InsertResult Result, InsertReason Reason, bool IsTerminal, long Milliseconds);

/// <summary>
/// Die Regeln für den Weg des Texts als reine Logik (Playbook Abschnitt 17). Die Reihenfolge der Prüfungen ist Teil der Regel.
/// </summary>
public static class InsertPlanner
{
    /// <summary>
    /// Entscheidet den Weg: kein Ziel, Fenster weg, höhere Rechte, Passwortfeld, Terminal, sonst Zwischenablage.
    /// </summary>
    public static InsertPlan Decide(InsertTargetFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.HasTarget || facts.IsOwnWindow) return new InsertPlan(InsertResult.HistoryOnly, InsertReason.NoTarget);
        if (!facts.Exists) return new InsertPlan(InsertResult.HistoryOnly, InsertReason.WindowGone);
        if (facts.HigherRights) return new InsertPlan(InsertResult.Blocked, InsertReason.HigherRights);
        if (facts.PasswordField) return new InsertPlan(InsertResult.Blocked, InsertReason.PasswordField);
        if (facts.IsTerminal) return new InsertPlan(InsertResult.Typed, InsertReason.None);
        return new InsertPlan(InsertResult.Inserted, InsertReason.None);
    }

    /// <summary>
    /// Festes Wort fürs Protokoll.
    /// </summary>
    public static string ToWord(this InsertResult result) => result switch
    {
        InsertResult.Inserted => "zwischenablage",
        InsertResult.Typed => "tippen",
        InsertResult.Blocked => "gesperrt",
        _ => "verlauf",
    };

    /// <summary>
    /// Festes Wort fürs Protokoll.
    /// </summary>
    public static string ToWord(this InsertReason reason) => reason switch
    {
        InsertReason.NoTarget => "kein_ziel",
        InsertReason.WindowGone => "fenster_weg",
        InsertReason.HigherRights => "hoehere_rechte",
        InsertReason.PasswordField => "passwortfeld",
        InsertReason.NotForeground => "fenster_nicht_vorn",
        InsertReason.InputFailed => "eingabe_gescheitert",
        InsertReason.Canceled => "abgebrochen",
        _ => "keiner",
    };
}
