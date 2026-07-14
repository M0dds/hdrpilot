using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Einstellungsdialog im Windows-11-Stil: Sektionen als Cards
/// (Darstellung / Verhalten), Akzent-Button zum Speichern.
/// Arbeitet auf einer Kopie; erst "Speichern" übernimmt die Werte.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly ModernComboBox _language = new();
    private readonly ModernComboBox _theme = new();
    private readonly ModernComboBox _target = new();
    private readonly CheckBox _autostart = new();
    private readonly CheckBox _notify = new();
    private readonly CheckBox _restore = new();
    private readonly ModernComboBox _onDelay = new();
    private readonly ModernComboBox _offDelay = new();

    // Auswahlwerte der Verzögerungs-Dropdowns (Presets + ggf. individueller Wert aus config.json)
    private readonly List<int> _onDelayValues = new();
    private readonly List<int> _offDelayValues = new();
    private static readonly int[] DelayPresets = { 0, 250, 500, 750, 1000, 1500, 2000, 3000, 5000, 10000 };

    private readonly AppConfig _config;

    /// <summary>Wird beim Speichern mit der aktualisierten Konfiguration ausgelöst.</summary>
    public event Action<AppConfig>? Saved;

    // Reihenfolge der Combo-Einträge -> Konfigurationswerte
    private static readonly string[] LanguageCodes = { "system", "de", "en", "fr", "es" };

    public SettingsForm(AppConfig config)
    {
        _config = config;

        Text = Loc.T("set.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = UiFonts.Body();
        ClientSize = new Size(500, 560);

        BuildLayout();
        LoadFromConfig();
        ThemeManager.Apply(this);
    }

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

        // ---- Überschrift ----
        AddRootRow(root, new Label
        {
            Text = Loc.T("set.title"),
            AutoSize = true,
            Font = UiFonts.Display(16f),
            Margin = new Padding(0, 0, 0, 14)
        });

        // ---- Card: Darstellung ----
        AddRootRow(root, SectionLabel(Loc.T("set.appearance")));

        var appearance = NewCardTable();
        _language.Items.AddRange(new[]
        {
            Loc.T("theme.system"), "Deutsch", "English", "Français", "Español"
        });
        AddRow(appearance, Loc.T("set.language"), _language);

        _theme.Items.AddRange(new[]
        {
            Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark")
        });
        AddRow(appearance, Loc.T("set.theme"), _theme);

        AddRootRow(root, WrapInCard(appearance));

        // ---- Card: Verhalten ----
        AddRootRow(root, SectionLabel(Loc.T("set.behavior")));

        var behavior = NewCardTable();
        _autostart.Text = Loc.T("tray.menu.autostart");
        _autostart.AutoSize = true;
        AddWide(behavior, _autostart);

        _notify.Text = Loc.T("set.notify");
        _notify.AutoSize = true;
        AddWide(behavior, _notify);

        _restore.Text = Loc.T("set.restore");
        _restore.AutoSize = true;
        _restore.MaximumSize = new Size(415, 0); // Umbruch für lange Übersetzungen
        AddWide(behavior, _restore);

        _target.Items.AddRange(new[] { Loc.T("target.primary"), Loc.T("target.all") });
        AddRow(behavior, Loc.T("set.target"), _target);

        AddRow(behavior, Loc.T("set.onDelay"), _onDelay);
        AddRow(behavior, Loc.T("set.offDelay"), _offDelay);

        AddRootRow(root, WrapInCard(behavior));

        // ---- Buttonleiste ----
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 58,
            Padding = new Padding(20, 12, 20, 12)
        };
        // Gleiche Margins, sonst stehen die Buttons im FlowLayout versetzt.
        var save = new ModernButton { Text = Loc.T("set.save"), Primary = true, Width = 120, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new ModernButton { Text = Loc.T("common.cancel"), Width = 120, Margin = new Padding(0) };
        save.Click += (_, _) => DoSave();
        // Nicht-modal geöffnet -> DialogResult schließt nicht, explizit schließen.
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    // ---- Layout-Helfer ----

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiFonts.Strong(10.5f),
        Margin = new Padding(2, 0, 0, 6)
    };

    private static TableLayoutPanel NewCardTable()
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    private static CardPanel WrapInCard(Control content)
    {
        var card = new CardPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 16),
            Dock = DockStyle.Top
        };
        card.Controls.Add(content);
        return card;
    }

    private static void AddRootRow(TableLayoutPanel root, Control control)
    {
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (control is CardPanel) control.Dock = DockStyle.Top;
        root.Controls.Add(control, 0, root.RowCount - 1);
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, bool fill = true)
    {
        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 6)
        }, 0, table.RowCount - 1);

        if (fill) control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 1, table.RowCount - 1);
    }

    private static void AddWide(TableLayoutPanel table, Control control)
    {
        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 5, 0, 5);
        table.Controls.Add(control, 0, table.RowCount - 1);
        table.SetColumnSpan(control, 2);
    }

    private void LoadFromConfig()
    {
        int langIdx = Array.IndexOf(LanguageCodes, _config.Language.ToLowerInvariant());
        _language.SelectedIndex = langIdx >= 0 ? langIdx : 0;

        _theme.SelectedIndex = _config.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };

        _autostart.Checked = _config.StartWithWindows;
        _notify.Checked = _config.ShowNotifications;
        _restore.Checked = _config.RestorePreviousState;
        _target.SelectedIndex = _config.TargetMode == TargetMode.PrimaryOnly ? 0 : 1;
        FillDelayCombo(_onDelay, _onDelayValues, _config.OnDebounceMs);
        FillDelayCombo(_offDelay, _offDelayValues, _config.OffDebounceMs);
    }

    /// <summary>
    /// Befüllt ein Verzögerungs-Dropdown mit den Presets; ein abweichender Wert
    /// aus der config.json wird einsortiert, damit nichts verloren geht.
    /// </summary>
    private static void FillDelayCombo(ModernComboBox combo, List<int> values, int current)
    {
        values.Clear();
        values.AddRange(DelayPresets);
        if (!values.Contains(current))
        {
            values.Add(current);
            values.Sort();
        }
        combo.Items.Clear();
        combo.Items.AddRange(values.Select(v => $"{v} ms"));
        combo.SelectedIndex = values.IndexOf(current);
    }

    private void DoSave()
    {
        _config.Language = LanguageCodes[Math.Max(0, _language.SelectedIndex)];
        _config.Theme = _theme.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };
        _config.StartWithWindows = _autostart.Checked;
        _config.ShowNotifications = _notify.Checked;
        _config.RestorePreviousState = _restore.Checked;
        _config.TargetMode = _target.SelectedIndex == 0 ? TargetMode.PrimaryOnly : TargetMode.AllHdrCapable;
        _config.OnDebounceMs = _onDelayValues[Math.Max(0, _onDelay.SelectedIndex)];
        _config.OffDebounceMs = _offDelayValues[Math.Max(0, _offDelay.SelectedIndex)];

        Saved?.Invoke(_config);
        Close();
    }
}
