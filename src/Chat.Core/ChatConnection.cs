using System.Net.Sockets;
using System.Text;

namespace Chat.Core;

/// <summary>
/// Клиентское TCP-подключение к чату.
/// События приходят в захваченном <see cref="SynchronizationContext"/>.
/// </summary>
public sealed class ChatConnection : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SynchronizationContext? _context;

    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _readTask;
    private bool _disposed;

    public ChatConnection()
    {
        _context = SynchronizationContext.Current;
    }

    public bool IsConnected => !_disposed && _tcp is { Connected: true };

    public event Action<string>? LineReceived;
    public event Action<string?>? Disconnected;

    public async Task ConnectAsync(
        string host,
        int port,
        string nickname,
        CancellationToken token = default)
    {
        if (_tcp is not null)
            throw new InvalidOperationException("Соединение уже установлено.");

        var tcp = new TcpClient { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(host, port, token).ConfigureAwait(true);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }

        _tcp = tcp;
        NetworkStream stream = tcp.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        await _writer.WriteLineAsync(nickname).ConfigureAwait(true);
        _readTask = Task.Run(ReadLoopAsync);
    }

    public async Task SendAsync(string text)
    {
        if (_disposed || _writer is null)
            throw new InvalidOperationException("Нет соединения.");

        await _sendLock.WaitAsync().ConfigureAwait(true);
        try
        {
            await _writer.WriteLineAsync(text).ConfigureAwait(true);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // Сокет уже закрыт.
        }

        try
        {
            _reader?.Dispose();
        }
        catch
        {
            // Сокет уже закрыт.
        }

        try
        {
            _tcp?.Close();
        }
        catch
        {
            // Сокет уже закрыт.
        }

        _tcp?.Dispose();

        if (_readTask is not null)
        {
            try
            {
                await _readTask.ConfigureAwait(false);
            }
            catch
            {
                // Чтение уже завершено.
            }
        }

        _sendLock.Dispose();
    }

    private async Task ReadLoopAsync()
    {
        string? reason = null;

        try
        {
            string? line;
            while ((line = await _reader!.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                string current = line;
                Raise(() => LineReceived?.Invoke(current));
            }

            reason = "Сервер закрыл соединение.";
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            reason = _disposed ? null : "Соединение потеряно.";
        }
        finally
        {
            if (!_disposed)
                Raise(() => Disconnected?.Invoke(reason));
        }
    }

    private void Raise(Action action)
    {
        if (_context is null || _context == SynchronizationContext.Current)
            action();
        else
            _context.Post(_ => action(), null);
    }
}
