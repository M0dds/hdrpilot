using HdrPilot.Models;

namespace HdrPilot.UI;

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
        ClientSize = new Size(500, 590);

        BuildLayout();
        LoadFromConfig();
        ThemeManager.Apply(this);
        FitHeightToContent();
    }

    private TableLayoutPanel? _root;
    private Control? _footer;

    /// <summary>Passt die Fensterhöhe an den Inhalt an (keine Leerfläche über den Buttons).</summary>
    private void FitHeightToContent()
    {
        if (_root is null || _footer is null) return;
        int contentHeight = _root.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
        ClientSize = new Size(ClientSize.Width, contentHeight + _footer.Height);
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
        CardLayout.AddRootRow(root, new Label
        {
            Text = Loc.T("set.title"),
            AutoSize = true,
            Font = UiFonts.Display(16f),
            Margin = new Padding(0, 0, 0, 14)
        });

        // ---- Card: Darstellung ----
        CardLayout.AddRootRow(root, CardLayout.Section(Loc.T("set.appearance")));

        var appearance = CardLayout.NewCardTable();
        _language.Items.AddRange(new[]
        {
            Loc.T("theme.system"), "Deutsch", "English", "Français", "Español"
        });
        CardLayout.AddRow(appearance, Loc.T("set.language"), _language);

        _theme.Items.AddRange(new[]
        {
            Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark")
        });
        CardLayout.AddRow(appearance, Loc.T("set.theme"), _theme);

        CardLayout.AddRootRow(root, CardLayout.WrapInCard(appearance));

        // ---- Card: Verhalten ----
        CardLayout.AddRootRow(root, CardLayout.Section(Loc.T("set.behavior")));

        var behavior = CardLayout.NewCardTable();
        _autostart.Text = Loc.T("tray.menu.autostart");
        _autostart.AutoSize = true;
        CardLayout.AddWide(behavior, _autostart);

        _notify.Text = Loc.T("set.notify");
        _notify.AutoSize = true;
        CardLayout.AddWide(behavior, _notify);

        _restore.Text = Loc.T("set.restore");
        _restore.AutoSize = true;
        _restore.MaximumSize = new Size(415, 0); // Umbruch für lange Übersetzungen
        CardLayout.AddWide(behavior, _restore);

        _target.Items.AddRange(new[] { Loc.T("target.primary"), Loc.T("target.all") });
        CardLayout.AddRow(behavior, Loc.T("set.target"), _target);

        CardLayout.AddRow(behavior, Loc.T("set.onDelay"), _onDelay);
        CardLayout.AddRow(behavior, Loc.T("set.offDelay"), _offDelay);

        CardLayout.AddRootRow(root, CardLayout.WrapInCard(behavior));

        // ---- Fußleiste: seitlich auf einer Flucht mit den Cards (24px) ----
        var save = new ModernButton { Text = Loc.T("set.save"), Primary = true };
        var cancel = new ModernButton { Text = Loc.T("common.cancel") };
        save.Click += (_, _) => DoSave();
        // Nicht-modal geöffnet -> DialogResult schließt nicht, explizit schließen.
        cancel.Click += (_, _) => Close();

        var footer = CardLayout.Footer(24, null, save, cancel);
        Controls.Add(root);
        Controls.Add(footer);
        _root = root;
        _footer = footer;
        AcceptButton = save;
        CancelButton = cancel;
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
