// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Talk.Core;

namespace Talk.App.Ui;

/// <summary>
/// Das Overlay: eine kleine dunkle Pille unten in der Mitte über der Taskleiste mit dem Talk Zeichen und dem Zustand.
/// Sichtbar nur während Aufnahme und Verarbeitung, bei einem Fehler kurz. Es folgt nur dem Zustand und dem Pegel des Ablaufs.
/// </summary>
/// <remarks>
/// Es nimmt nie den Fokus: Das Fenster wird ohne Aktivierung gezeigt und trägt die Windows Stile "nie aktivieren",
/// "Klicks gehen hindurch" und "Werkzeugfenster" (kein Eintrag in der Taskleiste und nicht in Alt+Tab).
/// Alle Aufrufe gehören auf den UI Thread.
/// </remarks>
public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const double BottomGap = 24;
    private const double BarCenter = 16;
    private const double BarRest = 4;
    private const double LeftBarMax = 24;
    private const double RightBarMax = 14;

    private static readonly TimeSpan ErrorShown = TimeSpan.FromSeconds(2.5);

    private readonly DispatcherTimer _hideTimer;
    private TrayStatus _status = TrayStatus.Idle;
    private bool _enabled = true;
    private bool _closed;

    // Gesetzt, wenn das Zeigen einmal gescheitert ist. Danach bleibt das Overlay für diesen Lauf aus.
    private bool _broken;

    public OverlayWindow()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = ErrorShown };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (_status == TrayStatus.Error) HideOverlay();
        };
    }

    /// <summary>Die Fensterkennung, sobald das Fenster einmal gezeigt wurde; sonst 0. Für die Prüfung, dass es nie vorn ist.</summary>
    public nint Handle { get; private set; }

    /// <summary>Mit false bleibt das Overlay immer verborgen. Die Einstellung des Nutzers setzt den Wert.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;

            // Den geltenden Zustand neu zeigen: Beim Abstellen verschwindet die Pille, beim Anstellen mitten im Diktat erscheint sie.
            TrayStatus current = _status;
            _status = TrayStatus.Idle;
            HideOverlay();
            Present(current);
        }
    }

    /// <summary>
    /// Zeigt den Zustand: Aufnahme und Verarbeitung sichtbar, Ruhe verborgen, Fehler für kurze Zeit sichtbar.
    /// Wirft nie: Das Overlay ist nur Anzeige. Scheitert das Zeigen, schaltet es sich für diesen Lauf ab, statt ein Diktat zu stören.
    /// </summary>
    public void Present(TrayStatus status)
    {
        if (_closed || _broken || status == _status) return;
        _status = status;
        _hideTimer.Stop();

        try
        {
            if (!_enabled || status == TrayStatus.Idle)
            {
                HideOverlay();
                return;
            }

            StateText.Text = status switch
            {
                TrayStatus.Recording => "Aufnahme",
                TrayStatus.Processing => "Verarbeitung",
                _ => "Fehler, siehe Diagnose",
            };

            // In der Verarbeitung pulsieren die beiden Punkte, sonst stehen sie still.
            StopPulse();
            SetBars(BarRest, BarRest);
            if (status == TrayStatus.Processing)
            {
                DoubleAnimation pulse = new(1.0, 0.25, new Duration(TimeSpan.FromMilliseconds(600)))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                LeftBar.BeginAnimation(OpacityProperty, pulse);
                RightBar.BeginAnimation(OpacityProperty, pulse);
            }

            Place();
            if (!IsVisible) Show();
            if (status == TrayStatus.Error) _hideTimer.Start();
        }
        catch (Exception)
        {
            _broken = true;
            Talk.App.Diagnostics.AppLog.Write("overlay", "error", "grund=zeigen");
            try
            {
                StopPulse();
                if (IsVisible) Hide();
            }
            catch
            {
                // Mehr als abschalten geht hier nicht.
            }
        }
    }

    /// <summary>
    /// Bewegt die Pegelbalken (Pegel 0 bis 100). Wirkt nur während der Aufnahme.
    /// </summary>
    public void SetLevel(double level)
    {
        if (_closed || _status != TrayStatus.Recording || !IsVisible) return;

        double share = Math.Clamp(level, 0, 100) / 100;
        SetBars(BarRest + ((LeftBarMax - BarRest) * share), BarRest + ((RightBarMax - BarRest) * share));
    }

    /// <summary>
    /// Zeichnet die Pille in ein Bild, ohne den Bildschirm abzufotografieren. Nur für die Prüfung des Aussehens.
    /// </summary>
    public BitmapSource Render()
    {
        Pill.Measure(new System.Windows.Size(Width, Height));
        Pill.Arrange(new Rect(0, 0, Width, Height));
        Pill.UpdateLayout();
        RenderTargetBitmap bitmap = new((int)(Width * 2), (int)(Height * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(Pill);
        return bitmap;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        Handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;
        _hideTimer.Stop();
    }

    private void HideOverlay()
    {
        // Eine laufende Animation tickt auch bei verborgenem Fenster weiter. In Ruhe soll Talk nichts tun.
        StopPulse();
        if (!_closed && IsVisible) Hide();
    }

    private void StopPulse()
    {
        LeftBar.BeginAnimation(OpacityProperty, null);
        RightBar.BeginAnimation(OpacityProperty, null);
        LeftBar.Opacity = 1;
        RightBar.Opacity = 1;
    }

    // Unten in der Mitte des Arbeitsbereichs, also über der Taskleiste. Wird vor jedem Zeigen neu gerechnet, falls sich der Bildschirm geändert hat.
    private void Place()
    {
        Rect area = SystemParameters.WorkArea;
        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Bottom - Height - BottomGap;
    }

    private void SetBars(double leftHeight, double rightHeight)
    {
        LeftBar.Height = leftHeight;
        Canvas.SetTop(LeftBar, BarCenter - (leftHeight / 2));
        RightBar.Height = rightHeight;
        Canvas.SetTop(RightBar, BarCenter - (rightHeight / 2));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
