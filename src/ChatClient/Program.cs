using System.Text;
using System.Threading.Channels;
using Chat.Core;

Console.OutputEncoding = Encoding.UTF8;

string host = args.Length > 0 ? args[0] : "127.0.0.1";
int port = ChatProtocol.DefaultPort;
if (args.Length > 1 && (!int.TryParse(args[1], out port) || port is < 1 or > 65535))
{
    Console.WriteLine("Использование: ChatClient [хост] [порт]");
    return;
}

Console.Write("Ваше имя: ");
string nickname = (Console.ReadLine() ?? "").Trim();
if (nickname.Length == 0)
    nickname = "guest";

var connection = new ChatConnection();
var closed = new TaskCompletionSource<string?>(
    TaskCreationOptions.RunContinuationsAsynchronously);

connection.LineReceived += line => Console.WriteLine(line);
connection.Disconnected += reason => closed.TrySetResult(reason);

try
{
    await connection.ConnectAsync(host, port, nickname);
}
catch (Exception ex)
{
    Console.WriteLine($"Не удалось подключиться к {host}:{port} — {ex.Message}");
    await connection.DisposeAsync();
    return;
}

// Ввод консоли читаем в отдельном потоке, чтобы завершиться,
// даже если пользователь ничего не вводит.
var input = Channel.CreateUnbounded<string?>();
_ = Task.Run(() =>
{
    while (true)
    {
        string? line = Console.ReadLine();
        if (line is null)
        {
            input.Writer.TryComplete();
            return;
        }

        if (!input.Writer.TryWrite(line))
            return;
    }
});

bool quitRequested = false;
bool serverClosed = false;

try
{
    while (true)
    {
        Task<string?> readTask = input.Reader.ReadAsync().AsTask();
        Task finished = await Task.WhenAny(readTask, closed.Task);

        if (finished == closed.Task)
        {
            serverClosed = true;
            break;
        }

        string? line = await readTask;
        if (line is null)
            break;

        line = line.Trim();
        if (line.Length == 0)
            continue;

        await connection.SendAsync(line);

        if (line.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        {
            quitRequested = true;
            break;
        }
    }
}
catch (IOException)
{
    // Соединение закрылось во время отправки.
}
catch (ChannelClosedException)
{
    // Стандартный ввод закрыт.
}
catch (InvalidOperationException)
{
    // Соединение уже закрыто.
}
finally
{
    if (quitRequested)
    {
        // Даём серверу время прислать прощальное сообщение.
        await Task.WhenAny(closed.Task, Task.Delay(1000));
    }

    await connection.DisposeAsync();
}

if (serverClosed)
{
    Console.WriteLine(
        closed.Task.Result ?? "Соединение с сервером закрыто.");
}
