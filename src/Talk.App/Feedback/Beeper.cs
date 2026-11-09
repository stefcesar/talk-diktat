namespace Talk.App.Feedback;

/// <summary>
/// Töne über Console.Beep in einem Hintergrund Task, damit nichts blockiert.
/// Start 880 Hz 80 ms, Stopp 660 Hz 80 ms, Abbruch 440 Hz 120 ms. In den Einstellungen abschaltbar.
/// </summary>
internal static class Beeper
{
    /// <summary>Mit false bleibt Talk still. Die Einstellung des Nutzers setzt den Wert.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Ton für den Start der Aufnahme.</summary>
    public static void Start() => Play(880, 80);

    /// <summary>Ton für das Ende der Aufnahme.</summary>
    public static void Stop() => Play(660, 80);

    /// <summary>Ton für den Abbruch.</summary>
    public static void Cancel() => Play(440, 120);

    private static void Play(int frequencyHz, int durationMs)
    {
        if (!Enabled) return;

        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(frequencyHz, durationMs);
            }
            catch
            {
                // Der Ton ist Komfort. Fehlt ein Audiogerät, bleibt es still.
            }
        });
    }
}
