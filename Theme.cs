using System.Drawing.Text;

namespace CaliberClean;

/// Colors pulled directly from CaliberHQ's caliber-themes.js ([data-theme="dark-army"/"light-army"]) —
/// keep these in sync with that file if the web palette ever changes.
public sealed class Palette
{
    public required Color Bg;
    public required Color Surface;
    public required Color Panel;
    public required Color Panel2;
    public required Color Border;
    public required Color Border2;
    public required Color Gold;
    public required Color Army;
    public required Color White;
    public required Color Muted;
    public required Color Red;
    public required Color Orange;
    public required Color Green;

    /// Approximates CSS's translucent --gold-dim/--army-dim tokens by alpha-compositing
    /// over a given backdrop, since WinForms controls don't do true alpha backgrounds.
    public static Color Blend(Color fg, Color backdrop, double alpha) => Color.FromArgb(
        (int)(fg.R * alpha + backdrop.R * (1 - alpha)),
        (int)(fg.G * alpha + backdrop.G * (1 - alpha)),
        (int)(fg.B * alpha + backdrop.B * (1 - alpha)));

    public static readonly Palette DarkArmy = new()
    {
        Bg = Color.FromArgb(0x08, 0x08, 0x08),
        Surface = Color.FromArgb(0x0F, 0x0F, 0x0F),
        Panel = Color.FromArgb(0x14, 0x14, 0x14),
        Panel2 = Color.FromArgb(0x19, 0x19, 0x19),
        Border = Color.FromArgb(0x1E, 0x1E, 0x1E),
        Border2 = Color.FromArgb(0x26, 0x26, 0x26),
        Gold = Color.FromArgb(0xFF, 0xCC, 0x01),
        Army = Color.FromArgb(0x8B, 0x9E, 0x6B),
        White = Color.FromArgb(0xF0, 0xED, 0xE6),
        Muted = Color.FromArgb(0x55, 0x55, 0x55),
        Red = Color.FromArgb(0xC0, 0x39, 0x2B),
        Orange = Color.FromArgb(0xE6, 0x7E, 0x22),
        Green = Color.FromArgb(0x27, 0xAE, 0x60),
    };

    public static readonly Palette LightArmy = new()
    {
        Bg = Color.FromArgb(0xF1, 0xE4, 0xC7),
        Surface = Color.FromArgb(0xEB, 0xDC, 0xBE),
        Panel = Color.FromArgb(0xDE, 0xCF, 0xAF),
        Panel2 = Color.FromArgb(0xD4, 0xC4, 0xA0),
        Border = Color.FromArgb(0xC8, 0xB8, 0x8A),
        Border2 = Color.FromArgb(0xB8, 0xA8, 0x7A),
        Gold = Color.FromArgb(0xB8, 0x93, 0x0A),
        Army = Color.FromArgb(0x5A, 0x6E, 0x3B),
        White = Color.FromArgb(0x1C, 0x1A, 0x14),
        Muted = Color.FromArgb(0x7A, 0x6A, 0x4A),
        Red = Color.FromArgb(0xB0, 0x30, 0x20),
        Orange = Color.FromArgb(0xC0, 0x5A, 0x00),
        Green = Color.FromArgb(0x2E, 0x7D, 0x32),
    };
}

public static class Fonts
{
    private static readonly Lazy<bool> HasBebas = new(() => IsInstalled("Bebas Neue"));
    private static readonly Lazy<bool> HasBarlowCondensed = new(() => IsInstalled("Barlow Condensed"));
    private static readonly Lazy<bool> HasBarlow = new(() => IsInstalled("Barlow"));
    private static readonly Lazy<bool> HasShareTechMono = new(() => IsInstalled("Share Tech Mono"));

    private static bool IsInstalled(string name)
    {
        using var fonts = new InstalledFontCollection();
        return fonts.Families.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// --font-d / --font-head (Bebas Neue) — wordmark, card values, drive letters.
    public static Font Display(float px, FontStyle style = FontStyle.Regular) =>
        HasBebas.Value ? new Font("Bebas Neue", px, style, GraphicsUnit.Pixel)
                        : new Font("Arial", px, style | FontStyle.Bold, GraphicsUnit.Pixel);

    /// --font-ui (Barlow Condensed) — nav items, labels, buttons, pills.
    public static Font UI(float px, FontStyle style = FontStyle.Regular) =>
        HasBarlowCondensed.Value ? new Font("Barlow Condensed", px, style, GraphicsUnit.Pixel)
                                  : new Font("Segoe UI", px, style, GraphicsUnit.Pixel);

    /// --font-b (Barlow) — body/subtitle text.
    public static Font Body(float px, FontStyle style = FontStyle.Regular) =>
        HasBarlow.Value ? new Font("Barlow", px, style, GraphicsUnit.Pixel)
                         : new Font("Segoe UI", px, style, GraphicsUnit.Pixel);

    /// --font-m (Share Tech Mono) — byte counts, mono figures.
    public static Font Mono(float px, FontStyle style = FontStyle.Regular) =>
        HasShareTechMono.Value ? new Font("Share Tech Mono", px, style, GraphicsUnit.Pixel)
                                : new Font("Consolas", px, style, GraphicsUnit.Pixel);
}
