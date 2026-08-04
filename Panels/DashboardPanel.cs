using CaliberClean;
using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-dashboard exactly: Disk Usage, Last Cleanup,
/// Scheduled Cleaning, Block Ads & Trackers — same four cards, same order.
/// Last Cleanup / Scheduled Cleaning are static placeholders on the web side
/// (CaliberHQ never wired them to live data), but this standalone app has
/// direct access to CleanHistory/ScheduleManager, so it shows the real values;
/// the "nothing yet" fallback text matches CaliberHQ's copy exactly either way.
public sealed class DashboardPanel : UserControl
{
    private readonly Palette _pal;

    private Label _blockDetailLbl = null!;
    private Label _blockActionStatusLbl = null!;
    private Button _blockToggleBtn = null!;
    private Button _blockRefreshBtn = null!;

    public DashboardPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        BuildUI();
    }

    private void BuildUI()
    {
        // Deterministic Location-based stacking inside a plain AutoScroll panel —
        // FlowLayoutPanel's AutoSize does not measure Dock=Top children correctly,
        // which silently collapsed the whole card stack to zero height.
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

        var cards = new List<Panel>
        {
            BuildDiskUsageCard(),
            BuildLastCleanupCard(),
            BuildScheduledCleaningCard(),
            BuildBlockAdsCard(),
        };

        int y = 20;
        foreach (var card in cards)
        {
            card.Location = new Point(20, y);
            y += card.Height + 12;
            scroll.Controls.Add(card);
        }

        void LayoutWidths()
        {
            int w = Math.Max(200, scroll.ClientSize.Width - 40);
            foreach (var c in cards) c.Width = w;
        }
        scroll.SizeChanged += (_, _) => LayoutWidths();
        LayoutWidths();

        Controls.Add(scroll);
    }

    // ── Card chrome (.cui-card / .cui-card-label) ─────────────────────────

    private Panel NewCard(int height, string label)
    {
        var card = new Panel
        {
            Height = height,
            Width = 400,
            BackColor = _pal.Panel,
        };
        card.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(_pal.Border2), 0, 0, card.Width - 1, card.Height - 1);

        var lbl = new Label
        {
            Text = label.ToUpperInvariant(),
            UseMnemonic = false, // a bare '&' is a mnemonic marker in WinForms and gets swallowed otherwise
            Location = new Point(14, 12),
            AutoSize = true,
            ForeColor = _pal.Gold,
            Font = Fonts.UI(13f, FontStyle.Bold),
        };
        card.Controls.Add(lbl);
        return card;
    }

    /// .clean-stat-row: a value label on the left, a pill/button on the right.
    private Label NewStatValue(string text, int y) => new()
    {
        Text = text,
        Location = new Point(14, y),
        AutoSize = true,
        ForeColor = _pal.White,
        Font = Fonts.Display(17f),
    };

    /// .cui-tab — the small pill used for status text ("No history", "Not Configured", ...).
    private Label NewPill(string text, int y, bool active)
    {
        var pill = new Label
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(11, 6, 11, 6),
            ForeColor = active ? _pal.Army : _pal.Muted,
            BackColor = active ? Palette.Blend(_pal.Army, _pal.Bg, 0.15) : _pal.Bg,
            Font = Fonts.UI(13f),
        };
        pill.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(_pal.Border2), 0, 0, pill.Width - 1, pill.Height - 1);
        pill.Location = new Point(0, y); // repositioned by caller once its Width is known
        return pill;
    }

    private void AnchorRight(Control c, Panel card, int rightMargin = 14)
    {
        void Reposition() => c.Left = card.Width - rightMargin - c.Width;
        c.SizeChanged += (_, _) => Reposition();
        card.SizeChanged += (_, _) => Reposition();
        Reposition();
    }

    // ── Disk Usage ─────────────────────────────────────────────────────────

    private Panel BuildDiskUsageCard()
    {
        DriveInfo[] drives;
        try { drives = DiskUsageAnalyzer.GetDrives(); } catch { drives = []; }

        const int labelH = 12 + 18 + 10; // top pad + label + gap before first row
        const int rowH = 18 + 2 + 14 + 10; // bar row + gap + meta row + row gap
        int contentH = drives.Length == 0 ? 20 : drives.Length * rowH - 10;
        var card = NewCard(labelH + contentH + 12, "Disk Usage");

        if (drives.Length == 0)
        {
            card.Controls.Add(new Label
            {
                Text = "No drives found.",
                Location = new Point(14, labelH),
                AutoSize = true,
                ForeColor = _pal.Muted,
                Font = Fonts.Body(12f, FontStyle.Italic),
            });
            return card;
        }

        int y = labelH;
        foreach (var drive in drives)
        {
            long total = 0, free = 0;
            try { total = drive.TotalSize; free = drive.AvailableFreeSpace; } catch { }
            long used = total - free;
            int pct = total > 0 ? (int)Math.Round(used * 100.0 / total) : 0;
            Color fill = pct > 90 ? _pal.Red : pct > 75 ? _pal.Orange : _pal.Gold;

            var letter = new Label
            {
                Text = drive.Name.TrimEnd('\\'),
                Location = new Point(14, y),
                AutoSize = true,
                ForeColor = _pal.White,
                Font = Fonts.Display(18f),
            };

            var pctLbl = new Label
            {
                Text = $"{pct}%",
                AutoSize = true,
                Location = new Point(0, y),
                ForeColor = _pal.White,
                Font = Fonts.Display(17f),
                TextAlign = ContentAlignment.MiddleRight,
            };
            AnchorRight(pctLbl, card);

            var barBg = new Panel { BackColor = _pal.Bg, Location = new Point(0, y + 1), Height = 10 };
            var barFill = new Panel { BackColor = fill, Location = new Point(0, 0), Height = 10 };
            barBg.Controls.Add(barFill);
            barBg.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(_pal.Border2), 0, 0, barBg.Width - 1, barBg.Height - 1);
            void LayoutBar()
            {
                int left = 14 + letter.Width + 10;
                int right = card.Width - 14 - pctLbl.Width - 10;
                barBg.Left = left;
                barBg.Width = Math.Max(10, right - left);
                barFill.Width = (int)(barBg.Width * (pct / 100.0));
            }
            card.SizeChanged += (_, _) => LayoutBar();
            letter.SizeChanged += (_, _) => LayoutBar();
            pctLbl.SizeChanged += (_, _) => LayoutBar();

            var meta = new Label
            {
                Text = $"{DiskUsageAnalyzer.FormatSize(used)} used   of {DiskUsageAnalyzer.FormatSize(total)}",
                Location = new Point(14, y + 22),
                AutoSize = true,
                ForeColor = _pal.Muted,
                Font = Fonts.Mono(11f),
            };
            var freeLbl = new Label
            {
                Text = $"{DiskUsageAnalyzer.FormatSize(free)} free",
                AutoSize = true,
                Location = new Point(0, y + 22),
                ForeColor = _pal.Army,
                Font = Fonts.Mono(11f),
            };
            AnchorRight(freeLbl, card);

            card.Controls.Add(letter);
            card.Controls.Add(pctLbl);
            card.Controls.Add(barBg);
            card.Controls.Add(meta);
            card.Controls.Add(freeLbl);
            LayoutBar();

            y += rowH;
        }

        return card;
    }

    // ── Last Cleanup ───────────────────────────────────────────────────────

    private Panel BuildLastCleanupCard()
    {
        var card = NewCard(12 + 18 + 10 + 20 + 12, "Last Cleanup");

        CleanHistoryRecord history;
        try { history = CleanHistory.Load(); } catch { history = new CleanHistoryRecord(DateTime.MinValue, 0); }

        bool hasRun = history.LastCleanDate != DateTime.MinValue;
        var value = NewStatValue(hasRun ? history.LastCleanDate.ToString("MMM d, yyyy h:mm tt") : "Not run yet", 40);
        var pill = NewPill(hasRun ? $"Freed {DiskUsageAnalyzer.FormatSize(history.LastCleanFreedBytes)}" : "No history", 36, hasRun);
        AnchorRight(pill, card);

        card.Controls.Add(value);
        card.Controls.Add(pill);
        return card;
    }

    // ── Scheduled Cleaning ─────────────────────────────────────────────────

    private Panel BuildScheduledCleaningCard()
    {
        var card = NewCard(12 + 18 + 10 + 20 + 12, "Scheduled Cleaning");

        ScheduleStatus status;
        try { status = ScheduleManager.GetScheduleStatus(); } catch { status = new ScheduleStatus(false, null); }

        var value = NewStatValue("Automated cleanup", 40);
        var pill = NewPill(status.IsEnabled ? status.Frequency?.ToString() ?? "Enabled" : "Not Configured", 36, status.IsEnabled);
        AnchorRight(pill, card);

        card.Controls.Add(value);
        card.Controls.Add(pill);
        return card;
    }

    // ── Block Ads & Trackers ───────────────────────────────────────────────
    // Same elevation-aware call pattern as the previous UI (RelaunchElevated,
    // IProgress<string> status, FriendlyError) — only the visuals changed here.

    private Panel BuildBlockAdsCard()
    {
        const int valueY = 40, detailY = 66, actionsY = 90, actionStatusY = 128, tipY = 148;
        var card = NewCard(12 + 18 + 10 + 130 + 12, "Block Ads & Trackers");

        var value = NewStatValue("System-wide (hosts file)", valueY);

        _blockToggleBtn = new Button
        {
            Text = "Disabled",
            AutoSize = true,
            Padding = new Padding(13, 4, 13, 4),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(11f),
            Cursor = Cursors.Hand,
            BackColor = _pal.Panel2,
            ForeColor = _pal.White,
        };
        _blockToggleBtn.FlatAppearance.BorderColor = _pal.Border2;
        _blockToggleBtn.FlatAppearance.BorderSize = 1;
        _blockToggleBtn.Click += BlockToggle_Click;
        AnchorRight(_blockToggleBtn, card);
        _blockToggleBtn.Top = valueY - 2;

        _blockDetailLbl = new Label
        {
            Text = "Loading…",
            Location = new Point(14, detailY),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        _blockRefreshBtn = new Button
        {
            Text = "Refresh List",
            Location = new Point(14, actionsY),
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(12f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = _pal.Gold,
            ForeColor = _pal.Bg,
        };
        _blockRefreshBtn.FlatAppearance.BorderColor = _pal.Gold;
        _blockRefreshBtn.FlatAppearance.BorderSize = 1;
        _blockRefreshBtn.Click += BlockRefresh_Click;

        _blockActionStatusLbl = new Label
        {
            Location = new Point(14, actionStatusY),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(10f),
        };

        var tip = new Label
        {
            Text = "Tip: pair with uBlock Origin in your browser — hosts-file blocking won't catch everything a browser extension will.",
            Location = new Point(14, tipY),
            AutoSize = false,
            Height = 28,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(10f),
        };
        card.SizeChanged += (_, _) => tip.Width = card.Width - 28;

        card.Controls.Add(value);
        card.Controls.Add(_blockToggleBtn);
        card.Controls.Add(_blockDetailLbl);
        card.Controls.Add(_blockRefreshBtn);
        card.Controls.Add(_blockActionStatusLbl);
        card.Controls.Add(tip);

        RefreshBlockAdsUI();
        return card;
    }

    private void RefreshBlockAdsUI()
    {
        BlocklistStatus status;
        try { status = HostsBlocklistService.GetStatus(); }
        catch (Exception ex) { status = new BlocklistStatus(false, 0, null, null, ex.Message); }

        _blockToggleBtn.Text = status.IsEnabled ? "✓ Enabled" : "Disabled";
        _blockToggleBtn.ForeColor = status.IsEnabled ? _pal.Army : _pal.White;

        if (status.Error != null)
        {
            _blockDetailLbl.Text = status.Error;
            _blockDetailLbl.ForeColor = _pal.Red;
        }
        else if (status.IsEnabled)
        {
            _blockDetailLbl.Text = $"{status.DomainCount:N0} domains blocked" +
                (status.LastRefreshedAt.HasValue ? $" · updated {status.LastRefreshedAt.Value:MMM d, yyyy}" : "");
            _blockDetailLbl.ForeColor = _pal.Muted;
        }
        else
        {
            _blockDetailLbl.Text = "List never downloaded · click to enable";
            _blockDetailLbl.ForeColor = _pal.Muted;
        }
    }

    private async void BlockToggle_Click(object? sender, EventArgs e)
    {
        bool wantEnable = !HostsBlocklistService.GetStatus().IsEnabled;

        if (!HostsBlocklistService.IsElevated())
        {
            PromptForElevation("Blocking ads & trackers system-wide requires administrator privileges to edit the hosts file.");
            return;
        }

        _blockToggleBtn.Enabled = false;
        _blockActionStatusLbl.Text = wantEnable ? "Applying…" : "Removing block list…";
        var progress = new Progress<string>(msg => _blockActionStatusLbl.Text = msg);

        try
        {
            if (wantEnable) await HostsBlocklistService.EnableAsync(progress);
            else await Task.Run(HostsBlocklistService.Disable);
            _blockActionStatusLbl.Text = $"✓ {(wantEnable ? "Enabled" : "Disabled")}";
        }
        catch (Exception ex)
        {
            _blockActionStatusLbl.Text = $"✗ {HostsBlocklistService.FriendlyError(ex)}";
        }
        finally
        {
            _blockToggleBtn.Enabled = true;
            RefreshBlockAdsUI();
        }
    }

    private async void BlockRefresh_Click(object? sender, EventArgs e)
    {
        if (!HostsBlocklistService.IsElevated())
        {
            PromptForElevation("Refreshing the block list requires administrator privileges.");
            return;
        }

        _blockRefreshBtn.Enabled = false;
        _blockActionStatusLbl.Text = "Waiting for UAC…";
        var progress = new Progress<string>(msg => _blockActionStatusLbl.Text = msg);

        try
        {
            await HostsBlocklistService.RefreshAsync(progress);
            _blockActionStatusLbl.Text = "✓ List refreshed";
        }
        catch (Exception ex)
        {
            _blockActionStatusLbl.Text = $"✗ {HostsBlocklistService.FriendlyError(ex)}";
        }
        finally
        {
            _blockRefreshBtn.Enabled = true;
            RefreshBlockAdsUI();
        }
    }

    private static void PromptForElevation(string reason)
    {
        var result = MessageBox.Show(
            $"{reason}\n\nRestart CaliberClean as administrator now?",
            "Administrator Required", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        if (HostsBlocklistService.RelaunchElevated())
        {
            Application.Exit();
            return;
        }

        MessageBox.Show("Elevation was cancelled — no changes were made.", "Administrator Required",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
