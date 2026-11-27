using System.Drawing;
using Chat.Client;
using Chat.Common;

namespace Chat.WinForms;

/// <summary>
/// Окно входа: адрес, порт, ник. Пока идёт подключение, кнопка заблокирована,
/// окно не замирает — подключение выполняется в фоновой задаче.
/// </summary>
public sealed class LoginForm : Form
{
    private readonly TextBox _hostBox = new();
    private readonly TextBox _portBox = new();
    private readonly TextBox _nickBox = new();
    private readonly Button _loginButton = new();
    private readonly Label _errorLabel = new();

    private bool _busy;

    public LoginForm()
    {
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "Чат — вход";
        ClientSize = new Size(420, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.75f);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(18),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        _hostBox.Text = "127.0.0.1";
        _hostBox.Dock = DockStyle.Fill;
        _hostBox.Margin = new Padding(0, 4, 0, 8);

        _portBox.Text = "5000";
        _portBox.Dock = DockStyle.Fill;
        _portBox.Margin = new Padding(0, 4, 0, 8);

        _nickBox.Dock = DockStyle.Fill;
        _nickBox.PlaceholderText = "Как вас зовут в чате?";
        _nickBox.MaxLength = Nicknames.MaxLength;
        _nickBox.Margin = new Padding(0, 4, 0, 8);

        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.ForeColor = Color.FromArgb(178, 34, 34);
        _errorLabel.TextAlign = ContentAlignment.MiddleLeft;

        _loginButton.Text = "Войти";
        _loginButton.Dock = DockStyle.Right;
        _loginButton.Width = 140;
        _loginButton.Margin = new Padding(0, 4, 0, 0);
        _loginButton.Click += Login_Click;

        grid.Controls.Add(MakeLabel("Сервер:"), 0, 0);
        grid.Controls.Add(_hostBox, 1, 0);
        grid.Controls.Add(MakeLabel("Порт:"), 0, 1);
        grid.Controls.Add(_portBox, 1, 1);
        grid.Controls.Add(MakeLabel("Ник:"), 0, 2);
        grid.Controls.Add(_nickBox, 1, 2);
        grid.Controls.Add(_errorLabel, 0, 3);
        grid.SetColumnSpan(_errorLabel, 2);
        grid.Controls.Add(_loginButton, 1, 4);

        var hint = new Label
        {
            Text = "Сервер запускается отдельно:" + Environment.NewLine + "dotnet run --project src/Chat.Server",
            Dock = DockStyle.Bottom,
            Height = 46,
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = Color.Gray,
            Padding = new Padding(18, 0, 0, 6),
        };

        Controls.Add(grid);
        Controls.Add(hint);
        AcceptButton = _loginButton;
        ActiveControl = _nickBox;
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private async void Login_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var host = _hostBox.Text.Trim();
        var nick = Nicknames.Normalize(_nickBox.Text);

        if (!int.TryParse(_portBox.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            _errorLabel.Text = "Порт — это число от 1 до 65535.";
            return;
        }

        if (!Nicknames.IsValid(nick, out var nickError))
        {
            _errorLabel.Text = nickError;
            return;
        }

        SetBusy(true, "Подключаемся…");

        var client = new ChatClient();
        var result = await Task.Run(() => client.Connect(host, port, nick, TimeSpan.FromSeconds(5)));

        if (!result.Success)
        {
            client.Dispose();
            SetBusy(false, result.Error);
            return;
        }

        Hide();
        using (var chat = new ChatForm(client, host, port))
        {
            chat.ShowDialog(this);
        }

        client.Dispose();
        Close();
    }

    private void SetBusy(bool busy, string message = "")
    {
        _busy = busy;
        _loginButton.Enabled = !busy;
        _hostBox.Enabled = !busy;
        _portBox.Enabled = !busy;
        _nickBox.Enabled = !busy;
        _errorLabel.ForeColor = busy ? Color.Gray : Color.FromArgb(178, 34, 34);
        _errorLabel.Text = message;
        UseWaitCursor = busy;
    }
}
