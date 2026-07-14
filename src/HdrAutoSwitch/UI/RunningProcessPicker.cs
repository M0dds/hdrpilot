using System.Diagnostics;
using HdrAutoSwitch.Core;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Dialog, der aktuell laufende Prozesse mit Fenster/Namen listet,
/// damit der Nutzer ein Programm bequem auswählen kann.
/// </summary>
public sealed class RunningProcessPicker : Form
{
    private readonly ListView _list = new();
    private readonly TextBox _filter = new();
    private List<ProcessEvent> _all = new();

    public ProcessEvent? SelectedProcess { get; private set; }

    public RunningProcessPicker()
    {
        Text = "Laufendes Programm auswählen";
        Width = 560;
        Height = 480;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        LoadProcesses();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        filterPanel.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        _filter.Width = 300;
        _filter.TextChanged += (_, _) => ApplyFilter();
        filterPanel.Controls.Add(_filter);
        root.Controls.Add(filterPanel, 0, 0);

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.Columns.Add("Prozess", 180);
        _list.Columns.Add("Pfad", 320);
        _list.DoubleClick += (_, _) => Accept();
        root.Controls.Add(_list, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var ok = new Button { Text = "Auswählen", Width = 100, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Abbrechen", Width = 100, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Accept();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void LoadProcesses()
    {
        // Nur Prozesse mit sichtbarem Fenster oder eindeutigem Namen; nach Name gruppiert.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _all = ProcessWatcher.EnumerateRunning()
            .Where(p => !string.IsNullOrEmpty(p.ProcessName))
            .Where(p => seen.Add(p.ProcessName)) // Duplikate nach Name entfernen
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

            _list.Items.Add(new ListViewItem(new[] { p.ProcessName, p.FullPath ?? "(Pfad nicht lesbar)" }) { Tag = p });
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
