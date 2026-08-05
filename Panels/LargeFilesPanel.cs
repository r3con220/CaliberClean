using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-large-files exactly: header .clean-section (label,
/// subtitle, single "Scan Now" action, status line — same shape as Duplicate Finder's
/// header, no filter box) + a separate #large-files-list. Unlike Duplicate Finder,
/// rows are flat (not grouped) — each is its own .clean-section flex row: name+path on
/// the left, gold size, then a red "Delete" .clean-action-btn, list gap is 6px not 10px.
/// Deletes through DuplicateFileFinder.DeleteFile — the HTML's own comment confirms
/// Large Files and Duplicate Finder share the exact same delete-by-path endpoint, so
/// this reuses that instead of adding a second copy of the same three lines.
public sealed class LargeFilesPanel : UserControl
{
    private readonly Palette _pal;
    private readonly Panel _scroll;
    private readonly Panel _headerCard;
    private List<Panel> _cards = [];
    private LargeFileEntry[] _files = [];
    private Button _scanBtn = null!;
    private Label _statusLbl = null!;
    private readonly LargeFileFinder _finder = new();

    public LargeFilesPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        _scroll.SizeChanged += (_, _) => LayoutWidths();
        _headerCard = BuildHeaderCard();
        Controls.Add(_scroll);
        RenderFiles();
    }

    private void LayoutWidths()
    {
        int w = Math.Max(200, _scroll.ClientSize.Width - 40);
        foreach (var c in _cards) c.Width = w;
    }

    private Panel BuildHeaderCard()
    {
        var card = CardChrome.NewCard(_pal, 12 + 15 + 4 + 16 + 10 + 38 + 8 + 16 + 12, "Large Files", 11f);

        var subtitle = new Label
        {
            Text = "Top 50 largest files on C:\\ (system folders excluded)",
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

    private Panel BuildFileCard(LargeFileEntry file)
    {
        var card = CardChrome.NewBareCard(_pal, 8 + 15 + 2 + 13 + 8);
        const int rightZone = 150; // reserved for size label + delete button + gaps

        var nameLbl = new Label
        {
            Text = file.FileName,
            UseMnemonic = false,
            Location = new Point(12, 8),
            Size = new Size(card.Width - 12 - rightZone, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true,
            ForeColor = _pal.White,
            Font = Fonts.Body(12f, FontStyle.Bold),
        };
        var pathLbl = new Label
        {
            Text = file.FilePath,
            UseMnemonic = false,
            Location = new Point(12, 25),
            Size = new Size(card.Width - 12 - rightZone, 13),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(10f),
        };
        card.SizeChanged += (_, _) =>
        {
            int w = card.Width - 12 - rightZone;
            nameLbl.Width = w;
            pathLbl.Width = w;
        };
        card.Controls.Add(nameLbl);
        card.Controls.Add(pathLbl);

        var sizeLbl = new Label
        {
            Text = LargeFileFinder.FormatSize(file.Size),
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = _pal.Gold,
            Font = Fonts.UI(10f, FontStyle.Bold),
        };
        sizeLbl.Location = new Point(0, (card.Height - sizeLbl.Height) / 2);
        CardChrome.AnchorRight(sizeLbl, card, rightMargin: 14 + 64 + 10);
        card.Controls.Add(sizeLbl);

        var deleteBtn = new Button
        {
            Text = "Delete",
            Size = new Size(64, 24),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = _pal.Panel,
            ForeColor = _pal.Red,
        };
        deleteBtn.FlatAppearance.BorderColor = _pal.Red;
        deleteBtn.FlatAppearance.BorderSize = 1;
        deleteBtn.Location = new Point(0, (card.Height - deleteBtn.Height) / 2);
        CardChrome.AnchorRight(deleteBtn, card);
        deleteBtn.Click += (_, _) => DeleteFile(file.FilePath, nameLbl, deleteBtn);
        card.Controls.Add(deleteBtn);

        return card;
    }

    private void DeleteFile(string path, Label nameLbl, Button btn)
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

        nameLbl.Text += "  — deleted";
        nameLbl.ForeColor = _pal.Green;
        btn.Parent?.Controls.Remove(btn);
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private async Task ScanAsync()
    {
        _scanBtn.Enabled = false;
        _scanBtn.Text = "Scanning…";
        _statusLbl.Text = "Scanning C:\\ — this can take a minute on a large drive…";
        _statusLbl.ForeColor = _pal.Muted;

        try
        {
            var (files, totalSize) = await _finder.ScanAsync(@"C:\", includeSystem: false);
            _files = files;
            _statusLbl.Text = $"{_files.Length} file(s) found — {LargeFileFinder.FormatSize(totalSize)} total";
        }
        catch (Exception ex)
        {
            _files = [];
            _statusLbl.Text = $"✗ {ex.Message}";
            _statusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _scanBtn.Enabled = true;
            _scanBtn.Text = "Scan Now";
        }

        RenderFiles();
    }

    private void RenderFiles()
    {
        _scroll.Controls.Clear();
        _cards = [_headerCard, .. _files.Select(BuildFileCard)];

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
