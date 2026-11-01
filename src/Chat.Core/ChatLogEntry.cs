namespace Chat.Core;

public enum ChatLogKind
{
    Info,
    Join,
    Leave,
    Rename,
    Message,
    Error
}

/// <summary>
/// Событие журнала сервера.
/// </summary>
public sealed record ChatLogEntry(
    ChatLogKind Kind,
    string? Nickname,
    string Text,
    DateTime Time);
