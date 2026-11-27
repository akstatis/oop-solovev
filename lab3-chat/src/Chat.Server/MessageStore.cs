using System.Text;
using Chat.Common;

namespace Chat.Server;

/// <summary>
/// История сообщений: держит последние N в памяти и дописывает каждое в файл,
/// чтобы история пережила перезапуск сервера. Один объект — один файл.
/// </summary>
public sealed class MessageStore
{
    private readonly object _gate = new();
    private readonly List<ChatMessage> _items = new();
    private readonly string _path;
    private readonly int _capacity;

    public MessageStore(string path, int capacity = 200)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Не указан путь к файлу истории.", nameof(path));
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Ёмкость истории должна быть положительной.");
        }

        _path = path;
        _capacity = capacity;
        Load();
    }

    public string Path => _path;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>Добавить сообщение в память и дописать строку в файл.</summary>
    public void Add(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            _items.Add(message);
            if (_items.Count > _capacity)
            {
                _items.RemoveRange(0, _items.Count - _capacity);
            }

            try
            {
                var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.AppendAllText(_path, message.ToJson() + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
                // История — вспомогательная вещь: не смогли записать, чат продолжает работать.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Последние сообщения, старые раньше новых.</summary>
    public IReadOnlyList<ChatMessage> Last(int count)
    {
        if (count <= 0)
        {
            return Array.Empty<ChatMessage>();
        }

        lock (_gate)
        {
            return _items.Skip(Math.Max(0, _items.Count - count)).ToList();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            foreach (var line in File.ReadLines(_path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var message = ChatMessage.FromJson(line);
                    if (message.Type == MessageTypes.Text && !message.IsPrivate)
                    {
                        _items.Add(message);
                    }
                }
                catch (InvalidDataException)
                {
                    // Битую строку пропускаем: файл мог обрезаться при падении.
                }
            }

            if (_items.Count > _capacity)
            {
                _items.RemoveRange(0, _items.Count - _capacity);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
