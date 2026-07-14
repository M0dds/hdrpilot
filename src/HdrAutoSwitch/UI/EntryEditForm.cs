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
    private readonly WhitelistEntry _original;

    public WhitelistEntry Result { get; private set; }

    public EntryEditForm(WhitelistEntry entry, List<MonitorInfo> monitors)
    {
        _original = entry;
        _monitorList = monitors;
        Result = entry;

        Text = "Eintrag bearbeiten";
        Width = 480;
        Height = 500;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        LoadFromEntry(entry);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = false
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(root, "Anzeigename:", _name);
        AddRow(root, "Prozessname:", _processName);

        // Pfad mit Durchsuchen-Button
        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Height = 28, Margin = new Padding(0, 3, 0, 3) };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        _path.Dock = DockStyle.Fill;
        var browse = new Button { Text = "…", Dock = DockStyle.Fill };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _path.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(_processName.Text))
                    _processName.Text = Path.GetFileName(dlg.FileName);
            }
        };
        pathPanel.Controls.Add(_path, 0, 0);
        pathPanel.Controls.Add(browse, 1, 0);
        AddLabel(root, "Pfad zur .exe:");
        root.Controls.Add(pathPanel, 1, root.RowCount - 1);

        // Erkennungsmodus
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(new object[]
        {
            "Prozessname",
            "Voller Pfad",
            "Name oder Pfad (empfohlen)"
        });
        _mode.Dock = DockStyle.Fill;
        _mode.Margin = new Padding(0, 3, 0, 3);
        AddLabel(root, "Erkennung:");
        root.Controls.Add(_mode, 1, root.RowCount - 1);

        _enabled.Text = "Eintrag aktiv";
        _enabled.AutoSize = true;
        AddLabel(root, "");
        root.Controls.Add(_enabled, 1, root.RowCount - 1);

        // Monitor-Ziel
        _allMonitors.Text = "Alle HDR-fähigen Monitore";
        _allMonitors.AutoSize = true;
        _allMonitors.CheckedChanged += (_, _) => _monitors.Enabled = _selectedMonitors.Checked;
        _selectedMonitors.Text = "Nur ausgewählte Monitore:";
        _selectedMonitors.AutoSize = true;
        _selectedMonitors.CheckedChanged += (_, _) => _monitors.Enabled = _selectedMonitors.Checked;

        var monPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            Height = 150
        };
        monPanel.Controls.Add(_allMonitors);
        monPanel.Controls.Add(_selectedMonitors);
        _monitors.Height = 100;
        _monitors.Width = 320;
        _monitors.CheckOnClick = true;
        foreach (var m in _monitorList)
        {
            string label = m.HdrSupported ? m.FriendlyName : $"{m.FriendlyName} (kein HDR)";
            _monitors.Items.Add(new MonitorItem(m, label));
        }
        monPanel.Controls.Add(_monitors);

        AddLabel(root, "HDR schalten auf:");
        root.Controls.Add(monPanel, 1, root.RowCount - 1);

        // OK / Abbrechen
        var okCancel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(12, 6, 12, 6)
        };
        var ok = new Button { Text = "OK", Width = 90, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Abbrechen", Width = 90, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => { if (Validate2()) { SaveToResult(); } else DialogResult = DialogResult.None; };
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

    private bool Validate2()
    {
        if (string.IsNullOrWhiteSpace(_processName.Text) && string.IsNullOrWhiteSpace(_path.Text))
        {
            MessageBox.Show(this, "Bitte mindestens einen Prozessnamen oder einen Pfad angeben.",
                "Unvollständig", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                ? (_processName.Text.Length > 0 ? _processName.Text : "Eintrag")
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
