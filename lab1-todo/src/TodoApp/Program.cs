using TodoApp.Core;

namespace TodoApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var list = new TodoList();
        Application.Run(new MainForm(list, TodoStorage.DefaultFilePath));
    }
}
