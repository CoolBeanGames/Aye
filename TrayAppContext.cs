using System.Drawing.Drawing2D;

namespace Aye;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly PrintScreenHook _printScreenHook;
    private bool _captureActive;
    private string? _lastScreenshotPath;

    public TrayAppContext()
    {
        var menu = DarkMenu.Create();
        menu.Items.Add(DarkMenu.Item("Take screenshot", (_, _) => BeginCapture()));
        menu.Items.Add(DarkMenu.Item("Edit last screenshot", (_, _) => EditLastScreenshot()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(DarkMenu.Item("Exit Aye", (_, _) => ExitThread()));

        _trayIcon = new NotifyIcon
        {
            Icon = CreateEyeIcon(),
            Text = "Aye — screenshot tools",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => BeginCapture();

        StartupRegistration.EnsureConfigured();
        _printScreenHook = new PrintScreenHook(BeginCapture);
        if (!_printScreenHook.IsInstalled)
            _trayIcon.ShowBalloonTip(3500, "Aye", "Print Screen could not be registered. Restart Aye to try again.", ToolTipIcon.Warning);
    }

    private void BeginCapture()
    {
        if (_captureActive)
            return;

        _captureActive = true;
        _trayIcon.Visible = false;
        var overlay = new ScreenshotOverlay();
        overlay.ScreenshotSaved += path => _lastScreenshotPath = path;
        overlay.FormClosed += (_, _) =>
        {
            _captureActive = false;
            _trayIcon.Visible = true;
        };
        overlay.Show();
        overlay.Activate();
    }

    private void EditLastScreenshot()
    {
        var path = FindLastScreenshot();
        if (path is null)
        {
            _trayIcon.ShowBalloonTip(2500, "Aye", "Take a screenshot first, then edit it here.", ToolTipIcon.Info);
            return;
        }

        using var editor = new EditorForm(path);
        editor.ShowDialog();
        if (editor.SavedPath is not null)
            _lastScreenshotPath = editor.SavedPath;
    }

    private string? FindLastScreenshot()
    {
        if (_lastScreenshotPath is not null && File.Exists(_lastScreenshotPath))
            return _lastScreenshotPath;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _printScreenHook.Dispose();
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
}
