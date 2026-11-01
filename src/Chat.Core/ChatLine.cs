namespace Chat.Core;

public enum ChatLineKind
{
    Server,
    Event,
    Message,
    Raw
}

public sealed record ChatLine(ChatLineKind Kind, string? Author, string Text);

/// <summary>
/// Разбирает строку протокола на служебную, событие или сообщение.
/// </summary>
public static class ChatLineParser
{
    public static ChatLine Parse(string line)
    {
        if (line.StartsWith(ChatProtocol.ServerPrefix, StringComparison.Ordinal))
        {
            return new ChatLine(
                ChatLineKind.Server,
                null,
                line[ChatProtocol.ServerPrefix.Length..]);
        }

        if (line.StartsWith(ChatProtocol.EventPrefix, StringComparison.Ordinal))
        {
            return new ChatLine(
                ChatLineKind.Event,
                null,
                line[ChatProtocol.EventPrefix.Length..]);
        }

        int separator = line.IndexOf(": ", StringComparison.Ordinal);
        if (separator > 0)
        {
            return new ChatLine(
                ChatLineKind.Message,
                line[..separator],
                line[(separator + 2)..]);
        }

        return new ChatLine(ChatLineKind.Raw, null, line);
    }
}
