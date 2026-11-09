// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Hotkey;

namespace Talk.App.Input;

/// <summary>
/// Prüft alle 25 ms, ob die Push to Talk Frist der Steuerung erreicht ist, und ruft dann <see cref="HotkeyController.OnDeadline"/>.
/// Eine Aktion ungleich None geht über denselben Weg weiter wie die Aktionen aus dem Hook.
/// Der Timer läuft nur, wenn Push to Talk eingeschaltet ist; das steuert der <see cref="KeyboardHookService"/>.
/// </summary>
internal sealed class PushToTalkTimer : IDisposable
{
    private const int IntervalMs = 25;

    private readonly Func<HotkeyController> _controller;
    private readonly Action<HotkeyAction> _dispatch;
    private readonly object _gate = new();

    private System.Threading.Timer? _timer;
    private bool _disposed;

    /// <param name="controller">Liefert die aktuelle Steuerung (sie kann zur Laufzeit ausgetauscht werden).</param>
    /// <param name="dispatch">Nimmt eine Aktion ungleich None entgegen, wie beim Hook.</param>
    public PushToTalkTimer(Func<HotkeyController> controller, Action<HotkeyAction> dispatch)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
    }

    /// <summary>True, solange der Timer tickt.</summary>
    public bool IsRunning
    {
        get { lock (_gate) return _timer is not null; }
    }

    /// <summary>Startet den Timer, falls er nicht schon läuft.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _timer is not null) return;
            _timer = new System.Threading.Timer(Tick, null, IntervalMs, IntervalMs);
        }
    }

    /// <summary>Hält den Timer an.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Tick(object? state)
    {
        try
        {
            HotkeyController controller = _controller();
            long? deadline = controller.PushToTalkDeadlineMs;
            if (deadline is null) return;

            long now = Environment.TickCount64;
            if (now < deadline.Value) return;

            HotkeyAction action = controller.OnDeadline(now);
            if (action != HotkeyAction.None) _dispatch(action);
        }
        catch
        {
            // Der Timer darf nie eine Ausnahme nach außen tragen; der nächste Tick kommt in 25 ms.
        }
    }
}
