using System.Net.Sockets;
using System.Text;
using Chat.Common;

namespace Chat.Client;

/// <summary>Результат входа: либо ник принят, либо причина отказа.</summary>
public sealed class LoginResult
{
    public bool Success { get; private init; }

    public string Nick { get; private init; } = "";

    public string Error { get; private init; } = "";

    public static LoginResult Ok(string nick) => new() { Success = true, Nick = nick };

    public static LoginResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Сетевой клиент: подключается, отправляет сообщения и читает сеть в фоновом
/// потоке. Про окно ничего не знает — отдаёт сообщения через события.
/// </summary>
public sealed class ChatClient : IDisposable
{
    private readonly object _writeGate = new();

    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Thread? _readThread;
    private volatile bool _running;
    private int _disconnectedRaised;

    private readonly object _subscriberGate = new();
    private readonly List<ChatMessage> _pending = new();
    private Action<ChatMessage>? _messageHandlers;

    /// <summary>
    /// Пришло сообщение от сервера. Вызывается из фонового потока.
    /// Сообщения, пришедшие до подписки окна, не теряются: они копятся и
    /// отдаются сразу при подписке (важно для истории, которая идёт сразу после входа).
    /// </summary>
    public event Action<ChatMessage>? MessageReceived
    {
        add
        {
            var handler = value;
            if (handler is null)
            {
                return;
            }

            List<ChatMessage> buffered;
            lock (_subscriberGate)
            {
                _messageHandlers += handler;
                buffered = _pending.ToList();
                _pending.Clear();
            }

            foreach (var message in buffered)
            {
                handler(message);
            }
        }

        remove
        {
            var handler = value;
            if (handler is null)
            {
                return;
            }

            lock (_subscriberGate)
            {
                _messageHandlers -= handler;
            }
        }
    }

    /// <summary>Соединение закрылось (сервер упал, сеть пропала).</summary>
    public event Action<string>? Disconnected;

    public string Nick { get; private set; } = "";

    public bool IsConnected => _running && _tcp?.Connected == true;

    /// <summary>Подключиться и войти под ником. Ждёт ответ сервера не дольше timeout.</summary>
    public LoginResult Connect(string host, int port, string nick, TimeSpan timeout)
    {
        Disconnect();

        var normalized = Nicknames.Normalize(nick);
        if (!Nicknames.IsValid(normalized, out var error))
        {
            return LoginResult.Fail(error);
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return LoginResult.Fail("Не указан адрес сервера.");
        }

        TcpClient? tcp = null;
        try
        {
            tcp = new TcpClient();
            var connecting = tcp.ConnectAsync(host.Trim(), port);
            if (!connecting.Wait(timeout))
            {
                tcp.Dispose();
                return LoginResult.Fail($"Сервер {host}:{port} не отвечает.");
            }

            if (connecting.IsFaulted)
            {
                tcp.Dispose();
                var reason = connecting.Exception?.InnerException?.Message ?? "не удалось подключиться";
                return LoginResult.Fail($"Не удалось подключиться к {host}:{port}: {reason}");
            }

            var stream = tcp.GetStream();
            stream.ReadTimeout = (int)Math.Max(1000, timeout.TotalMilliseconds);
            _tcp = tcp;
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

            Send(ChatMessage.Login(normalized));

            var line = _reader.ReadLine();
            if (line is null)
            {
                Disconnect();
                return LoginResult.Fail("Сервер закрыл соединение во время входа.");
            }

            var reply = ChatMessage.FromJson(line);
            if (reply.Type == MessageTypes.LoginError)
            {
                Disconnect();
                return LoginResult.Fail(string.IsNullOrWhiteSpace(reply.Text) ? "Сервер отказал во входе." : reply.Text);
            }

            if (reply.Type != MessageTypes.LoginOk)
            {
                Disconnect();
                return LoginResult.Fail("Неожиданный ответ сервера на вход.");
            }

            Nick = reply.Nick;
            stream.ReadTimeout = Timeout.Infinite;
            Interlocked.Exchange(ref _disconnectedRaised, 0);
            _running = true;
            _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "chat-read" };
            _readThread.Start();
            return LoginResult.Ok(Nick);
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException or AggregateException or ArgumentException)
        {
            tcp?.Dispose();
            Disconnect();
            return LoginResult.Fail($"Не удалось войти: {ex.Message}");
        }
    }

    public void SendText(string text, string? target = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Send(ChatMessage.Say(Nick, text.Trim(), string.IsNullOrWhiteSpace(target) ? null : target));
    }

    public void Send(ChatMessage message)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Клиент не подключён.");
        }

        lock (_writeGate)
        {
            _writer.WriteLine(message.ToJson());
        }
    }

    /// <summary>Попросить у сервера актуальный список участников.</summary>
    public void RequestUsers() => Send(new ChatMessage { Type = MessageTypes.Who, Nick = Nick, Time = DateTime.Now });

    public void Disconnect()
    {
        _running = false;

        try
        {
            _tcp?.Close();
        }
        catch (IOException)
        {
        }

        _tcp?.Dispose();
        _tcp = null;
        _reader = null;
        _writer = null;
    }

    /// <summary>Отдать сообщение подписчикам или придержать до подписки.</summary>
    private void Deliver(ChatMessage message)
    {
        Action<ChatMessage>? handlers;
        lock (_subscriberGate)
        {
            handlers = _messageHandlers;
            if (handlers is null)
            {
                if (_pending.Count < 500)
                {
                    _pending.Add(message);
                }

                return;
            }
        }

        handlers(message);
    }

    private void ReadLoop()
    {
        var reason = "Соединение закрыто.";
        try
        {
            while (_running)
            {
                var line = _reader?.ReadLine();
                if (line is null)
                {
                    break;
                }

                ChatMessage message;
                try
                {
                    message = ChatMessage.FromJson(line);
                }
                catch (InvalidDataException)
                {
                    continue;
                }

                Deliver(message);            }
        }
        catch (IOException)
        {
            reason = "Связь с сервером потеряна.";
        }
        catch (ObjectDisposedException)
        {
            reason = "Соединение закрыто.";
        }
        catch (SocketException)
        {
            reason = "Связь с сервером потеряна.";
        }
        finally
        {
            var wasRunning = _running;
            _running = false;

            if (wasRunning && Interlocked.Exchange(ref _disconnectedRaised, 1) == 0)
            {
                Disconnected?.Invoke(reason);
            }
        }
    }

    public void Dispose() => Disconnect();
}
