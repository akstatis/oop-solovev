using System.IO;

namespace PaintApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Можно открыть готовый рисунок сразу: PaintApp.exe рисунок.paint.json
        var initialPath = args.Length > 0 && File.Exists(args[0]) ? args[0] : null;
        Application.Run(new MainForm(initialPath));
    }
}
