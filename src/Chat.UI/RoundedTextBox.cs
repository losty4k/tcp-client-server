using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Chat.UI;

/// <summary>
/// Текстовое поле в скруглённой рамке с подсказкой и акцентом при фокусе.
/// </summary>
public class RoundedTextBox : UserControl
{
    private const int HorizontalPadding = 13;

    private readonly TextBox _box;
    private int _cornerRadius = 10;
    private bool _focused;

    public RoundedTextBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Surface;
        Height = 42;
        Cursor = Cursors.IBeam;
        Font = Theme.Body;

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextPrimary,
            Font = Theme.Body,
            TabIndex = 0
        };

        _box.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        _box.GotFocus += (_, _) =>
        {
            _focused = true;
            Invalidate();
        };
        _box.LostFocus += (_, _) =>
        {
            _focused = false;
            Invalidate();
        };
        _box.KeyDown += (_, e) => OnKeyDown(e);
        _box.KeyPress += (_, e) => OnKeyPress(e);
        _box.KeyUp += (_, e) => OnKeyUp(e);

        Controls.Add(_box);
    }

    public TextBox InnerTextBox => _box;

    [AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value ?? string.Empty;
    }

    public string PlaceholderText
    {
        get => _box.PlaceholderText;
        set => _box.PlaceholderText = value;
    }

    public int MaxLength
    {
        get => _box.MaxLength;
        set => _box.MaxLength = value;
    }

    public bool ReadOnly
    {
        get => _box.ReadOnly;
        set => _box.ReadOnly = value;
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            UpdateRegion();
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_box is null)
            return;

        int boxHeight = _box.PreferredHeight;
        _box.SetBounds(
            HorizontalPadding,
            Math.Max(0, (Height - boxHeight) / 2),
            Math.Max(10, Width - HorizontalPadding * 2),
            boxHeight);

        UpdateRegion();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _box.Focus();
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(bounds, CornerRadius);

        using (var fill = new SolidBrush(Theme.Surface))
            e.Graphics.FillPath(fill, path);

        using var pen = new Pen(
            _focused ? Theme.Accent : Theme.Border,
            _focused ? 1.4f : 1f);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using var path = Theme.RoundedRect(
            new RectangleF(0, 0, Width, Height), CornerRadius);
        var region = new Region(path);
        var old = Region;
        Region = region;
        old?.Dispose();
    }
}
