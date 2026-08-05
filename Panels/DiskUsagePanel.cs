using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-disk-usage exactly: one header .clean-section
/// (label, subtitle, Refresh button, status), then a separate list of per-drive
/// .clean-section cards with NO header label — different shape from the Dashboard's
/// disk card, which stacks all drives as rows inside one shared card. 14px gap
/// between the header and the drive list (.clean-pane's own gap), 10px between
/// drive cards (#disk-usage-list's own gap) — not the same value, checked both.
/// Wired to the same DiskUsageAnalyzer.GetDrives() the Dashboard card already uses.
public sealed class DiskUsagePanel : UserControl
{
    private readonly Palette _pal;
    private readonly Panel _scroll;
    private List<Panel> _cards = [];
    private Button _refreshBtn = null!;
    private Label _statusLbl = null!;

    public DiskUsagePanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        // Registered once — LoadDrives() only swaps _cards' contents, so this
        // single handler stays valid across repeated Refresh clicks instead of
        // accumulating a new stale-closure subscriber on every reload.
        _scroll.SizeChanged += (_, _) => LayoutWidths();
        Controls.Add(_scroll);
        LoadDrives();
    }

    private void LayoutWidths()
    {
        int w = Math.Max(200, _scroll.ClientSize.Width - 40);
        foreach (var c in _cards) c.Width = w;
    }

    private Panel BuildHeaderCard()
    {
        var card = CardChrome.NewCard(_pal, 12 + 15 + 4 + 16 + 10 + 38 + 10 + 16 + 12, "Disk Usage", 11f);

        var subtitle = new Label
        {
            Text = "Live used/free space per drive",
            Location = new Point(14, 31),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        _refreshBtn = new Button
        {
            Text = "Refresh",
            Location = new Point(14, 57),
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(12f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = _pal.Gold,
            ForeColor = _pal.Bg,
        };
        _refreshBtn.FlatAppearance.BorderColor = _pal.Gold;
        _refreshBtn.FlatAppearance.BorderSize = 1;
        _refreshBtn.Click += (_, _) => LoadDrives();

        _statusLbl = new Label
        {
            Location = new Point(14, 105),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        card.Controls.Add(subtitle);
        card.Controls.Add(_refreshBtn);
        card.Controls.Add(_statusLbl);
        return card;
    }

    private Panel BuildDriveCard(DriveInfo drive)
    {
        bool readable;
        long total = 0, free = 0, used = 0;
        int pct = 0;
        try { total = drive.TotalSize; free = drive.AvailableFreeSpace; used = total - free; pct = total > 0 ? (int)Math.Round(used * 100.0 / total) : 0; readable = true; }
        catch { readable = false; }

        var card = CardChrome.NewBareCard(_pal, 12 + 18 + 2 + 14 + 12);
        Color fill = pct > 90 ? _pal.Red : pct > 75 ? _pal.Orange : _pal.Gold;

        var letter = new Label
        {
            Text = drive.Name.TrimEnd('\\'),
            Location = new Point(14, 12),
            AutoSize = true,
            ForeColor = _pal.White,
            Font = Fonts.Display(18f),
        };

        var pctLbl = new Label
        {
            Text = readable ? $"{pct}%" : "—",
            AutoSize = true,
            Location = new Point(0, 12),
            ForeColor = _pal.White,
            Font = Fonts.Display(17f),
            TextAlign = ContentAlignment.MiddleRight,
        };
        CardChrome.AnchorRight(pctLbl, card);

        var barBg = new Panel { BackColor = _pal.Bg, Location = new Point(0, 13), Height = 10 };
        var barFill = new Panel { BackColor = fill, Location = new Point(0, 0), Height = 10 };
        barBg.Controls.Add(barFill);
        barBg.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(_pal.Border2), 0, 0, barBg.Width - 1, barBg.Height - 1);
        void LayoutBar()
        {
            int left = 14 + letter.Width + 10;
            int right = card.Width - 14 - pctLbl.Width - 10;
            barBg.Left = left;
            barBg.Width = Math.Max(10, right - left);
            barFill.Width = readable ? (int)(barBg.Width * (pct / 100.0)) : 0;
        }
        card.SizeChanged += (_, _) => LayoutBar();
        letter.SizeChanged += (_, _) => LayoutBar();
        pctLbl.SizeChanged += (_, _) => LayoutBar();

        var meta = new Label
        {
            Text = readable ? $"{DiskUsageAnalyzer.FormatSize(used)} used   of {DiskUsageAnalyzer.FormatSize(total)}" : "— used   of —",
            Location = new Point(14, 34),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Mono(11f),
        };
        var freeLbl = new Label
        {
            Text = readable ? $"{DiskUsageAnalyzer.FormatSize(free)} free" : "— free",
            AutoSize = true,
            Location = new Point(0, 34),
            ForeColor = _pal.Army,
            Font = Fonts.Mono(11f),
        };
        CardChrome.AnchorRight(freeLbl, card);

        card.Controls.Add(letter);
        card.Controls.Add(pctLbl);
        card.Controls.Add(barBg);
        card.Controls.Add(meta);
        card.Controls.Add(freeLbl);
        LayoutBar();
        return card;
    }

    private void LoadDrives()
    {
        _scroll.Controls.Clear();

        var header = BuildHeaderCard();
        _refreshBtn.Enabled = false;
        _refreshBtn.Text = "Loading…";

        DriveInfo[] drives;
        try
        {
            drives = DiskUsageAnalyzer.GetDrives();
            _statusLbl.Text = $"{drives.Length} drive(s)";
            _statusLbl.ForeColor = _pal.Muted;
        }
        catch (Exception ex)
        {
            drives = [];
            _statusLbl.Text = $"✗ {ex.Message}";
            _statusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _refreshBtn.Enabled = true;
            _refreshBtn.Text = "Refresh";
        }

        _cards = [header, .. drives.Select(BuildDriveCard)];

        int y = 20;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Location = new Point(20, y);
            y += _cards[i].Height + (i == 0 ? 14 : 10); // header-to-list gap (14) differs from between-drive gap (10)
            _scroll.Controls.Add(_cards[i]);
        }

        LayoutWidths();
    }
}
