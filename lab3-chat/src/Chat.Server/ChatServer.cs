using System.Net;
using System.Net.Sockets;
using Chat.Common;

namespace Chat.Server;

/// <summary>
/// Сервер чата: принимает подключения, ведёт список участников и рассылает
/// сообщения. Список меняют несколько потоков, поэтому доступ к нему под lock.
/// </summary>
public sealed class ChatServer : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ClientHandler> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly TcpListener _listener;
    private readonly MessageStore _history;
    private readonly int _requestedPort;

    private Thread? _acceptThread;
    private volatile bool _running;
    private int _port;

    public ChatServer(int port, string historyPath)
    {
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Порт должен быть от 0 до 65535.");
        }

        _requestedPort = port;
        _port = port;
        _listener = new TcpListener(IPAddress.Any, port);
        _history = new MessageStore(historyPath);
    }

    /// <summary>Событие для журнала сервера; консоль подписывается и печатает.</summary>
    public event Action<string>? ServerLog;

    /// <summary>Фактический порт: полезно, если сервер запущен на порту 0.</summary>
    public int Port => _port;

    public bool IsRunning => _running;

    public IReadOnlyList<string> OnlineUsers
    {
        get
        {
            lock (_gate)
            {
                return _clients.Keys.OrderBy(nick => nick, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
        }
    }

    public int OnlineCount
    {
        get
        {
            lock (_gate)
            {
                return _clients.Count;
            }
        }
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _running = true;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "chat-accept" };
        _acceptThread.Start();
        ServerLog?.Invoke($"Сервер слушает порт {_port}, история: {_history.Path}");
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
        }

        List<ClientHandler> handlers;
        lock (_gate)
        {
            handlers = _clients.Values.ToList();
            _clients.Clear();
        }

        foreach (var handler in handlers)
        {
            handler.Send(ChatMessage.Notice("Сервер остановлен."));
        }

        ServerLog?.Invoke("Сервер остановлен");
    }

    internal bool TryRegister(string nick, ClientHandler handler)
    {
        lock (_gate)
        {
            if (_clients.ContainsKey(nick))
            {
                return false;
            }

            _clients[nick] = handler;
            return true;
        }
    }

    internal void Unregister(ClientHandler handler)
    {
        lock (_gate)
        {
            if (handler.Nick.Length > 0 && _clients.TryGetValue(handler.Nick, out var existing) && ReferenceEquals(existing, handler))
            {
                _clients.Remove(handler.Nick);
            }
        }
    }

    internal bool SendTo(string nick, ChatMessage message)
    {
        ClientHandler? handler;
        lock (_gate)
        {
            _clients.TryGetValue(nick, out handler);
        }

        if (handler is null)
        {
            return false;
        }

        handler.Send(message);
        return true;
    }

    internal void Broadcast(ChatMessage message)
    {
        List<ClientHandler> snapshot;
        lock (_gate)
        {
            snapshot = _clients.Values.ToList();
        }

        foreach (var handler in snapshot)
        {
            handler.Send(message);
        }
    }

    internal void BroadcastUsers() => Broadcast(ChatMessage.Online(OnlineUsers));

    internal void BroadcastNotice(string text)
    {
        Broadcast(ChatMessage.Notice(text));
        ServerLog?.Invoke(text);
    }

    internal void Remember(ChatMessage message) => _history.Add(message);

    /// <summary>Записать строку в журнал сервера (событие поднимается только здесь).</summary>
    internal void Log(string message) => ServerLog?.Invoke(message);

    /// <summary>Отправить новому клиенту последние 20 сообщений.</summary>
    internal void SendHistory(ClientHandler handler)
    {
        foreach (var message in _history.Last(20))
        {
            handler.Send(ChatMessage.Say(message.Nick, message.Text, null, history: true));
        }
    }

    public void Dispose()
    {
        Stop();
        _listener.Stop();
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient tcp;
            try
            {
                tcp = _listener.AcceptTcpClient();
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            var handler = new ClientHandler(this, tcp);
            var thread = new Thread(handler.Run) { IsBackground = true, Name = "chat-client" };
            thread.Start();
        }
    }
}
