namespace TodoApp.Core;

/// <summary>
/// Одна задача. Поля закрыты: снаружи их можно менять только методами,
/// а методы проверяют данные и не дают объекту стать некорректным.
/// Класс ничего не знает об окне и ничего не выводит на экран.
/// </summary>
public sealed class TodoItem
{
    private string _title = "";
    private bool _isDone;
    private Priority _priority = Priority.Normal;

    public TodoItem(string title, Priority priority = Priority.Normal)
    {
        Rename(title);
        SetPriority(priority);
    }

    public string Title => _title;

    public bool IsDone => _isDone;

    public Priority Priority => _priority;

    /// <summary>Переименовать задачу. Пустое название недопустимо.</summary>
    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Название задачи не может быть пустым.");
        }

        _title = title.Trim();
    }

    /// <summary>Сменить приоритет. Значение вне перечисления недопустимо.</summary>
    public void SetPriority(Priority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Неизвестный приоритет.");
        }

        _priority = priority;
    }

    public void Complete() => _isDone = true;

    public void Reopen() => _isDone = false;

    public override string ToString() =>
        $"{_title} ({PriorityNames.Of(_priority)}){(IsDone ? " — выполнено" : "")}";
}
