namespace HdrAutoSwitch.Core;

/// <summary>
/// Einfaches, robustes Datei-Logging nach %AppData%\HdrAutoSwitch\log.txt.
/// Threadsicher; rotiert bei ~1 MB nach log.old.txt. Logging-Fehler werden
/// verschluckt, damit sie nie die eigentliche Funktion stören.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HdrAutoSwitch", "log.txt");

    private const long MaxSizeBytes = 1_000_000;

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                string dir = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(dir);

                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > MaxSizeBytes)
                {
                    string old = Path.Combine(dir, "log.old.txt");
                    File.Copy(LogPath, old, overwrite: true);
                    File.Delete(LogPath);
                }

                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging darf niemals die App zum Absturz bringen.
        }
    }
}
