namespace Chat.Core;

/// <summary>
/// Общие константы протокола чата.
/// </summary>
public static class ChatProtocol
{
    public const int DefaultPort = 5000;
    public const int MaxNicknameLength = 20;

    public const string ServerPrefix = "[server] ";
    public const string EventPrefix = "* ";
    public const string WelcomePrefix = ServerPrefix + "Добро пожаловать, ";
    public const string UsersPrefix = ServerPrefix + "В чате (";

    /// <summary>
    /// Убирает управляющие символы и ограничивает длину ника.
    /// </summary>
    public static string SanitizeNickname(string raw)
    {
        var chars = raw.Where(c => !char.IsControl(c)).ToArray();
        string cleaned = new string(chars).Trim();

        if (cleaned.Length == 0)
            cleaned = "guest";

        return cleaned.Length <= MaxNicknameLength
            ? cleaned
            : cleaned[..MaxNicknameLength];
    }
}
