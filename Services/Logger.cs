namespace SqwizzeySwitch.Services;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SqwizzeySwitch", "log.txt");

    private static readonly object _sync = new();

    public static void Log(string message)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

                // Rotate log when it exceeds 512 KB
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > 512 * 1024)
                    File.WriteAllText(LogPath, string.Empty);

                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { /* logging must never throw */ }
    }

    public static void Log(Exception ex, string context = "")
    {
        Log($"ERROR {context}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}
