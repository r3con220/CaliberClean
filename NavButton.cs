namespace CaliberClean;

/// Owner-drawn nav-rail item matching CaliberHQ's .cui-nav-item: icon + label in a
/// flex row, with hover/active states swapping background and text color together
/// (so the icon, drawn in ForeColor, recolors with the rest of the row for free).
/// Derives from Control (not Button) — Button's native Win32 chrome fought with
/// owner-drawn text even under ControlStyles.UserPaint, producing ghosted labels.
public sealed class NavButton : Control
{
    public int SectionIndex { get; }
    public NavIconKind IconKind { get; }
    public bool IsActive { get; set; }

    private bool _hover;
    private readonly Palette _pal;
    private readonly string _label;

    public NavButton(int sectionIndex, NavIconKind icon, string label, Palette palette)
    {
        SectionIndex = sectionIndex;
        IconKind = icon;
        _label = label;
        _pal = palette;

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Dock = DockStyle.Top;
        Height = 34;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 0, 2);

        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color bg = IsActive ? Palette.Blend(_pal.Army, _pal.Panel, 0.14)
                 : _hover ? _pal.Panel2
                 : Parent?.BackColor ?? _pal.Panel2;
        Color fg = IsActive ? _pal.Army : _hover ? _pal.White : _pal.Muted;

        using (var b = new SolidBrush(bg))
            g.FillRectangle(b, ClientRectangle);
        if (IsActive)
            using (var p = new Pen(_pal.Army))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

        var iconBox = new Rectangle(14, (Height - 17) / 2, 17, 17);
        NavIcons.Draw(g, IconKind, iconBox, fg);

        using var font = Fonts.UI(12f);
        var textRect = new Rectangle(14 + 17 + 9, 0, Width - (14 + 17 + 9) - 8, Height);
        TextRenderer.DrawText(g, _label, font, textRect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}
