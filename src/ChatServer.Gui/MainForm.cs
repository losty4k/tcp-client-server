using System.Net;
using System.Net.Sockets;
using Chat.Core;
using Chat.UI;
using ChatServerCore = Chat.Core.ChatServer;

namespace ChatServer.Gui;

public sealed class MainForm : AppForm
{
    private static readonly string? LanAddress = DetectLanAddress();

    private readonly RoundedTextBox _portBox;
    private readonly RoundedButton _startButton;
    private readonly RoundedButton _clearButton;
    private readonly StatusDot _statusDot;
    private readonly Label _statusTitle;
    private readonly Label _statusSubtitle;
    private readonly Label _errorLabel;
    private readonly Label _onlineHeader;
    private readonly Label _onlineEmpty;
    private readonly OnlineList _onlineList;
    private readonly RichTextBox _logBox;
    private readonly Label _logEmpty;
    private readonly Label _addressLocal;
    private readonly Label _addressLan;

    private ChatServerCore? _server;
    private bool _closing;
    private bool _closeReady;

    public MainForm() : base("Chat Server", "локальный TCP-чат", new Size(940, 680))
    {
        BuildMain(out _clearButton, out _logBox, out _logEmpty);
        BuildSidebar(
            out _portBox,
            out _startButton,
            out _statusDot,
            out _statusTitle,
            out _statusSubtitle,
            out _errorLabel,
            out _onlineHeader,
            out _onlineEmpty,
            out _onlineList,
            out _addressLocal,
            out _addressLan);

        _startButton.Click += OnStartStopClick;
        _clearButton.Click += (_, _) =>
        {
            _logBox.Clear();
            _logEmpty.Visible = true;
            _clearButton.Enabled = false;
        };

        _portBox.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        };
        _portBox.TextChanged += (_, _) => UpdateAddress();

        UpdateAddress();
        UpdateStatus(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _server = new ChatServerCore();
        _server.Log += OnServerLog;
        _server.ClientsChanged += OnClientsChanged;
        _server.RunningChanged += OnRunningChanged;
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

        if (_server is { IsRunning: true })
        {
            e.Cancel = true;
            _closing = true;

            _startButton.Enabled = false;
            _startButton.Text = "Останавливаем…";

            await _server.StopAsync();

            _closeReady = true;
            Close();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_server is not null)
        {
            _server.Log -= OnServerLog;
            _server.ClientsChanged -= OnClientsChanged;
            _server.RunningChanged -= OnRunningChanged;
        }

        base.OnFormClosed(e);
    }

    private void BuildMain(
        out RoundedButton clearButton,
        out RichTextBox logBox,
        out Label logEmpty)
    {
        var main = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Padding = new Padding(30, 0, 30, 28)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = Theme.Background
        };

        var title = new Label
        {
            Text = "Журнал",
            Font = Theme.Section,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(0, 24)
        };

        var subtitle = new Label
        {
            Text = "Подключения и сообщения",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(1, 52)
        };

        clearButton = new RoundedButton
        {
            Text = "Очистить",
            Variant = ButtonVariant.Ghost,
            Font = Theme.BodySmall,
            Size = new Size(96, 32),
            Enabled = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        RoundedButton clearButtonLocal = clearButton;
        header.Resize += (_, _) =>
            clearButtonLocal.Location = new Point(
                header.ClientSize.Width - clearButtonLocal.Width, 30);

        header.Controls.AddRange(new Control[] { title, subtitle, clearButton });

        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            FillColor = Theme.Surface,
            BorderColor = Theme.Border,
            CornerRadius = 16,
            Padding = new Padding(16)
        };

        logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextPrimary,
            Font = Theme.Log,
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            HideSelection = false,
            TabStop = false
        };

        logEmpty = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Пока тихо.\r\nЗапустите сервер и подключите клиентов.",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = Theme.BodySmall,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Surface
        };

        card.Controls.Add(logBox);
        card.Controls.Add(logEmpty);
        main.Controls.Add(card);
        main.Controls.Add(header);
        Content.Controls.Add(main);
    }

    private void BuildSidebar(
        out RoundedTextBox portBox,
        out RoundedButton startButton,
        out StatusDot statusDot,
        out Label statusTitle,
        out Label statusSubtitle,
        out Label errorLabel,
        out Label onlineHeader,
        out Label onlineEmpty,
        out OnlineList onlineList,
        out Label addressLocal,
        out Label addressLan)
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 280,
            Height = ClientSize.Height - 56,
            BackColor = Theme.Surface
        };

        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        statusDot = new StatusDot
        {
            Location = new Point(24, 31),
            DotColor = Theme.TextMuted
        };

        statusTitle = new Label
        {
            Text = "Сервер остановлен",
            Font = Theme.BodyMedium,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(42, 24)
        };

        statusSubtitle = new Label
        {
            Text = "Готов к запуску",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(42, 44)
        };

        var portCaption = new Label
        {
            Text = "ПОРТ",
            Font = Theme.TinyMedium,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, 94)
        };

        portBox = new RoundedTextBox
        {
            Text = ChatProtocol.DefaultPort.ToString(),
            PlaceholderText = "5000",
            MaxLength = 5,
            Location = new Point(24, 114),
            Size = new Size(232, 42)
        };

        startButton = new RoundedButton
        {
            Text = "Запустить",
            Variant = ButtonVariant.Primary,
            Location = new Point(24, 168),
            Size = new Size(232, 44)
        };

        var hint = new Label
        {
            Text = "Доступен всем в локальной сети.",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(24, 222),
            Size = new Size(232, 30)
        };

        errorLabel = new Label
        {
            Font = Theme.BodySmall,
            ForeColor = Theme.Danger,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(24, 258),
            Size = new Size(232, 36),
            Visible = false
        };

        var separator = new Panel
        {
            BackColor = Theme.Border,
            Location = new Point(24, 304),
            Size = new Size(232, 1)
        };

        onlineHeader = new Label
        {
            Text = "ОНЛАЙН · 0",
            Font = Theme.TinyMedium,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, 324)
        };

        onlineList = new OnlineList
        {
            Location = new Point(16, 348),
            Size = new Size(248, 136),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        };

        onlineEmpty = new Label
        {
            Text = "Пока никого нет",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextMuted,
            BackColor = Theme.Surface,
            AutoSize = true,
            Location = new Point(28, 360)
        };

        var bottomSeparator = new Panel
        {
            BackColor = Theme.Border,
            Location = new Point(24, sidebar.Height - 132),
            Size = new Size(232, 1),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        var localCaption = new Label
        {
            Text = "ЛОКАЛЬНО",
            Font = Theme.TinyMedium,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, sidebar.Height - 112),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        addressLocal = new Label
        {
            Text = "127.0.0.1:5000",
            Font = Theme.Body,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, sidebar.Height - 92),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        var lanCaption = new Label
        {
            Text = "ПО СЕТИ",
            Font = Theme.TinyMedium,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, sidebar.Height - 64),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        addressLan = new Label
        {
            Text = "—",
            Font = Theme.BodySmall,
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, sidebar.Height - 44),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        sidebar.Controls.AddRange(new Control[]
        {
            statusDot,
            statusTitle,
            statusSubtitle,
            portCaption,
            portBox,
            startButton,
            hint,
            errorLabel,
            separator,
            onlineHeader,
            onlineList,
            onlineEmpty,
            bottomSeparator,
            localCaption,
            addressLocal,
            lanCaption,
            addressLan
        });

        Content.Controls.Add(sidebar);
    }

    private async void OnStartStopClick(object? sender, EventArgs e)
    {
        if (_server is null)
            return;

        if (_server.IsRunning)
        {
            _startButton.Enabled = false;
            _startButton.Text = "Останавливаем…";
            await _server.StopAsync();
            return;
        }

        if (!int.TryParse(_portBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            ShowError("Укажите порт от 1 до 65535.");
            return;
        }

        try
        {
            _server.Start(port);
            HideError();
        }
        catch (SocketException)
        {
            ShowError("Порт занят. Выберите другой.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnServerLog(ChatLogEntry entry)
    {
        RunOnUi(() =>
        {
            AppendLog(entry);
            _logEmpty.Visible = false;
            _clearButton.Enabled = true;

            if (_logBox.TextLength > 120_000)
            {
                _logBox.Select(0, 40_000);
                _logBox.SelectedText = string.Empty;
            }
        });
    }

    private void OnClientsChanged(IReadOnlyList<string> clients)
    {
        RunOnUi(() =>
        {
            _onlineList.BeginUpdate();
            _onlineList.Items.Clear();

            foreach (string nickname in clients)
                _onlineList.Items.Add(nickname);

            _onlineList.EndUpdate();

            _onlineEmpty.Visible = clients.Count == 0;
            _onlineHeader.Text = $"ОНЛАЙН · {clients.Count}";
        });
    }

    private void OnRunningChanged(bool running)
    {
        RunOnUi(() =>
        {
            _startButton.Enabled = true;
            _startButton.Text = running ? "Остановить" : "Запустить";
            _startButton.Variant = running ? ButtonVariant.Danger : ButtonVariant.Primary;
            _portBox.ReadOnly = running;

            UpdateStatus(running);
            UpdateAddress();

            if (running)
                HideError();
        });
    }

    private void UpdateStatus(bool running)
    {
        _statusDot.DotColor = running ? Theme.Online : Theme.TextMuted;
        _statusTitle.Text = running ? "Сервер работает" : "Сервер остановлен";
        _statusSubtitle.Text = running
            ? $"Порт {_server?.Port ?? 0}"
            : "Готов к запуску";
    }

    private void UpdateAddress()
    {
        string port = _portBox.Text.Trim();
        if (port.Length == 0)
            port = ChatProtocol.DefaultPort.ToString();

        _addressLocal.Text = $"127.0.0.1:{port}";
        _addressLan.Text = LanAddress is null
            ? "нет сетевого адреса"
            : $"{LanAddress}:{port}";
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }

    private void HideError()
    {
        _errorLabel.Visible = false;
        _errorLabel.Text = "";
    }

    private void AppendLog(ChatLogEntry entry)
    {
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;

        AppendPart($"{entry.Time:HH:mm}  ", Theme.Log, Theme.TextMuted);

        switch (entry.Kind)
        {
            case ChatLogKind.Join:
                AppendPart("→  ", Theme.Log, Theme.Online);
                AppendPart(entry.Nickname ?? "", Theme.BodyMedium, Theme.TextPrimary);
                AppendPart(" подключился", Theme.Log, Theme.TextSecondary);
                break;

            case ChatLogKind.Leave:
                AppendPart("←  ", Theme.Log, Theme.TextMuted);
                AppendPart(entry.Nickname ?? "", Theme.BodyMedium, Theme.TextSecondary);
                AppendPart(" покинул чат", Theme.Log, Theme.TextMuted);
                break;

            case ChatLogKind.Rename:
                AppendPart("↻  ", Theme.Log, Theme.Accent);
                AppendPart(entry.Nickname ?? "", Theme.BodyMedium, Theme.Accent);
                AppendPart($" {entry.Text}", Theme.Log, Theme.TextSecondary);
                break;

            case ChatLogKind.Message:
                AppendPart(entry.Nickname ?? "", Theme.BodyMedium, Theme.Accent);
                AppendPart(": ", Theme.Log, Theme.TextMuted);
                AppendPart(entry.Text, Theme.Log, Theme.TextPrimary);
                break;

            case ChatLogKind.Error:
                AppendPart("!  ", Theme.Log, Theme.Danger);
                AppendPart(entry.Text, Theme.Log, Theme.Danger);
                break;

            default:
                AppendPart("•  ", Theme.Log, Theme.TextMuted);
                AppendPart(entry.Text, Theme.Log, Theme.TextSecondary);
                break;
        }

        _logBox.AppendText(Environment.NewLine);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void AppendPart(string text, Font font, Color color)
    {
        _logBox.SelectionFont = font;
        _logBox.SelectionColor = color;
        _logBox.AppendText(text);
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

    private static string? DetectLanAddress()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
