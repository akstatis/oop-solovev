using System.Text;
using TodoApp.Core;

namespace TodoApp.Core.Checks;

/// <summary>
/// Проверки модели без окна и без NuGet: обычная консольная программа.
/// Запуск: dotnet run --project tests/CoreChecks
/// </summary>
internal static class Program
{
    private static int _failed;
    private static int _passed;

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        CheckItemRules();
        CheckListOperations();
        CheckFilterSortSearch();
        CheckPersistence();
        CheckCorruptedFile();

        Console.WriteLine();
        Console.WriteLine($"Пройдено: {_passed}, провалено: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void CheckItemRules()
    {
        Section("TodoItem: конструктор, инкапсуляция, исключения");

        Throws<ArgumentException>(() => new TodoItem(""), "пустое название отклонено");
        Throws<ArgumentException>(() => new TodoItem("   "), "название из пробелов отклонено");
        Throws<ArgumentOutOfRangeException>(() => new TodoItem("x", (Priority)42), "неизвестный приоритет отклонён");

        var item = new TodoItem("  Купить хлеб  ", Priority.Low);
        Equal("Купить хлеб", item.Title, "название обрезано по краям");
        Equal(Priority.Low, item.Priority, "приоритет сохранён");
        False(item.IsDone, "новая задача активна");

        item.Complete();
        True(item.IsDone, "Complete помечает выполненной");
        item.Reopen();
        False(item.IsDone, "Reopen возвращает в работу");

        Throws<ArgumentException>(() => item.Rename(" "), "Rename не принимает пустое имя");
        Equal("Купить хлеб", item.Title, "после неудачного Rename название прежнее");

        item.Rename("Купить молоко");
        Equal("Купить молоко", item.Title, "Rename меняет название");

        var second = new TodoItem("Вторая");
        second.Complete();
        False(item.IsDone, "this: выполнение второй задачи не трогает первую");
    }

    private static void CheckListOperations()
    {
        Section("TodoList: добавление, удаление, изменение");

        var list = new TodoList();
        Equal(0, list.Count, "новый список пуст");

        var a = list.Add("Первая", Priority.Normal);
        var b = list.Add("Вторая", Priority.High);
        Equal(2, list.Count, "две задачи добавлены");
        Equal(2, list.ActiveCount, "обе активны");

        list.Complete(a);
        Equal(1, list.DoneCount, "одна выполнена");
        Equal(1, list.ActiveCount, "одна активна");

        list.SetPriority(a, Priority.Low);
        Equal(Priority.Low, a.Priority, "приоритет изменён через список");

        list.Rename(b, "Вторая задача");
        Equal("Вторая задача", b.Title, "задача переименована через список");

        Throws<ArgumentException>(() => list.Add(""), "добавление пустой задачи запрещено");
        Equal(2, list.Count, "неудачное добавление не изменило список");

        var stranger = new TodoItem("Чужая");
        Throws<ArgumentNullException>(() => list.Remove(null!), "Remove(null) отклонён");
        Throws<InvalidOperationException>(() => list.Remove(stranger), "удаление чужой задачи отклонено");
        Throws<InvalidOperationException>(() => list.Complete(stranger), "выполнение чужой задачи отклонено");
        Equal(2, list.Count, "список не изменился после ошибочных вызовов");

        list.Remove(a);
        Equal(1, list.Count, "задача удалена");
    }

    private static void CheckFilterSortSearch()
    {
        Section("TodoList: фильтр, сортировка, поиск");

        var list = new TodoList();
        var milk = list.Add("Молоко", Priority.Low);
        var bread = list.Add("Хлеб", Priority.High);
        var tea = list.Add("Чай", Priority.Normal);
        list.Complete(tea);

        Equal(3, list.Filter(TodoFilter.All).Count, "фильтр «все»");
        Equal(2, list.Filter(TodoFilter.Active).Count, "фильтр «активные»");
        Equal(1, list.Filter(TodoFilter.Done).Count, "фильтр «выполненные»");
        Throws<ArgumentOutOfRangeException>(() => list.Filter((TodoFilter)99), "неизвестный фильтр отклонён");

        var byPriority = list.Sort(list.Items, TodoSort.Priority);
        Equal(bread, byPriority[0], "высокий приоритет первым");
        Equal(milk, byPriority[^1], "низкий приоритет последним");

        var byTitle = list.Sort(list.Items, TodoSort.Title);
        Equal("Молоко", byTitle[0].Title, "сортировка по названию");

        var byStatus = list.Sort(list.Items, TodoSort.Status);
        False(byStatus[0].IsDone, "сначала активные при сортировке по статусу");
        True(byStatus[^1].IsDone, "выполненные в конце");

        Equal(1, list.Find("хлеб").Count, "поиск без учёта регистра");
        Equal(3, list.Find("").Count, "пустой поиск возвращает всё");
        Equal(3, list.Find(null).Count, "null в поиске возвращает всё");

        var view = list.BuildView(TodoFilter.Active, TodoSort.Priority, "л");
        Equal(2, view.Count, "вид: активные, содержащие «л» (Молоко и Хлеб)");
        Equal(bread, view[0], "вид отсортирован по приоритету");
    }

    private static void CheckPersistence()
    {
        Section("Сохранение и загрузка JSON");

        var directory = Path.Combine(Path.GetTempPath(), "todo-checks-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "todos.json");

        try
        {
            var list = new TodoList();
            list.Load(path);
            Equal(0, list.Count, "нет файла — пустой список");

            list.Add("Позвонить маме", Priority.High);
            list.Add("Выучить C#", Priority.Normal);
            var done = list.Add("Помыть посуду", Priority.Low);
            list.Complete(done);
            list.Save(path);

            True(File.Exists(path), "файл создан вместе с папкой");

            var restored = new TodoList();
            restored.Load(path);
            Equal(3, restored.Count, "загружены все задачи");
            Equal("Позвонить маме", restored.Items[0].Title, "название сохранилось");
            Equal(Priority.High, restored.Items[0].Priority, "приоритет сохранился");
            True(restored.Items[2].IsDone, "отметка выполнения сохранилась");
            False(restored.Items[1].IsDone, "активная задача осталась активной");

            restored.Add("Новая", Priority.Low);
            restored.Save(path);
            var again = new TodoList();
            again.Load(path);
            Equal(4, again.Count, "перезапись файла работает");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void CheckCorruptedFile()
    {
        Section("Повреждённый файл");

        var path = Path.Combine(Path.GetTempPath(), "todo-broken-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ это не json ");
            var list = new TodoList();
            Throws<InvalidDataException>(() => list.Load(path), "битый JSON даёт понятную ошибку");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
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
