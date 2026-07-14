using System.Runtime.InteropServices;
using HdrAutoSwitch.Models;
using Microsoft.Win32;

namespace HdrAutoSwitch.UI;

/// <summary>Farbpalette eines Themes.</summary>
internal sealed record ThemePalette(
    Color WindowBack,   // Fensterhintergrund
    Color Surface,      // Flächen (Buttons, Menü, Karten)
    Color InputBack,    // Eingabefelder / Listen
    Color HeaderBack,   // ListView-Spaltenköpfe
    Color Hover,        // Hover-Zustand
    Color Text,         // Primärtext
    Color TextMuted,    // Sekundärtext
    Color Border,       // Rahmen / Trennlinien
    Color Accent,       // Akzentfarbe (Primär-Buttons)
    Color AccentHover,
    Color AccentText,   // Text auf Akzentfläche
    bool IsDark);

/// <summary>
/// Zentrales Theming (Hell/Dunkel/System). WinForms unter .NET 8 hat keinen
/// eingebauten Dark Mode, daher werden alle Controls rekursiv eingefärbt und
/// die Titelleiste per DWM-Attribut umgeschaltet.
/// </summary>
internal static class ThemeManager
{
    /// <summary>Konfigurierter Modus; System folgt der Windows-Einstellung "App-Modus".</summary>
    public static AppTheme Mode { get; set; } = AppTheme.System;

    public static bool IsDark => Mode switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => SystemPrefersDark()
    };

    public static ThemePalette Palette => IsDark ? Dark : Light;

    private static readonly ThemePalette Light = new(
        WindowBack: Color.FromArgb(0xF3, 0xF3, 0xF3),
        Surface: Color.White,
        InputBack: Color.White,
        HeaderBack: Color.FromArgb(0xEC, 0xEC, 0xEC),
        Hover: Color.FromArgb(0xE6, 0xE6, 0xE6),
        Text: Color.FromArgb(0x1A, 0x1A, 0x1A),
        TextMuted: Color.FromArgb(0x5C, 0x5C, 0x5C),
        Border: Color.FromArgb(0xD5, 0xD5, 0xD5),
        Accent: Color.FromArgb(0x00, 0x67, 0xC0),   // Windows-11-Blau
        AccentHover: Color.FromArgb(0x19, 0x75, 0xC5),
        AccentText: Color.White,
        IsDark: false);

    private static readonly ThemePalette Dark = new(
        WindowBack: Color.FromArgb(0x20, 0x20, 0x20),
        Surface: Color.FromArgb(0x2B, 0x2B, 0x2B),
        InputBack: Color.FromArgb(0x33, 0x33, 0x33),
        HeaderBack: Color.FromArgb(0x2F, 0x2F, 0x2F),
        Hover: Color.FromArgb(0x3A, 0x3A, 0x3A),
        Text: Color.FromArgb(0xF0, 0xF0, 0xF0),
        TextMuted: Color.FromArgb(0xA6, 0xA6, 0xA6),
        Border: Color.FromArgb(0x45, 0x45, 0x45),
        Accent: Color.FromArgb(0x4C, 0xC2, 0xFF),   // Windows-11-Dark-Akzent
        AccentHover: Color.FromArgb(0x6F, 0xCD, 0xFF),
        AccentText: Color.FromArgb(0x1B, 0x1B, 0x1B),
        IsDark: true);

    /// <summary>Liest die Windows-Einstellung "App-Modus" (hell/dunkel).</summary>
    private static bool SystemPrefersDark()
    {
        try
        {
            object? v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Wendet das aktuelle Theme auf ein Formular und alle Kind-Controls an.
    /// Einmalig im Konstruktor aufrufen (nach dem Aufbau des Layouts).
    /// </summary>
    public static void Apply(Form form)
    {
        var p = Palette;
        form.BackColor = p.WindowBack;
        form.ForeColor = p.Text;
        ApplyDarkTitleBar(form);
        StyleChildren(form, p);
    }

    private static void StyleChildren(Control parent, ThemePalette p)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case Button b:
                    StyleButton(b, p);
                    break;

                case TextBox tb:
                    tb.BackColor = p.InputBack;
                    tb.ForeColor = p.Text;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox cb:
                    cb.BackColor = p.InputBack;
                    cb.ForeColor = p.Text;
                    cb.FlatStyle = FlatStyle.Flat;
                    break;

                case NumericUpDown nud:
                    nud.BackColor = p.InputBack;
                    nud.ForeColor = p.Text;
                    nud.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListView lv:
                    StyleListView(lv, p);
                    break;

                case CheckedListBox clb:
                    clb.BackColor = p.InputBack;
                    clb.ForeColor = p.Text;
                    clb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case Label lbl:
                    // Tag "muted" = Sekundärtext, Tag "heading" = Überschrift.
                    lbl.ForeColor = Equals(lbl.Tag, "muted") ? p.TextMuted : p.Text;
                    break;

                case CheckBox or RadioButton:
                    c.ForeColor = p.Text;
                    break;

                default:
                    c.ForeColor = p.Text;
                    break;
            }
            if (c.HasChildren)
                StyleChildren(c, p);
        }
    }

    /// <summary>Flat-Style; Tag "primary" macht den Button zur Akzentfläche.</summary>
    public static void StyleButton(Button b, ThemePalette p)
    {
        bool primary = Equals(b.Tag, "primary");
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = p.Border;
        b.BackColor = primary ? p.Accent : p.Surface;
        b.ForeColor = primary ? p.AccentText : p.Text;
        b.FlatAppearance.MouseOverBackColor = primary ? p.AccentHover : p.Hover;
        b.FlatAppearance.MouseDownBackColor = primary ? p.Accent : p.HeaderBack;
        b.Cursor = Cursors.Hand;
    }

    /// <summary>
    /// ListView einfärben. Die Spaltenköpfe werden selbst gezeichnet, weil sie
    /// sonst immer im hellen Systemstil erscheinen; die Zeilen zeichnet weiterhin
    /// das System (DrawDefault), nur mit unseren Farben.
    /// </summary>
    private static void StyleListView(ListView lv, ThemePalette p)
    {
        lv.BackColor = p.InputBack;
        lv.ForeColor = p.Text;
        lv.BorderStyle = BorderStyle.FixedSingle;

        if (lv.OwnerDraw) return; // schon verdrahtet (Apply wird nur einmal aufgerufen)
        lv.OwnerDraw = true;
        lv.DrawColumnHeader += (_, e) =>
        {
            var pal = Palette;
            using var back = new SolidBrush(pal.HeaderBack);
            e.Graphics.FillRectangle(back, e.Bounds);
            using var line = new Pen(pal.Border);
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", lv.Font,
                Rectangle.Inflate(e.Bounds, -6, 0), pal.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
        lv.DrawItem += (_, e) => e.DrawDefault = true;
        lv.DrawSubItem += (_, e) => e.DrawDefault = true;
    }

    // ---- Dunkle Titelleiste (DWM) ----

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private static void ApplyDarkTitleBar(Form form)
    {
        void Set()
        {
            int dark = IsDark ? 1 : 0;
            _ = DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        if (form.IsHandleCreated) Set();
        else form.HandleCreated += (_, _) => Set();
    }

    // ---- Kontextmenü (Tray) ----

    /// <summary>Färbt ein ContextMenuStrip passend zum aktuellen Theme.</summary>
    public static void ApplyToMenu(ContextMenuStrip menu)
    {
        var p = Palette;
        menu.Renderer = new ThemedMenuRenderer(p);
        menu.BackColor = p.Surface;
        menu.ForeColor = p.Text;
        foreach (ToolStripItem item in menu.Items)
        {
            item.BackColor = p.Surface;
            item.ForeColor = p.Text;
        }
    }

    private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemePalette _p;

        public ThemedMenuRenderer(ThemePalette p) : base(new ThemedColorTable(p)) => _p = p;

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _p.Text : _p.TextMuted;
            base.OnRenderItemText(e);
        }
    }

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly ThemePalette _p;
        public ThemedColorTable(ThemePalette p) { _p = p; UseSystemColors = false; }

        public override Color ToolStripDropDownBackground => _p.Surface;
        public override Color ImageMarginGradientBegin => _p.Surface;
        public override Color ImageMarginGradientMiddle => _p.Surface;
        public override Color ImageMarginGradientEnd => _p.Surface;
        public override Color MenuBorder => _p.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => _p.Hover;
        public override Color MenuItemSelectedGradientBegin => _p.Hover;
        public override Color MenuItemSelectedGradientEnd => _p.Hover;
        public override Color MenuItemPressedGradientBegin => _p.Hover;
        public override Color MenuItemPressedGradientEnd => _p.Hover;
        public override Color SeparatorDark => _p.Border;
        public override Color SeparatorLight => _p.Border;
        public override Color CheckBackground => _p.Hover;
        public override Color CheckSelectedBackground => _p.Hover;
        public override Color CheckPressedBackground => _p.Hover;
    }
}
