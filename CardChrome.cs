namespace CaliberClean;

/// Shared .cui-card / .clean-section chrome — same background/border/padding in
/// both CSS classes, just different label sizes per tab (Dashboard's .cui-card-label
/// is 0.83rem/13px; every other tab's .clean-section-lbl is 0.68rem/11px). Used by
/// every real (non-stub) panel so the card shell stays pixel-identical across tabs.
public static class CardChrome
{
    public static Panel NewCard(Palette pal, int height, string label, float labelSize)
    {
        var card = new Panel { Height = height, Width = 400, BackColor = pal.Panel };
        card.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(pal.Border2), 0, 0, card.Width - 1, card.Height - 1);

        var lbl = new Label
        {
            Text = label.ToUpperInvariant(),
            UseMnemonic = false, // a bare '&' is a mnemonic marker in WinForms and gets swallowed otherwise
            Location = new Point(14, 12),
            AutoSize = true,
            ForeColor = pal.Gold,
            Font = Fonts.UI(labelSize, FontStyle.Bold),
        };
        card.Controls.Add(lbl);
        return card;
    }

    /// Same .clean-section bg/border, no header label — used for per-item cards in
    /// a results list (Disk Usage's per-drive cards, and later Duplicate Finder /
    /// Large Files, which follow the same "header card + bare-card list" pattern).
    public static Panel NewBareCard(Palette pal, int height)
    {
        var card = new Panel { Height = height, Width = 400, BackColor = pal.Panel };
        card.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(pal.Border2), 0, 0, card.Width - 1, card.Height - 1);
        return card;
    }

    /// Keeps a right-aligned control (a pct/free-space label, a status pill, ...)
    /// pinned to the card's right edge as the card is resized to fill its container.
    public static void AnchorRight(Control c, Panel card, int rightMargin = 14)
    {
        void Reposition() => c.Left = card.Width - rightMargin - c.Width;
        c.SizeChanged += (_, _) => Reposition();
        card.SizeChanged += (_, _) => Reposition();
        Reposition();
    }

    /// Vertically stacks a list of already-built cards inside an AutoScroll panel,
    /// each pinned to the container's width — the deterministic layout every panel
    /// uses instead of FlowLayoutPanel.AutoSize (which doesn't measure Dock=Top
    /// children and silently collapses to zero height).
    public static void StackVertically(Panel scroll, IReadOnlyList<Panel> cards, int startY = 20, int gap = 12)
    {
        int y = startY;
        foreach (var card in cards)
        {
            card.Location = new Point(20, y);
            y += card.Height + gap;
            scroll.Controls.Add(card);
        }

        void LayoutWidths()
        {
            int w = Math.Max(200, scroll.ClientSize.Width - 40);
            foreach (var c in cards) c.Width = w;
        }
        scroll.SizeChanged += (_, _) => LayoutWidths();
        LayoutWidths();
    }
}
