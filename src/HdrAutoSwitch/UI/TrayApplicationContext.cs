using HdrAutoSwitch.Core;
using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// ApplicationContext ohne Hauptfenster: Die App lebt im Infobereich (Tray).
/// Verdrahtet Konfiguration, Prozessüberwachung, HDR-Steuerung, Theme und Sprache.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ConfigStore _store = new();
    private readonly HdrController _hdr = new();
    private readonly ProcessWatcher _watcher = new();
    private readonly AutoSwitchEngine _engine;
    private AppConfig _config;

    private readonly NotifyIcon _tray;
    private WhitelistForm? _whitelistForm;
    private SettingsForm? _settingsForm;
    private readonly SynchronizationContext _ui;

    // Menüeinträge als Felder, damit Texte bei Sprachwechsel aktualisiert werden können.
    private readonly ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _whitelistItem = new();
    private readonly ToolStripMenuItem _settingsItem = new();
    private readonly ToolStripMenuItem _forceOffItem = new();
    private readonly ToolStripMenuItem _autostartItem = new() { CheckOnClick = true };
    private readonly ToolStripMenuItem _exitItem = new();

    public TrayApplicationContext(bool startHidden)
    {
        // Läuft auf dem UI-Thread (Application.Run). Kontext für spätere Marshals sichern.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _config = _store.Load();
        Loc.Apply(_config.Language);
        ThemeManager.Mode = _config.Theme;

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
                Loc.T("tray.watcherFailed") + ex.Message, ToolTipIcon.Error);
        }

        if (!_config.Whitelist.Any() && !startHidden)
        {
            OpenWhitelist();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _whitelistItem.Click += (_, _) => OpenWhitelist();
        _settingsItem.Click += (_, _) => OpenSettings();
        _forceOffItem.Click += (_, _) => ForceAllOff();
        _exitItem.Click += (_, _) => ExitApp();
        _autostartItem.CheckedChanged += (_, _) =>
        {
            if (_config.StartWithWindows == _autostartItem.Checked) return;
            _config.StartWithWindows = _autostartItem.Checked;
            _store.Save(_config); // schreibt config.json und den HKCU-Run-Key
            Logger.Info("Autostart " + (_config.StartWithWindows ? "aktiviert." : "deaktiviert."));
        };

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_whitelistItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_forceOffItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        // Beim Öffnen des Menüs den aktuellen HDR-Status live abfragen.
        menu.Opening += (_, _) =>
        {
            UpdateStatusItem();
            _autostartItem.Checked = _config.StartWithWindows;
        };

        RefreshMenuTexts();
        ThemeManager.ApplyToMenu(menu);
        return menu;
    }

    /// <summary>Setzt alle Menütexte in der aktiven Sprache (initial und nach Sprachwechsel).</summary>
    private void RefreshMenuTexts()
    {
        _statusItem.Text = Loc.T("tray.status.detecting");
        _whitelistItem.Text = Loc.T("tray.menu.whitelist");
        _settingsItem.Text = Loc.T("tray.menu.settings");
        _forceOffItem.Text = Loc.T("tray.menu.forceOff");
        _autostartItem.Text = Loc.T("tray.menu.autostart");
        _exitItem.Text = Loc.T("tray.menu.exit");
        _autostartItem.Checked = _config.StartWithWindows;
    }

    private void UpdateStatusItem()
    {
        try
        {
            var hdrMonitors = _hdr.GetMonitors().Where(m => m.HdrSupported).ToList();
            _statusItem.Text = hdrMonitors.Count == 0
                ? Loc.T("tray.status.none")
                : "HDR: " + string.Join(" | ", hdrMonitors.Select(m =>
                    $"{m.FriendlyName}{(m.IsPrimary ? $" ({Loc.T("tray.primaryTag")})" : "")}: " +
                    (m.HdrEnabled ? Loc.T("common.on") : Loc.T("common.off"))));
        }
        catch (Exception ex)
        {
            _statusItem.Text = Loc.T("tray.status.unavailable");
            Logger.Error("HDR-Statusabfrage für das Tray-Menü fehlgeschlagen.", ex);
        }
    }

    private void OpenWhitelist()
    {
        if (_whitelistForm is { IsDisposed: false })
        {
            _whitelistForm.Activate();
            return;
        }
        _whitelistForm = new WhitelistForm(_config, _hdr);
        _whitelistForm.Saved += cfg =>
        {
            // Nur die Whitelist übernehmen - Einstellungen könnten sich
            // parallel über den Einstellungsdialog geändert haben.
            _config.Whitelist = cfg.Whitelist;
            _store.Save(_config);
            _engine.UpdateConfig(_config);
            // Nach dem Speichern erneut laufende Prozesse prüfen.
            _engine.ScanExisting();
        };
        _whitelistForm.Show();
        _whitelistForm.BringToFront();
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        // Kopie übergeben, damit Abbrechen keine Spuren hinterlässt.
        var clone = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(
            System.Text.Json.JsonSerializer.Serialize(_config)) ?? new AppConfig();

        _settingsForm = new SettingsForm(clone);
        _settingsForm.Saved += cfg =>
        {
            // Nur die Einstellungen übernehmen - die Whitelist könnte sich
            // parallel über das Whitelist-Fenster geändert haben.
            _config.StartWithWindows = cfg.StartWithWindows;
            _config.ShowNotifications = cfg.ShowNotifications;
            _config.RestorePreviousState = cfg.RestorePreviousState;
            _config.TargetMode = cfg.TargetMode;
            _config.OnDebounceMs = cfg.OnDebounceMs;
            _config.OffDebounceMs = cfg.OffDebounceMs;
            _config.Theme = cfg.Theme;
            _config.Language = cfg.Language;
            _store.Save(_config);
            _engine.UpdateConfig(_config);

            // Sprache/Theme sofort anwenden.
            Loc.Apply(_config.Language);
            ThemeManager.Mode = _config.Theme;
            RefreshMenuTexts();
            if (_tray.ContextMenuStrip is { } menu)
                ThemeManager.ApplyToMenu(menu);

            // Offenes Whitelist-Fenster schließen, damit es beim nächsten
            // Öffnen in neuer Sprache/neuem Theme erscheint.
            if (_whitelistForm is { IsDisposed: false })
                _whitelistForm.Close();
        };
        _settingsForm.Show();
        _settingsForm.BringToFront();
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
        string title = notice.Enabled ? Loc.T("notify.on") : Loc.T("notify.off");
        string reason = notice.TriggerName ?? Loc.T("notify.allEnded");
        string body = $"{reason} → {monitors}";
        // Das Event kommt von einem Timer-/WMI-Thread. NotifyIcon ist an den
        // UI-Thread gebunden, daher zurück auf diesen marshallen.
        _ui.Post(_ => _tray.ShowBalloonTip(2500, title, body, ToolTipIcon.Info), null);
    }

    private void ExitApp()
    {
        _engine.ShutdownCleanup(); // vorherigen HDR-Zustand wiederherstellen
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
