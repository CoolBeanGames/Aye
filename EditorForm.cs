using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Aye;

internal sealed class EditorForm : Form
{
    private readonly string _sourcePath;
    private readonly EditCanvas _canvas;
    private readonly FlowLayoutPanel _tools;
    private readonly NumericUpDown _penSize;
    private readonly Button _colorButton;
    private readonly ComboBox _aspect;
    private readonly ComboBox _scale;
    private TextBox? _textEditor;
    private TextItem? _editingText;
    private Color _activeColor = Theme.Accent;

    public string? SavedPath { get; private set; }

    public EditorForm(string sourcePath)
    {
        _sourcePath = sourcePath;
        Text = $"Aye — {Path.GetFileName(sourcePath)}";
        BackColor = Theme.Workspace;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI Variable", 10);
        MinimumSize = new Size(900, 620);
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        using var loaded = Image.FromFile(sourcePath);
        _canvas = new EditCanvas(new Bitmap(loaded)) { Dock = DockStyle.Fill };
        _canvas.TextEditRequested += BeginTextEdit;

        _tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(14, 13, 14, 10),
            BackColor = Color.FromArgb(0x12, 0x15, 0x1C),
            WrapContents = false
        };

        AddToolButton("PEN", EditTool.Pen);
        AddToolButton("BOX", EditTool.Rectangle);
        AddToolButton("CIRCLE", EditTool.Ellipse);
        AddToolButton("TEXT", EditTool.Text);
        AddToolButton("CROP", EditTool.Crop);
        AddToolButton("FOCUS", EditTool.Focus);

        _colorButton = MakeButton("COLOR");
        _colorButton.BackColor = _activeColor;
        _colorButton.ForeColor = Color.White;
        _colorButton.Click += (_, _) => ChooseColor();
        _tools.Controls.Add(_colorButton);

        _penSize = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 48,
            Value = 4,
            Width = 62,
            Height = 36,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(8, 0, 4, 0)
        };
        _penSize.ValueChanged += (_, _) => _canvas.StrokeWidth = (float)_penSize.Value;
        _tools.Controls.Add(_penSize);

        _aspect = MakeCombo(new[] { "FREE CROP", "1:1", "4:3", "16:9" });
        _aspect.SelectedIndexChanged += (_, _) => _canvas.CropAspect = _aspect.SelectedIndex switch
        {
            1 => 1f,
            2 => 4f / 3f,
            3 => 16f / 9f,
            _ => null
        };
        _tools.Controls.Add(_aspect);

        _scale = MakeCombo(new[] { "SCALE 100%", "SCALE 75%", "SCALE 50%", "SCALE 25%" });
        _scale.SelectedIndexChanged += (_, _) =>
        {
            var factor = new[] { 1f, .75f, .5f, .25f }[_scale.SelectedIndex];
            if (factor < 1f)
            {
                _canvas.ScaleImage(factor);
                _scale.SelectedIndex = 0;
            }
        };
        _tools.Controls.Add(_scale);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            Padding = new Padding(14),
            BackColor = Color.FromArgb(0x12, 0x15, 0x1C),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var overwrite = MakeButton("OVERWRITE");
        overwrite.BackColor = Theme.Accent;
        overwrite.Click += (_, _) => Save(overwrite: true);
        var saveCopy = MakeButton("SAVE A COPY");
        saveCopy.Click += (_, _) => Save(overwrite: false);
        var cancel = MakeButton("CANCEL");
        cancel.Click += (_, _) => Close();
        footer.Controls.Add(overwrite);
        footer.Controls.Add(saveCopy);
        footer.Controls.Add(cancel);

        Controls.Add(_canvas);
        Controls.Add(_tools);
        Controls.Add(footer);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        _canvas.ActiveColor = _activeColor;
        _canvas.StrokeWidth = (float)_penSize.Value;
        SetTool(EditTool.Pen);
    }

    private void AddToolButton(string label, EditTool tool)
    {
        var button = MakeButton(label);
        button.Tag = tool;
        button.Click += (_, _) => SetTool(tool);
        _tools.Controls.Add(button);
    }

    private void SetTool(EditTool tool)
    {
        CommitTextEdit();
        _canvas.Tool = tool;
        foreach (Control control in _tools.Controls)
        {
            if (control is Button button && button.Tag is EditTool value)
                button.BackColor = value == tool ? Color.FromArgb(0x3A, 0x35, 0x6B) : Theme.Panel;
        }
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { Color = _activeColor, FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _activeColor = dialog.Color;
        _canvas.ActiveColor = dialog.Color;
        _colorButton.BackColor = dialog.Color;
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
        try { Clipboard.SetImage(result); } catch (ExternalException) { }
        SavedPath = path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MinimumSize = new Size(76, 36),
        FlatStyle = FlatStyle.Flat,
        BackColor = Theme.Panel,
        ForeColor = Theme.Text,
        Margin = new Padding(4, 0, 4, 0),
        Padding = new Padding(10, 0, 10, 0),
        Cursor = Cursors.Hand
    };

    private static ComboBox MakeCombo(string[] items)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat,
            Width = 125,
            Height = 36,
            Margin = new Padding(8, 0, 4, 0)
        };
        combo.Items.AddRange(items);
        combo.SelectedIndex = 0;
        return combo;
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
    public Font Font { get; set; } = new("Segoe UI Variable", 18);
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
