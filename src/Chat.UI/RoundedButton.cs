using System.Drawing.Drawing2D;

namespace Chat.UI;

public enum ButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger
}

/// <summary>
/// Плоская кнопка со скруглёнными углами и мягкими состояниями наведения.
/// </summary>
public class RoundedButton : Control
{
    private ButtonVariant _variant = ButtonVariant.Primary;
    private int _cornerRadius = 10;
    private bool _hover;
    private bool _pressed;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = Theme.BodyMedium;
        ForeColor = Color.White;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(160, 42);
    }

    public ButtonVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            Invalidate();
        }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
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

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(bounds, CornerRadius);

        (Color fill, Color text) = ResolveColors();

        if (fill.A > 0)
        {
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(0, 0, Width, Height),
            text,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues && fill.A > 0)
        {
            using var focusPen = new Pen(Color.FromArgb(70, Theme.Accent), 1f);
            e.Graphics.DrawPath(focusPen, path);
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
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = false;
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    private (Color Fill, Color Text) ResolveColors()
    {
        if (!Enabled)
        {
            return Variant == ButtonVariant.Ghost
                ? (Color.Transparent, Theme.TextMuted)
                : (Theme.SurfaceMuted, Theme.TextMuted);
        }

        return Variant switch
        {
            ButtonVariant.Secondary => (
                _pressed ? Theme.SurfacePressed : _hover ? Theme.SurfaceHover : Theme.SurfaceMuted,
                Theme.TextPrimary),

            ButtonVariant.Ghost => (
                _pressed || _hover ? Theme.AccentSoft : Color.Transparent,
                Theme.Accent),

            ButtonVariant.Danger => (
                _pressed ? Theme.DangerPressed : _hover ? Theme.DangerHover : Theme.DangerSoft,
                Theme.Danger),

            _ => (
                _pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent,
                Color.White)
        };
    }
}
