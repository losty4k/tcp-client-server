using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Chat.UI;

/// <summary>
/// Окно без стандартной рамки: мягкий заголовок, логотип и свои кнопки.
/// </summary>
public class AppForm : Form
{
    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    private readonly Panel _titleBar;
    private readonly Panel _content;

    public AppForm(string title, string subtitle, Size clientSize)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = clientSize;
        BackColor = Theme.Background;
        Font = Theme.Body;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background
        };

        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Width = ClientSize.Width,
            Height = 56,
            BackColor = Theme.Background
        };
        _titleBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
        };

        var logo = new LogoMark
        {
            Location = new Point(20, 13),
            Size = new Size(30, 30)
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = Theme.Title,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(62, 9)
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            Font = Theme.Subtitle,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(63, 30)
        };

        var minimize = new WindowButton(WindowButtonKind.Minimize)
        {
            Location = new Point(ClientSize.Width - 86, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;

        var close = new WindowButton(WindowButtonKind.Close)
        {
            Location = new Point(ClientSize.Width - 48, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        close.Click += (_, _) => Close();

        _titleBar.Controls.AddRange(new Control[]
        {
            logo, titleLabel, subtitleLabel, minimize, close
        });

        foreach (Control control in new Control[] { _titleBar, logo, titleLabel, subtitleLabel })
            control.MouseDown += DragWindow;

        Controls.Add(_content);
        Controls.Add(_titleBar);
    }

    public Panel Content => _content;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        ReleaseCapture();
        SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}

public enum WindowButtonKind
{
    Minimize,
    Close
}

public sealed class WindowButton : Control
{
    private bool _hover;

    public WindowButton(WindowButtonKind kind)
    {
        Kind = kind;
        Size = new Size(36, 32);
        Cursor = Cursors.Hand;
        TabStop = false;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
    }

    public WindowButtonKind Kind { get; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_hover)
        {
            using var path = Theme.RoundedRect(
                new RectangleF(0, 0, Width, Height), 7);
            using var brush = new SolidBrush(
                Kind == WindowButtonKind.Close ? Theme.DangerSoft : Theme.SurfaceHover);
            e.Graphics.FillPath(brush, path);
        }

        Color color = Kind == WindowButtonKind.Close && _hover
            ? Theme.Danger
            : Theme.TextSecondary;

        using var pen = new Pen(color, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float cx = Width / 2f;
        float cy = Height / 2f;

        if (Kind == WindowButtonKind.Minimize)
        {
            e.Graphics.DrawLine(pen, cx - 5, cy + 3, cx + 5, cy + 3);
        }
        else
        {
            e.Graphics.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
            e.Graphics.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
}

/// <summary>
/// Акцентный знак с тремя точками — минималистичный символ чата.
/// </summary>
public sealed class LogoMark : Control
{
    public LogoMark()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Size = new Size(30, 30);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = Theme.RoundedRect(
            new RectangleF(0, 0, Width, Height), 9);
        using var brush = new SolidBrush(Theme.Accent);
        e.Graphics.FillPath(brush, path);

        using var dot = new SolidBrush(Color.White);
        float y = Height / 2f - 1.6f;
        float[] xs = { Width / 2f - 6.4f, Width / 2f - 1.6f, Width / 2f + 3.2f };

        foreach (float x in xs)
            e.Graphics.FillEllipse(dot, x, y, 3.2f, 3.2f);
    }
}

/// <summary>
/// Маленький цветной индикатор состояния.
/// </summary>
public sealed class StatusDot : Control
{
    private Color _dotColor = Theme.Online;

    public StatusDot()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Size = new Size(10, 10);
    }

    public Color DotColor
    {
        get => _dotColor;
        set
        {
            _dotColor = value;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var halo = new SolidBrush(Color.FromArgb(50, DotColor));
        e.Graphics.FillEllipse(halo, 0, 0, Width - 1, Height - 1);

        using var dot = new SolidBrush(DotColor);
        e.Graphics.FillEllipse(dot, 2, 2, Width - 5, Height - 5);
    }
}

/// <summary>
/// Компактная «таблетка» для счётчиков и меток.
/// </summary>
public sealed class Pill : Control
{
    public Pill()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = Theme.TinyMedium;
        Size = new Size(84, 24);
    }

    public Color FillColor { get; set; } = Theme.AccentSoft;

    public Color TextColor { get; set; } = Theme.Accent;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = Theme.RoundedRect(
            new RectangleF(0, 0, Width, Height), Height / 2f);
        using var brush = new SolidBrush(FillColor);
        e.Graphics.FillPath(brush, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(0, 0, Width, Height),
            TextColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }
}
