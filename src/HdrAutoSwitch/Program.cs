using HdrAutoSwitch.Core;
using HdrAutoSwitch.UI;

namespace HdrAutoSwitch;

internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main(string[] args)
    {
        // Nur eine Instanz zulassen.
        _singleInstance = new Mutex(initiallyOwned: true, "HdrAutoSwitch_SingleInstance_9F2A", out bool isNew);
        if (!isNew)
            return;

        // Globale Fehlerbehandlung: alles landet in %AppData%\HdrAutoSwitch\log.txt.
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

        Logger.Info($"HdrAutoSwitch {typeof(Program).Assembly.GetName().Version} gestartet " +
                    $"(Windows-Build {Environment.OSVersion.Version.Build}).");

        ApplicationConfiguration.Initialize();

        bool startHidden = args.Contains("--background", StringComparer.OrdinalIgnoreCase);

        using var app = new TrayApplicationContext(startHidden);
        Application.Run(app);

        Logger.Info("HdrAutoSwitch beendet.");
        GC.KeepAlive(_singleInstance);
    }
}
