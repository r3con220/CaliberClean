using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-scheduled exactly: same four .clean-section
/// blocks (Scheduled Clean/Refresh, Automatic Cleaning, Categories to Auto-Clean,
/// Last Run), same copy, same 0.68rem section-label size (smaller than the
/// Dashboard's .cui-card-label — a different class in the source). Wired to
/// ScheduleManager directly (in-process) rather than CaliberHQ's /api/caliberclean
/// fetch calls, since this app doesn't need the network hop.
public sealed class ScheduledCleanPanel : UserControl
{
    private readonly Palette _pal;

    private Button _refreshBtn = null!;
    private Label _statusLbl = null!;
    private CheckBox _enabledCb = null!;
    private ComboBox _frequencyCombo = null!;
    private CheckBox _cbWinTemp = null!, _cbUserTemp = null!, _cbPrefetch = null!, _cbWuCache = null!;
    private CheckBox _cbChrome = null!, _cbEdge = null!, _cbFirefox = null!;
    private Label _lastRunLbl = null!;
    private Button _saveBtn = null!;
    private Label _saveStatusLbl = null!;

    public ScheduledCleanPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;
        BuildUI();
        LoadSchedule();
    }

    private void BuildUI()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

        var cards = new List<Panel>
        {
            BuildRefreshSection(),
            BuildAutomaticCleaningSection(),
            BuildCategoriesSection(),
            BuildLastRunSection(),
        };

        CardChrome.StackVertically(scroll, cards);
        Controls.Add(scroll);
    }

    // ── .clean-section chrome (11px label) — see CardChrome.cs ──

    private Panel NewSection(int height, string label) => CardChrome.NewCard(_pal, height, label, 11f);

    private CheckBox NewCheck(string text, Point location, float fontSize = 12f)
    {
        return new CheckBox
        {
            Text = text,
            UseMnemonic = false,
            Location = location,
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = _pal.White,
            Font = Fonts.Body(fontSize),
            Cursor = Cursors.Hand,
        };
    }

    // ── Section 1: header + refresh ─────────────────────────────────────────

    private Panel BuildRefreshSection()
    {
        var section = NewSection(12 + 15 + 4 + 16 + 10 + 38 + 10 + 16 + 12, "Scheduled Clean");

        var subtitle = new Label
        {
            Text = "Set it and forget it — runs silently in the background",
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
        _refreshBtn.Click += (_, _) => LoadSchedule();

        _statusLbl = new Label
        {
            Location = new Point(14, 105),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        section.Controls.Add(subtitle);
        section.Controls.Add(_refreshBtn);
        section.Controls.Add(_statusLbl);
        return section;
    }

    // ── Section 2: Automatic Cleaning ───────────────────────────────────────

    private Panel BuildAutomaticCleaningSection()
    {
        var section = NewSection(12 + 15 + 4 + 22 + 4 + 26 + 12, "Automatic Cleaning");

        _enabledCb = NewCheck("Enable automatic cleaning", new Point(14, 31), 13f);

        var freqLbl = new Label
        {
            Text = "Frequency:",
            Location = new Point(14, 60),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        _frequencyCombo = new ComboBox
        {
            Location = new Point(84, 56),
            Width = 110,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _pal.Panel2,
            ForeColor = _pal.White,
            Font = Fonts.UI(12f),
            FlatStyle = FlatStyle.Flat,
        };
        _frequencyCombo.Items.AddRange(["Daily", "Weekly", "Monthly"]);
        _frequencyCombo.SelectedIndex = 0;

        section.Controls.Add(_enabledCb);
        section.Controls.Add(freqLbl);
        section.Controls.Add(_frequencyCombo);
        return section;
    }

    // ── Section 3: Categories to Auto-Clean ─────────────────────────────────

    private Panel BuildCategoriesSection()
    {
        const int rowH = 21;
        var section = NewSection(12 + 15 + 4 + 18 + 4 + 15 + rowH * 4 + 8 + 15 + rowH * 3 + 12, "Categories to Auto-Clean");

        var warn = new Label
        {
            Text = "⚠ Recycle Bin is always excluded from automatic cleaning.",
            Location = new Point(14, 31),
            AutoSize = true,
            ForeColor = Color.FromArgb(0xD9, 0xA4, 0x41),
            Font = Fonts.Body(11f),
        };

        int y = 53;
        var tempLbl = new Label { Text = "Temp Files", Location = new Point(14, y), AutoSize = true, ForeColor = _pal.Army, Font = Fonts.UI(11f) };
        y += 19;
        _cbWinTemp = NewCheck("Windows Temp", new Point(14, y)); y += rowH;
        _cbUserTemp = NewCheck("User Temp", new Point(14, y)); y += rowH;
        _cbPrefetch = NewCheck("Prefetch", new Point(14, y)); y += rowH;
        _cbWuCache = NewCheck("Windows Update Cache", new Point(14, y)); y += rowH + 8;

        var browserLbl = new Label { Text = "Browser Cache", Location = new Point(14, y), AutoSize = true, ForeColor = _pal.Army, Font = Fonts.UI(11f) };
        y += 19;
        _cbChrome = NewCheck("Google Chrome", new Point(14, y)); y += rowH;
        _cbEdge = NewCheck("Microsoft Edge", new Point(14, y)); y += rowH;
        _cbFirefox = NewCheck("Mozilla Firefox", new Point(14, y));

        section.Controls.Add(warn);
        section.Controls.Add(tempLbl);
        section.Controls.Add(_cbWinTemp);
        section.Controls.Add(_cbUserTemp);
        section.Controls.Add(_cbPrefetch);
        section.Controls.Add(_cbWuCache);
        section.Controls.Add(browserLbl);
        section.Controls.Add(_cbChrome);
        section.Controls.Add(_cbEdge);
        section.Controls.Add(_cbFirefox);
        return section;
    }

    // ── Section 4: Last Run ──────────────────────────────────────────────────

    private Panel BuildLastRunSection()
    {
        var section = NewSection(12 + 15 + 4 + 18 + 10 + 38 + 10 + 16 + 12, "Last Run");

        _lastRunLbl = new Label
        {
            Text = "—",
            Location = new Point(14, 31),
            AutoSize = true,
            ForeColor = _pal.White,
            Font = Fonts.Body(13f),
        };

        _saveBtn = new Button
        {
            Text = "Save Schedule",
            Location = new Point(14, 59),
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(12f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = _pal.Gold,
            ForeColor = _pal.Bg,
        };
        _saveBtn.FlatAppearance.BorderColor = _pal.Gold;
        _saveBtn.FlatAppearance.BorderSize = 1;
        _saveBtn.Click += SaveBtn_Click;

        _saveStatusLbl = new Label
        {
            Location = new Point(14, 107),
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        section.Controls.Add(_lastRunLbl);
        section.Controls.Add(_saveBtn);
        section.Controls.Add(_saveStatusLbl);
        return section;
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void LoadSchedule()
    {
        _refreshBtn.Enabled = false;
        _refreshBtn.Text = "Loading…";
        _statusLbl.Text = "";

        try
        {
            var status = ScheduleManager.GetScheduleStatus();
            var config = ScheduleManager.LoadConfig();
            var nextRun = ScheduleManager.GetNextRun();
            var (lastRun, lastRunSummary) = AutoCleanRunner.ReadLastRun();

            _enabledCb.Checked = status.IsEnabled;
            _frequencyCombo.SelectedItem = (status.Frequency ?? CleanFrequency.Daily).ToString();

            _cbWinTemp.Checked = config?.CleanWinTemp ?? true;
            _cbUserTemp.Checked = config?.CleanUserTemp ?? true;
            _cbPrefetch.Checked = config?.CleanPrefetch ?? false;
            _cbWuCache.Checked = config?.CleanWuCache ?? false;
            _cbChrome.Checked = config?.CleanChrome ?? false;
            _cbEdge.Checked = config?.CleanEdge ?? false;
            _cbFirefox.Checked = config?.CleanFirefox ?? false;

            _lastRunLbl.Text = lastRun.HasValue
                ? $"{lastRun.Value:MMM d, yyyy h:mm tt} — {lastRunSummary}"
                : (string.IsNullOrEmpty(lastRunSummary) ? "Never run" : lastRunSummary);

            _statusLbl.Text = status.IsEnabled
                ? $"Enabled ({status.Frequency}) — next run {(nextRun.HasValue ? nextRun.Value.ToString("MMM d, yyyy h:mm tt") : "unknown")}"
                : "Not configured";
            _statusLbl.ForeColor = _pal.Muted;
        }
        catch (Exception ex)
        {
            _statusLbl.Text = $"✗ {ex.Message}";
            _statusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _refreshBtn.Enabled = true;
            _refreshBtn.Text = "Refresh";
        }
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        bool wantEnabled = _enabledCb.Checked;

        // EnableSchedule/DisableSchedule both shell out to schtasks.exe with
        // /rl HIGHEST, which Windows only allows from an elevated caller —
        // same relaunch prompt as the Block Ads & Trackers card.
        if (!HostsBlocklistService.IsElevated())
        {
            Elevation.PromptIfNeeded("Scheduling automatic cleaning requires administrator privileges to register the Windows task.");
            return;
        }

        _saveBtn.Enabled = false;
        _saveBtn.Text = "Saving…";
        _saveStatusLbl.Text = "";

        try
        {
            if (!Enum.TryParse<CleanFrequency>((string)_frequencyCombo.SelectedItem!, out var frequency))
                frequency = CleanFrequency.Daily;

            var config = new ScheduleConfig(
                Enabled: wantEnabled,
                Frequency: frequency,
                CleanWinTemp: _cbWinTemp.Checked,
                CleanUserTemp: _cbUserTemp.Checked,
                CleanPrefetch: _cbPrefetch.Checked,
                CleanWuCache: _cbWuCache.Checked,
                CleanChrome: _cbChrome.Checked,
                CleanEdge: _cbEdge.Checked,
                CleanFirefox: _cbFirefox.Checked);

            var (ok, error) = wantEnabled
                ? ScheduleManager.EnableSchedule(frequency, config)
                : ScheduleManager.DisableSchedule();

            if (!ok)
            {
                _saveStatusLbl.Text = $"✗ {error}";
                _saveStatusLbl.ForeColor = _pal.Red;
            }
            else
            {
                _saveStatusLbl.Text = "✓ Schedule saved";
                _saveStatusLbl.ForeColor = _pal.Green;
                LoadSchedule();
            }
        }
        catch (Exception ex)
        {
            _saveStatusLbl.Text = $"✗ {ex.Message}";
            _saveStatusLbl.ForeColor = _pal.Red;
        }
        finally
        {
            _saveBtn.Enabled = true;
            _saveBtn.Text = "Save Schedule";
        }
    }
}
