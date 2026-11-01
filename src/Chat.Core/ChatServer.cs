using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Chat.Core;

/// <summary>
/// Многоклиентский TCP-сервер чата.
/// События поднимаются в захваченном <see cref="SynchronizationContext"/>,
/// поэтому WinForms-клиент может подписываться на них напрямую.
/// </summary>
public sealed class ChatServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServerSession> _clients =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<Task, byte> _clientTasks = new();
    private readonly Lock _nicknameLock = new();
    private readonly SynchronizationContext? _context;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private volatile bool _stopping;

    public ChatServer()
    {
        _context = SynchronizationContext.Current;
    }

    public bool IsRunning { get; private set; }

    public int Port { get; private set; }

    public IReadOnlyList<string> Clients => _clients.Keys
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public event Action<ChatLogEntry>? Log;
    public event Action<IReadOnlyList<string>>? ClientsChanged;
    public event Action<bool>? RunningChanged;

    public void Start(int port)
    {
        if (IsRunning)
            throw new InvalidOperationException("Сервер уже запущен.");

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        _listener = listener;
        _cts = new CancellationTokenSource();
        Port = port;
        IsRunning = true;

        Raise(() =>
        {
            Log?.Invoke(new ChatLogEntry(
                ChatLogKind.Info, null, $"Сервер запущен на порту {port}.", DateTime.Now));
            RunningChanged?.Invoke(true);
        });

        _acceptTask = AcceptLoopAsync(listener, _cts.Token);
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        _stopping = true;

        _cts?.Cancel();
        _listener?.Stop();

        foreach (var client in _clients.Values)
            client.Dispose();

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch
            {
                // Приём уже остановлен.
            }
        }

        if (!_clientTasks.IsEmpty)
        {
            try
            {
                await Task.WhenAll(_clientTasks.Keys).ConfigureAwait(false);
            }
            catch
            {
                // Отдельные обработчики могли завершиться с ошибкой.
            }
        }

        _clients.Clear();
        _clientTasks.Clear();
        _cts?.Dispose();
        _cts = null;
        _listener = null;
        _acceptTask = null;
        _stopping = false;

        Raise(() =>
        {
            ClientsChanged?.Invoke(Array.Empty<string>());
            Log?.Invoke(new ChatLogEntry(ChatLogKind.Info, null, "Сервер остановлен.", DateTime.Now));
            RunningChanged?.Invoke(false);
        });
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                    break;
                }

                Task handler = HandleClientAsync(tcp, token);
                _clientTasks.TryAdd(handler, 0);
                _ = handler.ContinueWith(
                    t => _clientTasks.TryRemove(t, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (Exception ex)
        {
            Raise(() => Log?.Invoke(new ChatLogEntry(
                ChatLogKind.Error, null, ex.Message, DateTime.Now)));
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken token)
    {
        tcp.NoDelay = true;
        var session = new ServerSession(tcp);
        bool registered = false;

        try
        {
            // Первая строка от клиента — желаемый ник.
            string? firstLine = await session.ReadLineAsync(token).ConfigureAwait(false);
            if (firstLine is null)
                return;

            string nickname = Register(session, ChatProtocol.SanitizeNickname(firstLine));
            registered = true;

            Raise(() => Log?.Invoke(new ChatLogEntry(
                ChatLogKind.Join, nickname, "подключился", DateTime.Now)));
            RaiseClientsChanged();

            await session.SendAsync(
                $"{ChatProtocol.WelcomePrefix}{nickname}! Введите /help, чтобы увидеть команды.",
                token).ConfigureAwait(false);

            await BroadcastAsync(
                $"{ChatProtocol.EventPrefix}{nickname} присоединился к чату",
                except: session,
                token: token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                string? line = await session.ReadLineAsync(token).ConfigureAwait(false);
                if (line is null)
                    break;

                line = line.Trim();
                if (line.Length == 0)
                    continue;

                if (line[0] == '/')
                {
                    if (!await HandleCommandAsync(session, line, token).ConfigureAwait(false))
                        break;
                    continue;
                }

                Raise(() => Log?.Invoke(new ChatLogEntry(
                    ChatLogKind.Message, session.Nickname, line, DateTime.Now)));

                await BroadcastAsync($"{session.Nickname}: {line}", token: token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Сервер останавливается.
        }
        catch (IOException)
        {
            // Соединение оборвалось.
        }
        catch (SocketException)
        {
            // Соединение оборвалось.
        }
        catch (ObjectDisposedException)
        {
            // Сессия закрыта при остановке сервера.
        }
        finally
        {
            if (registered)
            {
                Unregister(session);
                RaiseClientsChanged();
            }

            session.Dispose();

            if (registered && !_stopping)
            {
                Raise(() => Log?.Invoke(new ChatLogEntry(
                    ChatLogKind.Leave, session.Nickname, "покинул чат", DateTime.Now)));

                try
                {
                    await BroadcastAsync(
                        $"{ChatProtocol.EventPrefix}{session.Nickname} покинул чат").ConfigureAwait(false);
                }
                catch
                {
                    // Клиенты уже отключены.
                }
            }
        }
    }

    private async Task<bool> HandleCommandAsync(
        ServerSession session,
        string line,
        CancellationToken token)
    {
        int space = line.IndexOf(' ');
        string command = (space < 0 ? line : line[..space]).ToLowerInvariant();
        string argument = space < 0 ? "" : line[(space + 1)..].Trim();

        switch (command)
        {
            case "/help":
                await session.SendAsync(
                    $"{ChatProtocol.ServerPrefix}Команды: /nick <имя> — сменить имя, /users — участники, /quit — выйти.",
                    token).ConfigureAwait(false);
                return true;

            case "/users":
                string names = string.Join(
                    ", ",
                    _clients.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
                await session.SendAsync(
                    $"{ChatProtocol.UsersPrefix}{_clients.Count}): {names}",
                    token).ConfigureAwait(false);
                return true;

            case "/nick":
                if (argument.Length == 0)
                {
                    await session.SendAsync(
                        $"{ChatProtocol.ServerPrefix}Использование: /nick <имя>",
                        token).ConfigureAwait(false);
                    return true;
                }

                string oldName = session.Nickname;
                string? newName = Rename(session, ChatProtocol.SanitizeNickname(argument));
                if (newName is null)
                {
                    await session.SendAsync(
                        $"{ChatProtocol.ServerPrefix}Это имя уже занято.",
                        token).ConfigureAwait(false);
                    return true;
                }

                Raise(() => Log?.Invoke(new ChatLogEntry(
                    ChatLogKind.Rename, oldName, $"сменил имя на {newName}", DateTime.Now)));

                await session.SendAsync(
                    $"{ChatProtocol.ServerPrefix}Теперь вы {newName}.",
                    token).ConfigureAwait(false);
                await BroadcastAsync(
                    $"{ChatProtocol.EventPrefix}{oldName} теперь {newName}",
                    except: session,
                    token: token).ConfigureAwait(false);
                return true;

            case "/quit":
            case "/exit":
                await session.SendAsync(
                    $"{ChatProtocol.ServerPrefix}До встречи!",
                    token).ConfigureAwait(false);
                return false;

            default:
                await session.SendAsync(
                    $"{ChatProtocol.ServerPrefix}Неизвестная команда. Введите /help.",
                    token).ConfigureAwait(false);
                return true;
        }
    }

    private async Task BroadcastAsync(
        string text,
        ServerSession? except = null,
        CancellationToken token = default)
    {
        foreach (var client in _clients.Values)
        {
            if (ReferenceEquals(client, except))
                continue;

            try
            {
                await client.SendAsync(text, token).ConfigureAwait(false);
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // Клиент отключился — его цикл сам уберёт его из списка.
            }
        }
    }

    private string Register(ServerSession session, string desired)
    {
        lock (_nicknameLock)
        {
            string candidate = desired;
            int suffix = 2;

            // Если ник занят, добавляем суффикс: alice -> alice-2, alice-3, ...
            while (!_clients.TryAdd(candidate, session))
                candidate = $"{desired}-{suffix++}";

            session.Nickname = candidate;
            return candidate;
        }
    }

    private void Unregister(ServerSession session)
    {
        lock (_nicknameLock)
        {
            if (_clients.TryGetValue(session.Nickname, out var existing) &&
                ReferenceEquals(existing, session))
            {
                _clients.TryRemove(session.Nickname, out _);
            }
        }
    }

    private string? Rename(ServerSession session, string desired)
    {
        string? result;
        bool changed = false;

        lock (_nicknameLock)
        {
            if (string.Equals(session.Nickname, desired, StringComparison.OrdinalIgnoreCase))
                return session.Nickname;

            if (_clients.ContainsKey(desired))
                return null;

            if (!_clients.TryRemove(session.Nickname, out var removed) ||
                !ReferenceEquals(removed, session))
            {
                return null;
            }

            _clients[desired] = session;
            session.Nickname = desired;
            result = desired;
            changed = true;
        }

        if (changed)
            RaiseClientsChanged();

        return result;
    }

    private void RaiseClientsChanged()
    {
        IReadOnlyList<string> snapshot = Clients;
        Raise(() => ClientsChanged?.Invoke(snapshot));
    }

    private void Raise(Action action)
    {
        if (_context is null || _context == SynchronizationContext.Current)
            action();
        else
            _context.Post(_ => action(), null);
    }
}
