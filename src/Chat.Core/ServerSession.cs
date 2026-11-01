using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Chat.Core;

/// <summary>
/// Одно подключение к серверу: чтение строк и потокобезопасная отправка.
/// </summary>
internal sealed class ServerSession : IDisposable
{
    private readonly TcpClient _tcp;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private bool _disposed;

    public ServerSession(TcpClient tcp)
    {
        _tcp = tcp;

        NetworkStream stream = tcp.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n"
        };
    }

    public string Nickname { get; set; } = "";

    public EndPoint? RemoteEndPoint => _tcp.Client.RemoteEndPoint;

    public ValueTask<string?> ReadLineAsync(CancellationToken token = default) =>
        _reader.ReadLineAsync(token);

    public async Task SendAsync(string message, CancellationToken token = default)
    {
        if (_disposed)
            return;

        await _sendLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            await _writer.WriteLineAsync(message.AsMemory(), token).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _writer.Dispose();
        _reader.Dispose();
        _tcp.Dispose();
    }
}
