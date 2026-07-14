using System.Drawing;
using HdrAutoSwitch.Core;
using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// ApplicationContext ohne Hauptfenster: Die App lebt im Infobereich (Tray).
/// Verdrahtet Konfiguration, Prozessüberwachung und HDR-Steuerung.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ConfigStore _store = new();
    private readonly HdrController _hdr = new();
    private readonly ProcessWatcher _watcher = new();
    private readonly AutoSwitchEngine _engine;
    private AppConfig _config;

    private readonly NotifyIcon _tray;
    private WhitelistForm? _openForm;
    private readonly SynchronizationContext _ui;

    public TrayApplicationContext(bool startHidden)
    {
        // Läuft auf dem UI-Thread (Application.Run). Kontext für spätere Marshals sichern.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _config = _store.Load();
        _engine = new AutoSwitchEngine(_hdr, _watcher, _config);

        _tray = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "HDR AutoSwitch",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => OpenWhitelist();

        _engine.Switched += OnSwitched;

        // Überwachung starten und bereits laufende Programme prüfen.
        try
        {
            _watcher.Start();
            _engine.ScanExisting();
        }
        catch (Exception ex)
        {
            Logger.Error("Prozessüberwachung konnte nicht gestartet werden.", ex);
            _tray.ShowBalloonTip(5000, "HDR AutoSwitch",
                "Prozessüberwachung konnte nicht gestartet werden: " + ex.Message,
                ToolTipIcon.Error);
        }

        if (!_config.Whitelist.Any() && !startHidden)
        {
            OpenWhitelist();
        }
    }

    private readonly ToolStripMenuItem _statusItem = new("HDR-Status wird ermittelt…") { Enabled = false };
    private readonly ToolStripMenuItem _autostartItem = new("Bei Windows-Start starten") { CheckOnClick = true };

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Whitelist verwalten…", null, (_, _) => OpenWhitelist());
        menu.Items.Add("HDR jetzt aus (alle Monitore)", null, (_, _) => ForceAllOff());

        _autostartItem.Checked = _config.StartWithWindows;
        _autostartItem.CheckedChanged += (_, _) =>
        {
            if (_config.StartWithWindows == _autostartItem.Checked) return;
            _config.StartWithWindows = _autostartItem.Checked;
            _store.Save(_config); // schreibt config.json und den HKCU-Run-Key
            Logger.Info("Autostart " + (_config.StartWithWindows ? "aktiviert." : "deaktiviert."));
        };
        menu.Items.Add(_autostartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitApp());

        // Beim Öffnen des Menüs den aktuellen HDR-Status live abfragen.
        menu.Opening += (_, _) =>
        {
            UpdateStatusItem();
            _autostartItem.Checked = _config.StartWithWindows;
        };

        return menu;
    }

    private void UpdateStatusItem()
    {
        try
        {
            var hdrMonitors = _hdr.GetMonitors().Where(m => m.HdrSupported).ToList();
            _statusItem.Text = hdrMonitors.Count == 0
                ? "HDR: kein HDR-fähiger Monitor"
                : "HDR: " + string.Join(" | ", hdrMonitors.Select(m =>
                    $"{m.FriendlyName}{(m.IsPrimary ? " (primär)" : "")}: {(m.HdrEnabled ? "Ein" : "Aus")}"));
        }
        catch (Exception ex)
        {
            _statusItem.Text = "HDR-Status nicht abrufbar";
            Logger.Error("HDR-Statusabfrage für das Tray-Menü fehlgeschlagen.", ex);
        }
    }

    private void OpenWhitelist()
    {
        if (_openForm is { IsDisposed: false })
        {
            _openForm.Activate();
            return;
        }
        _openForm = new WhitelistForm(_config, _hdr);
        _openForm.Saved += cfg =>
        {
            _config = cfg;
            _store.Save(_config);
            _engine.UpdateConfig(_config);
            // Nach dem Speichern erneut laufende Prozesse prüfen.
            _engine.ScanExisting();
        };
        _openForm.Show();
        _openForm.BringToFront();
    }

    private void ForceAllOff()
    {
        var monitors = _hdr.GetMonitors()
            .Where(m => m.HdrSupported && m.HdrEnabled)
            .Select(m => m.DevicePath)
            .ToList();
        _hdr.SetHdrOnTargets(monitors, enable: false);
    }

    private void OnSwitched(HdrSwitchNotice notice)
    {
        if (!_config.ShowNotifications) return;
        string monitors = string.Join(", ", notice.MonitorNames);
        string title = notice.Enabled ? "HDR aktiviert" : "HDR deaktiviert";
        string body = $"{notice.Reason} → {monitors}";
        // Das Event kommt von einem Timer-/WMI-Thread. NotifyIcon ist an den
        // UI-Thread gebunden, daher zurück auf diesen marshallen.
        _ui.Post(_ => _tray.ShowBalloonTip(2500, title, body, ToolTipIcon.Info), null);
    }

    private void ExitApp()
    {
        _engine.ShutdownCleanup(); // HDR sauber ausschalten
        _tray.Visible = false;
        _watcher.Dispose();
        _engine.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
