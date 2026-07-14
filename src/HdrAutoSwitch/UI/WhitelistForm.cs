using HdrAutoSwitch.Core;
using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Fenster zur Verwaltung der Whitelist: Einträge hinzufügen/bearbeiten/entfernen.
/// Globale Optionen liegen im <see cref="SettingsForm"/>.
/// Arbeitet auf einer Kopie; erst "Speichern &amp; Anwenden" übernimmt die Änderungen.
/// </summary>
public sealed class WhitelistForm : Form
{
    private readonly HdrController _hdr;
    private readonly AppConfig _config;

    private readonly ListView _list = new();
    private readonly Button _addFile = new();
    private readonly Button _addRunning = new();
    private readonly Button _edit = new();
    private readonly Button _remove = new();

    /// <summary>Wird ausgelöst, wenn der Nutzer speichert. Übergibt die aktualisierte Konfig.</summary>
    public event Action<AppConfig>? Saved;

    public WhitelistForm(AppConfig config, HdrController hdr)
    {
        _hdr = hdr;
        // Mit einer Kopie arbeiten, damit Abbrechen ohne Speichern möglich ist.
        _config = Clone(config);

        Text = Loc.T("wl.title");
        Width = 680;
        Height = 500;
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        RefreshList();
        ThemeManager.Apply(this);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20, 16, 20, 8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Kopfbereich: Titel + erklärender Untertitel
        var heading = new Label
        {
            Text = Loc.T("wl.heading"),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2)
        };
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        var hint = new Label
        {
            Text = Loc.T("wl.hint"),
            AutoSize = true,
            Tag = "muted",
            MaximumSize = new Size(620, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(hint, 0, 1);
        root.SetColumnSpan(hint, 2);

        // Liste
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.Columns.Add(Loc.T("wl.col.name"), 170);
        _list.Columns.Add(Loc.T("wl.col.mode"), 90);
        _list.Columns.Add(Loc.T("wl.col.target"), 150);
        _list.Columns.Add(Loc.T("wl.col.active"), 55);
        _list.Columns.Add(Loc.T("wl.col.procPath"), 170);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        root.Controls.Add(_list, 0, 2);

        // Buttonspalte rechts
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 0, 0, 0)
        };
        SetupButton(_addFile, Loc.T("wl.btn.addFile"), (_, _) => AddFromFile());
        SetupButton(_addRunning, Loc.T("wl.btn.addRunning"), (_, _) => AddFromRunning());
        SetupButton(_edit, Loc.T("wl.btn.edit"), (_, _) => EditSelected());
        SetupButton(_remove, Loc.T("wl.btn.remove"), (_, _) => RemoveSelected());
        btnPanel.Controls.AddRange(new Control[] { _addFile, _addRunning, _edit, _remove });
        root.Controls.Add(btnPanel, 1, 2);

        // Fußleiste: Speichern / Abbrechen
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 54,
            Padding = new Padding(16, 10, 16, 10)
        };
        var save = new Button { Text = Loc.T("wl.btn.save"), AutoSize = true, Height = 32, Padding = new Padding(10, 0, 10, 0), Tag = "primary" };
        var cancel = new Button { Text = Loc.T("common.cancel"), Width = 110, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => DoSave();
        cancel.Click += (_, _) => Close();
        footer.Controls.Add(save);
        footer.Controls.Add(cancel);

        Controls.Add(root);
        Controls.Add(footer);
        AcceptButton = save;
        CancelButton = cancel;
        UpdateButtonStates();
    }

    private static void SetupButton(Button b, string text, EventHandler onClick)
    {
        b.Text = text;
        b.Width = 150;
        b.Height = 32;
        b.Margin = new Padding(0, 0, 0, 8);
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
                ? Loc.T("wl.allMonitors")
                : string.Join(", ", e.TargetMonitorIds
                    .Select(id => monitors.FirstOrDefault(m => m.DevicePath == id)?.FriendlyName ?? "?"));

            string modeText = e.MatchMode switch
            {
                MatchMode.ProcessName => Loc.T("mode.name"),
                MatchMode.FullPath => Loc.T("mode.path"),
                _ => Loc.T("mode.namePath")
            };

            var item = new ListViewItem(new[]
            {
                e.DisplayName,
                modeText,
                target,
                e.Enabled ? Loc.T("common.yes") : Loc.T("common.no"),
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
            Title = Loc.T("dlg.pickExe"),
            Filter = Loc.T("dlg.exeFilter")
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
