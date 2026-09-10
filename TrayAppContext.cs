using System.Drawing.Drawing2D;

namespace Aye;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private bool _captureActive;

    public TrayAppContext()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Renderer = new DarkMenuRenderer()
        };
        menu.Items.Add("Take screenshot", null, (_, _) => BeginCapture());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit Aye", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = CreateEyeIcon(),
            Text = "Aye — screenshot tools",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => BeginCapture();
    }

    private void BeginCapture()
    {
        if (_captureActive)
            return;

        _captureActive = true;
        _trayIcon.Visible = false;
        var overlay = new ScreenshotOverlay();
        overlay.FormClosed += (_, _) =>
        {
            _captureActive = false;
            _trayIcon.Visible = true;
        };
        overlay.Show();
        overlay.Activate();
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }

    private static Icon CreateEyeIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var eyeBrush = new SolidBrush(Theme.Accent);
        using var pupilBrush = new SolidBrush(Theme.Workspace);
        graphics.FillEllipse(eyeBrush, 2, 8, 28, 16);
        graphics.FillEllipse(pupilBrush, 10, 8, 12, 16);
        graphics.FillEllipse(Brushes.White, 13, 11, 4, 4);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(0x1D, 0x22, 0x2C);
        public override Color MenuItemBorder => Theme.Accent;
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color ImageMarginGradientBegin => Theme.Panel;
        public override Color ImageMarginGradientMiddle => Theme.Panel;
        public override Color ImageMarginGradientEnd => Theme.Panel;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
