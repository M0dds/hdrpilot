using HdrPilot.Models;

namespace HdrPilot.UI;

/// <summary>
/// Dialog zum Bearbeiten eines einzelnen Whitelist-Eintrags, im selben
/// Card-Stil wie die Einstellungen: Sektion "Programm" (Name, Pfad,
/// Erkennung, Aktiv-Status) und Sektion "HDR schalten auf" (Ziel-Monitore).
/// </summary>
public sealed class EntryEditForm : Form
{
    private readonly ModernTextBox _name = new();
    private readonly ModernTextBox _processName = new();
    private readonly ModernTextBox _path = new();
    private readonly ModernComboBox _mode = new();
    private readonly CheckBox _enabled = new();
    private readonly CheckedListBox _monitors = new();
    private readonly RadioButton _allMonitors = new();
    private readonly RadioButton _selectedMonitors = new();
    private readonly List<MonitorInfo> _monitorList;

    public WhitelistEntry Result { get; private set; }

    public EntryEditForm(WhitelistEntry entry, List<MonitorInfo> monitors)
    {
        _monitorList = monitors;
        Result = entry;

        Text = Loc.T("entry.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = UiFonts.Body();
        ClientSize = new Size(500, 620);

        BuildLayout();
        LoadFromEntry(entry);
        ThemeManager.Apply(this);
        FitHeightToContent();
    }

    /// <summary>
    /// Passt die Fensterhöhe an den Inhalt an, damit unter der letzten Card
    /// keine überflüssige Luft zu den Buttons bleibt.
    /// </summary>
    private void FitHeightToContent()
    {
        if (_root is null || _footer is null) return;
        int contentHeight = _root.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
        ClientSize = new Size(ClientSize.Width, contentHeight + _footer.Height);
    }

    private TableLayoutPanel? _root;
    private Control? _footer;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(24, 18, 24, 8),
            AutoScroll = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // ---- Card: Programm ----
        CardLayout.AddRootRow(root, CardLayout.Section(Loc.T("entry.sec.program")));

        var program = CardLayout.NewCardTable(150);
        CardLayout.AddRow(program, Loc.T("entry.displayName"), _name);
        CardLayout.AddRow(program, Loc.T("entry.processName"), _processName);

        // Pfad mit Durchsuchen-Button
        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Height = 34, Margin = new Padding(0, 3, 0, 3) };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40)); // 34px Button + 6px Abstand
        _path.Dock = DockStyle.Fill;
        _path.Margin = new Padding(0);
        // Quadratischer Icon-Button; "" (More) ist im Gegensatz zu "…"
        // vertikal zentriert statt auf der Grundlinie.
        var browse = new ModernButton
        {
            Text = "",
            Font = UiFonts.Icon(),
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            Margin = new Padding(6, 0, 0, 0)
        };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Title = Loc.T("dlg.pickExe"), Filter = Loc.T("dlg.exeFilter") };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _path.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(_processName.Text))
                    _processName.Text = Path.GetFileName(dlg.FileName);
            }
        };
        pathPanel.Controls.Add(_path, 0, 0);
        pathPanel.Controls.Add(browse, 1, 0);
        CardLayout.AddRow(program, Loc.T("entry.path"), pathPanel);

        CardLayout.AddRow(program, Loc.T("entry.match"), _mode);
        _mode.Items.AddRange(new[]
        {
            Loc.T("entry.mode.name"),
            Loc.T("entry.mode.path"),
            Loc.T("entry.mode.namePath")
        });

        _enabled.Text = Loc.T("entry.active");
        _enabled.AutoSize = true;
        CardLayout.AddWide(program, _enabled);

        CardLayout.AddRootRow(root, CardLayout.WrapInCard(program));

        // ---- Card: HDR schalten auf ----
        CardLayout.AddRootRow(root, CardLayout.Section(Loc.T("entry.sec.target")));

        var target = CardLayout.NewCardTable(150);
        _allMonitors.Text = Loc.T("entry.allHdr");
        _allMonitors.AutoSize = true;
        _allMonitors.CheckedChanged += (_, _) => _monitors.Enabled = _selectedMonitors.Checked;
        CardLayout.AddWide(target, _allMonitors);
        _allMonitors.Margin = new Padding(0, 2, 0, 2);

        _selectedMonitors.Text = Loc.T("entry.selected");
        _selectedMonitors.AutoSize = true;
        _selectedMonitors.CheckedChanged += (_, _) => _monitors.Enabled = _selectedMonitors.Checked;
        CardLayout.AddWide(target, _selectedMonitors);
        _selectedMonitors.Margin = new Padding(0, 2, 0, 2);

        _monitors.CheckOnClick = true;
        _monitors.IntegralHeight = false;
        _monitors.Dock = DockStyle.Top;
        foreach (var m in _monitorList)
        {
            string label = m.HdrSupported ? m.FriendlyName : $"{m.FriendlyName} {Loc.T("entry.noHdr")}";
            _monitors.Items.Add(new MonitorItem(m, label));
        }
        // Höhe exakt an die Monitoranzahl anpassen - keine leere Fläche in der Card.
        _monitors.Height = Math.Max(1, _monitors.Items.Count) * 26 + 4;
        CardLayout.AddWide(target, _monitors);
        // Einrückung unter "Nur ausgewählte Monitore" macht die Zugehörigkeit sichtbar.
        _monitors.Margin = new Padding(24, 0, 0, 2);

        CardLayout.AddRootRow(root, CardLayout.WrapInCard(target));

        // ---- Fußleiste ----
        var ok = new ModernButton { Text = Loc.T("common.ok"), Primary = true, DialogResult = DialogResult.OK };
        var cancel = new ModernButton { Text = Loc.T("common.cancel"), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (ValidateInput()) { SaveToResult(); } else DialogResult = DialogResult.None; };

        var footer = CardLayout.Footer(24, null, ok, cancel);
        Controls.Add(root);
        Controls.Add(footer);
        _root = root;
        _footer = footer;
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void LoadFromEntry(WhitelistEntry e)
    {
        _name.Text = e.DisplayName;
        _processName.Text = e.ProcessName ?? "";
        _path.Text = e.FullPath ?? "";
        _mode.SelectedIndex = e.MatchMode switch
        {
            MatchMode.ProcessName => 0,
            MatchMode.FullPath => 1,
            _ => 2
        };
        _enabled.Checked = e.Enabled;

        bool useSelected = e.TargetMonitorIds.Count > 0;
        _allMonitors.Checked = !useSelected;
        _selectedMonitors.Checked = useSelected;
        _monitors.Enabled = useSelected;

        for (int i = 0; i < _monitors.Items.Count; i++)
        {
            var mi = (MonitorItem)_monitors.Items[i];
            if (e.TargetMonitorIds.Contains(mi.Monitor.DevicePath))
                _monitors.SetItemChecked(i, true);
        }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_processName.Text) && string.IsNullOrWhiteSpace(_path.Text))
        {
            MessageBox.Show(this, Loc.T("entry.incompleteMsg"),
                Loc.T("entry.incompleteTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private void SaveToResult()
    {
        var targets = new List<string>();
        if (_selectedMonitors.Checked)
        {
            foreach (int idx in _monitors.CheckedIndices)
                targets.Add(((MonitorItem)_monitors.Items[idx]).Monitor.DevicePath);
        }

        Result = new WhitelistEntry
        {
            DisplayName = string.IsNullOrWhiteSpace(_name.Text)
                ? (_processName.Text.Length > 0 ? _processName.Text : "?")
                : _name.Text.Trim(),
            ProcessName = string.IsNullOrWhiteSpace(_processName.Text) ? null : _processName.Text.Trim(),
            FullPath = string.IsNullOrWhiteSpace(_path.Text) ? null : _path.Text.Trim(),
            MatchMode = _mode.SelectedIndex switch
            {
                0 => MatchMode.ProcessName,
                1 => MatchMode.FullPath,
                _ => MatchMode.NameOrPath
            },
            Enabled = _enabled.Checked,
            TargetMonitorIds = targets
        };
    }

    private sealed record MonitorItem(MonitorInfo Monitor, string Label)
    {
        public override string ToString() => Label;
    }
}
