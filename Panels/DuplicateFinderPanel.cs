using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-duplicate exactly: header .clean-section (label,
/// subtitle, single "Scan Now" action, status line — no filter/folder box, the modal
/// always scans Downloads) + a separate #duplicate-groups-list. Each group is its own
/// bare card: a gold "N copies · size each" header line, then one row per path — the
/// first path is the keeper (muted, "KEEP — " prefix, no button, no top border), every
/// other path gets a top border and a red "Delete" button (renderDuplicateGroups in
/// CaliberCommandCenter.html — same shape as Large Files' rows, which reuses the same
/// delete-one-file-by-path operation for the same reason: deleting a file doesn't care
/// which scan surfaced it).
public sealed class DuplicateFinderPanel : UserControl
{
    private readonly Palette _pal;
    private readonly Panel _scroll;
    private readonly Panel _headerCard;
    private List<Panel> _cards = [];
    private DuplicateGroup[] _groups = [];
    private Button _scanBtn = null!;
    private Label _statusLbl = null!;
    private readonly DuplicateFileFinder _finder = new();

    public DuplicateFinderPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        _scroll.SizeChanged += (_, _) => LayoutWidths();
        // Built once and kept for the panel's lifetime, same reason as
        // UninstallManagerPanel's _headerCard: RenderGroups() runs after every scan,
        // and recreating the header each time is unnecessary churn.
        _headerCard = BuildHeaderCard();
        Controls.Add(_scroll);
        RenderGroups();
    }

    private void LayoutWidths()
    {
        int w = Math.Max(200, _scroll.ClientSize.Width - 40);
        foreach (var c in _cards) c.Width = w;
    }

    private Panel BuildHeaderCard()
    {
        var card = CardChrome.NewCard(_pal, 12 + 15 + 4 + 16 + 10 + 38 + 8 + 16 + 12, "Duplicate Finder", 11f);

        var subtitle = new Label
        {
            Text = "Scans your Downloads folder for byte-identical files (matched by MD5)",
            Location = new Point(14, 31),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        _scanBtn = new Button
        {
            Text = "Scan Now",
            Location = new Point(14, 57),
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(12f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = _pal.Gold,
            ForeColor = _pal.Bg,
        };
        _scanBtn.FlatAppearance.BorderColor = _pal.Gold;
        _scanBtn.FlatAppearance.BorderSize = 1;
        _scanBtn.Click += async (_, _) => await ScanAsync();

        _statusLbl = new Label
        {
            Location = new Point(14, 103),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        card.Controls.Add(subtitle);
        card.Controls.Add(_scanBtn);
        card.Controls.Add(_statusLbl);
        return card;
    }

    private Panel BuildGroupCard(DuplicateGroup group)
    {
        const int rowH = 24;
        var card = CardChrome.NewBareCard(_pal, 10 + 18 + 6 + group.Paths.Length * rowH + 10);

        var headerLbl = new Label
        {
            Text = $"{group.Paths.Length} copies · {DuplicateFileFinder.FormatSize(group.FileSize)} each",
            UseMnemonic = false,
            Location = new Point(12, 10),
            AutoSize = true,
            ForeColor = _pal.Gold,
            Font = Fonts.UI(9f, FontStyle.Bold),
        };
        card.Controls.Add(headerLbl);

        int y = 10 + 18 + 6;
        for (int pi = 0; pi < group.Paths.Length; pi++)
        {
            string path = group.Paths[pi];
            bool isKeeper = pi == 0;

            var pathLbl = new Label
            {
                Text = isKeeper ? $"KEEP — {path}" : path,
                UseMnemonic = false,
                Location = new Point(12, y + 4),
                Size = new Size(card.Width - 12 - (isKeeper ? 12 : 88), 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                ForeColor = isKeeper ? _pal.Muted : _pal.White,
                Font = Fonts.Body(9f),
            };
            card.SizeChanged += (_, _) => pathLbl.Width = card.Width - 12 - (isKeeper ? 12 : 88);
            card.Controls.Add(pathLbl);

            if (!isKeeper)
            {
                int gi = Array.IndexOf(_groups, group);
                int piCaptured = pi;
                var deleteBtn = new Button
                {
                    Text = "Delete",
                    Size = new Size(64, 22),
                    Location = new Point(0, y),
                    FlatStyle = FlatStyle.Flat,
                    Font = Fonts.UI(8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    BackColor = _pal.Panel,
                    ForeColor = _pal.Red,
                };
                deleteBtn.FlatAppearance.BorderColor = _pal.Red;
                deleteBtn.FlatAppearance.BorderSize = 1;
                CardChrome.AnchorRight(deleteBtn, card);
                deleteBtn.Click += (_, _) => DeleteFile(gi, piCaptured, path, pathLbl, deleteBtn);
                card.Controls.Add(deleteBtn);

                var divider = new Panel { Location = new Point(12, y), Size = new Size(card.Width - 24, 1), BackColor = _pal.Border2, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                card.Controls.Add(divider);
            }

            y += rowH;
        }

        return card;
    }

    private void DeleteFile(int groupIndex, int pathIndex, string path, Label pathLbl, Button btn)
    {
        var result = MessageBox.Show(
            $"Delete this file?\n\n{path}\n\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        btn.Enabled = false;
        btn.Text = "…";

        var (ok, error) = DuplicateFileFinder.DeleteFile(path);
        if (!ok)
        {
            btn.Enabled = true;
            btn.Text = "Delete";
            MessageBox.Show($"Could not delete file: {error}", "CaliberClean",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        pathLbl.Text = "Deleted";
        pathLbl.ForeColor = _pal.Green;
        btn.Parent?.Controls.Remove(btn);
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private async Task ScanAsync()
    {
        _scanBtn.Enabled = false;
        _scanBtn.Text = "Scanning…";
        _statusLbl.Text = "Scanning Downloads for duplicates…";
        _statusLbl.ForeColor = _pal.Muted;

        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        try
        {
            var (groups, totalRecoverableBytes) = await _finder.ScanAsync(downloads);
            _groups = groups;
            _statusLbl.Text = $"{_groups.Length} duplicate group(s) — {DuplicateFileFinder.FormatSize(totalRecoverableBytes)} recoverable in {downloads}";
        }
        catch (Exception ex)
        {
            _groups = [];
            _statusLbl.Text = $"✗ {ex.Message}";
            _statusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _scanBtn.Enabled = true;
            _scanBtn.Text = "Scan Now";
        }

        RenderGroups();
    }

    private void RenderGroups()
    {
        _scroll.Controls.Clear();
        _cards = [_headerCard, .. _groups.Select(BuildGroupCard)];

        int y = 20;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Location = new Point(20, y);
            y += _cards[i].Height + (i == 0 ? 14 : 6);
            _scroll.Controls.Add(_cards[i]);
        }

        LayoutWidths();
    }
}
