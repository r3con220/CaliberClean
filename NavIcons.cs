using System.Drawing.Drawing2D;

namespace CaliberClean;

public enum NavIconKind
{
    Dashboard,
    ScheduledClean,
    DiskUsage,
    StartupManager,
    UninstallManager,
    DuplicateFinder,
    LargeFiles,
    TempFiles,
    BrowserCache,
}

/// Small representative glyphs standing in for CaliberHQ's 24x24 SVG nav icons —
/// drawn with GDI+ primitives rather than replicated path-for-path, colored via
/// the caller's current fore color to mirror the web version's fill="currentColor".
public static class NavIcons
{
    public static void Draw(Graphics g, NavIconKind kind, Rectangle box, Color color)
    {
        var prevSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, Math.Max(1f, box.Width / 11f));
        using var brush = new SolidBrush(color);

        switch (kind)
        {
            case NavIconKind.Dashboard:
                DrawDashboard(g, brush, box);
                break;
            case NavIconKind.ScheduledClean:
                DrawClock(g, pen, box);
                break;
            case NavIconKind.DiskUsage:
                DrawGauge(g, pen, box);
                break;
            case NavIconKind.StartupManager:
                DrawBolt(g, brush, box);
                break;
            case NavIconKind.UninstallManager:
                DrawTrash(g, pen, brush, box, lidFlare: false);
                break;
            case NavIconKind.DuplicateFinder:
                DrawMagnifier(g, pen, box);
                break;
            case NavIconKind.LargeFiles:
                DrawBars(g, brush, box);
                break;
            case NavIconKind.TempFiles:
                DrawTrash(g, pen, brush, box, lidFlare: true);
                break;
            case NavIconKind.BrowserCache:
                DrawBrowser(g, pen, brush, box);
                break;
        }

        g.SmoothingMode = prevSmoothing;
    }

    private static void DrawDashboard(Graphics g, Brush brush, Rectangle b)
    {
        int gap = Math.Max(1, b.Width / 8);
        int cell = (b.Width - gap) / 2;
        g.FillRectangle(brush, b.Left, b.Top, cell, cell);
        g.FillRectangle(brush, b.Left + cell + gap, b.Top, cell, cell);
        g.FillRectangle(brush, b.Left, b.Top + cell + gap, cell, cell);
        g.FillRectangle(brush, b.Left + cell + gap, b.Top + cell + gap, cell, cell);
    }

    private static void DrawClock(Graphics g, Pen pen, Rectangle b)
    {
        g.DrawEllipse(pen, b);
        var cx = b.Left + b.Width / 2f;
        var cy = b.Top + b.Height / 2f;
        g.DrawLine(pen, cx, cy, cx, b.Top + b.Height * 0.22f);
        g.DrawLine(pen, cx, cy, b.Right - b.Width * 0.22f, cy);
    }

    private static void DrawGauge(Graphics g, Pen pen, Rectangle b)
    {
        g.DrawEllipse(pen, b);
        int inset = (int)(b.Width * 0.28f);
        g.DrawEllipse(pen, Rectangle.Inflate(b, -inset, -inset));
    }

    private static void DrawBolt(Graphics g, Brush brush, Rectangle b)
    {
        float w = b.Width, h = b.Height, x = b.Left, y = b.Top;
        PointF[] bolt =
        [
            new(x + w * 0.58f, y),
            new(x + w * 0.20f, y + h * 0.58f),
            new(x + w * 0.46f, y + h * 0.58f),
            new(x + w * 0.42f, y + h),
            new(x + w * 0.80f, y + h * 0.40f),
            new(x + w * 0.54f, y + h * 0.40f),
        ];
        g.FillPolygon(brush, bolt);
    }

    private static void DrawTrash(Graphics g, Pen pen, Brush brush, Rectangle b, bool lidFlare)
    {
        float w = b.Width, h = b.Height, x = b.Left, y = b.Top;
        float lidY = y + h * 0.22f;
        g.DrawLine(pen, x + w * 0.15f, lidY, x + w * 0.85f, lidY);
        g.DrawLine(pen, x + w * 0.38f, lidY, x + w * (lidFlare ? 0.30f : 0.38f), y);
        g.DrawLine(pen, x + w * 0.62f, lidY, x + w * (lidFlare ? 0.70f : 0.62f), y);
        if (!lidFlare) g.DrawLine(pen, x + w * 0.38f, y, x + w * 0.62f, y);

        PointF[] body =
        [
            new(x + w * 0.22f, lidY),
            new(x + w * 0.78f, lidY),
            new(x + w * 0.68f, y + h),
            new(x + w * 0.32f, y + h),
        ];
        g.DrawPolygon(pen, body);
    }

    private static void DrawMagnifier(Graphics g, Pen pen, Rectangle b)
    {
        int d = (int)(b.Width * 0.62f);
        var circle = new Rectangle(b.Left, b.Top, d, d);
        g.DrawEllipse(pen, circle);
        g.DrawLine(pen, b.Left + d * 0.78f, b.Top + d * 0.78f, b.Right, b.Bottom);
    }

    private static void DrawBars(Graphics g, Brush brush, Rectangle b)
    {
        float w = b.Width, h = b.Height, x = b.Left, y = b.Top;
        float barH = h * 0.18f, gap = h * 0.14f;
        g.FillRectangle(brush, x, y, w * 0.55f, barH);
        g.FillRectangle(brush, x, y + barH + gap, w * 0.78f, barH);
        g.FillRectangle(brush, x, y + (barH + gap) * 2, w, barH);
    }

    private static void DrawBrowser(Graphics g, Pen pen, Brush brush, Rectangle b)
    {
        g.DrawRectangle(pen, b);
        float headerH = b.Height * 0.28f;
        g.DrawLine(pen, b.Left, b.Top + headerH, b.Right, b.Top + headerH);
        float r = Math.Max(1f, b.Width * 0.06f);
        float dotY = b.Top + headerH / 2f;
        for (int i = 0; i < 3; i++)
        {
            float dotX = b.Left + b.Width * 0.14f + i * r * 2.6f;
            g.FillEllipse(brush, dotX - r, dotY - r, r * 2, r * 2);
        }
    }
}
