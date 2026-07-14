using HdrAutoSwitch.Models;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Einstellungsdialog: Sprache, Design, Autostart, Benachrichtigungen,
/// Zustands-Wiederherstellung, Ziel-Monitore und Debounce-Zeiten.
/// Arbeitet auf einer Kopie; erst "Speichern" übernimmt die Werte.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly ComboBox _language = new();
    private readonly ComboBox _theme = new();
    private readonly ComboBox _target = new();
    private readonly CheckBox _autostart = new();
    private readonly CheckBox _notify = new();
    private readonly CheckBox _restore = new();
    private readonly NumericUpDown _onDelay = new();
    private readonly NumericUpDown _offDelay = new();

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
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(440, 420);

        BuildLayout();
        LoadFromConfig();
        ThemeManager.Apply(this);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(20, 16, 20, 12),
            AutoSize = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddHeading(root, Loc.T("set.appearance"), first: true);

        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Items.AddRange(new object[]
        {
            Loc.T("theme.system"), "Deutsch", "English", "Français", "Español"
        });
        AddRow(root, Loc.T("set.language"), _language);

        _theme.DropDownStyle = ComboBoxStyle.DropDownList;
        _theme.Items.AddRange(new object[]
        {
            Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark")
        });
        AddRow(root, Loc.T("set.theme"), _theme);

        AddHeading(root, Loc.T("set.behavior"));

        _autostart.Text = Loc.T("tray.menu.autostart");
        _autostart.AutoSize = true;
        AddWide(root, _autostart);

        _notify.Text = Loc.T("set.notify");
        _notify.AutoSize = true;
        AddWide(root, _notify);

        _restore.Text = Loc.T("set.restore");
        _restore.AutoSize = true;
        _restore.MaximumSize = new Size(390, 0); // Umbruch für lange Übersetzungen
        AddWide(root, _restore);

        _target.DropDownStyle = ComboBoxStyle.DropDownList;
        _target.Items.AddRange(new object[] { Loc.T("target.primary"), Loc.T("target.all") });
        AddRow(root, Loc.T("set.target"), _target);

        _onDelay.Minimum = 0;
        _onDelay.Maximum = 30000;
        _onDelay.Increment = 250;
        _onDelay.Width = 90;
        AddRow(root, Loc.T("set.onDelay"), _onDelay, fill: false);

        _offDelay.Minimum = 0;
        _offDelay.Maximum = 30000;
        _offDelay.Increment = 250;
        _offDelay.Width = 90;
        AddRow(root, Loc.T("set.offDelay"), _offDelay, fill: false);

        // Buttonleiste
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(16, 10, 16, 10)
        };
        var save = new Button { Text = Loc.T("set.save"), Width = 110, Height = 32, Tag = "primary", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = Loc.T("common.cancel"), Width = 110, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => DoSave();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddHeading(TableLayoutPanel root, string text, bool first = false)
    {
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var lbl = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            Margin = new Padding(0, first ? 0 : 18, 0, 8)
        };
        root.Controls.Add(lbl, 0, root.RowCount - 1);
        root.SetColumnSpan(lbl, 2);
    }

    private static void AddRow(TableLayoutPanel root, string label, Control control, bool fill = true)
    {
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 0, 5)
        }, 0, root.RowCount - 1);

        if (fill) control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);
        root.Controls.Add(control, 1, root.RowCount - 1);
    }

    private static void AddWide(TableLayoutPanel root, Control control)
    {
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 4, 0, 4);
        root.Controls.Add(control, 0, root.RowCount - 1);
        root.SetColumnSpan(control, 2);
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
        _onDelay.Value = Math.Clamp(_config.OnDebounceMs, 0, 30000);
        _offDelay.Value = Math.Clamp(_config.OffDebounceMs, 0, 30000);
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
        _config.OnDebounceMs = (int)_onDelay.Value;
        _config.OffDebounceMs = (int)_offDelay.Value;

        Saved?.Invoke(_config);
        Close();
    }
}
