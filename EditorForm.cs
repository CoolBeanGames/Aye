using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Aye;

internal sealed class EditorForm : Form
{
    private const int RailWidth = 244;
    private const int RailControlWidth = RailWidth - 44;

    private static readonly int[] StrokeSizes = { 2, 4, 8, 16, 24 };

    private readonly string _sourcePath;
    private readonly EditCanvas _canvas;
    private readonly FlowLayoutPanel _tools = new();
    private readonly List<FlatButton> _toolButtons = new();
    private readonly List<FlatButton> _strokeButtons = new();
    private FlatButton _colorButton = null!;
    private readonly FlatButton _aspectButton;
    private readonly FlatButton _scaleButton;
    private TextBox? _textEditor;
    private TextItem? _editingText;
    private Color _activeColor = Theme.Accent;
    private float? _cropAspect;

    public string? SavedPath { get; private set; }

    public EditorForm(string sourcePath)
    {
        _sourcePath = sourcePath;
        Text = $"Aye — {Path.GetFileName(sourcePath)}";
        BackColor = Theme.Workspace;
        ForeColor = Theme.Text;
        Font = Ui.Regular(10f);
        MinimumSize = new Size(960, 640);
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        using var loaded = Image.FromFile(sourcePath);
        _canvas = new EditCanvas(new Bitmap(loaded)) { Dock = DockStyle.Fill };
        _canvas.TextEditRequested += BeginTextEdit;

        // Center stage: the canvas sits on the near-black workspace with a calm gutter.
        var stage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Workspace,
            Padding = new Padding(24)
        };
        stage.Controls.Add(_canvas);

        // Left rail: stacked, sectioned tool groups in the Zen column style.
        _tools.FlowDirection = FlowDirection.TopDown;
        _tools.WrapContents = false;
        _tools.AutoScroll = true;
        _tools.Dock = DockStyle.Fill;
        _tools.BackColor = Theme.PanelDeep;
        _tools.Padding = new Padding(18, 16, 14, 16);

        AddSection("Tools", first: true);
        AddToolButton("Pen", EditTool.Pen);
        AddToolButton("Box", EditTool.Rectangle);
        AddToolButton("Circle", EditTool.Ellipse);
        AddToolButton("Text", EditTool.Text);
        AddToolButton("Crop", EditTool.Crop);
        AddToolButton("Focus", EditTool.Focus);

        AddSection("Colour");
        _colorButton = MakeRailButton("Choose colour…");
        _colorButton.BorderColor = _activeColor;
        _colorButton.Click += (_, _) => ChooseColor();
        _tools.Controls.Add(_colorButton);

        AddSection("Stroke width");
        var strokeRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            Width = RailControlWidth
        };
        foreach (var size in StrokeSizes)
        {
            var button = new FlatButton
            {
                Text = size.ToString(),
                Size = new Size(34, 30),
                Margin = new Padding(0, 0, 6, 0),
                Tag = size
            };
            button.Click += (_, _) => SetStroke(size);
            _strokeButtons.Add(button);
            strokeRow.Controls.Add(button);
        }
        _tools.Controls.Add(strokeRow);

        AddSection("Crop ratio");
        _aspectButton = MakeRailButton("Free crop  ▾");
        _aspectButton.Click += (_, _) => ShowMenu(_aspectButton, new (string, Action)[]
        {
            ("Free crop", () => ApplyAspect(null, "Free crop")),
            ("Square 1:1", () => ApplyAspect(1f, "Square 1:1")),
            ("Standard 4:3", () => ApplyAspect(4f / 3f, "Standard 4:3")),
            ("Widescreen 16:9", () => ApplyAspect(16f / 9f, "Widescreen 16:9")),
        });
        _tools.Controls.Add(_aspectButton);

        AddSection("Scale image");
        _scaleButton = MakeRailButton("Resize…  ▾");
        _scaleButton.Click += (_, _) => ShowMenu(_scaleButton, new (string, Action)[]
        {
            ("75% of current size", () => _canvas.ScaleImage(.75f)),
            ("50% of current size", () => _canvas.ScaleImage(.5f)),
            ("25% of current size", () => _canvas.ScaleImage(.25f)),
        });
        _tools.Controls.Add(_scaleButton);

        var rail = new Panel { Dock = DockStyle.Left, Width = RailWidth, BackColor = Theme.PanelDeep };
        var railDivider = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Theme.Border };
        rail.Controls.Add(_tools);
        rail.Controls.Add(railDivider);

        // Footer: primary actions stay visible along the bottom, accent action leading.
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 66,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = Theme.PanelDeep,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var footerDivider = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border };
        var overwrite = new FlatButton
        {
            Text = "Overwrite",
            Accent = true,
            Size = new Size(120, 38),
            Margin = new Padding(8, 0, 0, 0)
        };
        overwrite.Click += (_, _) => Save(overwrite: true);
        var saveCopy = new FlatButton { Text = "Save a copy", Size = new Size(120, 38), Margin = new Padding(8, 0, 0, 0) };
        saveCopy.Click += (_, _) => Save(overwrite: false);
        var cancel = new FlatButton { Text = "Cancel", Size = new Size(96, 38), Margin = new Padding(0) };
        cancel.Click += (_, _) => Close();
        footer.Controls.Add(overwrite);
        footer.Controls.Add(saveCopy);
        footer.Controls.Add(cancel);

        Controls.Add(stage);
        Controls.Add(footerDivider);
        Controls.Add(footer);
        Controls.Add(rail);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        _canvas.ActiveColor = _activeColor;
        SetStroke(4);
        SetTool(EditTool.Pen);
    }

    private void AddSection(string text, bool first = false) => _tools.Controls.Add(new Label
    {
        Text = text.ToUpperInvariant(),
        AutoSize = true,
        ForeColor = Theme.TextMuted,
        Font = Ui.Semibold(8f),
        Margin = new Padding(2, first ? 2 : 18, 0, 8)
    });

    private void AddToolButton(string label, EditTool tool)
    {
        var button = MakeRailButton(label);
        button.Tag = tool;
        button.Click += (_, _) => SetTool(tool);
        _toolButtons.Add(button);
        _tools.Controls.Add(button);
    }

    private FlatButton MakeRailButton(string text) => new()
    {
        Text = text,
        Size = new Size(RailControlWidth, 34),
        Margin = new Padding(0, 0, 0, 6)
    };

    private void SetTool(EditTool tool)
    {
        CommitTextEdit();
        _canvas.Tool = tool;
        _canvas.CropAspect = tool == EditTool.Crop ? _cropAspect : null;
        foreach (var button in _toolButtons)
            button.Active = button.Tag is EditTool value && value == tool;
    }

    private void SetStroke(int size)
    {
        _canvas.StrokeWidth = size;
        foreach (var button in _strokeButtons)
            button.Active = button.Tag is int value && value == size;
    }

    private void ApplyAspect(float? aspect, string label)
    {
        _cropAspect = aspect;
        _aspectButton.Text = label + "  ▾";
        if (_canvas.Tool == EditTool.Crop)
            _canvas.CropAspect = aspect;
    }

    private static void ShowMenu(Control anchor, (string Label, Action OnPick)[] options)
    {
        var menu = DarkMenu.Create();
        foreach (var (label, onPick) in options)
            menu.Items.Add(DarkMenu.Item(label, (_, _) => onPick()));
        menu.Show(anchor, new Point(0, anchor.Height + 2));
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { Color = _activeColor, FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _activeColor = dialog.Color;
        _canvas.ActiveColor = dialog.Color;
        _colorButton.BorderColor = dialog.Color;
        _colorButton.Invalidate();
    }

    private void BeginTextEdit(TextItem item, Point screenLocation)
    {
        CommitTextEdit();
        _editingText = item;
        _textEditor = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            Text = item.Text,
            Font = item.Font,
            ForeColor = item.Color,
            BackColor = Theme.Panel,
            BorderStyle = BorderStyle.FixedSingle,
            Location = PointToClient(screenLocation),
            Size = new Size(Math.Max(260, item.Bounds.Width + 30), Math.Max(44, item.Bounds.Height + 16))
        };
        _textEditor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                CommitTextEdit();
            }
        };
        _textEditor.LostFocus += (_, _) => CommitTextEdit();
        Controls.Add(_textEditor);
        _textEditor.BringToFront();
        _textEditor.Focus();
        _textEditor.SelectAll();
    }

    private void CommitTextEdit()
    {
        if (_textEditor is null || _editingText is null)
            return;
        _editingText.Text = _textEditor.Text.TrimEnd('\r', '\n');
        var editor = _textEditor;
        _textEditor = null;
        _editingText = null;
        Controls.Remove(editor);
        editor.Dispose();
        _canvas.RemoveEmptyText();
        _canvas.Invalidate();
    }

    private void Save(bool overwrite)
    {
        CommitTextEdit();
        using var result = _canvas.RenderResult();
        var path = overwrite ? _sourcePath : GetCopyPath(_sourcePath);
        if (overwrite)
        {
            var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Guid.NewGuid():N}.png");
            result.Save(temporary, ImageFormat.Png);
            File.Move(temporary, path, true);
        }
        else
        {
            result.Save(path, ImageFormat.Png);
        }
        ClipboardImage.Copy(result);
        SavedPath = path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string GetCopyPath(string source)
    {
        var directory = Path.GetDirectoryName(source)!;
        var stem = Path.GetFileNameWithoutExtension(source) + "_edited";
        var path = Path.Combine(directory, stem + ".png");
        for (var index = 2; File.Exists(path); index++)
            path = Path.Combine(directory, $"{stem}_{index}.png");
        return path;
    }
}

internal enum EditTool { Pen, Rectangle, Ellipse, Text, Crop, Focus }

internal sealed class TextItem
{
    public string Text { get; set; } = string.Empty;
    public Point Location { get; set; }
    public Color Color { get; init; }
    public Font Font { get; set; } = new("Segoe UI", 18);
    public Size Bounds { get; set; } = new(180, 34);
}

internal sealed class EditCanvas : ScrollableControl
{
    private Bitmap _image;
    private readonly List<TextItem> _textItems = [];
    private Point _start;
    private Point _current;
    private Point _last;
    private bool _drawing;
    private const int ImageMargin = 24;

    public event Action<TextItem, Point>? TextEditRequested;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EditTool Tool { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ActiveColor { get; set; } = Theme.Accent;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float StrokeWidth { get; set; } = 4;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float? CropAspect { get; set; }

    public EditCanvas(Bitmap image)
    {
        _image = image;
        BackColor = Theme.Workspace;
        DoubleBuffered = true;
        AutoScroll = true;
        UpdateScrollSize();
        Cursor = Cursors.Cross;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var point = ToImage(e.Location);
        if (e.Button != MouseButtons.Left || !ImageBounds.Contains(point))
            return;

        if (Tool == EditTool.Text)
        {
            var existing = _textItems.LastOrDefault(item => GetTextRectangle(item).Contains(point));
            var item = existing ?? new TextItem { Location = point, Color = ActiveColor };
            if (existing is null)
                _textItems.Add(item);
            TextEditRequested?.Invoke(item, PointToScreen(ToClient(item.Location)));
            return;
        }

        _start = _current = _last = point;
        _drawing = true;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_drawing)
            return;
        _current = Clamp(ToImage(e.Location));
        if (Tool == EditTool.Pen)
        {
            using var graphics = Graphics.FromImage(_image);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(ActiveColor, StrokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(pen, _last, _current);
            _last = _current;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_drawing || e.Button != MouseButtons.Left)
            return;
        _current = Clamp(ToImage(e.Location));
        _drawing = false;
        Capture = false;
        var selection = GetSelection(Tool == EditTool.Crop ? CropAspect : null);
        if (selection.Width < 2 || selection.Height < 2)
        {
            Invalidate();
            return;
        }

        if (Tool is EditTool.Rectangle or EditTool.Ellipse)
        {
            using var graphics = Graphics.FromImage(_image);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(ActiveColor, StrokeWidth);
            if (Tool == EditTool.Rectangle) graphics.DrawRectangle(pen, selection);
            else graphics.DrawEllipse(pen, selection);
        }
        else if (Tool == EditTool.Focus)
        {
            using var graphics = Graphics.FromImage(_image);
            using var shade = new SolidBrush(Color.FromArgb(150, Theme.Workspace));
            foreach (var outside in OutsideRectangles(selection))
                graphics.FillRectangle(shade, outside);
        }
        else if (Tool == EditTool.Crop)
        {
            CropTo(selection);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TranslateTransform(AutoScrollPosition.X + ImageMargin, AutoScrollPosition.Y + ImageMargin);
        e.Graphics.DrawImageUnscaled(_image, Point.Empty);
        foreach (var item in _textItems)
        {
            using var brush = new SolidBrush(item.Color);
            e.Graphics.DrawString(item.Text, item.Font, brush, item.Location);
            item.Bounds = Size.Ceiling(e.Graphics.MeasureString(string.IsNullOrEmpty(item.Text) ? "Text" : item.Text, item.Font));
        }

        if (_drawing && Tool is EditTool.Rectangle or EditTool.Ellipse or EditTool.Crop or EditTool.Focus)
        {
            var selection = GetSelection(Tool == EditTool.Crop ? CropAspect : null);
            using var pen = new Pen(Theme.Accent, 2) { DashStyle = DashStyle.Dash };
            if (Tool == EditTool.Ellipse) e.Graphics.DrawEllipse(pen, selection);
            else e.Graphics.DrawRectangle(pen, selection);
            if (Tool == EditTool.Focus)
            {
                using var shade = new SolidBrush(Color.FromArgb(120, Theme.Workspace));
                foreach (var outside in OutsideRectangles(selection)) e.Graphics.FillRectangle(shade, outside);
            }
        }
    }

    public Bitmap RenderResult()
    {
        var result = new Bitmap(_image);
        using var graphics = Graphics.FromImage(result);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var item in _textItems)
        {
            using var brush = new SolidBrush(item.Color);
            graphics.DrawString(item.Text, item.Font, brush, item.Location);
        }
        return result;
    }

    public void RemoveEmptyText() => _textItems.RemoveAll(item => string.IsNullOrWhiteSpace(item.Text));

    public void ScaleImage(float factor)
    {
        var width = Math.Max(1, (int)Math.Round(_image.Width * factor));
        var height = Math.Max(1, (int)Math.Round(_image.Height * factor));
        var resized = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(_image, 0, 0, width, height);
        }
        _image.Dispose();
        _image = resized;
        foreach (var item in _textItems)
        {
            item.Location = new Point((int)(item.Location.X * factor), (int)(item.Location.Y * factor));
            item.Bounds = new Size((int)(item.Bounds.Width * factor), (int)(item.Bounds.Height * factor));
            var oldFont = item.Font;
            item.Font = new Font(oldFont.FontFamily, Math.Max(6, oldFont.Size * factor), oldFont.Style);
            oldFont.Dispose();
        }
        UpdateScrollSize();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image.Dispose();
            foreach (var item in _textItems) item.Font.Dispose();
        }
        base.Dispose(disposing);
    }

    private void CropTo(Rectangle selection)
    {
        var cropped = _image.Clone(selection, PixelFormat.Format32bppArgb);
        _image.Dispose();
        _image = cropped;
        foreach (var item in _textItems)
        {
            item.Location = new Point(item.Location.X - selection.X, item.Location.Y - selection.Y);
        }
        _textItems.RemoveAll(item => !ImageBounds.IntersectsWith(GetTextRectangle(item)));
        UpdateScrollSize();
    }

    private Rectangle GetSelection(float? aspect)
    {
        var dx = _current.X - _start.X;
        var dy = _current.Y - _start.Y;
        if (aspect is not null && dx != 0 && dy != 0)
        {
            var width = Math.Abs(dx);
            var height = Math.Abs(dy);
            if (width / (float)height > aspect) width = (int)(height * aspect.Value);
            else height = (int)(width / aspect.Value);
            dx = Math.Sign(dx) * width;
            dy = Math.Sign(dy) * height;
        }
        return Rectangle.Intersect(ImageBounds, Rectangle.FromLTRB(
            Math.Min(_start.X, _start.X + dx), Math.Min(_start.Y, _start.Y + dy),
            Math.Max(_start.X, _start.X + dx), Math.Max(_start.Y, _start.Y + dy)));
    }

    private IEnumerable<Rectangle> OutsideRectangles(Rectangle selection)
    {
        yield return new Rectangle(0, 0, _image.Width, selection.Top);
        yield return new Rectangle(0, selection.Bottom, _image.Width, _image.Height - selection.Bottom);
        yield return new Rectangle(0, selection.Top, selection.Left, selection.Height);
        yield return new Rectangle(selection.Right, selection.Top, _image.Width - selection.Right, selection.Height);
    }

    private Rectangle ImageBounds => new(0, 0, _image.Width, _image.Height);
    private Rectangle GetTextRectangle(TextItem item) => new(item.Location, item.Bounds);
    private Point ToImage(Point point) => new(point.X - AutoScrollPosition.X - ImageMargin, point.Y - AutoScrollPosition.Y - ImageMargin);
    private Point ToClient(Point point) => new(point.X + AutoScrollPosition.X + ImageMargin, point.Y + AutoScrollPosition.Y + ImageMargin);
    private Point Clamp(Point point) => new(Math.Clamp(point.X, 0, _image.Width - 1), Math.Clamp(point.Y, 0, _image.Height - 1));
    private void UpdateScrollSize() => AutoScrollMinSize = new Size(_image.Width + ImageMargin * 2, _image.Height + ImageMargin * 2);
}
