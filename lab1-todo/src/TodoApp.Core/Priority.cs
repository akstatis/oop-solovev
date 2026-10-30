namespace TodoApp.Core;

/// <summary>Важность задачи. Хранится в JSON как строка.</summary>
public enum Priority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

/// <summary>Подписи приоритетов для окна. Модель не знает, как их рисуют.</summary>
public static class PriorityNames
{
    public static string Of(Priority priority) => priority switch
    {
        Priority.Low => "Низкий",
        Priority.High => "Высокий",
        _ => "Обычный",
    };

    public static IReadOnlyList<Priority> All { get; } =
        new[] { Priority.Low, Priority.Normal, Priority.High };
}
