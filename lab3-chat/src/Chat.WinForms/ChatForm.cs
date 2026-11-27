using System.Drawing;
using Chat.Client;
using Chat.Common;

namespace Chat.WinForms;

/// <summary>
/// Окно чата: лента, список онлайн, выбор адресата и поле ввода.
/// Сообщения приходят из фонового потока, поэтому все изменения окна
/// выполняются через BeginInvoke — в потоке окна.
/// </summary>
public sealed class ChatForm : Form
{
    private const string EveryoneCaption = "Всем";

    private readonly ChatClient _client;
    private readonly string _host;
    private readonly int _port;

    private readonly RichTextBox _feed = new();
    private readonly ListBox _users = new();
    private readonly ComboBox _recipient = new();
    private readonly TextBox _input = new();
    private readonly Button _sendButton = new();
    private readonly Label _onlineLabel = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    private readonly Font _nickFont = new("Segoe UI", 9.75f, FontStyle.Bold);
    private readonly Font _normalFont = new("Segoe UI", 9.75f, FontStyle.Regular);
    private readonly Font _italicFont = new("Segoe UI", 9.75f, FontStyle.Italic);

    public ChatForm(ChatClient client, string host, int port)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _host = host;
        _port = port;

        BuildLayout();

        // Создаём окно заранее: сообщения из сети приходят сразу после входа,
        // а BeginInvoke работает только когда у формы уже есть дескриптор.
        _ = Handle;

        _client.MessageReceived += OnMessageReceived;
        _client.Disconnected += OnDisconnected;

        AppendNotice($"Вы вошли как {_client.Nick}. Сервер {_host}:{_port}");

        // Часть сообщений сервера (список участников, уведомление о входе) уходит
        // сразу после login-ok — до того, как окно успело подписаться на события.
        // Поэтому список участников спрашиваем заново, уже из окна.
        try
        {
            _client.RequestUsers();
        }
        catch (InvalidOperationException)
        {
        }

        ActiveControl = _input;
    }

    private void BuildLayout()
    {
        Text = $"Чат — {_client.Nick}";
        ClientSize = new Size(920, 620);
        MinimumSize = new Size(680, 460);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.75f);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));

        root.Controls.Add(BuildFeed(), 0, 0);
        root.Controls.Add(BuildSidePanel(), 1, 0);
        root.Controls.Add(BuildInputPanel(), 0, 1);
        root.SetColumnSpan(root.GetControlFromPosition(0, 1)!, 2);

        Controls.Add(root);
        Controls.Add(BuildStatusStrip());
    }

    private Control BuildFeed()
    {
        _feed.Dock = DockStyle.Fill;
        _feed.ReadOnly = true;
        _feed.BackColor = Color.White;
        _feed.BorderStyle = BorderStyle.FixedSingle;
        _feed.HideSelection = false;
        _feed.WordWrap = true;
        _feed.Font = _normalFont;
        _feed.Margin = new Padding(0, 0, 8, 8);
        return _feed;
    }

    private Control BuildSidePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 0, 8),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

        _onlineLabel.Text = "Онлайн: 0";
        _onlineLabel.Dock = DockStyle.Fill;
        _onlineLabel.Font = _nickFont;

        _users.Dock = DockStyle.Fill;
        _users.IntegralHeight = false;
        _users.Margin = new Padding(0, 0, 0, 8);

        var recipientLabel = new Label { Text = "Кому:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        _recipient.Dock = DockStyle.Fill;
        _recipient.DropDownStyle = ComboBoxStyle.DropDownList;
        _recipient.Items.Add(EveryoneCaption);
        _recipient.SelectedIndex = 0;

        panel.Controls.Add(_onlineLabel, 0, 0);
        panel.Controls.Add(_users, 0, 1);
        panel.Controls.Add(recipientLabel, 0, 2);
        panel.Controls.Add(_recipient, 0, 3);
        return panel;
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));

        _input.Dock = DockStyle.Fill;
        _input.PlaceholderText = "Сообщение… Enter — отправить";
        _input.Margin = new Padding(0, 6, 8, 4);
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendCurrent();
            }
        };

        _sendButton.Text = "Отправить";
        _sendButton.Dock = DockStyle.Fill;
        _sendButton.Margin = new Padding(0, 5, 0, 4);
        _sendButton.Click += (_, _) => SendCurrent();

        panel.Controls.Add(_input, 0, 0);
        panel.Controls.Add(_sendButton, 1, 0);
        return panel;
    }

    private Control BuildStatusStrip()
    {
        var strip = new StatusStrip { SizingGrip = false };
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = $"Вы: {_client.Nick}   Сервер: {_host}:{_port}";
        strip.Items.Add(_statusLabel);
        return strip;
    }

    private void SendCurrent()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var target = _recipient.SelectedIndex > 0 ? _recipient.SelectedItem as string : null;

        try
        {
            _client.SendText(text, target);
            _input.Clear();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException)
        {
            AppendNotice($"Не удалось отправить: {ex.Message}");
        }
    }

    /// <summary>Событие приходит из потока чтения сети — переходим в поток окна.</summary>
    private void OnMessageReceived(ChatMessage message) => SafeInvoke(() => HandleMessage(message));

    private void OnDisconnected(string reason) => SafeInvoke(() =>
    {
        AppendNotice(reason + " Закройте окно и войдите снова.");
        _input.Enabled = false;
        _sendButton.Enabled = false;
        _statusLabel.Text = $"Отключено от {_host}:{_port}";
    });

    private void HandleMessage(ChatMessage message)
    {
        switch (message.Type)
        {
            case MessageTypes.Text:
                AppendChat(message);
                break;
            case MessageTypes.Notice:
                AppendNotice(message.Text);
                break;
            case MessageTypes.Users:
                UpdateUsers(message.Users);
                break;
            case MessageTypes.LoginError:
                AppendNotice($"Сервер: {message.Text}");
                break;
        }
    }

    private void AppendChat(ChatMessage message)
    {
        var time = message.Time == default ? DateTime.Now : message.Time;

        if (message.History)
        {
            Append($"[{time:HH:mm}] ", _normalFont, Color.Silver);
            Append(message.Nick, _nickFont, Color.Silver);
            Append($": {message.Text}{Environment.NewLine}", _normalFont, Color.Silver);
            return;
        }

        if (message.IsPrivate)
        {
            var ink = Color.FromArgb(126, 34, 160);
            var background = Color.FromArgb(246, 232, 255);
            Append($"[{time:HH:mm}] ", _normalFont, ink, background);
            Append($"{message.Nick} → {message.Target} (лично): ", _nickFont, ink, background);
            Append($"{message.Text}{Environment.NewLine}", _normalFont, ink, background);
            return;
        }

        Append($"[{time:HH:mm}] ", _normalFont, Color.Gray);
        Append(message.Nick, _nickFont, Color.FromArgb(30, 82, 160));
        Append($": {message.Text}{Environment.NewLine}", _normalFont, Color.Black);
    }

    private void AppendNotice(string text)
    {
        Append($"— {text} —{Environment.NewLine}", _italicFont, Color.FromArgb(120, 120, 120));
    }

    private void Append(string text, Font font, Color color, Color? background = null)
    {
        var start = _feed.TextLength;
        _feed.AppendText(text);
        _feed.Select(start, text.Length);
        _feed.SelectionFont = font;
        _feed.SelectionColor = color;
        _feed.SelectionBackColor = background ?? _feed.BackColor;
        _feed.SelectionStart = _feed.TextLength;
        _feed.SelectionLength = 0;
        _feed.ScrollToCaret();
    }

    private void UpdateUsers(List<string>? users)
    {
        var list = users ?? new List<string>();
        _onlineLabel.Text = $"Онлайн: {list.Count}";

        var previous = _recipient.SelectedItem as string;

        _users.BeginUpdate();
        try
        {
            _users.Items.Clear();
            foreach (var nick in list)
            {
                _users.Items.Add(nick);
            }
        }
        finally
        {
            _users.EndUpdate();
        }

        _recipient.BeginUpdate();
        try
        {
            _recipient.Items.Clear();
            _recipient.Items.Add(EveryoneCaption);
            foreach (var nick in list.Where(n => !string.Equals(n, _client.Nick, StringComparison.OrdinalIgnoreCase)))
            {
                _recipient.Items.Add(nick);
            }

            var index = previous is null ? 0 : _recipient.Items.IndexOf(previous);
            _recipient.SelectedIndex = index < 0 ? 0 : index;
        }
        finally
        {
            _recipient.EndUpdate();
        }
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // Окно уже закрывается — сообщение показывать некуда.
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _client.Disconnect();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nickFont.Dispose();
            _normalFont.Dispose();
            _italicFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
