using CaliberClean;

namespace CaliberClean.Panels;

/// Placeholder for the 8 nav sections not yet rebuilt to match CaliberHQ — clickable,
/// doesn't crash, doesn't pretend to have real content. Each gets its own pass later.
public sealed class StubPanel : UserControl
{
    public StubPanel(string title, NavIconKind icon, Palette pal)
    {
        Dock = DockStyle.Fill;
        BackColor = pal.Surface;

        var iconHost = new Panel { Size = new Size(64, 64), BackColor = Color.Transparent };
        iconHost.Paint += (_, e) => NavIcons.Draw(e.Graphics, icon, new Rectangle(8, 8, 48, 48), pal.Border2);

        var titleLbl = new Label
        {
            Text = title.ToUpperInvariant(),
            AutoSize = true,
            ForeColor = pal.White,
            Font = Fonts.Display(26f),
        };

        var subLbl = new Label
        {
            Text = "Coming soon",
            AutoSize = true,
            ForeColor = pal.Muted,
            Font = Fonts.Body(12f, FontStyle.Italic),
        };

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.None,
        };
        stack.Controls.Add(iconHost);
        stack.Controls.Add(titleLbl);
        stack.Controls.Add(subLbl);
        foreach (Control c in stack.Controls) c.Margin = new Padding(0, 0, 0, 6);

        void Center()
        {
            stack.Left = (ClientSize.Width - stack.Width) / 2;
            stack.Top = (ClientSize.Height - stack.Height) / 2;
        }
        Resize += (_, _) => Center();
        stack.SizeChanged += (_, _) => Center();

        Controls.Add(stack);
        Center();
    }
}
