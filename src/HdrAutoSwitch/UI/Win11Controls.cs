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
    private static readonly string IconFamily = Pick("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI");

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

    /// <summary>Symbolschrift (Segoe Fluent Icons, z. B. "" = More).</summary>
    public static Font Icon(float size = 10f) => new(IconFamily, size);
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
/// Eingabefeld im Windows-11-Stil: gleiche Maße wie <see cref="ModernComboBox"/>
/// (34px hoch, 5px-Radius, Palette-Outline, Akzentrahmen bei Fokus). Innen sitzt
/// eine rahmenlose TextBox, außen wird selbst gezeichnet.
/// </summary>
internal sealed class ModernTextBox : Control
{
    private readonly TextBox _inner = new() { BorderStyle = BorderStyle.None };

    public ModernTextBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 34;
        MinimumSize = new Size(0, 34);
        Cursor = Cursors.IBeam;

        _inner.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        _inner.GotFocus += (_, _) => Invalidate();
        _inner.LostFocus += (_, _) => Invalidate();
        Controls.Add(_inner);

        Click += (_, _) => _inner.Focus();
    }

    /// <summary>Der eingegebene Text (durchgereicht an die innere TextBox).</summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _inner.Text;
        set => _inner.Text = value ?? "";
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _inner.Focus();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _inner.Font = Font;
        LayoutInner();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutInner();
    }

    private void LayoutInner()
    {
        int innerHeight = _inner.PreferredHeight;
        _inner.SetBounds(10, Math.Max(1, (Height - innerHeight) / 2), Math.Max(10, Width - 20), innerHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Palette;
        // Farben der inneren TextBox immer mit der Palette synchron halten.
        _inner.BackColor = p.InputBack;
        _inner.ForeColor = p.Text;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.WindowBack);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Win11Paint.RoundedRect(rect, 5);
        using var back = new SolidBrush(p.InputBack);
        g.FillPath(back, path);
        using var pen = new Pen(_inner.Focused ? p.Accent : p.Border);
        g.DrawPath(pen, path);
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
        ThemeManager.RoundPopupCorners(popup); // Win11-Rundung (kleiner Radius wie Menüs)
        popup.Activate();
    }

    /// <summary>
    /// Vollständig selbst gezeichnete Popup-Liste (kein natives ListBox-Rendering,
    /// keine System-Auswahlfarben). Hover per Maus, Klick übernimmt.
    /// </summary>
    private sealed class PopupList : Control
    {
        private const int ItemHeight = 32;
        // Innen-Padding oben/unten, damit der erste/letzte Hover-Pill denselben
        // Abstand zum Rand hat wie seitlich (Pad + Pill-Einzug 2 = 4px).
        private const int Pad = 2;
        private readonly List<string> _items;
        private int _hot;

        public event Action<int>? Committed;

        /// <summary>Gesamthöhe der Liste inkl. Rahmen und Innen-Padding (für die Popup-Form).</summary>
        public int PreferredListHeight => _items.Count * ItemHeight + Pad * 2 + 2;

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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Eigener Rahmen in Palettenfarbe, Radius passend zur
            // DWM-Fensterrundung (DWMWCP_ROUND = ~8px)
            using (var border = new Pen(p.Border))
            using (var borderPath = Win11Paint.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                g.DrawPath(border, borderPath);

            for (int i = 0; i < _items.Count; i++)
            {
                var bounds = new Rectangle(0, Pad + i * ItemHeight, Width, ItemHeight);
                if (i == _hot)
                {
                    // Hover-Pill mit demselben Radius wie die Controls (5px)
                    var pill = Rectangle.Inflate(bounds, -4, -2);
                    using var hover = new SolidBrush(p.Hover);
                    using var pillPath = Win11Paint.RoundedRect(pill, 5);
                    g.FillPath(hover, pillPath);
                    using var accent = new SolidBrush(p.Accent);
                    g.FillRectangle(accent, pill.X + 2, pill.Y + (pill.Height - 12) / 2, 3, 12);
                }
                TextRenderer.DrawText(g, _items[i], Font,
                    new Rectangle(bounds.X + 12, bounds.Y, bounds.Width - 16, bounds.Height), p.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = (e.Y - Pad) / ItemHeight;
            if (idx >= 0 && idx < _items.Count && idx != _hot)
            {
                _hot = idx;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int idx = (e.Y - Pad) / ItemHeight;
            if (idx >= 0 && idx < _items.Count)
                Committed?.Invoke(idx);
        }
    }
}

/// <summary>
/// Vollständig selbst gezeichnete, scrollbare Auswahlliste (Primär- + Sekundärtext
/// pro Zeile) mit schlanker Win11-Scrollbar. Ersetzt die native ListView dort,
/// wo deren Theme-Rendering Artefakte erzeugt (z. B. Prozess-Picker).
/// </summary>
internal sealed class ModernItemList : Control
{
    public sealed class Item
    {
        public string Primary { get; init; } = "";
        public string Secondary { get; init; } = "";
        public object? Tag { get; init; }
    }

    private const int RowHeight = 30;
    private const int ThumbWidth = 6;

    private readonly List<Item> _items = new();
    private int _selectedIndex = -1;
    private int _hotIndex = -1;
    private int _scrollOffset;
    private bool _draggingThumb;
    private bool _thumbHot;
    private int _dragStartY;
    private int _dragStartOffset;

    /// <summary>Breite der Primärtext-Spalte; rechts davon steht der Sekundärtext.</summary>
    public int PrimaryColumnWidth { get; set; } = 200;

    public event EventHandler? SelectionChanged;

    /// <summary>Doppelklick oder Enter auf einem Eintrag.</summary>
    public event EventHandler? ItemActivated;

    public Item? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public ModernItemList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
    }

    public void SetItems(IEnumerable<Item> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = -1;
        _hotIndex = -1;
        _scrollOffset = 0;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private int ContentHeight => _items.Count * RowHeight;
    private int MaxScroll => Math.Max(0, ContentHeight - ClientSize.Height);

    private void SetScroll(int value)
    {
        int clamped = Math.Max(0, Math.Min(value, MaxScroll));
        if (clamped == _scrollOffset) return;
        _scrollOffset = clamped;
        Invalidate();
    }

    private Rectangle ThumbBounds
    {
        get
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int trackHeight = ClientSize.Height - 4;
            int thumbHeight = Math.Max(28, trackHeight * ClientSize.Height / Math.Max(1, ContentHeight));
            int y = 2 + (int)((long)(trackHeight - thumbHeight) * _scrollOffset / MaxScroll);
            return new Rectangle(ClientSize.Width - ThumbWidth - 3, y, ThumbWidth, thumbHeight);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ThemeManager.Palette;
        var g = e.Graphics;
        g.Clear(p.Surface);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int rowAreaWidth = ClientSize.Width - (MaxScroll > 0 ? ThumbWidth + 8 : 0);
        int first = Math.Max(0, _scrollOffset / RowHeight);

        for (int i = first; i < _items.Count; i++)
        {
            int y = i * RowHeight - _scrollOffset;
            if (y > ClientSize.Height) break;
            var rowBounds = new Rectangle(0, y, rowAreaWidth, RowHeight);

            bool selected = i == _selectedIndex;
            bool hot = i == _hotIndex;
            if (selected || hot)
            {
                var r = Rectangle.Inflate(rowBounds, -3, -2);
                using var path = Win11Paint.RoundedRect(r, 4);
                using var fill = new SolidBrush(selected ? p.Hover : Win11Paint.Blend(p.Surface, p.Hover, 0.55f));
                g.FillPath(fill, path);
                if (selected)
                {
                    using var accent = new SolidBrush(p.Accent);
                    g.FillRectangle(accent, r.X + 2, r.Y + (r.Height - 12) / 2, 3, 12);
                }
            }

            var item = _items[i];
            var primaryRect = new Rectangle(12, y, Math.Min(PrimaryColumnWidth, rowAreaWidth) - 16, RowHeight);
            TextRenderer.DrawText(g, item.Primary, Font, primaryRect, p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (rowAreaWidth > PrimaryColumnWidth + 20)
            {
                var secondaryRect = new Rectangle(PrimaryColumnWidth, y, rowAreaWidth - PrimaryColumnWidth - 8, RowHeight);
                TextRenderer.DrawText(g, item.Secondary, Font, secondaryRect, p.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        // Schlanke Scrollbar (nur Daumen, keine Spur)
        if (MaxScroll > 0)
        {
            var thumb = ThumbBounds;
            using var thumbPath = Win11Paint.RoundedRect(thumb, ThumbWidth / 2);
            using var thumbFill = new SolidBrush(_draggingThumb || _thumbHot
                ? p.TextMuted
                : Win11Paint.Blend(p.Surface, p.TextMuted, 0.5f));
            g.FillPath(thumbFill, thumbPath);
        }
    }

    private int RowIndexAt(Point location)
    {
        int idx = (location.Y + _scrollOffset) / RowHeight;
        return idx >= 0 && idx < _items.Count ? idx : -1;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        SetScroll(_scrollOffset - e.Delta / 120 * RowHeight * 3);
        UpdateHot(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingThumb)
        {
            int trackHeight = ClientSize.Height - 4;
            int thumbHeight = ThumbBounds.Height;
            if (trackHeight > thumbHeight)
            {
                long delta = (long)(e.Y - _dragStartY) * MaxScroll / (trackHeight - thumbHeight);
                SetScroll(_dragStartOffset + (int)delta);
            }
            return;
        }

        bool overThumb = ThumbBounds.Contains(e.Location);
        if (overThumb != _thumbHot)
        {
            _thumbHot = overThumb;
            Invalidate();
        }
        UpdateHot(overThumb ? new Point(-1, -1) : e.Location);
    }

    private void UpdateHot(Point location)
    {
        int idx = location.X < 0 ? -1 : RowIndexAt(location);
        if (idx == _hotIndex) return;
        _hotIndex = idx;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotIndex = -1;
        _thumbHot = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (ThumbBounds.Contains(e.Location))
        {
            _draggingThumb = true;
            _dragStartY = e.Y;
            _dragStartOffset = _scrollOffset;
            Invalidate();
            return;
        }
        int idx = RowIndexAt(e.Location);
        if (idx >= 0 && idx != _selectedIndex)
        {
            _selectedIndex = idx;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_draggingThumb)
        {
            _draggingThumb = false;
            Invalidate();
        }
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        if (_selectedIndex >= 0)
            ItemActivated?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int page = Math.Max(1, ClientSize.Height / RowHeight - 1);
        int next = e.KeyCode switch
        {
            Keys.Up => _selectedIndex - 1,
            Keys.Down => _selectedIndex + 1,
            Keys.PageUp => _selectedIndex - page,
            Keys.PageDown => _selectedIndex + page,
            Keys.Home => 0,
            Keys.End => _items.Count - 1,
            _ => int.MinValue
        };
        if (next != int.MinValue)
        {
            if (_items.Count == 0) return;
            next = Math.Max(0, Math.Min(next, _items.Count - 1));
            if (next != _selectedIndex)
            {
                _selectedIndex = next;
                EnsureVisible(next);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _selectedIndex >= 0)
        {
            ItemActivated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void EnsureVisible(int index)
    {
        int top = index * RowHeight;
        if (top < _scrollOffset) SetScroll(top);
        else if (top + RowHeight > _scrollOffset + ClientSize.Height)
            SetScroll(top + RowHeight - ClientSize.Height);
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
