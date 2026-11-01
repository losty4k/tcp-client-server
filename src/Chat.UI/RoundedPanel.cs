using System.Drawing.Drawing2D;

namespace Chat.UI;

/// <summary>
/// Панель с мягкими скруглёнными углами и тонкой рамкой.
/// </summary>
public class RoundedPanel : Panel
{
    private int _cornerRadius = 16;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Surface;
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

    public Color FillColor { get; set; } = Theme.Surface;

    public Color BorderColor { get; set; } = Theme.Border;

    public int BorderWidth { get; set; } = 1;

    public bool DrawBorder { get; set; } = true;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(bounds, CornerRadius);

        using (var fill = new SolidBrush(FillColor))
            e.Graphics.FillPath(fill, path);

        if (DrawBorder && BorderWidth > 0)
        {
            using var pen = new Pen(BorderColor, BorderWidth);
            e.Graphics.DrawPath(pen, path);
        }
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
