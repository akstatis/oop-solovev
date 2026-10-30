namespace TodoApp.Core;

/// <summary>
/// Где лежит файл задач. Отдельный класс, чтобы путь не был зашит в окне.
/// Основное место — папка пользователя; если она недоступна для записи
/// (бывает на чужих или защищённых машинах), файл кладётся рядом с программой.
/// </summary>
public static class TodoStorage
{
    private const string FolderName = "OopTodo";
    private const string FileName = "todos.json";

    private static readonly Lazy<string> ResolvedPath = new(Resolve);

    /// <summary>Путь к файлу задач, вычисленный один раз при первом обращении.</summary>
    public static string DefaultFilePath => ResolvedPath.Value;

    private static string Resolve()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var preferred = Path.Combine(roaming, FolderName, FileName);
        var folder = Path.GetDirectoryName(preferred);

        if (!string.IsNullOrEmpty(folder) && CanWriteTo(folder))
        {
            return preferred;
        }

        return Path.Combine(AppContext.BaseDirectory, FileName);
    }

    /// <summary>Проверка «сюда можно писать»: создать папку и временный файл.</summary>
    public static bool CanWriteTo(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
