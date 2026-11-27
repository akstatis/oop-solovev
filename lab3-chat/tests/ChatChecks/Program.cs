using System.Diagnostics;
using Chat.Client;
using Chat.Common;
using Chat.Server;

namespace ChatChecks;

/// <summary>
/// Проверки чата без окон: протокол, правила ника, история и настоящий обмен
/// сообщениями между сервером и клиентами по TCP.
/// Запуск: dotnet run --project tests/ChatChecks
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        CheckProtocol();
        CheckNicknames();
        CheckStore();
        CheckTcpChat();
        CheckHistoryAfterRestart();
        CheckUnreachableServer();

        Console.WriteLine();
        Console.WriteLine($"Пройдено: {_passed}, провалено: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void CheckProtocol()
    {
        Section("Протокол: ChatMessage и JSON");

        var message = ChatMessage.Say("вася", "привет, мир", target: null);
        var json = message.ToJson();
        True(json.Contains("\"type\":\"text\""), "в JSON есть поле type");
        True(json.Contains("\"nick\":\"вася\""), "кириллица в JSON не экранируется");
        True(json.Contains("\"привет, мир\""), "текст сохранён как есть");
        False(json.Contains("\"target\""), "пустой адресат не пишется в JSON");

        var restored = ChatMessage.FromJson(json);
        Equal("text", restored.Type, "тип восстановлен");
        Equal("вася", restored.Nick, "ник восстановлен");
        Equal("привет, мир", restored.Text, "текст восстановлен");

        var privateMessage = ChatMessage.Say("вася", "только тебе", target: "петя");
        var privateJson = privateMessage.ToJson();
        True(privateJson.Contains("\"target\":\"петя\""), "адресат личного сообщения в JSON");
        True(ChatMessage.FromJson(privateJson).IsPrivate, "личное сообщение распознаётся");

        var users = ChatMessage.Online(new[] { "аня", "вася" });
        var usersJson = users.ToJson();
        var usersBack = ChatMessage.FromJson(usersJson);
        Equal(2, usersBack.Users?.Count ?? 0, "список участников пережил JSON");
        Equal("аня", usersBack.Users?[0], "порядок участников сохранён");

        Throws<InvalidDataException>(() => ChatMessage.FromJson("{ это не json"), "битый JSON отклонён");
        Throws<InvalidDataException>(() => ChatMessage.FromJson("{\"nick\":\"вася\"}"), "сообщение без типа отклонено");
        Throws<InvalidDataException>(() => ChatMessage.FromJson(""), "пустая строка отклонена");

        var login = ChatMessage.Login("вася");
        Equal("login", login.Type, "тип входа");
        Equal("login-ok", ChatMessage.LoginOk("вася").Type, "ответ на вход");
        Equal("login-error", ChatMessage.LoginError("занято").Type, "отказ во входе");
        Equal("notice", ChatMessage.Notice("вошёл").Type, "уведомление");
    }

    private static void CheckNicknames()
    {
        Section("Правила ника");

        True(Nicknames.IsValid("вася"), "обычный ник");
        True(Nicknames.IsValid("  Матвей  "), "пробелы по краям обрезаются");
        Equal("Матвей", Nicknames.Normalize("  Матвей  "), "Normalize убирает пробелы");
        True(Nicknames.IsValid("user_1-test"), "буквы, цифры, дефис и подчёркивание");
        False(Nicknames.IsValid(""), "пустой ник не годится");
        False(Nicknames.IsValid("   "), "пробелы вместо ника не годятся");
        False(Nicknames.IsValid(new string('я', Nicknames.MaxLength + 1)), "слишком длинный ник");
        True(Nicknames.IsValid(new string('я', Nicknames.MaxLength)), "ник ровно по длине");

        Nicknames.IsValid("", out var error);
        True(error.Contains("пустым"), "у отказа есть понятная причина");
    }

    private static void CheckStore()
    {
        Section("История: память и файл");

        var path = TempFile("store");
        try
        {
            var store = new MessageStore(path, capacity: 30);
            for (var i = 1; i <= 25; i++)
            {
                store.Add(ChatMessage.Say("аня", $"сообщение {i}"));
            }

            var last = store.Last(20);
            Equal(20, last.Count, "в память помещаются последние 20");
            Equal("сообщение 6", last[0].Text, "первое из последних 20");
            Equal("сообщение 25", last[^1].Text, "последнее сообщение");

            var reloaded = new MessageStore(path, capacity: 30);
            Equal(20, reloaded.Last(20).Count, "история пережила перезапуск");
            Equal("сообщение 6", reloaded.Last(20)[0].Text, "порядок сохранился");

            True(File.Exists(path), "файл истории создан");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static void CheckTcpChat()
    {
        Section("Сервер и клиенты: настоящий обмен по TCP");

        var history = TempFile("chat");
        ChatServer? server = null;
        try
        {
            server = new ChatServer(0, history);
            server.Start();
            True(server.Port > 0, "сервер занял порт");

            using var anna = new ChatClient();
            var annaInbox = new Inbox(anna);
            var annaLogin = anna.Connect("127.0.0.1", server.Port, "Аня", TimeSpan.FromSeconds(5));
            True(annaLogin.Success, "первый клиент вошёл");
            Equal("Аня", annaLogin.Nick, "ник подтверждён сервером");

            using var boris = new ChatClient();
            var borisInbox = new Inbox(boris);
            var borisLogin = boris.Connect("127.0.0.1", server.Port, "Борис", TimeSpan.FromSeconds(5));
            True(borisLogin.Success, "второй клиент вошёл");

            True(WaitUntil(() => annaInbox.Has(m => m.Type == MessageTypes.Notice && m.Text.Contains("Борис"))), "уведомление о входе пришло");
            True(WaitUntil(() => annaInbox.Has(m => m.Type == MessageTypes.Users && (m.Users?.Count ?? 0) == 2)), "список участников обновился");
            True(WaitUntil(() => borisInbox.Has(m => m.Type == MessageTypes.Users && (m.Users?.Count ?? 0) == 2)), "новый участник тоже видит список");
            Equal(2, server.OnlineCount, "сервер считает участников");

            anna.SendText("привет всем");
            True(WaitUntil(() => borisInbox.Has(m => m.Text == "привет всем" && !m.IsPrivate)), "сообщение дошло до второго клиента");
            True(WaitUntil(() => annaInbox.Has(m => m.Text == "привет всем")), "отправитель тоже видит сообщение");

            using var vera = new ChatClient();
            var veraInbox = new Inbox(vera);
            True(vera.Connect("127.0.0.1", server.Port, "Вера", TimeSpan.FromSeconds(5)).Success, "третий клиент вошёл");
            WaitUntil(() => veraInbox.Has(m => m.Type == MessageTypes.Users && (m.Users?.Count ?? 0) == 3));

            anna.SendText("только для Бориса", "Борис");            True(WaitUntil(() => borisInbox.Has(m => m.Text == "только для Бориса" && m.Target == "Борис")), "личное сообщение дошло до адресата");
            True(WaitUntil(() => annaInbox.Has(m => m.Text == "только для Бориса")), "отправитель видит личное сообщение");
            System.Threading.Thread.Sleep(300);
            False(veraInbox.Has(m => m.Text == "только для Бориса"), "третий клиент личного сообщения не получил");

            var duplicate = new ChatClient();
            var duplicateResult = duplicate.Connect("127.0.0.1", server.Port, "Аня", TimeSpan.FromSeconds(5));
            False(duplicateResult.Success, "повторный ник не пускают");
            True(duplicateResult.Error.Contains("занят"), "в отказе сказано, что ник занят");
            duplicate.Dispose();
            Equal(3, server.OnlineCount, "отказ не добавил клиента");

            boris.Dispose();
            True(WaitUntil(() => annaInbox.Has(m => m.Type == MessageTypes.Notice && m.Text.Contains("Борис") && m.Text.Contains("вышел"))), "уведомление о выходе пришло");
            True(WaitUntil(() => server.OnlineCount == 2), "сервер убрал отключившегося");

            anna.SendText("после выхода Бориса");
            True(WaitUntil(() => veraInbox.Has(m => m.Text == "после выхода Бориса")), "сервер работает после отключения клиента");

            anna.RequestUsers();
            True(
                WaitUntil(() => annaInbox.Has(m => m.Type == MessageTypes.Users && (m.Users?.Count ?? 0) == 2 && m.Users!.Contains("Аня") && !m.Users.Contains("Борис"))),
                "по запросу клиента сервер присылает точный список участников");
        }
        finally
        {
            server?.Dispose();
            Cleanup(history);
        }
    }

    private static void CheckHistoryAfterRestart()
    {
        Section("История: сервер запомнил сообщения после перезапуска");

        var history = TempFile("restart");
        try
        {
            var first = new ChatServer(0, history);
            first.Start();
            using (var anna = new ChatClient())
            {
                var inbox = new Inbox(anna);
                True(anna.Connect("127.0.0.1", first.Port, "Аня", TimeSpan.FromSeconds(5)).Success, "клиент вошёл в первый сервер");
                anna.SendText("сообщение до перезапуска");
                WaitUntil(() => inbox.Has(m => m.Text == "сообщение до перезапуска"));
            }

            first.Dispose();
            System.Threading.Thread.Sleep(200);

            var second = new ChatServer(0, history);
            second.Start();
            using var boris = new ChatClient();
            var borisInbox = new Inbox(boris);
            True(boris.Connect("127.0.0.1", second.Port, "Борис", TimeSpan.FromSeconds(5)).Success, "клиент вошёл во второй сервер");
            True(WaitUntil(() => borisInbox.Has(m => m.History && m.Text == "сообщение до перезапуска")), "новый клиент получил историю");
            True(WaitUntil(() => borisInbox.Has(m => m.History && m.Nick == "Аня")), "в истории есть автор сообщения");
            second.Dispose();
        }
        finally
        {
            Cleanup(history);
        }
    }

    private static void CheckUnreachableServer()
    {
        Section("Недоступный сервер");

        using var client = new ChatClient();
        var result = client.Connect("127.0.0.1", 1, "Аня", TimeSpan.FromSeconds(2));
        False(result.Success, "вход не удался");
        True(result.Error.Length > 0, "есть текст ошибки для окна");
        False(client.IsConnected, "клиент не считается подключённым");
    }

    /// <summary>Копилка входящих сообщений: события приходят из фонового потока.</summary>
    private sealed class Inbox
    {
        private readonly List<ChatMessage> _messages = new();
        private readonly object _gate = new();

        public Inbox(ChatClient client)
        {
            client.MessageReceived += message =>
            {
                lock (_gate)
                {
                    _messages.Add(message);
                }
            };
        }

        public bool Has(Func<ChatMessage, bool> predicate)
        {
            lock (_gate)
            {
                return _messages.Any(predicate);
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _messages.Count;
                }
            }
        }
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            System.Threading.Thread.Sleep(25);
        }

        return condition();
    }

    private static string TempFile(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"chat-{prefix}-{Guid.NewGuid():N}.jsonl");

    private static int TempFileCount(string path) => File.Exists(path) ? 1 : 0;

    private static void Cleanup(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
    }

    private static void True(bool condition, string description)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("  OK   " + description);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL " + description);
        }
    }

    private static void False(bool condition, string description) => True(!condition, description);

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            _passed++;
            Console.WriteLine("  OK   " + description);
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL {description}: ожидалось «{expected}», получено «{actual}»");
        }
    }

    private static void Throws<TException>(Action action, string description)
        where TException : Exception
    {
        try
        {
            action();
            _failed++;
            Console.WriteLine($"  FAIL {description}: исключения {typeof(TException).Name} не было");
        }
        catch (TException)
        {
            _passed++;
            Console.WriteLine("  OK   " + description);
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {description}: ожидалось {typeof(TException).Name}, получено {ex.GetType().Name}");
        }
    }
}
