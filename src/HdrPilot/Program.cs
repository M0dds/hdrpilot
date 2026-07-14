using HdrPilot.Core;
using HdrPilot.UI;

namespace HdrPilot;

internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main(string[] args)
    {
        // Daten aus der Zeit vor der Umbenennung (HdrAutoSwitch -> HDR Pilot) übernehmen.
        MigrateLegacyData();

        // Nur eine Instanz zulassen.
        _singleInstance = new Mutex(initiallyOwned: true, "HdrPilot_SingleInstance_9F2A", out bool isNew);
        if (!isNew)
            return;

        // Globale Fehlerbehandlung: alles landet in %AppData%\HdrPilot\log.txt.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            Logger.Error("Unbehandelte UI-Ausnahme.", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("Unbehandelte Ausnahme.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error("Unbeobachtete Task-Ausnahme.", e.Exception);
            e.SetObserved();
        };

        Logger.Info($"HdrPilot {typeof(Program).Assembly.GetName().Version} gestartet " +
                    $"(Windows-Build {Environment.OSVersion.Version.Build}).");

        ApplicationConfiguration.Initialize();

        bool startHidden = args.Contains("--background", StringComparer.OrdinalIgnoreCase);

        using var app = new TrayApplicationContext(startHidden);
        Application.Run(app);

        Logger.Info("HdrPilot beendet.");
        GC.KeepAlive(_singleInstance);
    }

    /// <summary>
    /// Einmalige Migration von Installationen unter dem alten Namen "HdrAutoSwitch":
    /// verschiebt den AppData-Ordner (Konfiguration + Log) und ersetzt den
    /// Autostart-Eintrag. Best-Effort - Fehler dürfen den Start nicht verhindern.
    /// </summary>
    private static void MigrateLegacyData()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string oldDir = Path.Combine(appData, "HdrAutoSwitch");
            string newDir = Path.Combine(appData, "HdrPilot");
            if (Directory.Exists(oldDir))
            {
                // Dateiweise verschieben statt Directory.Move: robust, auch wenn
                // der neue Ordner bereits (teilweise) existiert.
                Directory.CreateDirectory(newDir);
                foreach (string src in Directory.GetFiles(oldDir))
                {
                    string dest = Path.Combine(newDir, Path.GetFileName(src));
                    if (!File.Exists(dest))
                        File.Move(src, dest);
                }
                Directory.Delete(oldDir, recursive: true);
            }
        }
        catch { /* Migration ist Best-Effort */ }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key?.GetValue("HdrAutoSwitch") is not null)
            {
                key.DeleteValue("HdrAutoSwitch", throwOnMissingValue: false);
                string exe = Environment.ProcessPath ?? "";
                if (exe.Length > 0)
                    key.SetValue("HdrPilot", $"\"{exe}\" --background");
            }
        }
        catch { /* Migration ist Best-Effort */ }
    }
}
