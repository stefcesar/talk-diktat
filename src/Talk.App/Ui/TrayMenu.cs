using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Talk.App.Ui;

/// <summary>
/// Baut das Tray Menü in der Optik aus DESIGN.md: dunkler Grund, Einträge 40 hoch, Haken in Talk Orange, Schrift Public Sans.
/// Das Tray Symbol und der Schalter --uishots nutzen denselben Bau, damit das Bild genau das echte Menü zeigt.
/// </summary>
/// <remarks>
/// Windows Forms kennt die Schriften aus Ui\Fonts nicht; sie werden einmal aus den Ressourcen des Programms in eine private Sammlung
/// geladen. Gelingt das nicht, schreibt das Menü in Segoe UI weiter. Die runden Ecken setzt Windows 11 selbst (DwmSetWindowAttribute);
/// Windows 10 lehnt das ab, dann bleibt das Menü eckig.
/// </remarks>
internal static class TrayMenu
{
    // Dieselben Werte wie in Ui\Theme.xaml. Nur die Fläche unter der Maus ist im Menü heller (MenuHover in DESIGN.md), damit man
    // bei Bedienung mit Maus und Bildschirmtastatur sicher sieht, welcher Eintrag getroffen wird.
    private static readonly Color Surface = ColorTranslator.FromHtml("#1D2025");
    private static readonly Color MenuHover = ColorTranslator.FromHtml("#33383F");
    private static readonly Color Line = ColorTranslator.FromHtml("#2E3238");
    private static readonly Color Text = ColorTranslator.FromHtml("#ECEBE7");
    private static readonly Color TextMuted = ColorTranslator.FromHtml("#A3A19B");
    private static readonly Color Accent = ColorTranslator.FromHtml("#FF6A3D");

    private const float FontPoints = 10.5f;
    private const int ItemHeight = 40;

    private static readonly Lazy<FontFamily?> TextFamily = new(LoadTextFamily);

    /// <summary>
    /// Legt das Menü an. <paramref name="localOnlyItem"/> und <paramref name="smoothingItem"/> tragen später den Haken.
    /// </summary>
    public static ContextMenuStrip Build(TrayMenuActions actions, out ToolStripMenuItem localOnlyItem, out ToolStripMenuItem smoothingItem)
    {
        ArgumentNullException.ThrowIfNull(actions);

        DarkRenderer renderer = new();
        ContextMenuStrip menu = new()
        {
            Renderer = renderer,
            ShowImageMargin = false,
            ShowCheckMargin = true,
            BackColor = Surface,
            ForeColor = Text,
        };
        menu.Font = TextFamily.Value is { } family ? new Font(family, FontPoints) : new Font("Segoe UI", FontPoints);

        menu.Items.Add(Item(menu, "Letzten Text kopieren", actions.CopyLast));
        menu.Items.Add(Item(menu, "Verlauf", actions.ShowHistory));
        menu.Items.Add(new ToolStripSeparator());
        localOnlyItem = Item(menu, "Nur lokal (nichts geht ins Internet)", actions.ToggleLocalOnly);
        menu.Items.Add(localOnlyItem);
        smoothingItem = Item(menu, "Glätten (Text geht über Groq)", actions.ToggleSmoothing);
        menu.Items.Add(smoothingItem);
        menu.Items.Add(Item(menu, "Einstellungen", actions.ShowSettings));
        menu.Items.Add(Item(menu, "Diagnose", actions.ShowDiagnostics));
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem quit = Item(menu, "Talk beenden", actions.Quit);
        quit.ForeColor = TextMuted;
        menu.Items.Add(quit);

        SetItemPadding(menu);
        // Bei jedem Öffnen neu, weil das Menü auf einem Bildschirm mit anderer Skalierung aufgehen kann. Grenze: Beim Öffnen kennt
        // Windows Forms noch die Skalierung des letzten Bildschirms; wechselt sie, stimmt die Höhe erst beim nächsten Öffnen.
        menu.Opening += (_, _) => SetItemPadding(menu);
        menu.HandleCreated += (_, _) => renderer.DwmDrawsBorder = RoundCorners(menu);
        return menu;
    }

    private static ToolStripMenuItem Item(ContextMenuStrip menu, string text, Action action)
    {
        ToolStripMenuItem item = new(text) { ForeColor = Text };
        item.Click += (_, _) => action();
        return item;
    }

    // Höhe 40 in logischen Punkten für die aktuelle Skalierung: Windows Forms misst den Text, der Rest kommt als Innenabstand oben
    // und unten dazu. Rechts bleibt Platz, damit der längste Eintrag nie abgeschnitten wird.
    private static void SetItemPadding(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is not ToolStripMenuItem) continue;
            int textHeight = TextRenderer.MeasureText(item.Text, item.Font).Height;
            int vertical = Math.Max(0, (menu.LogicalToDeviceUnits(ItemHeight) - textHeight) / 2);
            item.Padding = new Padding(0, vertical, menu.LogicalToDeviceUnits(16), vertical);
        }
    }

    // Runde Ecken und Randfarbe von Windows 11. True, wenn Windows den Rand selbst zeichnet; dann zeichnet Talk keinen zweiten.
    private static bool RoundCorners(ContextMenuStrip menu)
    {
        try
        {
            int round = 2;
            int border = Line.R | (Line.G << 8) | (Line.B << 16);
            return DwmSetWindowAttribute(menu.Handle, 33, ref round, sizeof(int)) == 0
                && DwmSetWindowAttribute(menu.Handle, 34, ref border, sizeof(int)) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Lädt Public Sans aus den Ressourcen des Programms. Der Speicher bleibt für die ganze Laufzeit stehen, weil GDI+ darauf zeigt.
    private static FontFamily? LoadTextFamily()
    {
        try
        {
            using System.IO.Stream? stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Ui/Fonts/PublicSans-Regular.ttf"))?.Stream;
            if (stream is null) return null;

            using System.IO.MemoryStream copy = new();
            stream.CopyTo(copy);
            byte[] bytes = copy.ToArray();
            IntPtr memory = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, memory, bytes.Length);

            // GDI+ (Zeichnen) und GDI (Messen der Einträge durch Windows Forms) brauchen die Schrift beide, sonst passt die Breite nicht.
            PrivateFontCollection collection = new();
            collection.AddMemoryFont(memory, bytes.Length);
            uint installed = 0;
            _ = AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, ref installed);
            return collection.Families.Length > 0 ? collection.Families[0] : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint length, IntPtr reserved, ref uint installed);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Zeichnet Grund, Auswahl, Trennlinie, Haken und Text selbst; Windows Forms liefert nur die Anordnung.</summary>
    private sealed class DarkRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using SolidBrush brush = new(Surface);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        /// <summary>True, wenn Windows 11 den runden Rand zeichnet; dann entfällt der eigene eckige.</summary>
        public bool DwmDrawsBorder { get; set; }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (DwmDrawsBorder) return;
            using Pen pen = new(Line);
            Rectangle r = e.AffectedBounds;
            e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Kein abgesetzter Rand links: der Grund ist überall gleich.
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;

            int inset = e.ToolStrip?.LogicalToDeviceUnits(6) ?? 6;
            Rectangle r = new(inset, 2, e.Item.Width - (2 * inset), e.Item.Height - 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = Rounded(r, e.ToolStrip?.LogicalToDeviceUnits(6) ?? 6);
            using SolidBrush brush = new(MenuHover);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int inset = e.ToolStrip?.LogicalToDeviceUnits(12) ?? 12;
            int y = e.Item.Height / 2;
            using Pen pen = new(Line);
            e.Graphics.DrawLine(pen, inset, y, e.Item.Width - inset, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Ein schlichter Haken in Talk Orange, ohne Kasten darum.
            Rectangle r = e.ImageRectangle;
            float s = Math.Min(r.Width, r.Height);
            float x = r.X + ((r.Width - s) / 2f);
            float y = r.Y + ((r.Height - s) / 2f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen pen = new(Accent, Math.Max(2f, s / 7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            e.Graphics.DrawLines(pen,
            [
                new PointF(x + (s * 0.18f), y + (s * 0.52f)),
                new PointF(x + (s * 0.42f), y + (s * 0.76f)),
                new PointF(x + (s * 0.84f), y + (s * 0.26f)),
            ]);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Mit derselben Technik (GDI), mit der Windows Forms die Einträge misst, damit Breite und Text zusammenpassen.
            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont ?? e.Item.Font,
                e.TextRectangle,
                e.Item.Enabled ? e.Item.ForeColor : TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
