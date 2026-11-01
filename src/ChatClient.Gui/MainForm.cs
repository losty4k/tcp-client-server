using System.Net.Sockets;
using Chat.Core;
using Chat.UI;

namespace ChatClient.Gui;

public sealed class MainForm : AppForm
{
    private readonly Panel _connectionView;
    private readonly RoundedPanel _connectionCard;
    private readonly RoundedTextBox _hostBox;
    private readonly RoundedTextBox _portBox;
    private readonly RoundedTextBox _nicknameBox;
    private readonly RoundedButton _connectButton;
    private readonly Label _connectError;

    private readonly Panel _chatView;
    private readonly Panel _chatHeader;
    private readonly StatusDot _chatStatus;
    private readonly Label _chatSubtitle;
    private readonly Pill _onlinePill;
    private readonly ChatFeed _feed;
    private readonly RoundedTextBox _messageBox;
    private readonly RoundedButton _sendButton;

    private ChatConnection? _connection;
    private string _nickname = "";
    private int _silentUsersRequests;
    private bool _connectInProgress;
    private bool _closing;
    private bool _closeReady;

    public MainForm() : base("Chat", "локальный TCP-чат", new Size(860, 640))
    {
        _connectionView = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background
        };

        _connectionCard = new RoundedPanel
        {
            Size = new Size(440, 440),
            FillColor = Theme.Surface,
            BorderColor = Theme.Border,
            CornerRadius = 20
        };

        var heading = new Label
        {
            Text = "Подключиться к чату",
            Font = Theme.Heading,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(36, 32)
        };

        var subheading = new Label
        {
            Text = "Простой чат в локальной сети",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(37, 70)
        };

        var hostCaption = CreateCaption("СЕРВЕР", 36, 116);
        _hostBox = new RoundedTextBox
        {
            Text = "127.0.0.1",
            PlaceholderText = "127.0.0.1",
            MaxLength = 100,
            Location = new Point(36, 136),
            Size = new Size(368, 42)
        };

        var portCaption = CreateCaption("ПОРТ", 36, 194);
        _portBox = new RoundedTextBox
        {
            Text = ChatProtocol.DefaultPort.ToString(),
            PlaceholderText = "5000",
            MaxLength = 5,
            Location = new Point(36, 214),
            Size = new Size(104, 42)
        };

        var nicknameCaption = CreateCaption("ИМЯ", 160, 194);
        _nicknameBox = new RoundedTextBox
        {
            PlaceholderText = "Ваше имя",
            MaxLength = ChatProtocol.MaxNicknameLength,
            Location = new Point(160, 214),
            Size = new Size(244, 42)
        };

        _connectButton = new RoundedButton
        {
            Text = "Подключиться",
            Variant = ButtonVariant.Primary,
            Location = new Point(36, 280),
            Size = new Size(368, 46)
        };

        _connectError = new Label
        {
            Font = Theme.BodySmall,
            ForeColor = Theme.Danger,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(36, 336),
            Size = new Size(368, 42),
            Visible = false
        };

        var hint = new Label
        {
            Text = "Сервер должен быть запущен на выбранном порту.",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(36, 390),
            Size = new Size(368, 24)
        };

        _connectionCard.Controls.AddRange(new Control[]
        {
            heading,
            subheading,
            hostCaption,
            _hostBox,
            portCaption,
            _portBox,
            nicknameCaption,
            _nicknameBox,
            _connectButton,
            _connectError,
            hint
        });

        _connectionView.Controls.Add(_connectionCard);
        _connectionView.Resize += (_, _) => CenterCard();

        BuildChatView(
            out _chatView,
            out _chatHeader,
            out _chatStatus,
            out _chatSubtitle,
            out _onlinePill,
            out _feed,
            out _messageBox,
            out _sendButton);

        _chatView.Visible = false;

        Content.Controls.Add(_connectionView);
        Content.Controls.Add(_chatView);

        _connectButton.Click += OnConnectClick;
        _connectButton.Enabled = true;

        _portBox.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        };

        _hostBox.KeyDown += OnConnectionFieldKeyDown;
        _portBox.KeyDown += OnConnectionFieldKeyDown;
        _nicknameBox.KeyDown += OnConnectionFieldKeyDown;

        _messageBox.TextChanged += (_, _) => UpdateSendState();
        _messageBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendCurrentMessageAsync();
            }
        };
        _sendButton.Click += async (_, _) => await SendCurrentMessageAsync();

        CenterCard();
        UpdateSendState();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _nicknameBox.InnerTextBox.Focus();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (_closeReady)
        {
            base.OnFormClosing(e);
            return;
        }

        if (_closing)
        {
            e.Cancel = true;
            return;
        }

        _closing = true;

        if (_connection is not null)
        {
            e.Cancel = true;

            ChatConnection connection = _connection;
            _connection = null;
            connection.LineReceived -= OnLineReceived;
            connection.Disconnected -= OnDisconnected;

            await connection.DisposeAsync();

            _closeReady = true;
            Close();
            return;
        }

        base.OnFormClosing(e);
    }

    private static Label CreateCaption(string text, int x, int y) => new()
    {
        Text = text,
        Font = Theme.TinyMedium,
        ForeColor = Theme.TextMuted,
        BackColor = Color.Transparent,
        AutoSize = true,
        Location = new Point(x, y)
    };

    private void BuildChatView(
        out Panel chatView,
        out Panel chatHeader,
        out StatusDot chatStatus,
        out Label chatSubtitle,
        out Pill onlinePill,
        out ChatFeed feed,
        out RoundedTextBox messageBox,
        out RoundedButton sendButton)
    {
        chatView = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background
        };

        chatHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Theme.Surface
        };

        Panel chatHeaderLocal = chatHeader;
        chatHeaderLocal.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(
                pen, 0, chatHeaderLocal.Height - 1, chatHeaderLocal.Width, chatHeaderLocal.Height - 1);
        };

        chatStatus = new StatusDot
        {
            Location = new Point(28, 34),
            DotColor = Theme.Online
        };

        var chatTitle = new Label
        {
            Text = "Чат",
            Font = Theme.Section,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(46, 16)
        };

        chatSubtitle = new Label
        {
            Text = "подключение…",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(47, 43)
        };

        onlinePill = new Pill
        {
            Text = "онлайн · 1",
            Size = new Size(98, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        Pill onlinePillLocal = onlinePill;
        chatHeaderLocal.Resize += (_, _) =>
            onlinePillLocal.Location = new Point(
                chatHeaderLocal.ClientSize.Width - onlinePillLocal.Width - 28, 24);

        chatHeaderLocal.Controls.AddRange(new Control[]
        {
            chatStatus, chatTitle, chatSubtitle, onlinePill
        });

        feed = new ChatFeed
        {
            Dock = DockStyle.Fill
        };

        var composer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 84,
            BackColor = Theme.Background
        };

        messageBox = new RoundedTextBox
        {
            PlaceholderText = "Сообщение…",
            MaxLength = 2000,
            CornerRadius = 14,
            Location = new Point(24, 18),
            Size = new Size(700, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        sendButton = new RoundedButton
        {
            Text = "→",
            Variant = ButtonVariant.Primary,
            CornerRadius = 22,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Size = new Size(44, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        RoundedButton sendButtonLocal = sendButton;
        RoundedTextBox messageBoxLocal = messageBox;
        composer.Resize += (_, _) =>
        {
            int right = 24;
            sendButtonLocal.Location = new Point(
                composer.ClientSize.Width - right - sendButtonLocal.Width, 20);
            messageBoxLocal.Width = Math.Max(
                120,
                composer.ClientSize.Width - right * 2 - sendButtonLocal.Width - 10);
        };

        composer.Controls.AddRange(new Control[] { messageBox, sendButton });

        chatView.Controls.Add(feed);
        chatView.Controls.Add(composer);
        chatView.Controls.Add(chatHeaderLocal);
    }

    private void CenterCard()
    {
        _connectionCard.Location = new Point(
            Math.Max(0, (_connectionView.ClientSize.Width - _connectionCard.Width) / 2),
            Math.Max(0, (_connectionView.ClientSize.Height - _connectionCard.Height) / 2 - 8));
    }

    private async void OnConnectClick(object? sender, EventArgs e)
    {
        await ConnectAsync();
    }

    private async void OnConnectionFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (_connectInProgress)
            return;

        string host = _hostBox.Text.Trim();
        string nickname = _nicknameBox.Text.Trim();

        if (host.Length == 0)
        {
            ShowConnectError("Укажите адрес сервера.");
            return;
        }

        if (!int.TryParse(_portBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            ShowConnectError("Укажите порт от 1 до 65535.");
            return;
        }

        if (nickname.Length == 0)
        {
            ShowConnectError("Введите имя.");
            return;
        }

        nickname = ChatProtocol.SanitizeNickname(nickname);

        _connectInProgress = true;
        _connectButton.Enabled = false;
        _connectButton.Text = "Подключаемся…";
        HideConnectError();

        var connection = new ChatConnection();
        connection.LineReceived += OnLineReceived;
        connection.Disconnected += OnDisconnected;

        try
        {
            await connection.ConnectAsync(host, port, nickname);

            _connection = connection;
            _nickname = nickname;
            _silentUsersRequests = 0;

            _feed.ClearMessages();
            _chatSubtitle.Text = $"{nickname} · подключено";
            _onlinePill.Text = "онлайн · 1";
            _chatStatus.DotColor = Theme.Online;

            _connectionView.Visible = false;
            _chatView.Visible = true;
            _chatView.BringToFront();

            _feed.AddSystem($"Вы подключились к {host}:{port}.");
            _messageBox.Text = "";
            _messageBox.InnerTextBox.Focus();
        }
        catch (Exception ex)
        {
            connection.LineReceived -= OnLineReceived;
            connection.Disconnected -= OnDisconnected;
            await connection.DisposeAsync();

            ShowConnectError($"Не удалось подключиться: {ex.Message}");
        }
        finally
        {
            _connectInProgress = false;
            _connectButton.Enabled = true;
            _connectButton.Text = "Подключиться";
        }
    }

    private void OnLineReceived(string line)
    {
        RunOnUi(() => HandleLine(line));
    }

    private void HandleLine(string line)
    {
        ChatLine parsed = ChatLineParser.Parse(line);

        switch (parsed.Kind)
        {
            case ChatLineKind.Server:
                HandleServerLine(parsed.Text);
                break;

            case ChatLineKind.Event:
                _feed.AddEvent(parsed.Text);
                _ = RefreshOnlineAsync();
                break;

            case ChatLineKind.Message:
                bool own = string.Equals(
                    parsed.Author, _nickname, StringComparison.OrdinalIgnoreCase);

                _feed.AddMessage(new ChatMessage(
                    own ? ChatMessageKind.Own : ChatMessageKind.Other,
                    parsed.Author,
                    parsed.Text));
                break;

            default:
                _feed.AddSystem(parsed.Text);
                break;
        }
    }

    private void HandleServerLine(string text)
    {
        if (TryParseWelcome(text, out string assignedNickname))
        {
            _nickname = assignedNickname;
            _chatSubtitle.Text = $"{assignedNickname} · подключено";
            _feed.AddSystem($"Вы вошли как {assignedNickname}.");
            _ = RefreshOnlineAsync();
            return;
        }

        if (TryParseNickChange(text, out string newNickname))
        {
            _nickname = newNickname;
            _chatSubtitle.Text = $"{newNickname} · подключено";
            _feed.AddSystem(text);
            return;
        }

        if (TryParseUsers(text, out int online))
        {
            UpdateOnline(online);

            if (_silentUsersRequests > 0)
            {
                _silentUsersRequests--;
                return;
            }

            _feed.AddSystem(text);
            return;
        }

        _feed.AddSystem(text);
    }

    private void OnDisconnected(string? reason)
    {
        RunOnUi(() =>
        {
            if (_connection is null)
                return;

            ChatConnection connection = _connection;
            _connection = null;
            _silentUsersRequests = 0;

            connection.LineReceived -= OnLineReceived;
            connection.Disconnected -= OnDisconnected;
            _ = connection.DisposeAsync().AsTask();

            _chatView.Visible = false;
            _connectionView.Visible = true;
            _connectionView.BringToFront();

            _chatStatus.DotColor = Theme.TextMuted;
            UpdateSendState();
            ShowConnectError(reason ?? "Соединение закрыто.");
            _nicknameBox.InnerTextBox.Focus();
        });
    }

    private async Task RefreshOnlineAsync()
    {
        if (_connection is null)
            return;

        _silentUsersRequests++;

        try
        {
            await _connection.SendAsync("/users");
        }
        catch
        {
            _silentUsersRequests = Math.Max(0, _silentUsersRequests - 1);
        }
    }

    private async Task SendCurrentMessageAsync()
    {
        string text = _messageBox.Text.Trim();

        if (text.Length == 0 || _connection is null)
            return;

        _messageBox.Text = "";

        try
        {
            await _connection.SendAsync(text);
        }
        catch
        {
            _feed.AddSystem("Не удалось отправить сообщение.");
        }

        _messageBox.InnerTextBox.Focus();
    }

    private void UpdateSendState()
    {
        _sendButton.Enabled =
            _connection is not null &&
            _messageBox.Text.Trim().Length > 0;
    }

    private void UpdateOnline(int count)
    {
        _onlinePill.Text = $"онлайн · {count}";
    }

    private void ShowConnectError(string message)
    {
        _connectError.Text = message;
        _connectError.Visible = true;
    }

    private void HideConnectError()
    {
        _connectError.Visible = false;
        _connectError.Text = "";
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        if (InvokeRequired)
            BeginInvoke(action);
        else
            action();
    }

    private static bool TryParseWelcome(string text, out string nickname)
    {
        nickname = "";

        const string prefix = "Добро пожаловать, ";
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        int end = text.IndexOf('!');
        if (end <= prefix.Length)
            return false;

        nickname = text[prefix.Length..end];
        return nickname.Length > 0;
    }

    private static bool TryParseNickChange(string text, out string nickname)
    {
        nickname = "";

        const string prefix = "Теперь вы ";
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string value = text[prefix.Length..].Trim();
        if (value.EndsWith('.'))
            value = value[..^1];

        if (value.Length == 0)
            return false;

        nickname = value;
        return true;
    }

    private static bool TryParseUsers(string text, out int count)
    {
        count = 0;

        const string prefix = "В чате (";
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        int close = text.IndexOf(')');
        if (close <= prefix.Length)
            return false;

        return int.TryParse(text[prefix.Length..close], out count);
    }
}
