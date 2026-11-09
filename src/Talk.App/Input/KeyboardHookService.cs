using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

using Microsoft.Win32;

using Talk.Core;
using Talk.Core.Hotkey;

namespace Talk.App.Input;

/// <summary>
/// Globaler Tastaturhook (WH_KEYBOARD_LL) in einem eigenen Thread "Talk Hook" mit eigener Nachrichtenschleife.
/// </summary>
/// <remarks>
/// Die Hook Prozedur sieht sich von jedem Ereignis zuerst nur den Tastencode an. Alles außer Caps Lock (0x14) und Esc (0x1B)
/// geht sofort und ungeprüft an Windows zurück: ohne Protokoll, ohne Zähler, ohne weitere Verarbeitung.
/// Für diese zwei Tasten liest sie den Umschalt Zustand, fragt die <see cref="HotkeyController"/> und gibt die Entscheidung zurück.
/// Aktionen landen in einer Warteschlange und werden außerhalb des Hook Threads über <see cref="ActionReceived"/> gemeldet.
/// Diagnosemeldungen ohne Aktion (Umschalt plus Caps Lock durchgelassen) gehen über eine zweite Warteschlange an <see cref="NoticeReceived"/>.
/// Ein Wächter setzt den Hook nach dem Aufwachen, nach dem Entsperren und alle 5 Minuten im Ruhezustand neu.
/// </remarks>
internal sealed class KeyboardHookService : IDisposable
{
    /// <summary>Eigene Thread Nachricht: Hook im Hook Thread lösen und neu setzen.</summary>
    private const uint WM_TALK_REINSTALL = NativeMethods.WM_APP + 1;

    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    // Der Delegat liegt in einem Feld, damit der Speicherbereiniger ihn nicht freigibt, solange Windows ihn ruft.
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
    private readonly Channel<HotkeyAction> _actions = Channel.CreateUnbounded<HotkeyAction>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Channel<HookNotice> _notices = Channel.CreateUnbounded<HookNotice>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly ConcurrentQueue<HookReinstallReason> _reinstallReasons = new();
    private readonly ManualResetEventSlim _started = new(false);
    private readonly CancellationTokenSource _cts = new();
    private readonly PushToTalkTimer _pushToTalkTimer;
    private readonly object _lifecycleGate = new();

    private volatile HotkeyController _controller;
    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    private int _installError;
    private System.Threading.Timer? _watchdog;
    private Task? _consumer;
    private Task? _noticeConsumer;
    private bool _systemEventsSubscribed;
    private bool _disposed;

    // Nur im Hook Thread benutzt: true, solange ein durchgelassenes Umschalt plus Caps Lock gehalten wird.
    // Verhindert, dass die automatische Wiederholung die Meldung je Drücken mehrfach erzeugt.
    private bool _shiftCapsPassActive;

    /// <summary>Eine Aktion der Steuerung (Start, Stopp, Abbruch). Wird nie im Hook Thread ausgelöst.</summary>
    public event Action<HotkeyAction>? ActionReceived;

    /// <summary>Eine Diagnosemeldung des Hooks ohne Aktion, zum Beispiel Umschalt plus Caps Lock durchgelassen. Wird nie im Hook Thread ausgelöst.</summary>
    public event Action<HookNotice>? NoticeReceived;

    /// <summary>Der Hook wurde neu gesetzt: Grund und ob das Setzen geklappt hat. Wird nie im Hook Thread ausgelöst.</summary>
    public event Action<HookReinstallReason, bool>? HookReinstalled;

    public KeyboardHookService(HotkeyController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _hookProc = HookProc;
        _pushToTalkTimer = new PushToTalkTimer(() => _controller, Enqueue);
    }

    /// <summary>Die aktuelle Steuerung. Kann über <see cref="ReplaceController"/> getauscht werden.</summary>
    public HotkeyController Controller => _controller;

    /// <summary>True, wenn der Hook gerade gesetzt ist.</summary>
    public bool IsInstalled => Volatile.Read(ref _hook) != 0;

    /// <summary>Windows Fehlercode des letzten Setzversuchs, 0 bei Erfolg.</summary>
    public int LastInstallError => Volatile.Read(ref _installError);

    /// <summary>
    /// Startet Hook Thread, Verbraucher, Wächter und Push to Talk Timer. Kehrt zurück, sobald der Hook gesetzt wurde
    /// (oder das Setzen gescheitert ist, siehe <see cref="IsInstalled"/> und <see cref="LastInstallError"/>).
    /// </summary>
    public void Start()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread is not null) return;

            _consumer = Task.Run(() => ConsumeAsync(_cts.Token));
            _noticeConsumer = Task.Run(() => ConsumeNoticesAsync(_cts.Token));

            _thread = new Thread(HookThreadMain)
            {
                Name = "Talk Hook",
                IsBackground = true,
            };
            _thread.Start();
            _started.Wait(StartTimeout);

            SubscribeSystemEvents();
            _watchdog = new System.Threading.Timer(OnWatchdogTick, null, WatchdogInterval, WatchdogInterval);
            UpdatePushToTalkTimer();
        }
    }

    /// <summary>Tauscht die Steuerung aus, zum Beispiel nach geänderten Einstellungen. Der Hook benutzt ab sofort die neue.</summary>
    public void ReplaceController(HotkeyController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;
        UpdatePushToTalkTimer();
    }

    /// <summary>Bittet den Hook Thread, den Hook zu lösen und neu zu setzen. Kehrt sofort zurück.</summary>
    public void RequestReinstall(HookReinstallReason reason)
    {
        if (_disposed || _threadId == 0) return;
        _reinstallReasons.Enqueue(reason);
        NativeMethods.PostThreadMessageW(_threadId, WM_TALK_REINSTALL, 0, 0);
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _watchdog?.Dispose();
        _pushToTalkTimer.Dispose();
        UnsubscribeSystemEvents();

        if (_thread is not null && _threadId != 0)
        {
            NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, 0, 0);
            if (!_thread.Join(StopTimeout))
            {
                // Notnagel, falls die Schleife nicht reagiert: den Hook trotzdem lösen.
                Uninstall();
            }
        }

        _actions.Writer.TryComplete();
        _notices.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            _consumer?.Wait(StopTimeout);
            _noticeConsumer?.Wait(StopTimeout);
        }
        catch
        {
            // Die Verbraucher sind beim Beenden nicht mehr wichtig.
        }

        _cts.Dispose();
        _started.Dispose();
    }

    /// <summary>Legt eine Aktion in die Warteschlange, genau wie der Hook es tut. None wird verworfen.</summary>
    internal void Enqueue(HotkeyAction action)
    {
        if (action != HotkeyAction.None) _actions.Writer.TryWrite(action);
    }

    private void UpdatePushToTalkTimer()
    {
        if (_controller.Options.PushToTalkEnabled) _pushToTalkTimer.Start();
        else _pushToTalkTimer.Stop();
    }

    // Hook Thread: Hook setzen, dann Nachrichten verarbeiten, bis WM_QUIT kommt.
    private void HookThreadMain()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        Install();
        _started.Set();

        while (NativeMethods.GetMessageW(out NativeMethods.MSG msg, 0, 0, 0) > 0)
        {
            if (msg.message == WM_TALK_REINSTALL)
            {
                Reinstall();
                continue;
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        Uninstall();
    }

    private void Install()
    {
        nint handle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandleW(null),
            0);

        Volatile.Write(ref _installError, handle == 0 ? Marshal.GetLastWin32Error() : 0);
        Volatile.Write(ref _hook, handle);
    }

    private void Uninstall()
    {
        nint handle = Interlocked.Exchange(ref _hook, 0);
        if (handle != 0) NativeMethods.UnhookWindowsHookEx(handle);
    }

    private void Reinstall()
    {
        if (!_reinstallReasons.TryDequeue(out HookReinstallReason reason)) reason = HookReinstallReason.Unknown;

        Uninstall();
        Install();
        _controller.Reset();

        bool installed = IsInstalled;
        HookReinstallReason reasonCopy = reason;
        _ = Task.Run(() =>
        {
            try
            {
                HookReinstalled?.Invoke(reasonCopy, installed);
            }
            catch
            {
                // Ein Fehler im Abnehmer darf den Dienst nicht stören.
            }
        });
    }

    // Die Hook Prozedur. Läuft im Hook Thread, muss sofort zurückkehren, protokolliert nichts und wirft nie.
    private nint HookProc(int nCode, nint wParam, nint lParam)
    {
        try
        {
            if (nCode != NativeMethods.HC_ACTION) return Next();

            // Zuerst nur den Tastencode lesen. Jede andere Taste geht sofort und ungeprüft weiter.
            uint vkCode = (uint)Marshal.ReadInt32(lParam);
            if (vkCode != NativeMethods.VK_CAPITAL && vkCode != NativeMethods.VK_ESCAPE) return Next();

            NativeMethods.KBDLLHOOKSTRUCT data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (data.dwExtraInfo == NativeMethods.TalkInjectedMarker) return Next();

            KeyDirection direction;
            switch ((uint)wParam)
            {
                case NativeMethods.WM_KEYDOWN:
                case NativeMethods.WM_SYSKEYDOWN:
                    direction = KeyDirection.Down;
                    break;
                case NativeMethods.WM_KEYUP:
                case NativeMethods.WM_SYSKEYUP:
                    direction = KeyDirection.Up;
                    break;
                default:
                    return Next();
            }

            HotkeyKey key = vkCode == NativeMethods.VK_CAPITAL ? HotkeyKey.CapsLock : HotkeyKey.Escape;
            bool shiftHeld = (NativeMethods.GetAsyncKeyState((int)NativeMethods.VK_SHIFT) & 0x8000) != 0;

            HotkeyDecision decision = _controller.Handle(new KeyEvent(key, direction, shiftHeld, Environment.TickCount64));
            if (decision.Action != HotkeyAction.None) _actions.Writer.TryWrite(decision.Action);

            // Diagnose ohne Aktion: Umschalt plus Caps Lock wurde beim Drücken durchgelassen (Regel 4). Einmal je Drücken,
            // die automatische Wiederholung erzeugt keine weitere Meldung. Protokolliert wird außerhalb des Hook Threads.
            if (key == HotkeyKey.CapsLock)
            {
                if (direction == KeyDirection.Down && shiftHeld && !decision.Swallow)
                {
                    if (!_shiftCapsPassActive)
                    {
                        _shiftCapsPassActive = true;
                        _notices.Writer.TryWrite(HookNotice.ShiftCapsPassthrough);
                    }
                }
                else if (direction == KeyDirection.Up)
                {
                    _shiftCapsPassActive = false;
                }
            }

            return decision.Swallow ? 1 : Next();
        }
        catch
        {
            return Next();
        }

        nint Next() => NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // Verbraucher: liest die Warteschlange und löst ActionReceived außerhalb des Hook Threads aus.
    private async Task ConsumeAsync(CancellationToken token)
    {
        try
        {
            await foreach (HotkeyAction action in _actions.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    ActionReceived?.Invoke(action);
                }
                catch
                {
                    // Ein Fehler im Abnehmer darf die Warteschlange nicht anhalten.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normales Ende beim Dispose.
        }
        catch
        {
            // Der Verbraucher endet still; die App läuft weiter.
        }
    }

    // Zweiter Verbraucher: liest die Diagnosemeldungen und löst NoticeReceived außerhalb des Hook Threads aus.
    private async Task ConsumeNoticesAsync(CancellationToken token)
    {
        try
        {
            await foreach (HookNotice notice in _notices.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    NoticeReceived?.Invoke(notice);
                }
                catch
                {
                    // Ein Fehler im Abnehmer darf die Warteschlange nicht anhalten.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normales Ende beim Dispose.
        }
        catch
        {
            // Der Verbraucher endet still; die App läuft weiter.
        }
    }

    private void SubscribeSystemEvents()
    {
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _systemEventsSubscribed = true;
        }
        catch
        {
            // Ohne Systemereignisse bleibt der Wächter per Timer aktiv.
            _systemEventsSubscribed = false;
        }
    }

    private void UnsubscribeSystemEvents()
    {
        if (!_systemEventsSubscribed) return;
        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
        catch
        {
            // Beim Beenden nicht mehr wichtig.
        }

        _systemEventsSubscribed = false;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) RequestReinstall(HookReinstallReason.Resume);
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock) RequestReinstall(HookReinstallReason.Unlock);
        else if (e.Reason == SessionSwitchReason.SessionLogon) RequestReinstall(HookReinstallReason.Logon);
    }

    private void OnWatchdogTick(object? state)
    {
        try
        {
            if (_controller.State == DictationState.Idle) RequestReinstall(HookReinstallReason.Interval);
        }
        catch
        {
            // Der Wächter versucht es beim nächsten Intervall erneut.
        }
    }
}
