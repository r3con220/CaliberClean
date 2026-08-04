namespace CaliberClean;

/// A label whose text renders in two colors (e.g. "CALIBER" gold + "CLEAN" army) —
/// mirrors CaliberHQ's two-<span> wordmark. Fully self-painted (no base Label text
/// rendering to fight with); call Measure() after changing Font/PartA/PartB to
/// resize before laying it out.
public sealed class TwoToneLabel : Control
{
    public string PartA { get; set; } = "";
    public Color ColorA { get; set; } = Color.White;
    public string PartB { get; set; } = "";
    public Color ColorB { get; set; } = Color.White;

    public TwoToneLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public void Measure()
    {
        using var g = CreateGraphics();
        var sizeA = TextRenderer.MeasureText(g, PartA, Font, Size.Empty, TextFormatFlags.NoPadding);
        var sizeB = TextRenderer.MeasureText(g, PartB, Font, Size.Empty, TextFormatFlags.NoPadding);
        Size = new Size(sizeA.Width + sizeB.Width, Math.Max(sizeA.Height, sizeB.Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var sizeA = TextRenderer.MeasureText(e.Graphics, PartA, Font, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, PartA, Font, new Point(0, 0), ColorA, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, PartB, Font, new Point(sizeA.Width, 0), ColorB, TextFormatFlags.NoPadding);
    }
}
