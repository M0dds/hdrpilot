using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Dialog zum Bearbeiten eines einzelnen Whitelist-Eintrags:
/// Name, Prozessname/Pfad, Erkennungsmodus, Aktiv-Status und Ziel-Monitore.
/// </summary>
public sealed class EntryEditForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _processName = new();
    private readonly TextBox _path = new();
    private readonly ComboBox _mode = new();
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
        Width = 500;
        Height = 520;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        LoadFromEntry(entry);
        ThemeManager.Apply(this);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoSize = false
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(root, Loc.T("entry.displayName"), _name);
        AddRow(root, Loc.T("entry.processName"), _processName);

        // Pfad mit Durchsuchen-Button
        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Height = 30, Margin = new Padding(0, 3, 0, 3) };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        _path.Dock = DockStyle.Fill;
        var browse = new Button { Text = "…", Dock = DockStyle.Fill };
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
        AddLabel(root, Loc.T("entry.path"));
        root.Controls.Add(pathPanel, 1, root.RowCount - 1);

        // Erkennungsmodus
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(new object[]
        {
            Loc.T("entry.mode.name"),
            Loc.T("entry.mode.path"),
            Loc.T("entry.mode.namePath")
        });
        _mode.Dock = DockStyle.Fill;
        _mode.Margin = new Padding(0, 3, 0, 3);
        AddLabel(root, Loc.T("entry.match"));
        root.Controls.Add(_mode, 1, root.RowCount - 1);

        _enabled.Text = Loc.T("entry.active");
        _enabled.AutoSize = true;
        AddLabel(root, "");
        root.Controls.Add(_enabled, 1, root.RowCount - 1);

        // Monitor-Ziel
        _allMonitors.Text = Loc.T("entry.allHdr");
        _allMonitors.AutoSize = true;
        _allMonitors.CheckedChanged += (_, _) => _monitors.Enabled = _selectedMonitors.Checked;
        _selectedMonitors.Text = Loc.T("entry.selected");
        _selectedMonitors.AutoSize = true;
        _selectedMonitors.CheckedChanged += (_, _) => _monitors.Enabled = _selectedMonitors.Checked;

        var monPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            Height = 160
        };
        monPanel.Controls.Add(_allMonitors);
        monPanel.Controls.Add(_selectedMonitors);
        _monitors.Height = 100;
        _monitors.Width = 320;
        _monitors.CheckOnClick = true;
        foreach (var m in _monitorList)
        {
            string label = m.HdrSupported ? m.FriendlyName : $"{m.FriendlyName} {Loc.T("entry.noHdr")}";
            _monitors.Items.Add(new MonitorItem(m, label));
        }
        monPanel.Controls.Add(_monitors);

        AddLabel(root, Loc.T("entry.switchOn"));
        root.Controls.Add(monPanel, 1, root.RowCount - 1);

        // OK / Abbrechen
        var okCancel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(16, 10, 16, 10)
        };
        var ok = new Button { Text = Loc.T("common.ok"), Width = 100, Height = 32, Tag = "primary", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = Loc.T("common.cancel"), Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (ValidateInput()) { SaveToResult(); } else DialogResult = DialogResult.None; };
        okCancel.Controls.Add(ok);
        okCancel.Controls.Add(cancel);

        Controls.Add(root);
        Controls.Add(okCancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static void AddLabel(TableLayoutPanel root, string text)
    {
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var lbl = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 0, 3) };
        root.Controls.Add(lbl, 0, root.RowCount - 1);
    }

    private static void AddRow(TableLayoutPanel root, string label, Control control)
    {
        AddLabel(root, label);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);
        root.Controls.Add(control, 1, root.RowCount - 1);
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
