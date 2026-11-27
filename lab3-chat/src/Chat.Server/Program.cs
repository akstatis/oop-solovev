using Chat.Server;

namespace Chat.Server;

/// <summary>
/// Консольный сервер: dotnet run --project src/Chat.Server -- --port 5000
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var port = ReadInt(args, "--port", 5000);
        var historyPath = ReadValue(args, "--history", Path.Combine(AppContext.BaseDirectory, "history.jsonl"));

        using var server = new ChatServer(port, historyPath);
        server.ServerLog += line => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {line}");

        server.Start();
        Console.WriteLine("Сервер чата запущен. Ctrl+C — остановить.");

        var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Set();
        };

        stop.Wait();
        server.Stop();
    }

    private static int ReadInt(string[] args, string name, int fallback) =>
        int.TryParse(ReadValue(args, name, ""), out var value) ? value : fallback;

    private static string ReadValue(string[] args, string name, string fallback)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return fallback;
    }
}
