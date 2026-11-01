using System.Drawing.Drawing2D;
using Chat.UI;

namespace ChatServer.Gui;

/// <summary>
/// Список участников с зелёной точкой и аккуратными строками.
/// </summary>
public sealed class OnlineList : ListBox
{
    public OnlineList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 34;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Surface;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Body;
        SelectionMode = SelectionMode.None;
        IntegralHeight = false;
        TabStop = false;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var background = new SolidBrush(Theme.Surface))
            e.Graphics.FillRectangle(background, e.Bounds);

        string nickname = Items[e.Index]?.ToString() ?? "";
        int centerY = e.Bounds.Top + e.Bounds.Height / 2;

        using (var dot = new SolidBrush(Theme.Online))
            e.Graphics.FillEllipse(dot, e.Bounds.Left + 4, centerY - 4, 8, 8);

        TextRenderer.DrawText(
            e.Graphics,
            nickname,
            Font,
            new Rectangle(
                e.Bounds.Left + 20,
                e.Bounds.Top,
                Math.Max(10, e.Bounds.Width - 24),
                e.Bounds.Height),
            Theme.TextPrimary,
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }
}
