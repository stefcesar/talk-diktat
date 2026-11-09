// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Hotkey;

/// <summary>
/// Reine Ablauflogik des Hotkeys, ohne Windows Aufrufe und deshalb automatisch testbar.
/// Der Hook ruft für jedes Caps Lock und Esc Ereignis <see cref="Handle"/> auf und bekommt sofort die Entscheidung zurück.
/// </summary>
/// <remarks>
/// Die neun Regeln (gleichlautend mit PLAYBOOK.md, Abschnitt 13):
/// 1. Ruhezustand: Ein einzelnes Drücken von Caps Lock tut nichts und wartet auf nichts. Ein zweites Drücken innerhalb des
///    Doppeltipp Fensters (Drücken zu Drücken) startet die Aufnahme sofort beim Drücken. Zu langsam zählt als neuer erster Tipp,
///    zu schnell gilt als Prellen und wird ignoriert.
/// 2. Aufnahme: Drücken von Caps Lock beendet die Aufnahme (Verarbeitung). Drücken von Esc bricht ab (Ruhe); das zugehörige
///    Loslassen von Esc und Wiederholungen des gehaltenen Esc werden ebenfalls geschluckt.
/// 3. Verarbeitung: Caps Lock wird geschluckt und ignoriert, Esc wird durchgelassen. Zurück in die Ruhe erst durch <see cref="MarkProcessingFinished"/>.
/// 4. Umschalt plus Caps Lock (Umschalt beim Drücken gehalten) wird durchgelassen (echte Großschreibung), zählt nie als Tipp
///    und lässt einen wartenden ersten Tipp verfallen.
/// 5. Caps Lock ohne Umschalt wird in jedem Zustand geschluckt (Drücken, Wiederholung, Loslassen), damit sich Zustand und
///    Leuchtanzeige nicht verstellen.
/// 6. Automatische Wiederholung bei gehaltener Taste wird ignoriert.
/// 7. Esc außerhalb der Aufnahme wird immer durchgelassen.
/// 8. Push to Talk (optional): Caps Lock länger als die Haltezeit gehalten startet die Aufnahme, Loslassen beendet sie.
///    Die App ruft dafür <see cref="OnDeadline"/>, wenn <see cref="PushToTalkDeadlineMs"/> erreicht ist. Ein Doppeltipp hat Vorrang.
/// 9. <see cref="Reset"/> setzt alles auf Ruhe zurück, zum Beispiel nach dem Neusetzen des Hooks.
/// Alle Methoden sind threadsicher.
/// </remarks>
public sealed class HotkeyController
{
    private readonly object _gate = new();
    private readonly HotkeyOptions _options;

    private DictationState _state = DictationState.Idle;
    private bool _capsIsDown;
    private bool _passCapsUpThrough;
    private bool _escDownSwallowed;
    private bool _pushToTalkActive;
    private long? _firstTapDownMs;
    private long? _pushToTalkDeadlineMs;

    /// <summary>Erzeugt eine Steuerung mit den angegebenen Einstellungen oder den Standardwerten.</summary>
    public HotkeyController(HotkeyOptions? options = null)
    {
        _options = options ?? new HotkeyOptions();
        if (_options.DoubleTapWindowMs <= 0) throw new ArgumentOutOfRangeException(nameof(options), "DoubleTapWindowMs muss größer als 0 sein.");
        if (_options.MinTapGapMs < 0) throw new ArgumentOutOfRangeException(nameof(options), "MinTapGapMs darf nicht negativ sein.");
        if (_options.PushToTalkHoldMs <= 0) throw new ArgumentOutOfRangeException(nameof(options), "PushToTalkHoldMs muss größer als 0 sein.");
    }

    /// <summary>Die verwendeten Einstellungen.</summary>
    public HotkeyOptions Options => _options;

    /// <summary>Aktueller Zustand des Diktats.</summary>
    public DictationState State
    {
        get { lock (_gate) return _state; }
    }

    /// <summary>
    /// Zeitpunkt in Millisekunden, zu dem die App <see cref="OnDeadline"/> rufen soll, oder null.
    /// Nur gesetzt, wenn Push to Talk eingeschaltet ist und Caps Lock im Ruhezustand gehalten wird.
    /// </summary>
    public long? PushToTalkDeadlineMs
    {
        get { lock (_gate) return _pushToTalkDeadlineMs; }
    }

    /// <summary>Verarbeitet ein Tastenereignis und liefert sofort die Entscheidung. Darf nie blockieren.</summary>
    public HotkeyDecision Handle(KeyEvent e)
    {
        lock (_gate)
        {
            return e.Key switch
            {
                HotkeyKey.CapsLock => HandleCapsLock(e),
                HotkeyKey.Escape => HandleEscape(e),
                _ => HotkeyDecision.Pass,
            };
        }
    }

    /// <summary>
    /// Wird von der App gerufen, wenn der Zeitpunkt <see cref="PushToTalkDeadlineMs"/> erreicht ist.
    /// Liefert <see cref="HotkeyAction.StartRecording"/>, wenn Caps Lock noch gehalten wird und der Ruhezustand gilt, sonst None.
    /// </summary>
    public HotkeyAction OnDeadline(long nowMs)
    {
        lock (_gate)
        {
            if (_pushToTalkDeadlineMs is null || nowMs < _pushToTalkDeadlineMs.Value) return HotkeyAction.None;
            _pushToTalkDeadlineMs = null;
            if (!_capsIsDown || _state != DictationState.Idle) return HotkeyAction.None;

            _pushToTalkActive = true;
            _firstTapDownMs = null;
            _state = DictationState.Recording;
            return HotkeyAction.StartRecording;
        }
    }

    /// <summary>Die App meldet, dass die Verarbeitung (Erkennung und Einfügen) fertig ist. Zurück in den Ruhezustand.</summary>
    public void MarkProcessingFinished()
    {
        lock (_gate)
        {
            if (_state == DictationState.Processing) _state = DictationState.Idle;
        }
    }

    /// <summary>Alles zurücksetzen, zum Beispiel nachdem der Hook neu gesetzt wurde. Der Zustand wird Ruhezustand.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _state = DictationState.Idle;
            _capsIsDown = false;
            _passCapsUpThrough = false;
            _escDownSwallowed = false;
            _pushToTalkActive = false;
            _firstTapDownMs = null;
            _pushToTalkDeadlineMs = null;
        }
    }

    private HotkeyDecision HandleCapsLock(KeyEvent e)
    {
        if (e.Direction == KeyDirection.Down)
        {
            if (_capsIsDown)
            {
                // Automatische Wiederholung bei gehaltener Taste: Durchlassen, wenn das Drücken durchgelassen wurde, sonst schlucken.
                return _passCapsUpThrough ? HotkeyDecision.Pass : HotkeyDecision.Swallowed;
            }

            _capsIsDown = true;

            if (e.ShiftHeld)
            {
                // Umschalt plus Caps Lock: echte Großschreibung. Durchlassen, kein Tipp.
                _passCapsUpThrough = true;
                _firstTapDownMs = null;
                _pushToTalkDeadlineMs = null;
                return HotkeyDecision.Pass;
            }

            _passCapsUpThrough = false;
            return HandleCapsDown(e.TimestampMs);
        }

        // Loslassen
        if (!_capsIsDown)
        {
            // Loslassen ohne vorheriges Drücken (zum Beispiel Hook wurde bei gehaltener Taste gesetzt): schlucken, nichts tun.
            return HotkeyDecision.Swallowed;
        }

        _capsIsDown = false;
        _pushToTalkDeadlineMs = null;

        if (_passCapsUpThrough)
        {
            _passCapsUpThrough = false;
            return HotkeyDecision.Pass;
        }

        if (_pushToTalkActive)
        {
            _pushToTalkActive = false;
            _firstTapDownMs = null;
            _state = DictationState.Processing;
            return new HotkeyDecision(true, HotkeyAction.StopRecording);
        }

        return HotkeyDecision.Swallowed;
    }

    private HotkeyDecision HandleCapsDown(long nowMs)
    {
        switch (_state)
        {
            case DictationState.Recording:
                _firstTapDownMs = null;
                _state = DictationState.Processing;
                return new HotkeyDecision(true, HotkeyAction.StopRecording);

            case DictationState.Processing:
                _firstTapDownMs = null;
                return HotkeyDecision.Swallowed;

            default:
                if (_firstTapDownMs is long first)
                {
                    long gap = nowMs - first;
                    if (gap < _options.MinTapGapMs)
                    {
                        // Prellen: ignorieren, der erste Tipp bleibt gültig.
                        return HotkeyDecision.Swallowed;
                    }

                    if (gap <= _options.DoubleTapWindowMs)
                    {
                        _firstTapDownMs = null;
                        _pushToTalkDeadlineMs = null;
                        _state = DictationState.Recording;
                        return new HotkeyDecision(true, HotkeyAction.StartRecording);
                    }
                }

                // Erster Tipp oder zu langsamer zweiter Tipp: neu zählen, nichts tun.
                _firstTapDownMs = nowMs;
                _pushToTalkDeadlineMs = _options.PushToTalkEnabled ? nowMs + _options.PushToTalkHoldMs : null;
                return HotkeyDecision.Swallowed;
        }
    }

    private HotkeyDecision HandleEscape(KeyEvent e)
    {
        if (e.Direction == KeyDirection.Down)
        {
            if (_escDownSwallowed)
            {
                // Wiederholung des noch gehaltenen Esc nach einem Abbruch: ebenfalls schlucken, sonst sähe das Zielprogramm
                // ein Drücken ohne Loslassen.
                return HotkeyDecision.Swallowed;
            }

            if (_state != DictationState.Recording) return HotkeyDecision.Pass;

            _state = DictationState.Idle;
            _pushToTalkActive = false;
            _firstTapDownMs = null;
            _pushToTalkDeadlineMs = null;
            _escDownSwallowed = true;
            return new HotkeyDecision(true, HotkeyAction.Cancel);
        }

        if (_escDownSwallowed)
        {
            // Das Loslassen zu einem geschluckten Drücken ebenfalls schlucken, damit kein einzelnes Esc Up beim Zielprogramm ankommt.
            _escDownSwallowed = false;
            return HotkeyDecision.Swallowed;
        }

        return HotkeyDecision.Pass;
    }
}
