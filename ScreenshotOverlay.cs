using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Aye;

internal sealed class ScreenshotOverlay : Form
{
    private readonly Bitmap _screen;
    private readonly Bitmap _blurred;
    private Point _start;
    private Point _current;
    private bool _drawing;

    public ScreenshotOverlay()
    {
        var bounds = SystemInformation.VirtualScreen;
        _screen = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(_screen))
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        _blurred = CreateBlurredCopy(_screen);

        AutoScaleMode = AutoScaleMode.None;
        Bounds = bounds;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    protected override bool ShowWithoutActivation => false;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            Close();
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        _start = _current = e.Location;
        _drawing = true;
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_drawing)
            return;
        _current = Clamp(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_drawing || e.Button != MouseButtons.Left)
            return;

        _drawing = false;
        Capture = false;
        _current = Clamp(e.Location);
        var selection = GetSelection();
        if (selection.Width < 2 || selection.Height < 2)
        {
            Close();
            return;
        }

        using var capture = _screen.Clone(selection, PixelFormat.Format32bppArgb);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        Directory.CreateDirectory(directory);
        var path = GetUniquePath(directory);
        capture.Save(path, ImageFormat.Png);
        try
        {
            Clipboard.SetImage(capture);
        }
        catch (ExternalException)
        {
            Thread.Sleep(100);
            Clipboard.SetImage(capture);
        }
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(_blurred, Point.Empty);
        using (var shade = new SolidBrush(Color.FromArgb(145, Theme.Workspace)))
            e.Graphics.FillRectangle(shade, ClientRectangle);

        if (_drawing)
        {
            var selection = GetSelection();
            if (selection.Width > 0 && selection.Height > 0)
                e.Graphics.DrawImage(_screen, selection, selection, GraphicsUnit.Pixel);
            using var border = new Pen(Theme.Accent, 2);
            e.Graphics.DrawRectangle(border, selection);
        }

        using var font = new Font("Segoe UI Variable", 10, FontStyle.Regular);
        const string help = "DRAG TO CAPTURE  •  ESC TO CANCEL";
        var size = e.Graphics.MeasureString(help, font);
        var box = new RectangleF((ClientSize.Width - size.Width) / 2 - 14, 22, size.Width + 28, 34);
        using var background = new SolidBrush(Color.FromArgb(220, Theme.Panel));
        using var text = new SolidBrush(Theme.Text);
        e.Graphics.FillRoundedRectangle(background, box, 10);
        e.Graphics.DrawString(help, font, text, box.X + 14, box.Y + 8);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _screen.Dispose();
            _blurred.Dispose();
        }
        base.Dispose(disposing);
    }

    private Rectangle GetSelection() => Rectangle.FromLTRB(
        Math.Min(_start.X, _current.X), Math.Min(_start.Y, _current.Y),
        Math.Max(_start.X, _current.X), Math.Max(_start.Y, _current.Y));

    private Point Clamp(Point point) => new(
        Math.Clamp(point.X, 0, ClientSize.Width - 1),
        Math.Clamp(point.Y, 0, ClientSize.Height - 1));

    private static Bitmap CreateBlurredCopy(Bitmap source)
    {
        var smallWidth = Math.Max(1, source.Width / 12);
        var smallHeight = Math.Max(1, source.Height / 12);
        using var small = new Bitmap(smallWidth, smallHeight);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(source, 0, 0, small.Width, small.Height);
        }
        var result = new Bitmap(source.Width, source.Height);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(small, 0, 0, result.Width, result.Height);
        }
        return result;
    }

    private static string GetUniquePath(string directory)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(directory, $"Aye_{stamp}.png");
        for (var suffix = 2; File.Exists(path); suffix++)
            path = Path.Combine(directory, $"Aye_{stamp}_{suffix}.png");
        return path;
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
