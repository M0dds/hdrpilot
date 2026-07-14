using HdrAutoSwitch.Core;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Dialog, der aktuell laufende Prozesse listet, damit der Nutzer ein
/// Programm bequem auswählen kann.
/// </summary>
public sealed class RunningProcessPicker : Form
{
    private readonly ListView _list = new();
    private readonly ModernTextBox _filter = new();
    private List<ProcessEvent> _all = new();

    public ProcessEvent? SelectedProcess { get; private set; }

    public RunningProcessPicker()
    {
        Text = Loc.T("picker.title");
        Width = 580;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        Font = UiFonts.Body();

        BuildLayout();
        LoadProcesses();
        ThemeManager.Apply(this);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(24, 18, 24, 8) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8) };
        filterPanel.Controls.Add(new Label { Text = Loc.T("picker.filter"), AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        _filter.Width = 300;
        _filter.TextChanged += (_, _) => ApplyFilter();
        filterPanel.Controls.Add(_filter);
        root.Controls.Add(filterPanel, 0, 0);

        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 8), Margin = new Padding(0) };
        _list.Tag = "native-scrollbars"; // lange, scrollende Liste -> dunkle Scrollbars
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.SmallImageList = new ImageList { ImageSize = new Size(1, 26) };
        _list.Columns.Add(Loc.T("picker.col.process"), 180);
        _list.Columns.Add(Loc.T("picker.col.path"), 320);
        _list.DoubleClick += (_, _) => Accept();
        _list.Resize += (_, _) =>
            _list.Columns[1].Width = Math.Max(160, _list.ClientSize.Width - _list.Columns[0].Width - 4);
        card.Controls.Add(_list);
        root.Controls.Add(card, 0, 1);

        var ok = new ModernButton { Text = Loc.T("picker.select"), Primary = true, DialogResult = DialogResult.OK };
        var cancel = new ModernButton { Text = Loc.T("common.cancel"), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Accept();

        Controls.Add(root);
        Controls.Add(CardLayout.Footer(24, null, ok, cancel));
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void LoadProcesses()
    {
        // Nach Name dedupliziert und sortiert.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _all = ProcessWatcher.EnumerateRunning()
            .Where(p => !string.IsNullOrEmpty(p.ProcessName))
            .Where(p => seen.Add(p.ProcessName))
            .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string f = _filter.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var p in _all)
        {
            if (f.Length > 0 &&
                !p.ProcessName.Contains(f, StringComparison.OrdinalIgnoreCase) &&
                !(p.FullPath?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;

            _list.Items.Add(new ListViewItem(new[] { p.ProcessName, p.FullPath ?? Loc.T("picker.noPath") }) { Tag = p });
        }
        _list.EndUpdate();
    }

    private void Accept()
    {
        if (_list.SelectedItems.Count > 0)
        {
            SelectedProcess = _list.SelectedItems[0].Tag as ProcessEvent;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
