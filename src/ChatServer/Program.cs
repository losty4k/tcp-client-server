using System.Text;
using Chat.Core;

Console.OutputEncoding = Encoding.UTF8;

int port = ChatProtocol.DefaultPort;
if (args.Length > 0 && (!int.TryParse(args[0], out port) || port is < 1 or > 65535))
{
    Console.WriteLine("Использование: ChatServer [порт]");
    return;
}

var server = new ChatServer();
server.Log += entry => Console.WriteLine(Format(entry));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    server.Start(port);
    Console.WriteLine("Ctrl+C — остановить сервер.");
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Штатная остановка по Ctrl+C.
}
catch (Exception ex)
{
    Console.WriteLine($"Не удалось запустить сервер: {ex.Message}");
}
finally
{
    await server.StopAsync();
}

static string Format(ChatLogEntry entry) => entry.Kind switch
{
    ChatLogKind.Join => $"+ {entry.Nickname} подключился",
    ChatLogKind.Leave => $"- {entry.Nickname} покинул чат",
    ChatLogKind.Rename => $"~ {entry.Nickname} {entry.Text}",
    ChatLogKind.Message => $"{entry.Nickname}: {entry.Text}",
    ChatLogKind.Error => $"! {entry.Text}",
    _ => $"• {entry.Text}"
};
