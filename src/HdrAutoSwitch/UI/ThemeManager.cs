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
    private static AppTheme _mode = AppTheme.System;

    /// <summary>Konfigurierter Modus; System folgt der Windows-Einstellung "App-Modus".</summary>
    public static AppTheme Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            UpdatePreferredAppMode();
        }
    }

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
                case ModernButton or ModernComboBox or ModernTextBox or ModernItemList:
                    // Zeichnen sich selbst mit Palette-Farben - nichts zu tun.
                    break;

                case CardPanel card:
                    // Kinder (z. B. borderlose ListViews) erben die Kartenfarbe.
                    card.BackColor = p.Surface;
                    break;

                case Button b:
                    StyleButton(b, p);
                    break;

                case TextBox tb:
                    tb.BackColor = p.InputBack;
                    tb.ForeColor = p.Text;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    ApplyNativeTheme(tb, "Explorer");
                    break;

                case ComboBox cb:
                    StyleComboBox(cb, p);
                    break;

                case NumericUpDown nud:
                    StyleNumericUpDown(nud, p);
                    break;

                case ListView lv:
                    StyleListView(lv, p);
                    break;

                case CheckedListBox clb:
                    // In einer Card: rahmenlos in Kartenfarbe, sonst als Eingabefeld.
                    clb.BackColor = clb.Parent is CardPanel or TableLayoutPanel { Parent: CardPanel } ? p.Surface : p.InputBack;
                    clb.ForeColor = p.Text;
                    clb.BorderStyle = clb.Parent is CardPanel or TableLayoutPanel { Parent: CardPanel } ? BorderStyle.None : BorderStyle.FixedSingle;
                    ApplyNativeTheme(clb, "Explorer");
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
            // ModernTextBox verwaltet seine innere TextBox selbst - nicht restylen.
            if (c.HasChildren && c is not ModernTextBox)
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
    /// <summary>
    /// DropDownList-Combos ignorieren BackColor teils trotz FlatStyle.Flat.
    /// OwnerDrawFixed zeichnet Textfläche UND Listeneinträge deterministisch
    /// mit Palette-Farben; der Flat-Adapter übernimmt Rahmen und Pfeil.
    /// </summary>
    private static void StyleComboBox(ComboBox cb, ThemePalette p)
    {
        cb.BackColor = p.InputBack;
        cb.ForeColor = p.Text;
        cb.FlatStyle = FlatStyle.Flat;

        if (cb.DrawMode == DrawMode.OwnerDrawFixed) return; // schon verdrahtet
        cb.DrawMode = DrawMode.OwnerDrawFixed;
        cb.DrawItem += (_, e) =>
        {
            var pal = Palette;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
            using var back = new SolidBrush(selected && inList ? pal.Hover : pal.InputBack);
            e.Graphics.FillRectangle(back, e.Bounds);
            if (e.Index >= 0)
            {
                TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font,
                    Rectangle.Inflate(e.Bounds, -2, 0), pal.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        };
    }

    /// <summary>
    /// NumericUpDown setzt seine Farben beim Handle-Erstellen teilweise zurück;
    /// daher zusätzlich nach Handle-Erzeugung und beim Anzeigen erneut anwenden.
    /// </summary>
    private static void StyleNumericUpDown(NumericUpDown nud, ThemePalette p)
    {
        void SetColors()
        {
            nud.BackColor = p.InputBack;
            nud.ForeColor = p.Text;
            foreach (Control child in nud.Controls)
            {
                child.BackColor = p.InputBack;
                child.ForeColor = p.Text;
            }
        }
        nud.BorderStyle = BorderStyle.FixedSingle;
        SetColors();
        nud.HandleCreated += (_, _) => SetColors();
        if (nud.FindForm() is { } owner)
            owner.Shown += (_, _) => SetColors();
    }

    private static void StyleListView(ListView lv, ThemePalette p)
    {
        // In einer Card: rahmenlos und in Kartenfarbe, die Card liefert die Kontur.
        bool inCard = lv.Parent is CardPanel;
        lv.BackColor = inCard ? p.Surface : p.InputBack;
        lv.ForeColor = p.Text;
        lv.BorderStyle = inCard ? BorderStyle.None : BorderStyle.FixedSingle;

        if (lv.OwnerDraw) return; // schon verdrahtet (Apply wird nur einmal aufgerufen)
        lv.OwnerDraw = true;

        // Flimmerfrei zeichnen (DoubleBuffered ist bei ListView protected).
        typeof(ListView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(lv, true);

        // Hover-Zeile selbst verfolgen - das System-Hot-Tracking zeichnet im
        // Dark Mode schwarzen Text auf helle Auswahl.
        int hotIndex = -1;
        void InvalidateRow(int index)
        {
            if (index >= 0 && index < lv.Items.Count)
                lv.Invalidate(lv.Items[index].Bounds);
        }
        lv.MouseMove += (_, e) =>
        {
            int idx = lv.HitTest(e.Location).Item?.Index ?? -1;
            if (idx == hotIndex) return;
            int old = hotIndex;
            hotIndex = idx;
            InvalidateRow(old);
            InvalidateRow(hotIndex);
        };
        lv.MouseLeave += (_, _) =>
        {
            int old = hotIndex;
            hotIndex = -1;
            InvalidateRow(old);
        };

        lv.DrawColumnHeader += (_, e) =>
        {
            var pal = Palette;
            using var back = new SolidBrush(lv.Parent is CardPanel ? pal.Surface : pal.HeaderBack);
            e.Graphics.FillRectangle(back, e.Bounds);
            using var line = new Pen(pal.Border);
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            // Sortier-Indikator: Spalten-Tag "asc"/"desc" -> Chevron rechtsbündig
            // zeichnen (statt Text-Suffix, das in schmalen Spalten abgeschnitten würde).
            string? sort = e.Header?.Tag as string;
            int reserved = sort is null ? 4 : 18;

            // Einzug wie beim Zeilentext (Spalte 0: 12px, sonst 6px), damit
            // Header und Inhalt exakt fluchten.
            int indent = e.ColumnIndex == 0 ? 12 : 6;
            var textRect = new Rectangle(e.Bounds.X + indent, e.Bounds.Y,
                e.Bounds.Width - indent - reserved, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", lv.Font,
                textRect, pal.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            if (sort is "asc" or "desc")
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int cx = e.Bounds.Right - 13;
                int cy = e.Bounds.Y + e.Bounds.Height / 2;
                using var chevron = new Pen(pal.TextMuted, 1.6f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                e.Graphics.DrawLines(chevron, sort == "asc"
                    ? new[] { new Point(cx - 4, cy + 2), new Point(cx, cy - 2), new Point(cx + 4, cy + 2) }
                    : new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
            }
        };

        // Zeilen komplett selbst zeichnen: abgerundete Hover-/Auswahlfläche mit
        // Akzentstreifen (wie im Dropdown-Popup), Textfarben aus der Palette.
        lv.DrawItem += (_, e) =>
        {
            var pal = Palette;
            var g = e.Graphics;
            Color baseBack = lv.Parent is CardPanel ? pal.Surface : pal.InputBack;
            using (var back = new SolidBrush(baseBack))
                g.FillRectangle(back, e.Bounds);

            bool selected = e.Item?.Selected == true;
            bool hot = e.ItemIndex == hotIndex;
            if (!selected && !hot) return;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = Rectangle.Inflate(e.Bounds, -4, -2);
            using var path = Win11Paint.RoundedRect(r, 4);
            using var fill = new SolidBrush(selected ? pal.Hover : Win11Paint.Blend(baseBack, pal.Hover, 0.55f));
            g.FillPath(fill, path);
            if (selected)
            {
                using var accent = new SolidBrush(pal.Accent);
                g.FillRectangle(accent, r.X + 2, r.Y + (r.Height - 12) / 2, 3, 12);
            }
        };
        lv.DrawSubItem += (_, e) =>
        {
            var pal = Palette;
            // Quirk: Für Spalte 0 liefert e.Bounds die GESAMTE Zeile.
            int cellWidth = e.ColumnIndex == 0 && lv.Columns.Count > 0
                ? lv.Columns[0].Width
                : e.Bounds.Width;
            var textRect = new Rectangle(
                e.Bounds.X + (e.ColumnIndex == 0 ? 12 : 6), e.Bounds.Y,
                cellWidth - (e.ColumnIndex == 0 ? 16 : 10), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", lv.Font, textRect, pal.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };

        // Explorer-Dark-Theme liefert dunkle Scrollbars, zeichnet aber auch
        // Spaltenlinien in den Leerbereich unter den Zeilen. Deshalb nur für
        // Listen aktivieren, die praktisch immer gefüllt sind und scrollen
        // (Kennzeichnung per Tag = "native-scrollbars", z. B. Prozess-Picker).
        if (Equals(lv.Tag, "native-scrollbars"))
            ApplyNativeTheme(lv, "Explorer");

        // Der Füllbereich rechts der letzten Spalte wird vom System-Header
        // gezeichnet - per ItemsView-Theme ebenfalls dunkel schalten.
        void ThemeHeader()
        {
            IntPtr header = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (header != IntPtr.Zero)
                try { SetWindowTheme(header, IsDark ? "DarkMode_ItemsView" : "ItemsView", null); } catch { }
        }
        if (lv.IsHandleCreated) ThemeHeader();
        else lv.HandleCreated += (_, _) => ThemeHeader();
    }

    private const int LVM_GETHEADER = 0x101F;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    /// <summary>
    /// Zeichnet den Spaltenkopf einer ListView neu (eigenes Fenster - wird von
    /// Control.Invalidate nicht erfasst). Nötig z. B. nach Änderung des
    /// Sortier-Indikators über Column.Tag.
    /// </summary>
    public static void RefreshListViewHeader(ListView lv)
    {
        if (!lv.IsHandleCreated) return;
        IntPtr header = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
        if (header != IntPtr.Zero)
            InvalidateRect(header, IntPtr.Zero, true);
    }

    // ---- Native Fenster-Themes (uxtheme) ----

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    // Undokumentiert, aber seit Win10 1809 stabil und verbreitet im Einsatz:
    // schaltet die "DarkMode_*"-Fenster-Themes für den Prozess frei.
    // 0 = Standard, 1 = AllowDark, 2 = ForceDark.
    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int preferredAppMode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    private static void UpdatePreferredAppMode()
    {
        try
        {
            SetPreferredAppMode(IsDark ? 2 : 0);
            FlushMenuThemes();
        }
        catch
        {
            // Nur Kosmetik - ohne den Aufruf bleiben Combo-Scrollbars hell.
        }
    }

    /// <summary>
    /// Weist einem Control das helle bzw. dunkle native Fenster-Theme zu
    /// (z. B. "Explorer" / "DarkMode_Explorer" für dunkle Scrollbars,
    /// "CFD" / "DarkMode_CFD" für dunkle ComboBoxen). Ab Windows 10 1809.
    /// </summary>
    private static void ApplyNativeTheme(Control c, string baseTheme)
    {
        void Set()
        {
            try { SetWindowTheme(c.Handle, IsDark ? "DarkMode_" + baseTheme : baseTheme, null); }
            catch { /* rein kosmetisch */ }
        }
        if (c.IsHandleCreated) Set();
        else c.HandleCreated += (_, _) => Set();
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

    /// <summary>Färbt ein Menü/Dropdown (inkl. ContextMenuStrip) passend zum aktuellen Theme.</summary>
    public static void ApplyToMenu(ToolStripDropDown menu)
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
