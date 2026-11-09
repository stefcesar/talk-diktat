using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using Talk.App.Diagnostics;
using Talk.Core;

namespace Talk.App.Ui;

/// <summary>Was die Punkte des Tray Menüs auslösen. Jede Aktion läuft auf dem UI Thread.</summary>
/// <param name="CopyLast">Den letzten Text aus dem Verlauf in die Zwischenablage legen.</param>
/// <param name="ShowHistory">Das Verlaufsfenster öffnen.</param>
/// <param name="ShowSettings">Das Einstellungsfenster öffnen.</param>
/// <param name="ShowDiagnostics">Das Diagnosefenster öffnen.</param>
/// <param name="ToggleLocalOnly">"Nur lokal" umschalten.</param>
/// <param name="ToggleSmoothing">Die Glättung umschalten.</param>
/// <param name="Quit">Talk beenden.</param>
internal sealed record TrayMenuActions(
    Action CopyLast,
    Action ShowHistory,
    Action ShowSettings,
    Action ShowDiagnostics,
    Action ToggleLocalOnly,
    Action ToggleSmoothing,
    Action Quit);

/// <summary>
/// Das Tray Symbol von Talk. Zeigt je <see cref="TrayStatus"/> ein eigenes Symbol und den Zustand als Kurzinfo und trägt das Menü.
/// Es hört auf den Ablauf und steuert nichts selbst; die Menüpunkte rufen nur die übergebenen Aktionen.
/// </summary>
/// <remarks>
/// Die Symbole sind als Ressourcen eingebettet (erzeugt von assets\symbol\erzeuge-symbole.ps1, dunkle Version: weißes Zeichen
/// für die dunkle Taskleiste). Alle Aufrufe gehören auf den UI Thread. Protokolliert wird nur ein Wechsel, mit festem Wort.
/// Das Menü öffnet sich mit einem einzelnen Klick, links wie rechts, weil der Nutzer mit einzelnen Klicks arbeitet.
/// </remarks>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Dictionary<TrayStatus, Icon> _icons = [];
    private ContextMenuStrip? _menu;
    private ToolStripMenuItem? _localOnlyItem;
    private ToolStripMenuItem? _smoothingItem;
    private TrayStatus? _shown;
    private bool _disposed;

    public TrayIcon()
    {
        Size size = SystemInformation.SmallIconSize;
        Assembly assembly = typeof(TrayIcon).Assembly;
        foreach (TrayStatus status in Enum.GetValues<TrayStatus>())
        {
            string name = $"Talk.App.Tray.tray-{status.ToWord()}.ico";
            using System.IO.Stream stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Tray Symbol fehlt: {name}");
            _icons[status] = new Icon(stream, size);
        }

        _notifyIcon = new NotifyIcon();
        Show(TrayStatus.Idle);
        _notifyIcon.Visible = true;
    }

    /// <summary>
    /// Zeigt das Symbol für den Zustand. Ohne Wechsel passiert nichts.
    /// </summary>
    public void Show(TrayStatus status)
    {
        if (_disposed || _shown == status) return;

        _shown = status;
        _notifyIcon.Icon = _icons[status];
        _notifyIcon.Text = status switch
        {
            TrayStatus.Recording => "Talk: Aufnahme",
            TrayStatus.Processing => "Talk: Verarbeitung",
            TrayStatus.Error => "Talk: Fehler",
            _ => "Talk: Ruhe",
        };
        AppLog.Write("tray", "state", $"zustand={status.ToWord()}");
    }

    /// <summary>
    /// Hängt das Menü an das Symbol. Jeder Punkt braucht nur einen einzelnen Klick.
    /// </summary>
    public void SetMenu(TrayMenuActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (_disposed) return;

        // Aufbau und Optik stehen in TrayMenu (DESIGN.md), damit --uishots dasselbe Menü zeichnen kann.
        ContextMenuStrip menu = TrayMenu.Build(actions, out ToolStripMenuItem localOnlyItem, out ToolStripMenuItem smoothingItem);
        _localOnlyItem = localOnlyItem;
        _smoothingItem = smoothingItem;

        _menu?.Dispose();
        _menu = menu;
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.MouseClick += OnMouseClick;
    }

    /// <summary>
    /// Setzt den Haken am Menüpunkt "Nur lokal".
    /// </summary>
    public void SetLocalOnly(bool localOnly)
    {
        if (!_disposed && _localOnlyItem is not null) _localOnlyItem.Checked = localOnly;
    }

    /// <summary>
    /// Setzt den Haken am Menüpunkt "Glätten".
    /// </summary>
    public void SetSmoothing(bool enabled)
    {
        if (!_disposed && _smoothingItem is not null) _smoothingItem.Checked = enabled;
    }

    /// <summary>
    /// Zeigt einen kurzen Hinweis am Symbol, zum Beispiel beim ersten Start ohne Fenster.
    /// </summary>
    public void ShowHint(string title, string text)
    {
        if (_disposed) return;

        try
        {
            _notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.None);
        }
        catch
        {
            // Der Hinweis ist Komfort. Sind Benachrichtigungen abgestellt, bleibt er aus.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Erst ausblenden, sonst bleibt das Symbol im Tray stehen, bis die Maus darüber fährt.
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu?.Dispose();
        foreach (Icon icon in _icons.Values) icon.Dispose();
        _icons.Clear();
    }

    // Windows öffnet das Menü von sich aus nur beim Rechtsklick. Für den Linksklick ruft Talk dieselbe Methode von NotifyIcon,
    // die auch der Rechtsklick benutzt; sie ist nicht öffentlich, deshalb über Reflexion. Fehlt sie, bleibt es beim Rechtsklick.
    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (_disposed || e.Button != MouseButtons.Left) return;

        try
        {
            typeof(NotifyIcon)
                .GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(_notifyIcon, null);
        }
        catch
        {
            // Dann öffnet nur der Rechtsklick das Menü.
        }
    }
}
