using HdrAutoSwitch.Core;
using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Fenster zur Verwaltung der Whitelist: Einträge hinzufügen/bearbeiten/entfernen,
/// globale Optionen setzen und speichern.
/// </summary>
public sealed class WhitelistForm : Form
{
    private readonly HdrController _hdr;
    private AppConfig _config;

    private readonly ListView _list = new();
    private readonly Button _addFile = new();
    private readonly Button _addRunning = new();
    private readonly Button _edit = new();
    private readonly Button _remove = new();
    private readonly CheckBox _autostart = new();
    private readonly CheckBox _notify = new();
    private readonly CheckBox _restore = new();
    private readonly ComboBox _targetMode = new();
    private readonly Button _save = new();
    private readonly Label _hint = new();

    /// <summary>Wird ausgelöst, wenn der Nutzer speichert. Übergibt die aktualisierte Konfig.</summary>
    public event Action<AppConfig>? Saved;

    public WhitelistForm(AppConfig config, HdrController hdr)
    {
        _hdr = hdr;
        // Mit einer Kopie arbeiten, damit Abbrechen ohne Speichern möglich ist.
        _config = Clone(config);

        Text = "HDR AutoSwitch – Whitelist";
        Width = 640;
        Height = 540;
        MinimumSize = new Size(560, 460);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        RefreshList();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _hint.Text = "Programme, die HDR automatisch aktivieren. HDR wird ausgeschaltet, sobald alle beendet sind.";
        _hint.AutoSize = false;
        _hint.Dock = DockStyle.Fill;
        _hint.ForeColor = SystemColors.GrayText;
        root.Controls.Add(_hint, 0, 0);
        root.SetColumnSpan(_hint, 2);

        // Liste
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.Columns.Add("Name", 180);
        _list.Columns.Add("Erkennung", 90);
        _list.Columns.Add("Ziel", 150);
        _list.Columns.Add("Aktiv", 50);
        _list.Columns.Add("Prozess/Pfad", 160);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        root.Controls.Add(_list, 0, 1);

        // Buttonspalte
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8, 0, 0, 0)
        };
        SetupButton(_addFile, "Aus Datei…", (_, _) => AddFromFile());
        SetupButton(_addRunning, "Aus laufenden…", (_, _) => AddFromRunning());
        SetupButton(_edit, "Bearbeiten…", (_, _) => EditSelected());
        SetupButton(_remove, "Entfernen", (_, _) => RemoveSelected());
        btnPanel.Controls.AddRange(new Control[] { _addFile, _addRunning, _edit, _remove });
        root.Controls.Add(btnPanel, 1, 1);

        // Optionen
        var optPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };
        _autostart.Text = "Mit Windows starten";
        _autostart.AutoSize = true;
        _autostart.Checked = _config.StartWithWindows;
        _notify.Text = "Benachrichtigung beim Umschalten anzeigen";
        _notify.AutoSize = true;
        _notify.Checked = _config.ShowNotifications;
        _restore.Text = "Vorherigen HDR-Zustand nach Programmende wiederherstellen";
        _restore.AutoSize = true;
        _restore.Checked = _config.RestorePreviousState;

        // Globale Monitorauswahl (gilt für Einträge ohne eigene Auswahl).
        var modePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
        modePanel.Controls.Add(new Label { Text = "HDR schalten auf:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        _targetMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _targetMode.Items.AddRange(new object[] { "Nur Primärmonitor", "Alle HDR-fähigen Monitore" });
        _targetMode.SelectedIndex = _config.TargetMode == TargetMode.PrimaryOnly ? 0 : 1;
        _targetMode.Width = 220;
        modePanel.Controls.Add(_targetMode);

        optPanel.Controls.Add(_autostart);
        optPanel.Controls.Add(_notify);
        optPanel.Controls.Add(_restore);
        optPanel.Controls.Add(modePanel);
        root.Controls.Add(optPanel, 0, 2);
        root.SetColumnSpan(optPanel, 2);

        // Speichern
        _save.Text = "Speichern & Anwenden";
        _save.AutoSize = true;
        _save.Padding = new Padding(8, 4, 8, 4);
        _save.Anchor = AnchorStyles.Right;
        _save.Click += (_, _) => DoSave();
        root.Controls.Add(_save, 1, 3);

        Controls.Add(root);
        UpdateButtonStates();
    }

    private static void SetupButton(Button b, string text, EventHandler onClick)
    {
        b.Text = text;
        b.Width = 130;
        b.Height = 30;
        b.Margin = new Padding(0, 0, 0, 6);
        b.Click += onClick;
    }

    private void UpdateButtonStates()
    {
        bool hasSel = _list.SelectedItems.Count > 0;
        _edit.Enabled = hasSel;
        _remove.Enabled = hasSel;
    }

    private void RefreshList()
    {
        _list.Items.Clear();
        var monitors = _hdr.GetMonitors();

        foreach (var e in _config.Whitelist)
        {
            string target = e.TargetMonitorIds.Count == 0
                ? "Alle HDR-Monitore"
                : string.Join(", ", e.TargetMonitorIds
                    .Select(id => monitors.FirstOrDefault(m => m.DevicePath == id)?.FriendlyName ?? "?"));

            string modeText = e.MatchMode switch
            {
                MatchMode.ProcessName => "Name",
                MatchMode.FullPath => "Pfad",
                _ => "Name+Pfad"
            };

            var item = new ListViewItem(new[]
            {
                e.DisplayName,
                modeText,
                target,
                e.Enabled ? "Ja" : "Nein",
                e.FullPath ?? e.ProcessName ?? ""
            })
            { Tag = e };
            _list.Items.Add(item);
        }
    }

    private WhitelistEntry? Selected =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as WhitelistEntry : null;

    private void AddFromFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Programm auswählen",
            Filter = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var entry = new WhitelistEntry
        {
            DisplayName = Path.GetFileNameWithoutExtension(dlg.FileName),
            ProcessName = Path.GetFileName(dlg.FileName),
            FullPath = dlg.FileName,
            MatchMode = MatchMode.NameOrPath
        };
        EditEntry(entry, isNew: true);
    }

    private void AddFromRunning()
    {
        using var picker = new RunningProcessPicker();
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedProcess is null)
            return;

        var p = picker.SelectedProcess;
        var entry = new WhitelistEntry
        {
            DisplayName = Path.GetFileNameWithoutExtension(p.ProcessName),
            ProcessName = p.ProcessName,
            FullPath = p.FullPath,
            MatchMode = MatchMode.NameOrPath
        };
        EditEntry(entry, isNew: true);
    }

    private void EditSelected()
    {
        if (Selected is { } e) EditEntry(e, isNew: false);
    }

    private void EditEntry(WhitelistEntry entry, bool isNew)
    {
        using var dlg = new EntryEditForm(entry, _hdr.GetMonitors());
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (isNew)
            _config.Whitelist.Add(dlg.Result);
        else
        {
            int idx = _config.Whitelist.IndexOf(entry);
            if (idx >= 0) _config.Whitelist[idx] = dlg.Result;
        }
        RefreshList();
    }

    private void RemoveSelected()
    {
        if (Selected is { } e)
        {
            _config.Whitelist.Remove(e);
            RefreshList();
            UpdateButtonStates();
        }
    }

    private void DoSave()
    {
        _config.StartWithWindows = _autostart.Checked;
        _config.ShowNotifications = _notify.Checked;
        _config.RestorePreviousState = _restore.Checked;
        _config.TargetMode = _targetMode.SelectedIndex == 0 ? TargetMode.PrimaryOnly : TargetMode.AllHdrCapable;
        Saved?.Invoke(Clone(_config));
        Close();
    }

    private static AppConfig Clone(AppConfig c)
    {
        // Tiefe Kopie über JSON, damit Bearbeiten die Live-Konfig nicht verändert.
        string json = System.Text.Json.JsonSerializer.Serialize(c);
        return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
}
