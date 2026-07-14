using System.Collections.Concurrent;
using HdrPilot.Models;

namespace HdrPilot.Core;

/// <summary>
/// Meldung über eine HDR-Umschaltung, z. B. für Tray-Benachrichtigungen.
/// TriggerName ist der auslösende Whitelist-Eintrag (null beim Ausschalten);
/// den anzeigbaren Text baut die UI-Schicht in der jeweiligen Sprache.
/// </summary>
public sealed record HdrSwitchNotice(bool Enabled, string? TriggerName, IReadOnlyList<string> MonitorNames);

/// <summary>
/// Kernlogik: verbindet <see cref="ProcessWatcher"/>, Whitelist und <see cref="HdrController"/>.
///
/// Verhalten:
///  - Startet ein Whitelist-Programm, wird HDR auf dessen Ziel-Monitoren aktiviert -
///    aber nur dort, wo es vorher AUS war (der vorherige Zustand wird gemerkt).
///  - Laufen mehrere Whitelist-Programme, zählt eine Referenz je aktivem Prozess.
///  - Erst wenn ALLE Whitelist-Prozesse beendet sind, wird der gemerkte vorherige
///    Zustand wiederhergestellt (abschaltbar über AppConfig.RestorePreviousState).
///  - Debounce (Standard 1500 ms) verhindert Flackern bei kurzen Start/Stop-Zyklen.
/// </summary>
public sealed class AutoSwitchEngine : IDisposable
{
    private readonly HdrController _hdr;
    private readonly ProcessWatcher _watcher;
    private AppConfig _config;

    // PID -> passender Whitelist-Eintrag (aktuell aktive, "HDR-haltende" Prozesse)
    private readonly ConcurrentDictionary<int, WhitelistEntry> _activePids = new();

    // Alle Monitor-DevicePaths, auf denen die App aktuell HDR aktiviert hat.
    // Dort war HDR vorher AUS - das ist der "gemerkte vorherige Zustand": Monitore,
    // auf denen HDR schon an war, werden nie angefasst und bleiben beim Restore an.
    private readonly HashSet<string> _monitorsWeEnabled = new();
    private readonly object _sync = new();

    private System.Threading.Timer? _offTimer;
    private readonly object _timerLock = new();

    public event Action<HdrSwitchNotice>? Switched;

    public AutoSwitchEngine(HdrController hdr, ProcessWatcher watcher, AppConfig config)
    {
        _hdr = hdr;
        _watcher = watcher;
        _config = config;
        _watcher.ProcessStarted += OnProcessStarted;
        _watcher.ProcessStopped += OnProcessStopped;
    }

    /// <summary>Aktualisiert die Konfiguration zur Laufzeit (nach Bearbeiten der Whitelist).</summary>
    public void UpdateConfig(AppConfig config)
    {
        lock (_sync) { _config = config; }
    }

    /// <summary>
    /// Beim Start einmalig laufende Prozesse prüfen, damit bereits geöffnete
    /// Whitelist-Programme sofort HDR auslösen.
    /// </summary>
    public void ScanExisting()
    {
        foreach (var evt in ProcessWatcher.EnumerateRunning())
            EvaluateStart(evt);
    }

    private void OnProcessStarted(ProcessEvent evt) => EvaluateStart(evt);

    private void EvaluateStart(ProcessEvent evt)
    {
        WhitelistEntry? match;
        lock (_sync)
        {
            match = _config.Whitelist.FirstOrDefault(w => w.Matches(evt.ProcessName, evt.FullPath));
        }
        if (match is null) return;

        bool firstActivation = _activePids.IsEmpty;
        _activePids[evt.Pid] = match;

        // Falls ein Off-Timer lief (kurzer Neustart), abbrechen.
        CancelOffTimer();

        Logger.Info($"Treffer: {evt.ProcessName} (PID {evt.Pid}) -> {match.DisplayName}");

        // Auto-HDR global sofort (ohne Debounce) einschalten: das Spiel liest die
        // Einstellung beim Erzeugen seiner Swapchain, jede Verzögerung riskiert,
        // dass die Session ohne Auto-HDR startet.
        if (match.EnableAutoHdr)
            AutoHdrController.EnableGlobalForSession();

        // Debounce beim Einschalten - nicht-blockierend, damit der WMI-Thread frei bleibt.
        int delay = firstActivation ? _config.OnDebounceMs : 0;
        if (delay > 0)
        {
            var captured = match;
            _ = System.Threading.Tasks.Task.Delay(delay).ContinueWith(_ =>
            {
                if (!_activePids.IsEmpty) ApplyOn(captured);
            });
        }
        else
        {
            ApplyOn(match);
        }
    }

    private void OnProcessStopped(ProcessEvent evt)
    {
        if (!_activePids.TryRemove(evt.Pid, out _)) return;

        Logger.Info($"Beendet: {evt.ProcessName} (PID {evt.Pid})");

        if (!_activePids.IsEmpty)
            return; // Es laufen noch andere Whitelist-Programme -> HDR bleibt an.

        // Kein Whitelist-Programm mehr aktiv -> nach Debounce ausschalten.
        ScheduleOff();
    }

    private void ApplyOn(WhitelistEntry entry)
    {
        Models.TargetMode mode;
        lock (_sync) { mode = _config.TargetMode; }

        // Hat der Eintrag eigene Ziel-Monitore, gelten diese; sonst der globale TargetMode.
        var changed = _hdr.SetHdrOnTargets(entry.TargetMonitorIds, enable: true, mode);
        lock (_sync)
        {
            foreach (var m in changed)
                _monitorsWeEnabled.Add(m.DevicePath);
        }
        if (changed.Count > 0)
        {
            Switched?.Invoke(new HdrSwitchNotice(
                true, entry.DisplayName, changed.Select(m => m.FriendlyName).ToList()));
        }
    }

    private void ScheduleOff()
    {
        lock (_timerLock)
        {
            _offTimer?.Dispose();
            _offTimer = new System.Threading.Timer(_ => ApplyOffIfIdle(), null,
                _config.OffDebounceMs, System.Threading.Timeout.Infinite);
        }
    }

    private void CancelOffTimer()
    {
        lock (_timerLock)
        {
            _offTimer?.Dispose();
            _offTimer = null;
        }
    }

    private void ApplyOffIfIdle()
    {
        // Sicherheits-Check: In der Debounce-Zeit könnte ein neues Programm gestartet sein.
        if (!_activePids.IsEmpty) return;

        // Globales Auto-HDR immer zurücksetzen - unabhängig von RestorePreviousState:
        // Das betrifft alle Spiele systemweit und soll nur während der Session gelten.
        AutoHdrController.RestoreGlobalAfterSession();

        bool restore;
        List<string> toDisable;
        lock (_sync)
        {
            restore = _config.RestorePreviousState;
            toDisable = _monitorsWeEnabled.ToList();
            _monitorsWeEnabled.Clear();
        }
        if (toDisable.Count == 0) return;

        if (!restore)
        {
            // Konfiguriert: HDR nach Programmende eingeschaltet lassen.
            Logger.Info("RestorePreviousState=false - HDR bleibt eingeschaltet.");
            return;
        }

        var changed = _hdr.SetHdrOnTargets(toDisable, enable: false);
        if (changed.Count > 0)
        {
            Switched?.Invoke(new HdrSwitchNotice(
                false, null, changed.Select(m => m.FriendlyName).ToList()));
        }
    }

    /// <summary>
    /// Beim Beenden der App: den vorherigen HDR-Zustand wiederherstellen,
    /// sofern die App HDR aktiviert hatte (und Restore konfiguriert ist).
    /// </summary>
    public void ShutdownCleanup()
    {
        CancelOffTimer();
        AutoHdrController.RestoreGlobalAfterSession();
        bool restore;
        List<string> toDisable;
        lock (_sync)
        {
            restore = _config.RestorePreviousState;
            toDisable = _monitorsWeEnabled.ToList();
            _monitorsWeEnabled.Clear();
        }
        if (restore && toDisable.Count > 0)
            _hdr.SetHdrOnTargets(toDisable, enable: false);
    }

    public void Dispose()
    {
        _watcher.ProcessStarted -= OnProcessStarted;
        _watcher.ProcessStopped -= OnProcessStopped;
        CancelOffTimer();
    }
}
