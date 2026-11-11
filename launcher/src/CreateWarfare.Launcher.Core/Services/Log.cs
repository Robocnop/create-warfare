namespace CreateWarfare.Launcher.Core.Services;

/// <summary>%LOCALAPPDATA%\CreateWarfareLauncher\logs\launcher-&lt;date&gt;.log</summary>
public static class Log
{
    static readonly object Gate = new();

    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CreateWarfareLauncher");

    public static string LogDir => Path.Combine(AppDataDir, "logs");

    public static void Info(string msg) => Write("INFO", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : $"{msg}\n{ex}");

    static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, $"launcher-{DateTime.Now:yyyy-MM-dd}.log"),
                    $"{DateTime.Now:HH:mm:ss} [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Supprime les journaux de plus de <paramref name="days"/> jours. Ne lève jamais d'exception.</summary>
    public static void Prune(int days = 14)
    {
        try
        {
            if (!Directory.Exists(LogDir)) return;
            var limit = DateTime.Now.AddDays(-days);
            foreach (var f in Directory.EnumerateFiles(LogDir, "launcher-*.log"))
                if (File.GetLastWriteTime(f) < limit) File.Delete(f);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
