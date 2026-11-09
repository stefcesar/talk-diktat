namespace Talk.App.Input;

/// <summary>
/// Grund für das Neusetzen des Tastaturhooks. Ein fester Typ statt freiem Text, damit ins Protokoll nie etwas anderes
/// als die festen Wörter aus <see cref="HookReinstallReasonExtensions.ToWord"/> gelangen kann.
/// </summary>
public enum HookReinstallReason
{
    /// <summary>Der Rechner ist aus dem Standby aufgewacht.</summary>
    Resume,

    /// <summary>Die Sitzung wurde entsperrt.</summary>
    Unlock,

    /// <summary>Die Sitzung wurde neu angemeldet.</summary>
    Logon,

    /// <summary>Vorsorgliche Neusetzung im festen Abstand (nur im Ruhezustand).</summary>
    Interval,

    /// <summary>Rückfall, wenn kein Grund vorliegt.</summary>
    Unknown,
}

/// <summary>Übersetzt den Grund in das feste Protokollwort.</summary>
public static class HookReinstallReasonExtensions
{
    /// <summary>Liefert das feste deutsche Wort für Protokoll und Anzeige.</summary>
    public static string ToWord(this HookReinstallReason reason) => reason switch
    {
        HookReinstallReason.Resume => "aufwachen",
        HookReinstallReason.Unlock => "entsperren",
        HookReinstallReason.Logon => "anmelden",
        HookReinstallReason.Interval => "intervall",
        _ => "unbekannt",
    };
}
