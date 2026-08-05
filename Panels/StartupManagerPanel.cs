using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-startup exactly: header .clean-section (label,
/// subtitle, Refresh, status) + a separate #startup-items-list of per-entry rows.
/// Different from Disk Usage in two ways worth calling out: the row cards are a
/// flex ROW (name+location on the left, a status pill on the right), not a column
/// like the disk bars, and the list's own gap is 6px, not Disk Usage's 10px.
/// Wired to the existing StartupManager service (GetEntries/ToggleEntry) — no
/// enumeration or registry logic re-implemented here.
public sealed class StartupManagerPanel : UserControl
{
    private readonly Palette _pal;
    private readonly Panel _scroll;
    private List<Panel> _cards = [];
    private List<StartupEntry> _entries = [];
    private Button _refreshBtn = null!;
    private Label _statusLbl = null!;

    public StartupManagerPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        _scroll.SizeChanged += (_, _) => LayoutWidths();
        Controls.Add(_scroll);
        LoadEntries();
    }

    private void LayoutWidths()
    {
        int w = Math.Max(200, _scroll.ClientSize.Width - 40);
        foreach (var c in _cards) c.Width = w;
    }

    private Panel BuildHeaderCard()
    {
        var card = CardChrome.NewCard(_pal, 12 + 15 + 4 + 16 + 10 + 38 + 10 + 16 + 12, "Startup Manager", 11f);

        var subtitle = new Label
        {
            Text = "Registry Run keys + Startup folder items",
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
        _refreshBtn.Click += (_, _) => LoadEntries();

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

    // ── .clean-status-pill ───────────────────────────────────────────────────

    private Label NewPillLabel(string text, bool on)
    {
        var pill = new Label
        {
            Text = text,
            UseMnemonic = false,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            ForeColor = on ? _pal.Green : _pal.Muted,
            BackColor = on ? Palette.Blend(_pal.Green, _pal.Panel, 0.1) : Color.Transparent,
            Font = Fonts.UI(9f, FontStyle.Bold),
        };
        pill.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(on ? _pal.Green : _pal.Border2), 0, 0, pill.Width - 1, pill.Height - 1);
        return pill;
    }

    private Button NewPillButton(string text, bool on)
    {
        var pill = new Button
        {
            Text = text,
            UseMnemonic = false,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            ForeColor = on ? _pal.Green : _pal.Muted,
            BackColor = on ? Palette.Blend(_pal.Green, _pal.Panel, 0.1) : _pal.Panel,
            Font = Fonts.UI(9f, FontStyle.Bold),
        };
        pill.FlatAppearance.BorderColor = on ? _pal.Green : _pal.Border2;
        pill.FlatAppearance.BorderSize = 1;
        return pill;
    }

    // ── Rows ──────────────────────────────────────────────────────────────

    private Panel BuildEntryCard(StartupEntry entry, int index)
    {
        var card = CardChrome.NewBareCard(_pal, 8 + 15 + 2 + 13 + 8);

        var nameLbl = new Label
        {
            Text = entry.Name,
            UseMnemonic = false,
            Location = new Point(12, 8),
            AutoSize = true,
            ForeColor = _pal.White,
            Font = Fonts.Body(12f, FontStyle.Bold),
        };

        var subLbl = new Label
        {
            Text = $"{StartupManager.LocationLabel(entry.Location)} · {entry.Command}",
            UseMnemonic = false,
            Location = new Point(12, 25),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(10f),
        };

        Control pill = entry.CanToggle ? NewPillButton(entry.IsEnabled ? "✓ Enabled" : "Disabled", entry.IsEnabled)
                                        : NewPillLabel("Folder item", false);
        pill.Location = new Point(0, (card.Height - pill.Height) / 2);
        CardChrome.AnchorRight(pill, card);

        if (pill is Button pillBtn)
            pillBtn.Click += (_, _) => ToggleEntry(index, pillBtn);

        card.Controls.Add(nameLbl);
        card.Controls.Add(subLbl);
        card.Controls.Add(pill);
        return card;
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void LoadEntries()
    {
        _scroll.Controls.Clear();

        var header = BuildHeaderCard();
        _refreshBtn.Enabled = false;
        _refreshBtn.Text = "Loading…";

        try
        {
            _entries = new StartupManager().GetEntries();
            _statusLbl.Text = $"{_entries.Count} item(s)";
            _statusLbl.ForeColor = _pal.Muted;
        }
        catch (Exception ex)
        {
            _entries = [];
            _statusLbl.Text = $"✗ {ex.Message}";
            _statusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _refreshBtn.Enabled = true;
            _refreshBtn.Text = "Refresh";
        }

        _cards = [header, .. _entries.Select(BuildEntryCard)];

        int y = 20;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Location = new Point(20, y);
            y += _cards[i].Height + (i == 0 ? 14 : 6); // header-to-list gap (14) vs between-row gap (6)
            _scroll.Controls.Add(_cards[i]);
        }

        LayoutWidths();
    }

    private void ToggleEntry(int index, Button pillBtn)
    {
        var entry = _entries[index];
        if (!entry.CanToggle) return;

        var originalText = pillBtn.Text;
        pillBtn.Enabled = false;
        pillBtn.Text = "…";

        var (ok, error) = new StartupManager().ToggleEntry(entry);
        if (!ok)
        {
            pillBtn.Text = originalText;
            pillBtn.Enabled = true;
            MessageBox.Show($"Could not toggle startup item: {error}", "CaliberClean",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Mirrors CaliberHQ's toggleStartupItem: flip the cached entry's state and
        // registry-key variant in place rather than re-querying the registry.
        bool nowEnabled = !entry.IsEnabled;
        var newLocation = entry.Location switch
        {
            StartupLocation.RegistryCurrentUser => StartupLocation.RegistryCurrentUserDisabled,
            StartupLocation.RegistryCurrentUserDisabled => StartupLocation.RegistryCurrentUser,
            StartupLocation.RegistryLocalMachine => StartupLocation.RegistryLocalMachineDisabled,
            StartupLocation.RegistryLocalMachineDisabled => StartupLocation.RegistryLocalMachine,
            _ => entry.Location,
        };
        _entries[index] = entry with { IsEnabled = nowEnabled, Location = newLocation };

        pillBtn.Text = nowEnabled ? "✓ Enabled" : "Disabled";
        pillBtn.ForeColor = nowEnabled ? _pal.Green : _pal.Muted;
        pillBtn.BackColor = nowEnabled ? Palette.Blend(_pal.Green, _pal.Panel, 0.1) : _pal.Panel;
        pillBtn.FlatAppearance.BorderColor = nowEnabled ? _pal.Green : _pal.Border2;
        pillBtn.Enabled = true;
    }
}
