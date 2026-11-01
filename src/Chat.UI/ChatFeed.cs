using System.Drawing.Drawing2D;

namespace Chat.UI;

public enum ChatMessageKind
{
    Own,
    Other,
    System,
    Event
}

public sealed record ChatMessage(ChatMessageKind Kind, string? Author, string Text);

/// <summary>
/// Прокручиваемая лента сообщений с аккуратными пузырями.
/// </summary>
public sealed class ChatFeed : Panel
{
    private const int SidePadding = 22;
    private const int TopPadding = 18;
    private const int Gap = 10;

    private readonly List<Bubble> _bubbles = new();

    public ChatFeed()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        BackColor = Theme.Background;

        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void AddMessage(ChatMessage message)
    {
        bool atBottom = IsAtBottom();

        var bubble = new Bubble(message);
        _bubbles.Add(bubble);
        Controls.Add(bubble);

        Relayout();

        if (atBottom || message.Kind == ChatMessageKind.Own)
            ScrollToBottom();
    }

    public void AddSystem(string text) =>
        AddMessage(new ChatMessage(ChatMessageKind.System, null, text));

    public void AddEvent(string text) =>
        AddMessage(new ChatMessage(ChatMessageKind.Event, null, text));

    public void ClearMessages()
    {
        foreach (var bubble in _bubbles)
        {
            Controls.Remove(bubble);
            bubble.Dispose();
        }

        _bubbles.Clear();
        AutoScrollMinSize = Size.Empty;
        AutoScrollPosition = Point.Empty;
        Invalidate();
    }

    public void ScrollToBottom()
    {
        if (AutoScrollMinSize.Height > ClientSize.Height)
            AutoScrollPosition = new Point(0, AutoScrollMinSize.Height);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        bool atBottom = IsAtBottom();
        Relayout();

        if (atBottom)
            ScrollToBottom();
    }

    private bool IsAtBottom()
    {
        if (AutoScrollMinSize.Height <= ClientSize.Height)
            return true;

        return -AutoScrollPosition.Y + ClientSize.Height >= AutoScrollMinSize.Height - 16;
    }

    private void Relayout()
    {
        // Ширину полосы прокрутки резервируем всегда, чтобы пузыри не «прыгали».
        int viewport = Math.Max(
            160,
            ClientSize.Width - SidePadding * 2 - SystemInformation.VerticalScrollBarWidth);

        int y = TopPadding;

        foreach (var bubble in _bubbles)
        {
            bubble.Measure(viewport);

            int x = bubble.Kind switch
            {
                ChatMessageKind.Own => SidePadding + viewport - bubble.Width,
                ChatMessageKind.System or ChatMessageKind.Event =>
                    SidePadding + (viewport - bubble.Width) / 2,
                _ => SidePadding
            };

            bubble.SetBounds(x, y, bubble.Width, bubble.Height);
            y += bubble.Height + Gap;
        }

        AutoScrollMinSize = new Size(0, y + TopPadding);
        Invalidate();
    }

    private sealed class Bubble : Control
    {
        private const int PadX = 14;
        private const int PadY = 10;
        private const int AuthorHeight = 18;

        private const TextFormatFlags TextFlags =
            TextFormatFlags.WordBreak |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding;

        private readonly ChatMessage _message;

        public Bubble(ChatMessage message)
        {
            _message = message;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable,
                true);

            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        public ChatMessageKind Kind => _message.Kind;

        public void Measure(int maxWidth)
        {
            if (Kind is ChatMessageKind.System or ChatMessageKind.Event)
            {
                int maxSystemText = Math.Max(60, maxWidth - 40);
                Size textSize = TextRenderer.MeasureText(
                    _message.Text,
                    Theme.BodySmall,
                    new Size(maxSystemText, int.MaxValue),
                    TextFlags);

                int extra = Kind == ChatMessageKind.Event ? 24 : 8;
                Width = Math.Min(maxSystemText, textSize.Width) + extra;
                Height = textSize.Height + (Kind == ChatMessageKind.Event ? 12 : 6);
                return;
            }

            int maxBubble = Math.Max(220, (int)(maxWidth * 0.72));
            int maxChatText = maxBubble - PadX * 2;

            Size size = TextRenderer.MeasureText(
                _message.Text,
                Theme.Body,
                new Size(maxChatText, int.MaxValue),
                TextFlags);

            int authorHeight =
                Kind == ChatMessageKind.Other && !string.IsNullOrEmpty(_message.Author)
                    ? AuthorHeight
                    : 0;

            Width = Math.Min(maxChatText, size.Width) + PadX * 2;
            Height = size.Height + PadY * 2 + authorHeight;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            switch (Kind)
            {
                case ChatMessageKind.System:
                    TextRenderer.DrawText(
                        e.Graphics,
                        _message.Text,
                        Theme.BodySmall,
                        ClientRectangle,
                        Theme.TextMuted,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFlags);
                    break;

                case ChatMessageKind.Event:
                    using (var eventPath = Theme.RoundedRect(
                        new RectangleF(0, 0, Width, Height), Height / 2f))
                    using (var eventBrush = new SolidBrush(Theme.SurfaceMuted))
                    {
                        e.Graphics.FillPath(eventBrush, eventPath);
                    }

                    TextRenderer.DrawText(
                        e.Graphics,
                        _message.Text,
                        Theme.BodySmall,
                        ClientRectangle,
                        Theme.TextSecondary,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFlags);
                    break;

                default:
                    bool own = Kind == ChatMessageKind.Own;

                    using (var path = Theme.RoundedRect(
                        new RectangleF(0, 0, Width, Height),
                        15,
                        bottomRight: !own,
                        bottomLeft: own))
                    using (var brush = new SolidBrush(own ? Theme.Accent : Theme.Surface))
                    {
                        e.Graphics.FillPath(brush, path);

                        if (!own)
                        {
                            using var pen = new Pen(Theme.Border);
                            e.Graphics.DrawPath(pen, path);
                        }
                    }

                    int textTop = PadY;

                    if (!own && !string.IsNullOrEmpty(_message.Author))
                    {
                        TextRenderer.DrawText(
                            e.Graphics,
                            _message.Author,
                            Theme.SmallMedium,
                            new Rectangle(PadX, PadY - 1, Width - PadX * 2, AuthorHeight),
                            Theme.Accent,
                            TextFormatFlags.Left |
                            TextFormatFlags.NoPrefix |
                            TextFormatFlags.EndEllipsis);

                        textTop += AuthorHeight;
                    }

                    TextRenderer.DrawText(
                        e.Graphics,
                        _message.Text,
                        Theme.Body,
                        new Rectangle(PadX, textTop, Width - PadX * 2, Height - textTop - PadY),
                        own ? Color.White : Theme.TextPrimary,
                        TextFormatFlags.Left |
                        TextFormatFlags.Top |
                        TextFlags);
                    break;
            }
        }
    }
}
