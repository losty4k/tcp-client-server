using System.Drawing.Drawing2D;

namespace Chat.UI;

/// <summary>
/// Спокойная светлая палитра и общие шрифты приложения.
/// </summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(0xF4, 0xF5, 0xF2);
    public static readonly Color Surface = Color.FromArgb(0xFF, 0xFF, 0xFF);
    public static readonly Color SurfaceMuted = Color.FromArgb(0xEC, 0xEE, 0xE9);
    public static readonly Color SurfaceHover = Color.FromArgb(0xE4, 0xE7, 0xE1);
    public static readonly Color SurfacePressed = Color.FromArgb(0xD8, 0xDC, 0xD4);
    public static readonly Color Border = Color.FromArgb(0xDD, 0xE1, 0xDA);
    public static readonly Color BorderStrong = Color.FromArgb(0xC9, 0xCF, 0xC6);

    public static readonly Color TextPrimary = Color.FromArgb(0x23, 0x28, 0x25);
    public static readonly Color TextSecondary = Color.FromArgb(0x6B, 0x73, 0x6C);
    public static readonly Color TextMuted = Color.FromArgb(0x9A, 0xA1, 0x9A);

    public static readonly Color Accent = Color.FromArgb(0x4E, 0x7A, 0x68);
    public static readonly Color AccentHover = Color.FromArgb(0x43, 0x6B, 0x5B);
    public static readonly Color AccentPressed = Color.FromArgb(0x39, 0x5C, 0x4E);
    public static readonly Color AccentSoft = Color.FromArgb(0xE3, 0xED, 0xE7);

    public static readonly Color Online = Color.FromArgb(0x5C, 0x9A, 0x7B);
    public static readonly Color Danger = Color.FromArgb(0xA8, 0x55, 0x55);
    public static readonly Color DangerSoft = Color.FromArgb(0xF6, 0xE7, 0xE7);
    public static readonly Color DangerHover = Color.FromArgb(0xEF, 0xD8, 0xD8);
    public static readonly Color DangerPressed = Color.FromArgb(0xE7, 0xC9, 0xC9);

    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font Log = new("Segoe UI", 9.5f);
    public static readonly Font BodySmall = new("Segoe UI", 9f);
    public static readonly Font BodyMedium = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font SmallMedium = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font Tiny = new("Segoe UI", 8f);
    public static readonly Font TinyMedium = new("Segoe UI", 8f, FontStyle.Bold);
    public static readonly Font Heading = new("Segoe UI", 17f, FontStyle.Bold);
    public static readonly Font Section = new("Segoe UI", 12.5f, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font Subtitle = new("Segoe UI", 8.5f);

    public static GraphicsPath RoundedRect(
        RectangleF bounds,
        float radius,
        bool topLeft = true,
        bool topRight = true,
        bool bottomRight = true,
        bool bottomLeft = true)
    {
        var path = new GraphicsPath();

        float r = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
        if (r <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        float d = r * 2f;

        path.StartFigure();
        path.AddLine(bounds.Left + (topLeft ? r : 0), bounds.Top,
                     bounds.Right - (topRight ? r : 0), bounds.Top);

        if (topRight)
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);

        path.AddLine(bounds.Right, bounds.Top + (topRight ? r : 0),
                     bounds.Right, bounds.Bottom - (bottomRight ? r : 0));

        if (bottomRight)
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);

        path.AddLine(bounds.Right - (bottomRight ? r : 0), bounds.Bottom,
                     bounds.Left + (bottomLeft ? r : 0), bounds.Bottom);

        if (bottomLeft)
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);

        path.AddLine(bounds.Left, bounds.Bottom - (bottomLeft ? r : 0),
                     bounds.Left, bounds.Top + (topLeft ? r : 0));

        if (topLeft)
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);

        path.CloseFigure();
        return path;
    }
}
