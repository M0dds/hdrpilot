using HdrPilot.Core;

namespace HdrPilot.UI;

/// <summary>
/// Dialog, der aktuell laufende Prozesse listet, damit der Nutzer ein
/// Programm bequem auswählen kann. Nutzt die vollständig selbst gezeichnete
/// <see cref="ModernItemList"/> (inkl. eigener Scrollbar) - die native
/// ListView erzeugt mit Dark-Theme und Owner-Draw Render-Artefakte.
/// </summary>
public sealed class RunningProcessPicker : Form
{
    private readonly ModernItemList _list = new();
    private readonly ModernTextBox _filter = new();
    private List<ProcessEvent> _all = new();

    public ProcessEvent? SelectedProcess { get; private set; }

    public RunningProcessPicker()
    {
        Text = Loc.T("picker.title");
        Width = 580;
        Height = 520;
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

        var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 10) };
        filterPanel.Controls.Add(new Label { Text = Loc.T("picker.filter"), AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        _filter.Width = 300;
        _filter.Margin = new Padding(0);
        _filter.TextChanged += (_, _) => ApplyFilter();
        filterPanel.Controls.Add(_filter);
        root.Controls.Add(filterPanel, 0, 0);

        var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(6), Margin = new Padding(0) };
        _list.Dock = DockStyle.Fill;
        _list.PrimaryColumnWidth = 210;
        _list.ItemActivated += (_, _) => Accept();
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
        _list.SetItems(_all
            .Where(p => f.Length == 0 ||
                        p.ProcessName.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        (p.FullPath?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(p => new ModernItemList.Item
            {
                Primary = p.ProcessName,
                Secondary = p.FullPath ?? Loc.T("picker.noPath"),
                Tag = p
            }));
    }

    private void Accept()
    {
        if (_list.SelectedItem?.Tag is ProcessEvent p)
        {
            SelectedProcess = p;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
