using System.Diagnostics;
using System.Management;

namespace HdrAutoSwitch.Core;

/// <summary>
/// Ereignis über einen gestarteten oder beendeten Prozess.
/// </summary>
public sealed record ProcessEvent(int Pid, string ProcessName, string? FullPath);

/// <summary>
/// Überwacht Prozess-Start und -Stop über WMI-Ereignis-Abos (ManagementEventWatcher),
/// kein aktives Polling durch die App.
///
/// Primär werden Win32_ProcessStartTrace/Win32_ProcessStopTrace abonniert (echte
/// ETW-Kernel-Ereignisse, praktisch keine Last). Diese erfordern allerdings meist
/// Administratorrechte. Läuft die App als Standard-Nutzer, wird automatisch auf
/// __InstanceCreationEvent/__InstanceDeletionEvent (WITHIN 2) zurückgefallen -
/// dabei prüft der WMI-Dienst intern alle 2 s, die App selbst pollt weiterhin nicht.
/// </summary>
public sealed class ProcessWatcher : IDisposable
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private bool _running;

    public event Action<ProcessEvent>? ProcessStarted;
    public event Action<ProcessEvent>? ProcessStopped;

    /// <summary>True, wenn statt der Trace-Ereignisse der Instanz-Ereignis-Fallback läuft.</summary>
    public bool UsingFallback { get; private set; }

    public void Start()
    {
        if (_running) return;

        try
        {
            StartTraceWatchers();
            Logger.Info("Prozessüberwachung aktiv: Win32_ProcessStart/StopTrace (ETW).");
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // Ohne Adminrechte liefert StartTrace "Zugriff verweigert" -> Fallback.
            DisposeWatchers();
            StartInstanceEventWatchers();
            UsingFallback = true;
            Logger.Warn("Win32_ProcessStartTrace nicht verfügbar (" + ex.Message.Trim() +
                        ") - Fallback auf __InstanceCreation/DeletionEvent (WITHIN 2).");
        }

        _running = true;
    }

    private void StartTraceWatchers()
    {
        _startWatcher = new ManagementEventWatcher(
            new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
        _startWatcher.EventArrived += (_, e) => HandleTrace(e, isStart: true);

        _stopWatcher = new ManagementEventWatcher(
            new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
        _stopWatcher.EventArrived += (_, e) => HandleTrace(e, isStart: false);

        _startWatcher.Start();
        _stopWatcher.Start();
    }

    private void StartInstanceEventWatchers()
    {
        _startWatcher = new ManagementEventWatcher(new WqlEventQuery(
            "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_Process'"));
        _startWatcher.EventArrived += (_, e) => HandleInstance(e, isStart: true);

        _stopWatcher = new ManagementEventWatcher(new WqlEventQuery(
            "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_Process'"));
        _stopWatcher.EventArrived += (_, e) => HandleInstance(e, isStart: false);

        _startWatcher.Start();
        _stopWatcher.Start();
    }

    private void HandleTrace(EventArrivedEventArgs e, bool isStart)
    {
        try
        {
            var props = e.NewEvent.Properties;
            string name = props["ProcessName"]?.Value?.ToString() ?? "";
            int pid = Convert.ToInt32(props["ProcessID"]?.Value ?? 0);
            if (string.IsNullOrEmpty(name)) return;

            string? path = isStart ? TryGetPath(pid) : null;
            Raise(new ProcessEvent(pid, name, path), isStart);
        }
        catch (Exception ex)
        {
            // Einzelnes fehlerhaftes Ereignis darf den Watcher nicht abwürgen.
            Logger.Error("Fehler beim Verarbeiten eines Trace-Ereignisses.", ex);
        }
    }

    private void HandleInstance(EventArrivedEventArgs e, bool isStart)
    {
        try
        {
            if (e.NewEvent["TargetInstance"] is not ManagementBaseObject inst) return;

            string name = inst["Name"]?.ToString() ?? "";
            int pid = Convert.ToInt32(inst["ProcessId"] ?? 0);
            if (string.IsNullOrEmpty(name)) return;

            // Win32_Process liefert den Pfad direkt mit - kein zweiter Lookup nötig.
            string? path = isStart ? inst["ExecutablePath"]?.ToString() : null;
            if (isStart && string.IsNullOrEmpty(path))
                path = TryGetPath(pid);

            Raise(new ProcessEvent(pid, name, path), isStart);
        }
        catch (Exception ex)
        {
            Logger.Error("Fehler beim Verarbeiten eines Instanz-Ereignisses.", ex);
        }
    }

    private void Raise(ProcessEvent evt, bool isStart)
    {
        if (isStart) ProcessStarted?.Invoke(evt);
        else ProcessStopped?.Invoke(evt);
    }

    /// <summary>Versucht, den vollständigen Pfad einer laufenden PID zu ermitteln.</summary>
    public static string? TryGetPath(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.MainModule?.FileName;
        }
        catch
        {
            // Zugriff verweigert (z. B. Prozess mit höheren Rechten) oder bereits beendet.
            return null;
        }
    }

    /// <summary>
    /// Listet aktuell laufende Prozesse. Wird beim Start genutzt, um bereits
    /// laufende Whitelist-Programme sofort zu erkennen.
    /// </summary>
    public static IEnumerable<ProcessEvent> EnumerateRunning()
    {
        foreach (var p in Process.GetProcesses())
        {
            string name;
            int pid;
            string? path = null;
            try
            {
                name = p.ProcessName + ".exe";
                pid = p.Id;
                try { path = p.MainModule?.FileName; } catch { /* Rechte */ }
            }
            catch
            {
                continue;
            }
            finally
            {
                p.Dispose();
            }
            yield return new ProcessEvent(pid, name, path);
        }
    }

    private void DisposeWatchers()
    {
        try { _startWatcher?.Stop(); } catch { }
        try { _stopWatcher?.Stop(); } catch { }
        _startWatcher?.Dispose();
        _stopWatcher?.Dispose();
        _startWatcher = null;
        _stopWatcher = null;
    }

    public void Dispose()
    {
        _running = false;
        DisposeWatchers();
    }
}
