using HdrAutoSwitch.Core;
using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Hauptfenster: Verwaltung der Whitelist im Windows-11-Stil.
/// Oben Titelzeile mit Zahnrad (öffnet die Einstellungen über
/// <see cref="SettingsRequested"/>), darunter Command-Bar und Liste in einer Card.
/// Arbeitet auf einer Kopie; erst "Speichern &amp; Anwenden" übernimmt die Änderungen.
/// </summary>
public sealed class WhitelistForm : Form
{
    private readonly HdrController _hdr;
    private readonly AppConfig _config;

    private readonly ListView _list = new();
    private readonly ModernButton _addFile = new();
    private readonly ModernButton _addRunning = new();
    private readonly ModernButton _edit = new();
    private readonly ModernButton _remove = new();

    // Sortierung per Spaltenklick
    private readonly string[] _columnTitles;
    private int _sortColumn = -1;
    private bool _sortAscending = true;

    /// <summary>Wird ausgelöst, wenn der Nutzer speichert. Übergibt die aktualisierte Konfig.</summary>
    public event Action<AppConfig>? Saved;

    /// <summary>Der Nutzer möchte die Einstellungen öffnen (Zahnrad-Button).</summary>
    public event Action? SettingsRequested;

    public WhitelistForm(AppConfig config, HdrController hdr)
    {
        _hdr = hdr;
        // Mit einer Kopie arbeiten, damit Abbrechen ohne Speichern möglich ist.
        _config = Clone(config);
        _columnTitles = new[]
        {
            Loc.T("wl.col.name"), Loc.T("wl.col.mode"), Loc.T("wl.col.target"),
            Loc.T("wl.col.active"), Loc.T("wl.col.procPath")
        };

        Text = Loc.T("wl.title");
        Width = 760;
        Height = 560;
        MinimumSize = new Size(660, 440);
        StartPosition = FormStartPosition.CenterScreen;
        Font = UiFonts.Body();

        BuildLayout();
        RefreshList();
        ThemeManager.Apply(this);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 20, 24, 8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Kopfzeile
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Untertitel
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Command-Bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Card mit Liste

        // ---- Kopfzeile: Titel ----
        var heading = new Label
        {
            Text = Loc.T("wl.heading"),
            AutoSize = true,
            Font = UiFonts.Display(16f),
            Margin = new Padding(0, 0, 0, 2)
        };
        root.Controls.Add(heading, 0, 0);

        // ---- Untertitel ----
        var hint = new Label
        {
            Text = Loc.T("wl.hint"),
            AutoSize = true,
            Tag = "muted",
            MaximumSize = new Size(660, 0),
            Margin = new Padding(0, 0, 0, 14)
        };
        root.Controls.Add(hint, 0, 1);

        // ---- Command-Bar über der Liste ----
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10)
        };
        SetupToolbarButton(_addFile, "＋ " + Loc.T("wl.btn.addFile"), (_, _) => AddFromFile());
        SetupToolbarButton(_addRunning, "＋ " + Loc.T("wl.btn.addRunning"), (_, _) => AddFromRunning());
        SetupToolbarButton(_edit, Loc.T("wl.btn.edit"), (_, _) => EditSelected());
        SetupToolbarButton(_remove, Loc.T("wl.btn.remove"), (_, _) => RemoveSelected());
        toolbar.Controls.AddRange(new Control[] { _addFile, _addRunning, _edit, _remove });
        root.Controls.Add(toolbar, 0, 2);

        // ---- Liste in einer Card (Margin 0, damit sie mit Buttons/Fußleiste fluchtet) ----
        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10), Margin = new Padding(0) };
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        // Kleiner Trick für luftigere Zeilen: unsichtbare 1x28-ImageList erhöht die Zeilenhöhe.
        _list.SmallImageList = new ImageList { ImageSize = new Size(1, 28) };
        _list.Columns.Add(_columnTitles[0], 180);
        _list.Columns.Add(_columnTitles[1], 100);
        _list.Columns.Add(_columnTitles[2], 160);
        _list.Columns.Add(_columnTitles[3], 76);
        _list.Columns.Add(_columnTitles[4], 180);
        _list.DoubleClick += (_, _) => EditSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        // ClientSizeChanged statt Resize: feuert auch, wenn die vertikale
        // Scrollbar erscheint/verschwindet und sich nur die Innenbreite ändert.
        _list.ClientSizeChanged += (_, _) => FitLastColumn();
        _list.ColumnClick += (_, e) => SortBy(e.Column);
        // Spaltenbreiten sind fixiert (letzte Spalte passt sich automatisch an).
        // Verhindert auch das Aufziehen des Header-Füllbereichs rechts.
        _list.ColumnWidthChanging += (_, e) =>
        {
            e.Cancel = true;
            e.NewWidth = _list.Columns[e.ColumnIndex].Width;
        };
        card.Controls.Add(_list);
        root.Controls.Add(card, 0, 3);

        // ---- Fußleiste: Einstellungen links, Abbrechen/Speichern als Gruppe rechts,
        //      seitlich auf einer Flucht mit dem Inhalt (24px) ----
        var settingsBtn = new ModernButton
        {
            Text = "⚙ " + Loc.T("set.title"),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        settingsBtn.Click += (_, _) => SettingsRequested?.Invoke();

        var save = new ModernButton { Text = Loc.T("wl.btn.save"), Primary = true };
        var cancel = new ModernButton { Text = Loc.T("common.cancel") };
        save.Click += (_, _) => DoSave();
        cancel.Click += (_, _) => Close();

        var footer = CardLayout.Footer(24, settingsBtn, save, cancel);

        Controls.Add(root);
        Controls.Add(footer);
        AcceptButton = save;
        CancelButton = cancel;
        UpdateButtonStates();
    }

    private static void SetupToolbarButton(ModernButton b, string text, EventHandler onClick)
    {
        b.Text = text;
        b.AutoSize = true;
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.Margin = new Padding(0, 0, 8, 0);
        b.Click += onClick;
    }

    /// <summary>Sortiert die Liste nach der geklickten Spalte (erneuter Klick kehrt um).</summary>
    private void SortBy(int column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }
        _list.ListViewItemSorter = new RowComparer(_sortColumn, _sortAscending);
        _list.Sort();

        // Sortier-Indikator: Tag steuert das Chevron im selbst gezeichneten Header.
        for (int i = 0; i < _list.Columns.Count; i++)
            _list.Columns[i].Tag = i == _sortColumn ? (_sortAscending ? "asc" : "desc") : null;
        ThemeManager.RefreshListViewHeader(_list);
    }

    private sealed class RowComparer : System.Collections.IComparer
    {
        private readonly int _column;
        private readonly bool _ascending;
        public RowComparer(int column, bool ascending) { _column = column; _ascending = ascending; }

        public int Compare(object? x, object? y)
        {
            string a = (x as ListViewItem)?.SubItems[_column].Text ?? "";
            string b = (y as ListViewItem)?.SubItems[_column].Text ?? "";
            int result = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
            return _ascending ? result : -result;
        }
    }

    /// <summary>
    /// Letzte Spalte füllt die Restbreite EXAKT bis zur Kante - sonst bleibt
    /// rechts ein sichtbarer Streifen des system-gezeichneten Header-Füllbereichs
    /// (und bei Überbreite käme eine horizontale Scrollbar).
    /// </summary>
    private void FitLastColumn()
    {
        if (_list.Columns.Count == 0) return;
        int others = 0;
        for (int i = 0; i < _list.Columns.Count - 1; i++)
            others += _list.Columns[i].Width;
        _list.Columns[^1].Width = Math.Max(120, _list.ClientSize.Width - others);
    }

    private void UpdateButtonStates()
    {
        bool hasSel = _list.SelectedItems.Count > 0;
        _edit.Enabled = hasSel;
        _remove.Enabled = hasSel;
        _edit.Invalidate();
        _remove.Invalidate();
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
