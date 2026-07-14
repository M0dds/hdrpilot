using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Schriftarten im Windows-11-Stil: Segoe UI Variable (auf Win11 vorinstalliert),
/// mit Fallback auf Segoe UI für ältere Systeme.
/// </summary>
internal static class UiFonts
{
    private static readonly string BodyFamily = Pick("Segoe UI Variable Text", "Segoe UI");
    private static readonly string DisplayFamily = Pick("Segoe UI Variable Display", "Segoe UI");

    private static string Pick(params string[] preferred)
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            var installed = fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in preferred)
                if (installed.Contains(name))
                    return name;
        }
        catch { /* Fallback unten */ }
        return "Segoe UI";
    }

    /// <summary>Fließtext (Win11 "Body").</summary>
    public static Font Body(float size = 9.75f) => new(BodyFamily, size);

    /// <summary>Hervorgehobener Text (Win11 "Body Strong").</summary>
    public static Font Strong(float size = 9.75f) => new(BodyFamily, size, FontStyle.Bold);

    /// <summary>Überschriften (Win11 "Subtitle"/"Title").</summary>
    public static Font Display(float size) => new(DisplayFamily, size, FontStyle.Bold);
}

/// <summary>
/// Gemeinsame Layout-Bausteine für Dialoge im Card-Stil
/// (Sektionstitel + abgerundete Karten mit Label/Feld-Zeilen).
/// </summary>
internal static class CardLayout
{
    /// <summary>Sektionstitel über einer Card.</summary>
    public static Label Section(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiFonts.Strong(10.5f),
        Margin = new Padding(2, 0, 0, 6)
    };

    /// <summary>Zweispaltige Tabelle (Label | Feld) für den Inhalt einer Card.</summary>
    public static TableLayoutPanel NewCardTable(int labelWidth = 180)
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    /// <summary>Packt den Inhalt in eine Card mit einheitlichem Innenabstand.</summary>
    public static CardPanel WrapInCard(Control content)
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

    /// <summary>Fügt eine Zeile in die einspaltige Wurzel-Tabelle eines Dialogs ein.</summary>
    public static void AddRootRow(TableLayoutPanel root, Control control)
    {
        root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (control is CardPanel) control.Dock = DockStyle.Top;
        root.Controls.Add(control, 0, root.RowCount - 1);
    }

    /// <summary>Label/Feld-Zeile in einer Card-Tabelle.</summary>
    public static void AddRow(TableLayoutPanel table, string label, Control control, bool fill = true)
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

    /// <summary>Zeile über beide Spalten (z. B. Checkboxen, Listen).</summary>
    public static void AddWide(TableLayoutPanel table, Control control)
    {
        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 5, 0, 5);
        table.Controls.Add(control, 0, table.RowCount - 1);
        table.SetColumnSpan(control, 2);
    }

    /// <summary>
    /// Einheitliche Fußleiste: optionaler Button links, OK/Abbrechen-Gruppe rechts.
    /// Seitliche Innenabstände = <paramref name="sidePadding"/> (auf einer Flucht
    /// mit dem Inhalt), unten genauso viel Luft wie zu den Seiten.
    /// </summary>
    public static TableLayoutPanel Footer(int sidePadding, Control? left, params ModernButton[] rightGroup)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            Height = 34 + 12 + sidePadding,
            Padding = new Padding(sidePadding, 12, sidePadding, sidePadding)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        if (left is not null)
        {
            left.Margin = new Padding(0);
            footer.Controls.Add(left, 0, 0);
        }

        var group = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0)
        };
        // Erster Button der Gruppe = ganz rechts; einheitliche Margins halten alles bündig.
        for (int i = 0; i < rightGroup.Length; i++)
        {
            var b = rightGroup[i];
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(96, 34);
            b.Margin = i == 0 ? new Padding(8, 0, 0, 0) : new Padding(0);
            group.Controls.Add(b);
        }
        footer.Controls.Add(group, 1, 0);
        return footer;
    }
}

/// <summary>Gemeinsame Zeichen-Helfer.</summary>
internal static class Win11Paint
{
    /// <summary>Pfad eines Rechtecks mit abgerundeten Ecken.</summary>
    public static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
}

/// <summary>
/// Button im Windows-11-Stil: abgerundete Ecken, flache Flächen, Hover-/Pressed-
/// Zustände. <see cref="Primary"/> macht ihn zur Akzentfläche (Fluent "Accent Button").
/// Farben kommen zur Zeichenzeit aus <see cref="ThemeManager.Palette"/>.
/// </summary>
internal sealed class ModernButton : Button
{
    private bool _hover;
    private bool _down;

    /// <summary>True = Akzent-Button (gefüllte Hauptaktion), false = Standard-Button.</summary>
    public bool Primary { get; set; }

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        MinimumSize = new Size(0, 34);
        Padding = new Padding(10, 0, 10, 0);

        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; _down = false; Invalidate(); };
        MouseDown += (_, _) => { _down = true; Invalidate(); };
        MouseUp += (_, _) => { _down = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Palette;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Hinter den runden Ecken muss die Farbe des Parents liegen.
        g.Clear(Parent?.BackColor ?? p.WindowBack);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color back, fore;
        if (Primary)
        {
            back = !Enabled ? Win11Paint.Blend(p.Accent, p.WindowBack, 0.55f)
                 : _down ? Win11Paint.Blend(p.Accent, p.WindowBack, 0.2f)
                 : _hover ? p.AccentHover
                 : p.Accent;
            fore = Enabled ? p.AccentText : Win11Paint.Blend(p.AccentText, back, 0.4f);
        }
        else
        {
            back = _down ? p.HeaderBack : _hover ? p.Hover : p.Surface;
            fore = Enabled ? p.Text : p.TextMuted;
        }

        using (var path = Win11Paint.RoundedRect(rect, 5))
        {
            using var brush = new SolidBrush(back);
            g.FillPath(brush, path);
            if (!Primary)
            {
                using var pen = new Pen(p.Border);
                g.DrawPath(pen, path);
            }
            if (Focused && ShowFocusCues)
            {
                using var focus = new Pen(p.Accent) { DashStyle = DashStyle.Dot };
                g.DrawPath(focus, path);
            }
        }

        TextRenderer.DrawText(g, Text, Font, rect, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Vollständig selbst gezeichnete Dropdown-Auswahl im Windows-11-Stil.
///
/// Hintergrund: Die native WinForms-ComboBox (DropDownList) lässt sich unter
/// Visual Styles nicht zuverlässig dunkel einfärben - der Flat-Adapter füllt
/// teils mit Systemfarben. Diese Control zeichnet die geschlossene Box selbst
/// (Palette-Farben, runde Ecken, dezente Outline) und öffnet als Popup eine
/// Owner-Draw-ListBox, die sich zuverlässig färben lässt.
/// </summary>
internal sealed class ModernComboBox : Control
{
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private bool _hover;
    private Form? _popup;
    private long _popupClosedAtTicks;

    public event EventHandler? SelectedIndexChanged;

    public List<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = Math.Max(-1, Math.Min(value, _items.Count - 1));
            if (clamped == _selectedIndex) return;
            _selectedIndex = clamped;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public ModernComboBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        Height = 34;
        MinimumSize = new Size(0, 34);
        TabStop = true;
        Cursor = Cursors.Hand;

        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
        GotFocus += (_, _) => Invalidate();
        LostFocus += (_, _) => Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Palette;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.WindowBack);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Win11Paint.RoundedRect(rect, 5))
        {
            using var back = new SolidBrush(_hover ? p.Hover : p.InputBack);
            g.FillPath(back, path);
            // Dezente Outline aus der Palette - nie Systemweiß.
            using var pen = new Pen(Focused ? p.Accent : p.Border);
            g.DrawPath(pen, path);
        }

        // Text links
        var textRect = new Rectangle(10, 0, Width - 36, Height);
        TextRenderer.DrawText(g, SelectedItem ?? "", Font, textRect,
            Enabled ? p.Text : p.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // Chevron rechts (von Hand gezeichnet, damit er in jeder Schrift gleich aussieht)
        int cx = Width - 20;
        int cy = Height / 2 - 1;
        using var chevron = new Pen(p.TextMuted, 1.6f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        g.DrawLines(chevron, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left) TogglePopup();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Down when e.Alt:
            case Keys.Enter:
            case Keys.Space:
                TogglePopup();
                e.Handled = true;
                break;
            case Keys.Down:
                SelectedIndex = Math.Min(_selectedIndex + 1, _items.Count - 1);
                e.Handled = true;
                break;
            case Keys.Up:
                SelectedIndex = Math.Max(_selectedIndex - 1, 0);
                e.Handled = true;
                break;
        }
    }

    private void TogglePopup()
    {
        if (_popup is { Visible: true })
        {
            _popup.Close();
            return;
        }
        if (_items.Count == 0) return;

        // Klick auf die Box, während das Popup offen ist: Deactivate hat es gerade
        // geschlossen - nicht sofort wieder öffnen ("Reopen-Flattern").
        if (Environment.TickCount64 - _popupClosedAtTicks < 250) return;

        // Rahmenlose, selbst gezeichnete Popup-Form statt ToolStripDropDown:
        // Letzteres zeichnet einen unkontrollierbaren hellen Non-Client-Rand.
        var list = new PopupList(_items, _selectedIndex, Font)
        {
            Dock = DockStyle.Fill,
            Width = Math.Max(Width, 120)
        };

        var popup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            Size = new Size(Math.Max(Width, 120), list.PreferredListHeight),
            KeyPreview = true
        };
        popup.Controls.Add(list);

        // Position: unter der Box; falls kein Platz nach unten, darüber aufklappen.
        var screenPos = PointToScreen(new Point(0, Height + 2));
        var workArea = Screen.FromControl(this).WorkingArea;
        if (screenPos.Y + popup.Height > workArea.Bottom)
            screenPos = PointToScreen(new Point(0, -popup.Height - 2));
        popup.Location = screenPos;

        list.Committed += idx =>
        {
            SelectedIndex = idx;
            popup.Close();
        };
        popup.Deactivate += (_, _) => popup.Close();   // Klick außerhalb schließt
        popup.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) popup.Close(); };
        popup.FormClosed += (_, _) =>
        {
            _popupClosedAtTicks = Environment.TickCount64;
            _popup = null;
            Invalidate();
        };

        _popup = popup;
        popup.Show(FindForm());
        popup.Activate();
    }

    /// <summary>
    /// Vollständig selbst gezeichnete Popup-Liste (kein natives ListBox-Rendering,
    /// keine System-Auswahlfarben). Hover per Maus, Klick übernimmt.
    /// </summary>
    private sealed class PopupList : Control
    {
        private const int ItemHeight = 32;
        private readonly List<string> _items;
        private int _hot;

        public event Action<int>? Committed;

        /// <summary>Gesamthöhe der Liste inkl. Rahmen (für die Popup-Form).</summary>
        public int PreferredListHeight => _items.Count * ItemHeight + 2;

        public PopupList(List<string> items, int selectedIndex, Font font)
        {
            _items = items;
            _hot = selectedIndex;
            Font = font;
            Height = PreferredListHeight;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Palette;
            var g = e.Graphics;
            g.Clear(p.Surface);

            // Eigener 1px-Rahmen in Palettenfarbe (nie System-Weiß)
            using (var border = new Pen(p.Border))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            for (int i = 0; i < _items.Count; i++)
            {
                var bounds = new Rectangle(0, i * ItemHeight, Width, ItemHeight);
                if (i == _hot)
                {
                    using var hover = new SolidBrush(p.Hover);
                    g.FillRectangle(hover, Rectangle.Inflate(bounds, -3, -2));
                    using var accent = new SolidBrush(p.Accent);
                    g.FillRectangle(accent, bounds.X + 3, bounds.Y + 9, 3, bounds.Height - 18);
                }
                TextRenderer.DrawText(g, _items[i], Font,
                    new Rectangle(bounds.X + 12, bounds.Y, bounds.Width - 16, bounds.Height), p.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = e.Y / ItemHeight;
            if (idx >= 0 && idx < _items.Count && idx != _hot)
            {
                _hot = idx;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int idx = e.Y / ItemHeight;
            if (idx >= 0 && idx < _items.Count)
                Committed?.Invoke(idx);
        }
    }
}

/// <summary>
/// Abgerundete "Card"-Fläche im Windows-11-Stil (heller/dunkler als der
/// Fensterhintergrund, feiner Rahmen). Für Listen und Einstellungsgruppen.
/// </summary>
internal sealed class CardPanel : Panel
{
    public int Radius { get; set; } = 8;

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Palette;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.WindowBack);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Win11Paint.RoundedRect(rect, Radius);
        using var brush = new SolidBrush(p.Surface);
        g.FillPath(brush, path);
        using var pen = new Pen(p.Border);
        g.DrawPath(pen, path);
    }
}
