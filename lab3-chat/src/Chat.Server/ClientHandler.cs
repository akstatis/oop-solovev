using System.Net.Sockets;
using System.Text;
using Chat.Common;

namespace Chat.Server;

/// <summary>
/// Одно подключение на сервере: свой поток, свой читатель и писатель.
/// Сначала ждёт вход, потом читает сообщения до разрыва соединения.
/// </summary>
internal sealed class ClientHandler
{
    private readonly ChatServer _server;
    private readonly TcpClient _tcp;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly object _writeGate = new();

    public ClientHandler(ChatServer server, TcpClient tcp)
    {
        _server = server;
        _tcp = tcp;
        var stream = tcp.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

    public string Nick { get; private set; } = "";

    /// <summary>Отправить сообщение этому клиенту. Безопасно из любого потока.</summary>
    public void Send(ChatMessage message)
    {
        try
        {
            lock (_writeGate)
            {
                _writer.WriteLine(message.ToJson());
            }
        }
        catch (IOException)
        {
            // Клиент уже отключился — его уберёт свой поток.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Run()
    {
        var loggedIn = false;
        try
        {
            var hello = _reader.ReadLine();
            if (hello is null)
            {
                return;
            }

            if (!TryLogin(hello))
            {
                return;
            }

            loggedIn = true;
            _server.BroadcastNotice($"{Nick} вошёл в чат");
            _server.BroadcastUsers();
            _server.Log($"{Nick} подключился");

            string? line;
            while ((line = _reader.ReadLine()) is not null)
            {
                ChatMessage message;
                try
                {
                    message = ChatMessage.FromJson(line);
                }
                catch (InvalidDataException)
                {
                    continue;
                }

                if (message.Type == MessageTypes.Text)
                {
                    HandleText(message);
                }
                else if (message.Type == MessageTypes.Who)
                {
                    Send(ChatMessage.Online(_server.OnlineUsers));
                }
            }
        }
        catch (IOException)
        {
        }
        catch (InvalidDataException)
        {
        }
        finally
        {
            if (loggedIn)
            {
                _server.Unregister(this);
                _server.BroadcastNotice($"{Nick} вышел из чата");
                _server.BroadcastUsers();
                _server.Log($"{Nick} отключился");
            }

            try
            {
                _tcp.Close();
            }
            catch (IOException)
            {
            }
        }
    }

    private bool TryLogin(string json)
    {
        ChatMessage request;
        try
        {
            request = ChatMessage.FromJson(json);
        }
        catch (InvalidDataException)
        {
            Send(ChatMessage.LoginError("Не удалось разобрать приветствие."));
            return false;
        }

        if (request.Type != MessageTypes.Login)
        {
            Send(ChatMessage.LoginError("Сначала нужно представиться."));
            return false;
        }

        var nick = Nicknames.Normalize(request.Nick);
        if (!Nicknames.IsValid(nick, out var error))
        {
            Send(ChatMessage.LoginError(error));
            return false;
        }

        if (!_server.TryRegister(nick, this))
        {
            Send(ChatMessage.LoginError($"Ник «{nick}» уже занят."));
            return false;
        }

        Nick = nick;
        Send(ChatMessage.LoginOk(nick));
        _server.SendHistory(this);
        return true;
    }

    private void HandleText(ChatMessage message)
    {
        var text = (message.Text ?? "").Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (text.Length > 2000)
        {
            text = text[..2000];
        }

        var target = string.IsNullOrWhiteSpace(message.Target) ? null : message.Target.Trim();
        var outgoing = ChatMessage.Say(Nick, text, target);

        if (target is null)
        {
            _server.Remember(outgoing);
            _server.Broadcast(outgoing);
            return;
        }

        // Личное сообщение: получателю и отправителю, остальным — ничего.
        if (!_server.SendTo(target, outgoing))
        {
            Send(ChatMessage.Notice($"Пользователь «{target}» уже не в сети."));
            return;
        }

        Send(outgoing);
    }
}
