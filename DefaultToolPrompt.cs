using System.Drawing.Drawing2D;

namespace Aye;

/// <summary>
/// One-time welcome dialog asking whether Aye should take over Print Screen and
/// launch at sign-in. Styled after the Zen dark design language.
/// </summary>
internal sealed class DefaultToolPrompt : Form
{
    private DefaultToolPrompt()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.PanelDeep;
        ForeColor = Theme.Text;
        Font = Ui.Regular();
        ClientSize = new Size(440, 232);
        ShowInTaskbar = true;
        Text = "Aye";

        var title = new Label
        {
            Text = "Make Aye your screenshot tool?",
            Font = Ui.Semibold(13f),
            ForeColor = Theme.Text,
            AutoSize = false,
            Bounds = new Rectangle(28, 28, 384, 28),
        };

        var body = new Label
        {
            Text = "Aye can take over the Print Screen key and start automatically when "
                 + "you sign in to Windows. You can change this any time from the tray "
                 + "menu.",
            ForeColor = Theme.TextMuted,
            AutoSize = false,
            Bounds = new Rectangle(28, 62, 384, 76),
        };

        var yes = new FlatButton
        {
            Text = "Yes, use Aye",
            Accent = true,
            Bounds = new Rectangle(28, 168, 190, 40),
            DialogResult = DialogResult.Yes,
        };

        var no = new FlatButton
        {
            Text = "Not now",
            Bounds = new Rectangle(230, 168, 182, 40),
            DialogResult = DialogResult.No,
        };

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(yes);
        Controls.Add(no);
        AcceptButton = yes;
        CancelButton = no;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    /// <summary>Shows the prompt and returns true when the user opts in.</summary>
    public static bool Ask()
    {
        using var dialog = new DefaultToolPrompt();
        return dialog.ShowDialog() == DialogResult.Yes;
    }
}
