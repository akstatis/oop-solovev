namespace TodoApp.Core;

/// <summary>Что показывать в списке.</summary>
public enum TodoFilter
{
    All = 0,
    Active = 1,
    Done = 2,
}

/// <summary>Как упорядочить список.</summary>
public enum TodoSort
{
    Manual = 0,
    Title = 1,
    Priority = 2,
    Status = 3,
}

/// <summary>
/// Список задач: хранит задачи и правила работы с ними.
/// Добавить, удалить, найти, отфильтровать, отсортировать, сохранить, загрузить.
/// Об окне класс не знает — поэтому его можно проверить без интерфейса.
/// </summary>
public sealed class TodoList
{
    private readonly List<TodoItem> _items = new();

    public IReadOnlyList<TodoItem> Items => _items;

    public int Count => _items.Count;

    public int DoneCount
    {
        get
        {
            var done = 0;
            foreach (var item in _items)
            {
                if (item.IsDone)
                {
                    done++;
                }
            }

            return done;
        }
    }

    public int ActiveCount => Count - DoneCount;

    /// <summary>Добавить задачу. Название проверяет <see cref="TodoItem"/>.</summary>
    public TodoItem Add(string title, Priority priority = Priority.Normal)
    {
        var item = new TodoItem(title, priority);
        _items.Add(item);
        return item;
    }

    /// <summary>Удалить задачу из списка.</summary>
    public void Remove(TodoItem item)
    {
        EnsureContains(item);
        _items.Remove(item);
    }

    public void Rename(TodoItem item, string title)
    {
        EnsureContains(item);
        item.Rename(title);
    }

    public void Complete(TodoItem item)
    {
        EnsureContains(item);
        item.Complete();
    }

    public void Reopen(TodoItem item)
    {
        EnsureContains(item);
        item.Reopen();
    }

    public void SetPriority(TodoItem item, Priority priority)
    {
        EnsureContains(item);
        item.SetPriority(priority);
    }

    /// <summary>Найти задачи по части названия. Пустой запрос возвращает все задачи.</summary>
    public IReadOnlyList<TodoItem> Find(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _items.ToArray();
        }

        var needle = query.Trim();
        var found = new List<TodoItem>();
        foreach (var item in _items)
        {
            if (item.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase))
            {
                found.Add(item);
            }
        }

        return found;
    }

    /// <summary>Оставить только нужные задачи: все, активные или выполненные.</summary>
    public IReadOnlyList<TodoItem> Filter(TodoFilter filter) => filter switch
    {
        TodoFilter.Active => _items.Where(item => !item.IsDone).ToArray(),
        TodoFilter.Done => _items.Where(item => item.IsDone).ToArray(),
        TodoFilter.All => _items.ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Неизвестный фильтр."),
    };

    /// <summary>Отсортировать переданные задачи. Исходный список не меняется.</summary>
    public IReadOnlyList<TodoItem> Sort(IEnumerable<TodoItem> items, TodoSort sort)
    {
        ArgumentNullException.ThrowIfNull(items);

        IEnumerable<TodoItem> ordered = sort switch
        {
            TodoSort.Manual => items,
            TodoSort.Title => items.OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            TodoSort.Priority => items
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            TodoSort.Status => items
                .OrderBy(item => item.IsDone)
                .ThenByDescending(item => item.Priority)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Неизвестная сортировка."),
        };

        return ordered.ToArray();
    }

    /// <summary>Что показать в окне: фильтр → поиск → сортировка.</summary>
    public IReadOnlyList<TodoItem> BuildView(TodoFilter filter, TodoSort sort, string? query)
    {
        var needle = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var matched = new List<TodoItem>();
        foreach (var item in _items)
        {
            if (!MatchesFilter(item, filter))
            {
                continue;
            }

            if (needle is not null && !item.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            matched.Add(item);
        }

        return Sort(matched, sort);
    }

    private static bool MatchesFilter(TodoItem item, TodoFilter filter) => filter switch
    {
        TodoFilter.All => true,
        TodoFilter.Active => !item.IsDone,
        TodoFilter.Done => item.IsDone,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Неизвестный фильтр."),
    };

    public void Clear() => _items.Clear();

    /// <summary>Сохранить задачи в JSON. Папку создаёт сам.</summary>
    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Не указан путь к файлу.", nameof(path));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = _items.Select(item => new TodoItemData
        {
            Title = item.Title,
            IsDone = item.IsDone,
            Priority = item.Priority,
        });

        var json = System.Text.Json.JsonSerializer.Serialize(data, JsonSettings);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Загрузить задачи из JSON. Файла нет — список остаётся пустым.
    /// Битый файл — исключение, чтобы окно сообщило о проблеме и не потеряло данные.
    /// </summary>
    public void Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Не указан путь к файлу.", nameof(path));
        }

        if (!File.Exists(path))
        {
            _items.Clear();
            return;
        }

        List<TodoItemData>? data;
        try
        {
            var json = File.ReadAllText(path);
            data = System.Text.Json.JsonSerializer.Deserialize<List<TodoItemData>>(json, JsonSettings);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidDataException($"Файл задач повреждён: {Path.GetFileName(path)}.", ex);
        }

        var loaded = new List<TodoItem>();
        foreach (var record in data ?? new List<TodoItemData>())
        {
            if (string.IsNullOrWhiteSpace(record.Title))
            {
                continue;
            }

            var priority = Enum.IsDefined(record.Priority) ? record.Priority : Priority.Normal;
            var item = new TodoItem(record.Title, priority);
            if (record.IsDone)
            {
                item.Complete();
            }

            loaded.Add(item);
        }

        _items.Clear();
        _items.AddRange(loaded);
    }

    private void EnsureContains(TodoItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_items.Contains(item))
        {
            throw new InvalidOperationException("Задача не найдена в списке.");
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonSettings = new()
    {
        WriteIndented = true,
        // Русские буквы пишем как есть, без \u0421: файл должен читаться глазами.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>Форма записи задачи в файле. Отдельная от модели, чтобы поля остались закрытыми.</summary>
    private sealed class TodoItemData
    {
        public string Title { get; set; } = "";

        public bool IsDone { get; set; }

        public Priority Priority { get; set; }
    }
}
