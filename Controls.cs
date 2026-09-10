using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Aye;

internal static class Ui
{
    public static readonly string FontFamily = "Segoe UI";

    public static Font Regular(float size = 9.75f) => new(FontFamily, size);
    public static Font Semibold(float size = 9.75f) => new(FontFamily, size, FontStyle.Bold);

    public static GraphicsPath RoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
}

/// <summary>Flat, rounded button matching the Zen design language — no 3D chrome.</summary>
internal sealed class FlatButton : Button
{
    private bool _hover;
    private bool _pressed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 8;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; set; } = Theme.Panel;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Theme.Border;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Accent { get; set; }

    private bool _active;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set { if (_active == value) return; _active = value; Invalidate(); }
    }

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        ForeColor = Theme.Text;
        Font = Ui.Regular();
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 0, 6);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.PanelDeep);

        var baseFill = Accent ? Theme.Accent : Fill;
        var fill = _pressed ? Ui.Mix(baseFill, Color.Black, 0.18f)
            : _hover ? (Accent ? Ui.Mix(baseFill, Color.White, 0.12f) : Theme.PanelRaised)
            : Active ? Theme.AccentDim
            : baseFill;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Ui.RoundedPath(rect, Radius);
        using (var b = new SolidBrush(fill))
            g.FillPath(b, path);

        var border = Accent ? Theme.Accent : Active ? Theme.Accent : _hover ? Ui.Mix(BorderColor, Color.White, 0.3f) : BorderColor;
        using (var p = new Pen(border, Active ? 1.4f : 1f))
            g.DrawPath(p, path);

        var textColor = Accent ? Color.White : Enabled ? ForeColor : Theme.TextMuted;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Dark, flat context menu styled after Zen's right-click menu.</summary>
internal static class DarkMenu
{
    public static ContextMenuStrip Create()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Font = Ui.Regular(),
            Renderer = new DarkRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = false,
        };
        menu.ItemAdded += (_, e) => { if (e.Item is not null) StyleItem(e.Item); };
        return menu;
    }

    public static ToolStripMenuItem Item(string text, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += onClick;
        return item;
    }

    private static void StyleItem(ToolStripItem item)
    {
        if (item is ToolStripSeparator)
            return;
        item.Padding = new Padding(10, 6, 18, 6);
        item.Font = Ui.Regular();
        item.ForeColor = Theme.Text;
        if (item is ToolStripMenuItem menuItem)
            foreach (ToolStripItem? child in menuItem.DropDownItems)
                if (child is not null)
                    StyleItem(child);
    }

    private sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) => RoundedEdges = true;

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var hot = e.Item.Selected || (e.Item is ToolStripMenuItem { DropDown.Visible: true });
            if (!hot)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using var path = Ui.RoundedPath(r, 6);
            using var brush = new SolidBrush(Theme.PanelRaised);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextMuted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var mid = e.Item.Bounds.Height / 2;
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 12, mid, e.Item.Bounds.Width - 12, mid);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Border);
            var r = new Rectangle(Point.Empty, e.ToolStrip.Size);
            r.Width -= 1;
            r.Height -= 1;
            e.Graphics.DrawRectangle(pen, r);
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color ImageMarginGradientBegin => Theme.Panel;
        public override Color ImageMarginGradientMiddle => Theme.Panel;
        public override Color ImageMarginGradientEnd => Theme.Panel;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.PanelRaised;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}

/// <summary>Best-effort image copy that survives process exit and offers several formats.</summary>
internal static class ClipboardImage
{
    public static bool Copy(Image image)
    {
        var bitmap = new Bitmap(image);
        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, true, bitmap);

        using (var stream = new MemoryStream())
        {
            bitmap.Save(stream, ImageFormat.Png);
            data.SetData("PNG", false, new MemoryStream(stream.ToArray()));
        }

        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(80);
            }
        }
        return false;
    }
}
