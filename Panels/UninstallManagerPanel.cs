using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-uninstall exactly: header .clean-section (label,
/// subtitle, Refresh, a live name FILTER textbox, status) + a separate
/// #uninstall-programs-list. New element vs the prior panels: the filter input
/// (width:100%, margin-top:8px) sits between the actions row and the status line.
/// Row cards are a flex row like Startup Manager's (8px 12px padding, 10px gap),
/// but the right-hand control differs: a red "Uninstall" .clean-action-btn for
/// programs with an uninstaller, or the same muted .clean-status-pill.off ("No
/// uninstaller") otherwise — not a toggle pill. List gap is 6px.
/// Wired to the existing UninstallManager service; LaunchUninstaller was moved
/// there from Program.cs's CLI handler so this panel reuses the exact same
/// quoted-path parsing and UAC-cancel heuristic instead of duplicating it.
public sealed class UninstallManagerPanel : UserControl
{
    private readonly Palette _pal;
    private readonly Panel _scroll;
    private readonly Panel _headerCard;
    private List<Panel> _cards = [];
    private InstalledProgram[] _programs = [];
    private Button _refreshBtn = null!;
    private TextBox _filterBox = null!;
    private Label _statusLbl = null!;

    public UninstallManagerPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        _scroll.SizeChanged += (_, _) => LayoutWidths();
        // Built once and kept for the panel's lifetime — RenderPrograms() runs on
        // every filter keystroke, and recreating the header (and _filterBox with
        // it) each time would destroy the textbox's focus after one character.
        _headerCard = BuildHeaderCard();
        Controls.Add(_scroll);
        LoadPrograms();
    }

    private void LayoutWidths()
    {
        int w = Math.Max(200, _scroll.ClientSize.Width - 40);
        foreach (var c in _cards) c.Width = w;
    }

    private Panel BuildHeaderCard()
    {
        var card = CardChrome.NewCard(_pal, 12 + 15 + 4 + 16 + 10 + 38 + 8 + 30 + 8 + 16 + 12, "Uninstall Manager", 11f);

        var subtitle = new Label
        {
            Text = "Browse installed programs and launch their uninstaller",
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
        _refreshBtn.Click += (_, _) => LoadPrograms();

        _filterBox = new TextBox
        {
            PlaceholderText = "Filter by name…",
            Location = new Point(14, 103),
            BackColor = _pal.Panel2,
            ForeColor = _pal.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Fonts.UI(12f),
        };
        _filterBox.TextChanged += (_, _) => RenderPrograms();
        card.SizeChanged += (_, _) => _filterBox.Width = card.Width - 28;

        _statusLbl = new Label
        {
            Location = new Point(14, 141),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        card.Controls.Add(subtitle);
        card.Controls.Add(_refreshBtn);
        card.Controls.Add(_filterBox);
        card.Controls.Add(_statusLbl);
        return card;
    }

    private Label NewPillLabel(string text)
    {
        var pill = new Label
        {
            Text = text,
            UseMnemonic = false,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            ForeColor = _pal.Muted,
            BackColor = Color.Transparent,
            Font = Fonts.UI(9f, FontStyle.Bold),
        };
        pill.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(_pal.Border2), 0, 0, pill.Width - 1, pill.Height - 1);
        return pill;
    }

    private Panel BuildProgramCard(InstalledProgram program, int index)
    {
        var card = CardChrome.NewBareCard(_pal, 8 + 15 + 2 + 13 + 8);
        bool hasUninstaller = !string.IsNullOrWhiteSpace(program.UninstallString);

        var nameLbl = new Label
        {
            Text = program.DisplayName,
            UseMnemonic = false,
            Location = new Point(12, 8),
            AutoSize = true,
            ForeColor = _pal.White,
            Font = Fonts.Body(12f, FontStyle.Bold),
        };

        var subParts = new[] { program.Publisher, program.InstallDate, UninstallManager.FormatSize(program.EstimatedSizeKb) }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var subLbl = new Label
        {
            Text = string.Join(" · ", subParts),
            UseMnemonic = false,
            Location = new Point(12, 25),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(10f),
        };

        card.Controls.Add(nameLbl);
        card.Controls.Add(subLbl);

        if (hasUninstaller)
        {
            var uninstallBtn = new Button
            {
                Text = "Uninstall",
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6),
                FlatStyle = FlatStyle.Flat,
                Font = Fonts.UI(10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = _pal.Panel,
                ForeColor = _pal.Red,
            };
            uninstallBtn.FlatAppearance.BorderColor = _pal.Red;
            uninstallBtn.FlatAppearance.BorderSize = 1;
            uninstallBtn.Location = new Point(0, (card.Height - uninstallBtn.Height) / 2);
            CardChrome.AnchorRight(uninstallBtn, card);
            uninstallBtn.Click += (_, _) => LaunchUninstaller(program, uninstallBtn, nameLbl);
            card.Controls.Add(uninstallBtn);
        }
        else
        {
            var pill = NewPillLabel("No uninstaller");
            pill.Location = new Point(0, (card.Height - pill.Height) / 2);
            CardChrome.AnchorRight(pill, card);
            card.Controls.Add(pill);
        }

        return card;
    }

    private void LaunchUninstaller(InstalledProgram program, Button btn, Label nameLbl)
    {
        var result = MessageBox.Show(
            $"Launch the uninstaller for:\n\n{program.DisplayName}\n\nThis will run the program's own uninstaller in a separate window. Continue?",
            "Confirm Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        btn.Enabled = false;
        btn.Text = "…";

        var (ok, error) = UninstallManager.LaunchUninstaller(program.UninstallString);
        if (!ok)
        {
            btn.Enabled = true;
            btn.Text = "Uninstall";
            MessageBox.Show($"Could not launch uninstaller: {error}", "CaliberClean",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        nameLbl.Text += "  — uninstaller launched, finish it in the window that opened";
        nameLbl.ForeColor = _pal.Green;
        var parent = btn.Parent;
        parent?.Controls.Remove(btn);
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void LoadPrograms()
    {
        _refreshBtn.Enabled = false;
        _refreshBtn.Text = "Loading…";

        try
        {
            _programs = UninstallManager.GetInstalledPrograms();
            _statusLbl.Text = $"{_programs.Length} program(s)";
            _statusLbl.ForeColor = _pal.Muted;
        }
        catch (Exception ex)
        {
            _programs = [];
            _statusLbl.Text = $"✗ {ex.Message}";
            _statusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _refreshBtn.Enabled = true;
            _refreshBtn.Text = "Refresh";
        }

        RenderPrograms();
    }

    private void RenderPrograms()
    {
        var filter = _filterBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(filter)
            ? _programs
            : _programs.Where(p => p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Removing+re-adding _headerCard (even unchanged) drops focus from
        // _filterBox — this runs on every filter keystroke, so restore it or
        // typing feels broken after the first character.
        bool filterHadFocus = _filterBox.Focused;
        int caret = _filterBox.SelectionStart;

        _scroll.Controls.Clear();
        _cards = [_headerCard, .. filtered.Select(BuildProgramCard)];

        int y = 20;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Location = new Point(20, y);
            y += _cards[i].Height + (i == 0 ? 14 : 6);
            _scroll.Controls.Add(_cards[i]);
        }

        LayoutWidths();

        if (filterHadFocus)
        {
            _filterBox.Focus();
            _filterBox.SelectionStart = caret;
        }
    }
}
