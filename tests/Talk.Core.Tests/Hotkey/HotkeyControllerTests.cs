// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Hotkey;
using Xunit;

namespace Talk.Core.Tests.Hotkey;

/// <summary>
/// Prüft die Hotkey Steuerung ausschließlich gegen die Regeln der Steuerung in PLAYBOOK.md, Abschnitt 13.
/// Die Tests kennen die Umsetzung nicht. Zeitstempel sind einfache Millisekunden Zahlen.
/// Die Regelnummer steht jeweils im Kommentar über dem Test.
/// </summary>
public sealed class HotkeyControllerTests
{
    // Hilfsmethoden: erzeugen die vier möglichen Tastenereignisse mit Zeit und optionalem Umschalt Zustand.

    private static KeyEvent CapsDown(long zeitMs, bool shiftHeld = false)
        => new(HotkeyKey.CapsLock, KeyDirection.Down, shiftHeld, zeitMs);

    private static KeyEvent CapsUp(long zeitMs, bool shiftHeld = false)
        => new(HotkeyKey.CapsLock, KeyDirection.Up, shiftHeld, zeitMs);

    private static KeyEvent EscDown(long zeitMs, bool shiftHeld = false)
        => new(HotkeyKey.Escape, KeyDirection.Down, shiftHeld, zeitMs);

    private static KeyEvent EscUp(long zeitMs, bool shiftHeld = false)
        => new(HotkeyKey.Escape, KeyDirection.Up, shiftHeld, zeitMs);

    /// <summary>Bringt eine Steuerung per Doppeltipp (Down 0, Up 50, Down 200) in die Aufnahme.</summary>
    private static HotkeyController InAufnahme(HotkeyOptions? options = null)
    {
        var steuerung = new HotkeyController(options);
        steuerung.Handle(CapsDown(0));
        steuerung.Handle(CapsUp(50));
        var start = steuerung.Handle(CapsDown(200));
        Assert.Equal(HotkeyAction.StartRecording, start.Action);
        Assert.Equal(DictationState.Recording, steuerung.State);
        return steuerung;
    }

    /// <summary>Bringt eine Steuerung per Doppeltipp und anschließendem Stopp in die Verarbeitung.</summary>
    private static HotkeyController InVerarbeitung(HotkeyOptions? options = null)
    {
        var steuerung = InAufnahme(options);
        steuerung.Handle(CapsUp(250));
        var stopp = steuerung.Handle(CapsDown(1000));
        Assert.Equal(HotkeyAction.StopRecording, stopp.Action);
        steuerung.Handle(CapsUp(1050));
        Assert.Equal(DictationState.Processing, steuerung.State);
        return steuerung;
    }

    // Regel 1: Doppeltipp innerhalb des Fensters startet sofort beim zweiten Drücken.
    [Fact]
    public void Doppeltipp_innerhalb_des_Fensters_startet_beim_zweiten_Druecken()
    {
        var steuerung = new HotkeyController();

        var erstesDown = steuerung.Handle(CapsDown(0));
        var erstesUp = steuerung.Handle(CapsUp(50));
        var zweitesDown = steuerung.Handle(CapsDown(200));

        Assert.Equal(HotkeyDecision.Swallowed, erstesDown);
        Assert.Equal(HotkeyDecision.Swallowed, erstesUp);
        Assert.True(zweitesDown.Swallow);
        Assert.Equal(HotkeyAction.StartRecording, zweitesDown.Action);
        Assert.Equal(DictationState.Recording, steuerung.State);

        var zweitesUp = steuerung.Handle(CapsUp(250));
        Assert.Equal(HotkeyDecision.Swallowed, zweitesUp);
        Assert.Equal(DictationState.Recording, steuerung.State);
    }

    // Regel 1: Ein zweites Drücken nach dem Fenster startet nicht und zählt als neuer erster Tipp.
    [Fact]
    public void Zweites_Druecken_nach_dem_Fenster_startet_nicht_und_zaehlt_als_neuer_erster_Tipp()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(DoubleTapWindowMs: 350));

        steuerung.Handle(CapsDown(0));
        steuerung.Handle(CapsUp(50));
        var zuSpaet = steuerung.Handle(CapsDown(400));

        Assert.Equal(HotkeyDecision.Swallowed, zuSpaet);
        Assert.Equal(DictationState.Idle, steuerung.State);

        steuerung.Handle(CapsUp(450));
        var drittesDown = steuerung.Handle(CapsDown(600));

        Assert.Equal(HotkeyAction.StartRecording, drittesDown.Action);
        Assert.True(drittesDown.Swallow);
        Assert.Equal(DictationState.Recording, steuerung.State);
    }

    // Regel 1: Ein zweites Drücken schneller als MinTapGapMs ist Prellen, der erste Tipp bleibt gültig.
    [Fact]
    public void Prellen_startet_nicht_und_der_erste_Tipp_bleibt_gueltig()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(DoubleTapWindowMs: 350, MinTapGapMs: 30));

        steuerung.Handle(CapsDown(0));
        steuerung.Handle(CapsUp(5));
        var prellen = steuerung.Handle(CapsDown(10));

        Assert.Equal(HotkeyDecision.Swallowed, prellen);
        Assert.Equal(DictationState.Idle, steuerung.State);

        steuerung.Handle(CapsUp(15));
        var drittesDown = steuerung.Handle(CapsDown(200));

        Assert.Equal(HotkeyAction.StartRecording, drittesDown.Action);
        Assert.True(drittesDown.Swallow);
        Assert.Equal(DictationState.Recording, steuerung.State);
    }

    // Regel 1 und 5: Ein einzelner Tipp im Ruhezustand tut nichts, wird aber geschluckt.
    [Fact]
    public void Einzelner_Tipp_im_Ruhezustand_tut_nichts_und_wird_geschluckt()
    {
        var steuerung = new HotkeyController();

        var down = steuerung.Handle(CapsDown(0));
        var up = steuerung.Handle(CapsUp(50));

        Assert.True(down.Swallow);
        Assert.Equal(HotkeyAction.None, down.Action);
        Assert.True(up.Swallow);
        Assert.Equal(HotkeyAction.None, up.Action);
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
    }

    // Regel 2: Drücken von Caps Lock in der Aufnahme beendet sie, Zustand wird Verarbeitung.
    [Fact]
    public void Caps_Lock_in_der_Aufnahme_stoppt_und_fuehrt_in_die_Verarbeitung()
    {
        var steuerung = InAufnahme();
        steuerung.Handle(CapsUp(250));

        var stopp = steuerung.Handle(CapsDown(1000));

        Assert.True(stopp.Swallow);
        Assert.Equal(HotkeyAction.StopRecording, stopp.Action);
        Assert.Equal(DictationState.Processing, steuerung.State);

        var up = steuerung.Handle(CapsUp(1050));
        Assert.Equal(HotkeyDecision.Swallowed, up);
        Assert.Equal(DictationState.Processing, steuerung.State);
    }

    // Regel 5: Caps Lock ohne Umschalt wird in jedem Zustand geschluckt (Drücken und Loslassen).
    [Fact]
    public void Caps_Lock_ohne_Umschalt_wird_in_allen_drei_Zustaenden_geschluckt()
    {
        var steuerung = new HotkeyController();

        // Ruhe
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.True(steuerung.Handle(CapsDown(0)).Swallow);
        Assert.True(steuerung.Handle(CapsUp(50)).Swallow);

        // Aufnahme (zweiter Tipp startet sie, das Loslassen danach wird geschluckt)
        Assert.True(steuerung.Handle(CapsDown(200)).Swallow);
        Assert.Equal(DictationState.Recording, steuerung.State);
        Assert.True(steuerung.Handle(CapsUp(250)).Swallow);

        // Stopp aus der Aufnahme heraus (Drücken und Loslassen geschluckt)
        Assert.True(steuerung.Handle(CapsDown(1000)).Swallow);
        Assert.Equal(DictationState.Processing, steuerung.State);
        Assert.True(steuerung.Handle(CapsUp(1050)).Swallow);

        // Verarbeitung
        Assert.True(steuerung.Handle(CapsDown(2000)).Swallow);
        Assert.True(steuerung.Handle(CapsUp(2050)).Swallow);
        Assert.Equal(DictationState.Processing, steuerung.State);
    }

    // Regel 2: Esc in der Aufnahme bricht ab, beide Esc Ereignisse werden geschluckt.
    [Fact]
    public void Esc_in_der_Aufnahme_bricht_ab_und_beide_Esc_Ereignisse_werden_geschluckt()
    {
        var steuerung = InAufnahme();
        steuerung.Handle(CapsUp(250));

        var escDown = steuerung.Handle(EscDown(1000));

        Assert.True(escDown.Swallow);
        Assert.Equal(HotkeyAction.Cancel, escDown.Action);
        Assert.Equal(DictationState.Idle, steuerung.State);

        var escUp = steuerung.Handle(EscUp(1050));
        Assert.Equal(HotkeyDecision.Swallowed, escUp);
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Regel 7 und 3: Esc außerhalb der Aufnahme wird immer durchgelassen (Ruhe und Verarbeitung).
    [Fact]
    public void Esc_im_Ruhezustand_und_in_der_Verarbeitung_wird_durchgelassen()
    {
        var ruhe = new HotkeyController();
        Assert.Equal(HotkeyDecision.Pass, ruhe.Handle(EscDown(0)));
        Assert.Equal(HotkeyDecision.Pass, ruhe.Handle(EscUp(50)));
        Assert.Equal(DictationState.Idle, ruhe.State);

        var verarbeitung = InVerarbeitung();
        Assert.Equal(HotkeyDecision.Pass, verarbeitung.Handle(EscDown(2000)));
        Assert.Equal(HotkeyDecision.Pass, verarbeitung.Handle(EscUp(2050)));
        Assert.Equal(DictationState.Processing, verarbeitung.State);
    }

    // Regel 4: Umschalt plus Caps Lock geht durch (Drücken und zugehöriges Loslassen),
    // zählt keinen Tipp und lässt einen wartenden ersten Tipp verfallen.
    [Fact]
    public void Umschalt_plus_Caps_Lock_geht_durch_zaehlt_nicht_und_verwirft_wartenden_Tipp()
    {
        var steuerung = new HotkeyController();

        // Ein erster Tipp wartet.
        steuerung.Handle(CapsDown(0));
        steuerung.Handle(CapsUp(50));

        // Umschalt plus Caps Lock: Drücken mit Umschalt, Loslassen bereits ohne Umschalt.
        var shiftDown = steuerung.Handle(CapsDown(100, shiftHeld: true));
        var shiftUp = steuerung.Handle(CapsUp(150, shiftHeld: false));

        Assert.Equal(HotkeyDecision.Pass, shiftDown);
        Assert.Equal(HotkeyDecision.Pass, shiftUp);
        Assert.Equal(DictationState.Idle, steuerung.State);

        // Ein folgendes einzelnes Drücken innerhalb des Fensters startet nicht:
        // weder der wartende Tipp von 0 noch das Umschalt Ereignis von 100 zählen.
        var danach = steuerung.Handle(CapsDown(200));
        Assert.Equal(HotkeyDecision.Swallowed, danach);
        Assert.Equal(DictationState.Idle, steuerung.State);

        // Dieses Drücken war selbst wieder ein erster Tipp: ein zweites startet (Regel 1).
        steuerung.Handle(CapsUp(250));
        var zweiter = steuerung.Handle(CapsDown(300));
        Assert.Equal(HotkeyAction.StartRecording, zweiter.Action);
        Assert.Equal(DictationState.Recording, steuerung.State);
    }

    // Regel 4 und 8: Umschalt plus Caps Lock zählt nicht als erstes Drücken, also keine Push to Talk Frist.
    [Fact]
    public void Umschalt_plus_Caps_Lock_setzt_keine_Push_to_Talk_Frist()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(PushToTalkEnabled: true, PushToTalkHoldMs: 300));

        var shiftDown = steuerung.Handle(CapsDown(1000, shiftHeld: true));

        Assert.Equal(HotkeyDecision.Pass, shiftDown);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(1300));
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Regel 6: Automatische Wiederholung (zweites Drücken ohne Loslassen) wird ignoriert.
    [Fact]
    public void Wiederholung_ohne_Loslassen_wird_ignoriert_und_startet_nicht()
    {
        var steuerung = new HotkeyController();

        var erstesDown = steuerung.Handle(CapsDown(0));
        var wiederholung1 = steuerung.Handle(CapsDown(100));
        var wiederholung2 = steuerung.Handle(CapsDown(200));
        var wiederholung3 = steuerung.Handle(CapsDown(300));

        Assert.Equal(HotkeyDecision.Swallowed, erstesDown);
        Assert.Equal(HotkeyDecision.Swallowed, wiederholung1);
        Assert.Equal(HotkeyDecision.Swallowed, wiederholung2);
        Assert.Equal(HotkeyDecision.Swallowed, wiederholung3);
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
    }

    // Regel 3: In der Verarbeitung tut Caps Lock nichts (geschluckt), zurück in die Ruhe erst durch MarkProcessingFinished.
    [Fact]
    public void Verarbeitung_ignoriert_Caps_Lock_bis_MarkProcessingFinished_und_dann_startet_Doppeltipp_wieder()
    {
        var steuerung = InVerarbeitung();

        var down = steuerung.Handle(CapsDown(2000));
        var up = steuerung.Handle(CapsUp(2050));

        Assert.Equal(HotkeyDecision.Swallowed, down);
        Assert.Equal(HotkeyDecision.Swallowed, up);
        Assert.Equal(DictationState.Processing, steuerung.State);

        steuerung.MarkProcessingFinished();
        Assert.Equal(DictationState.Idle, steuerung.State);

        steuerung.Handle(CapsDown(5000));
        steuerung.Handle(CapsUp(5050));
        var start = steuerung.Handle(CapsDown(5200));

        Assert.Equal(HotkeyAction.StartRecording, start.Action);
        Assert.Equal(DictationState.Recording, steuerung.State);
    }

    // Regel 3: Tipps in der Verarbeitung tun nichts, sie zählen deshalb auch nach MarkProcessingFinished nicht als erster Tipp.
    [Fact]
    public void Tipp_in_der_Verarbeitung_zaehlt_nach_MarkProcessingFinished_nicht_als_erster_Tipp()
    {
        var steuerung = InVerarbeitung();

        steuerung.Handle(CapsDown(2000));
        steuerung.Handle(CapsUp(2050));
        steuerung.MarkProcessingFinished();

        var down = steuerung.Handle(CapsDown(2200));

        Assert.Equal(HotkeyDecision.Swallowed, down);
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Regel 3: MarkProcessingFinished außerhalb der Verarbeitung ändert den Zustand nicht.
    [Fact]
    public void MarkProcessingFinished_in_Ruhe_oder_Aufnahme_aendert_den_Zustand_nicht()
    {
        var ruhe = new HotkeyController();
        ruhe.MarkProcessingFinished();
        Assert.Equal(DictationState.Idle, ruhe.State);

        var aufnahme = InAufnahme();
        aufnahme.MarkProcessingFinished();
        Assert.Equal(DictationState.Recording, aufnahme.State);
    }

    // Regel 8: Push to Talk ausgeschaltet, die Frist bleibt null und langes Halten zählt wie ein einzelner Tipp.
    [Fact]
    public void Push_to_Talk_aus_setzt_keine_Frist_und_OnDeadline_tut_nichts()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(PushToTalkEnabled: false));

        steuerung.Handle(CapsDown(1000));

        Assert.Null(steuerung.PushToTalkDeadlineMs);
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(1300));
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(5000));
        Assert.Equal(DictationState.Idle, steuerung.State);

        var up = steuerung.Handle(CapsUp(5000));
        Assert.Equal(HotkeyDecision.Swallowed, up);
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
    }

    // Regel 8: Push to Talk eingeschaltet, Frist = jetzt + PushToTalkHoldMs, OnDeadline startet bei erreichter Frist, Loslassen stoppt.
    [Fact]
    public void Push_to_Talk_an_startet_bei_erreichter_Frist_und_Loslassen_stoppt()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(PushToTalkEnabled: true, PushToTalkHoldMs: 300));

        var down = steuerung.Handle(CapsDown(1000));

        Assert.Equal(HotkeyDecision.Swallowed, down);
        Assert.Equal(1300L, steuerung.PushToTalkDeadlineMs);
        Assert.Equal(DictationState.Idle, steuerung.State);

        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(1200));
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Equal(1300L, steuerung.PushToTalkDeadlineMs);

        Assert.Equal(HotkeyAction.StartRecording, steuerung.OnDeadline(1300));
        Assert.Equal(DictationState.Recording, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);

        var up = steuerung.Handle(CapsUp(2000));
        Assert.True(up.Swallow);
        Assert.Equal(HotkeyAction.StopRecording, up.Action);
        Assert.Equal(DictationState.Processing, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
    }

    // Regel 8: Loslassen vor der Frist löscht die Frist, ein späteres OnDeadline tut nichts.
    [Fact]
    public void Push_to_Talk_an_Loslassen_vor_der_Frist_loescht_die_Frist()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(PushToTalkEnabled: true, PushToTalkHoldMs: 300));

        steuerung.Handle(CapsDown(1000));
        Assert.Equal(1300L, steuerung.PushToTalkDeadlineMs);

        var up = steuerung.Handle(CapsUp(1100));

        Assert.Equal(HotkeyDecision.Swallowed, up);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(1300));
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(2000));
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Regel 8: Ein Doppeltipp innerhalb des Fensters hat Vorrang vor Push to Talk und löscht die Frist.
    [Fact]
    public void Push_to_Talk_an_Doppeltipp_hat_Vorrang_und_loescht_die_Frist()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(DoubleTapWindowMs: 350, PushToTalkEnabled: true, PushToTalkHoldMs: 300));

        steuerung.Handle(CapsDown(0));
        steuerung.Handle(CapsUp(50));
        var zweitesDown = steuerung.Handle(CapsDown(200));

        Assert.True(zweitesDown.Swallow);
        Assert.Equal(HotkeyAction.StartRecording, zweitesDown.Action);
        Assert.Equal(DictationState.Recording, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);

        // Die alte Frist (0 + 300) darf nichts mehr auslösen.
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(300));
        Assert.Equal(DictationState.Recording, steuerung.State);
    }

    // Regel 8: OnDeadline startet nur im Ruhezustand. In Aufnahme oder Verarbeitung tut es nichts.
    [Fact]
    public void OnDeadline_ausserhalb_des_Ruhezustands_tut_nichts()
    {
        var options = new HotkeyOptions(PushToTalkEnabled: true, PushToTalkHoldMs: 300);

        var aufnahme = InAufnahme(options);
        Assert.Equal(HotkeyAction.None, aufnahme.OnDeadline(1000));
        Assert.Equal(HotkeyAction.None, aufnahme.OnDeadline(10000));
        Assert.Equal(DictationState.Recording, aufnahme.State);

        var verarbeitung = InVerarbeitung(options);
        Assert.Equal(HotkeyAction.None, verarbeitung.OnDeadline(2000));
        Assert.Equal(HotkeyAction.None, verarbeitung.OnDeadline(20000));
        Assert.Equal(DictationState.Processing, verarbeitung.State);
    }

    // Regel 9: Reset setzt alles auf Ruhe, auch aus der Aufnahme heraus. Ein verwaistes Loslassen danach wird geschluckt.
    [Fact]
    public void Reset_aus_der_Aufnahme_setzt_Ruhe_und_schluckt_danach_ein_verwaistes_Loslassen()
    {
        var steuerung = InAufnahme();

        steuerung.Reset();

        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);

        var verwaistesUp = steuerung.Handle(CapsUp(5000));
        Assert.Equal(HotkeyDecision.Swallowed, verwaistesUp);
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Regel 9: Reset löscht auch eine wartende Push to Talk Frist.
    [Fact]
    public void Reset_loescht_eine_wartende_Push_to_Talk_Frist()
    {
        var steuerung = new HotkeyController(new HotkeyOptions(PushToTalkEnabled: true, PushToTalkHoldMs: 300));

        steuerung.Handle(CapsDown(1000));
        Assert.Equal(1300L, steuerung.PushToTalkDeadlineMs);

        steuerung.Reset();

        Assert.Null(steuerung.PushToTalkDeadlineMs);
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Equal(HotkeyAction.None, steuerung.OnDeadline(1300));
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Regel 9: Reset setzt alles zurück, also verfällt auch ein wartender erster Tipp.
    [Fact]
    public void Reset_verwirft_einen_wartenden_ersten_Tipp()
    {
        var steuerung = new HotkeyController();

        steuerung.Handle(CapsDown(0));
        steuerung.Handle(CapsUp(50));

        steuerung.Reset();

        var down = steuerung.Handle(CapsDown(200));
        Assert.Equal(HotkeyDecision.Swallowed, down);
        Assert.Equal(DictationState.Idle, steuerung.State);
    }

    // Vertrag: Ungültige Optionen werfen ArgumentOutOfRangeException.
    [Theory]
    [InlineData(0, 30, 300)]
    [InlineData(350, -1, 300)]
    [InlineData(350, 30, 0)]
    public void Ungueltige_Optionen_werfen_ArgumentOutOfRangeException(int doubleTapWindowMs, int minTapGapMs, int pushToTalkHoldMs)
    {
        var options = new HotkeyOptions(doubleTapWindowMs, minTapGapMs, PushToTalkEnabled: false, pushToTalkHoldMs);

        Assert.Throws<ArgumentOutOfRangeException>(() => new HotkeyController(options));
    }

    // Vertrag: Options liefert die übergebenen Werte.
    [Fact]
    public void Options_liefert_die_uebergebenen_Werte()
    {
        var options = new HotkeyOptions(DoubleTapWindowMs: 500, MinTapGapMs: 40, PushToTalkEnabled: true, PushToTalkHoldMs: 600);

        var steuerung = new HotkeyController(options);

        Assert.Equal(500, steuerung.Options.DoubleTapWindowMs);
        Assert.Equal(40, steuerung.Options.MinTapGapMs);
        Assert.True(steuerung.Options.PushToTalkEnabled);
        Assert.Equal(600, steuerung.Options.PushToTalkHoldMs);
    }

    // Vertrag: Ohne Argument gelten die Standardwerte 350, 30, aus, 300.
    [Fact]
    public void Ohne_Argument_gelten_die_Standardwerte()
    {
        var steuerung = new HotkeyController();

        Assert.Equal(350, steuerung.Options.DoubleTapWindowMs);
        Assert.Equal(30, steuerung.Options.MinTapGapMs);
        Assert.False(steuerung.Options.PushToTalkEnabled);
        Assert.Equal(300, steuerung.Options.PushToTalkHoldMs);
        Assert.Equal(DictationState.Idle, steuerung.State);
        Assert.Null(steuerung.PushToTalkDeadlineMs);
    }

    // Regel 5 und 6: Die Wiederholung eines gehaltenen Caps Lock wird auch in Aufnahme und Verarbeitung geschluckt
    // und löst dort nichts aus.
    [Fact]
    public void Wiederholung_von_Caps_Lock_wird_in_Aufnahme_und_Verarbeitung_geschluckt()
    {
        var aufnahme = InAufnahme();
        // InAufnahme lässt die Taste gedrückt: eine Wiederholung in der Aufnahme darf weder stoppen noch etwas ändern.
        var wiederholungInAufnahme = aufnahme.Handle(CapsDown(230));
        Assert.True(wiederholungInAufnahme.Swallow);
        Assert.Equal(HotkeyAction.None, wiederholungInAufnahme.Action);
        Assert.Equal(DictationState.Recording, aufnahme.State);
        Assert.True(aufnahme.Handle(CapsUp(250)).Swallow);
        var stopp = aufnahme.Handle(CapsDown(1000));
        Assert.Equal(HotkeyAction.StopRecording, stopp.Action);
        Assert.Equal(DictationState.Processing, aufnahme.State);
        var wiederholungInVerarbeitung = aufnahme.Handle(CapsDown(1030));
        Assert.True(wiederholungInVerarbeitung.Swallow);
        Assert.Equal(HotkeyAction.None, wiederholungInVerarbeitung.Action);
        Assert.Equal(DictationState.Processing, aufnahme.State);
        Assert.True(aufnahme.Handle(CapsUp(1100)).Swallow);

        var verarbeitung = InVerarbeitung();
        Assert.True(verarbeitung.Handle(CapsDown(5000)).Swallow);
        var wiederholung = verarbeitung.Handle(CapsDown(5030));
        Assert.True(wiederholung.Swallow);
        Assert.Equal(HotkeyAction.None, wiederholung.Action);
        Assert.Equal(DictationState.Processing, verarbeitung.State);
        Assert.True(verarbeitung.Handle(CapsUp(5100)).Swallow);
    }

    // Regel 2: Nach dem Abbruch werden Wiederholungen des noch gehaltenen Esc geschluckt, bis das Loslassen kommt;
    // danach geht Esc im Ruhezustand wieder durch (Regel 7).
    [Fact]
    public void Esc_Wiederholungen_nach_dem_Abbruch_werden_bis_zum_Loslassen_geschluckt()
    {
        var steuerung = InAufnahme();

        var abbruch = steuerung.Handle(EscDown(1000));
        Assert.True(abbruch.Swallow);
        Assert.Equal(HotkeyAction.Cancel, abbruch.Action);
        Assert.Equal(DictationState.Idle, steuerung.State);

        var wiederholung1 = steuerung.Handle(EscDown(1030));
        var wiederholung2 = steuerung.Handle(EscDown(1060));
        Assert.True(wiederholung1.Swallow);
        Assert.True(wiederholung2.Swallow);
        Assert.Equal(HotkeyAction.None, wiederholung1.Action);
        Assert.Equal(HotkeyAction.None, wiederholung2.Action);

        Assert.True(steuerung.Handle(EscUp(1100)).Swallow);

        var danach = steuerung.Handle(EscDown(2000));
        Assert.False(danach.Swallow);
        Assert.Equal(HotkeyAction.None, danach.Action);
        Assert.False(steuerung.Handle(EscUp(2050)).Swallow);
    }
}
