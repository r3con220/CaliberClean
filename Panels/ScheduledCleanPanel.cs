using CaliberClean.Services;

namespace CaliberClean.Panels;

public class ScheduledCleanPanel : UserControl
{
    // ── Theme ─────────────────────────────────────────────────────────────────
    private static readonly Color BgColor     = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor  = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor   = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor   = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen   = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray   = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color DangerRed   = Color.FromArgb(0xC0, 0x39, 0x2B);

    // ── Controls ──────────────────────────────────────────────────────────────
    private CheckBox   _enableToggle  = null!;
    private ComboBox   _freqCombo     = null!;
    private Label      _freqLabel     = null!;
    private Label      _lastRunLabel  = null!;
    private Label      _summaryLabel  = null!;
    private Button     _runNowBtn     = null!;
    private Label      _statusLabel   = null!;
    private Panel      _optionsPanel  = null!;

    // Category checkboxes
    private CheckBox _cbWinTemp   = null!;
    private CheckBox _cbUserTemp  = null!;
    private CheckBox _cbPrefetch  = null!;
    private CheckBox _cbWuCache   = null!;
    private CheckBox _cbChrome    = null!;
    private CheckBox _cbEdge      = null!;
    private CheckBox _cbFirefox   = null!;

    public ScheduledCleanPanel()
    {
        Dock      = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
        LoadState();
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var scroll = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = BgColor,
            AutoScroll = true,
            Padding   = new Padding(32, 28, 32, 28),
        };
        Controls.Add(scroll);

        // ── Page title ────────────────────────────────────────────────────────
        var title = MakeLabel("SCHEDULED CLEANING", 22f, GoldColor, bold: true);
        title.Margin = new Padding(0, 0, 0, 2);
        var subtitle = MakeLabel("Set it and forget it — runs silently in the background", 9.5f, MutedGray);
        subtitle.Margin = new Padding(0, 0, 0, 20);

        // ── Card: Enable / Frequency ──────────────────────────────────────────
        var card1 = MakeCard();
        var cardTitle1 = MakeSectionHeader("AUTOMATIC CLEANING");

        _enableToggle = new CheckBox
        {
            Text      = "Enable automatic cleaning",
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            AutoSize  = true,
            Margin    = new Padding(0, 0, 0, 14),
            Cursor    = Cursors.Hand,
        };
        _enableToggle.CheckedChanged += EnableToggle_Changed;

        var freqRow = new FlowLayoutPanel
        {
            AutoSize        = true,
            FlowDirection   = FlowDirection.LeftToRight,
            BackColor       = Color.Transparent,
            Margin          = new Padding(0, 0, 0, 0),
            WrapContents    = false,
        };

        _freqLabel = MakeLabel("Frequency:", 9.5f, MutedGray);
        _freqLabel.Margin = new Padding(0, 4, 10, 0);

        _freqCombo = new ComboBox
        {
            DropDownStyle   = ComboBoxStyle.DropDownList,
            BackColor       = Color.FromArgb(0x1E, 0x1E, 0x1E),
            ForeColor       = TextColor,
            FlatStyle       = FlatStyle.Flat,
            Font            = new Font("Segoe UI", 9.5f),
            Width           = 140,
            Margin          = new Padding(0),
        };
        _freqCombo.Items.AddRange(["Daily", "Weekly", "Monthly"]);
        _freqCombo.SelectedIndex = 0;

        freqRow.Controls.Add(_freqLabel);
        freqRow.Controls.Add(_freqCombo);

        card1.Controls.Add(cardTitle1);
        card1.Controls.Add(_enableToggle);
        card1.Controls.Add(freqRow);

        // ── Card: Categories ──────────────────────────────────────────────────
        _optionsPanel = MakeCard();
        var cardTitle2 = MakeSectionHeader("CATEGORIES TO AUTO-CLEAN");

        var note = MakeLabel(
            "⚠  Recycle Bin is always excluded from automatic cleaning.",
            8.5f, Color.FromArgb(0xAA, 0x80, 0x00));
        note.Margin = new Padding(0, 0, 0, 10);

        var tempHeader = MakeLabel("Temp Files", 9f, ArmyGreen);
        tempHeader.Margin = new Padding(0, 0, 0, 4);

        _cbWinTemp  = MakeCategoryCheck("Windows Temp");
        _cbUserTemp = MakeCategoryCheck("User Temp");
        _cbPrefetch = MakeCategoryCheck("Prefetch");
        _cbWuCache  = MakeCategoryCheck("Windows Update Cache");

        var browserHeader = MakeLabel("Browser Cache", 9f, ArmyGreen);
        browserHeader.Margin = new Padding(0, 10, 0, 4);

        _cbChrome  = MakeCategoryCheck("Google Chrome");
        _cbEdge    = MakeCategoryCheck("Microsoft Edge");
        _cbFirefox = MakeCategoryCheck("Mozilla Firefox");

        _optionsPanel.Controls.Add(cardTitle2);
        _optionsPanel.Controls.Add(note);
        _optionsPanel.Controls.Add(tempHeader);
        _optionsPanel.Controls.Add(_cbWinTemp);
        _optionsPanel.Controls.Add(_cbUserTemp);
        _optionsPanel.Controls.Add(_cbPrefetch);
        _optionsPanel.Controls.Add(_cbWuCache);
        _optionsPanel.Controls.Add(browserHeader);
        _optionsPanel.Controls.Add(_cbChrome);
        _optionsPanel.Controls.Add(_cbEdge);
        _optionsPanel.Controls.Add(_cbFirefox);

        // ── Card: Last run + Run Now ──────────────────────────────────────────
        var card3 = MakeCard();
        var cardTitle3 = MakeSectionHeader("LAST RUN");

        _lastRunLabel = MakeLabel("—", 10f, TextColor);
        _lastRunLabel.Margin = new Padding(0, 0, 0, 2);

        _summaryLabel = MakeLabel("", 9f, MutedGray);
        _summaryLabel.Margin = new Padding(0, 0, 0, 14);

        _runNowBtn = new Button
        {
            Text      = "▶  RUN NOW",
            BackColor = ArmyGreen,
            ForeColor = BgColor,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Height    = 36,
            Width     = 140,
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 0, 0),
        };
        _runNowBtn.FlatAppearance.BorderSize = 0;
        _runNowBtn.Click += RunNow_Click;

        _statusLabel = MakeLabel("", 8.5f, MutedGray);
        _statusLabel.Margin = new Padding(0, 8, 0, 0);

        card3.Controls.Add(cardTitle3);
        card3.Controls.Add(_lastRunLabel);
        card3.Controls.Add(_summaryLabel);
        card3.Controls.Add(_runNowBtn);
        card3.Controls.Add(_statusLabel);

        // ── Save button ───────────────────────────────────────────────────────
        var saveBtn = new Button
        {
            Text      = "SAVE SCHEDULE",
            BackColor = GoldColor,
            ForeColor = BgColor,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Height    = 36,
            Width     = 160,
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 16, 0, 0),
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.Click += SaveBtn_Click;

        // ── Layout ────────────────────────────────────────────────────────────
        var layout = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            BackColor     = Color.Transparent,
        };

        layout.Controls.Add(title);
        layout.Controls.Add(subtitle);
        layout.Controls.Add(card1);
        layout.Controls.Add(_optionsPanel);
        layout.Controls.Add(card3);
        layout.Controls.Add(saveBtn);

        scroll.Controls.Add(layout);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private void LoadState()
    {
        var status = ScheduleManager.GetScheduleStatus();
        var config = ScheduleManager.LoadConfig();

        _enableToggle.Checked = status.IsEnabled;

        if (config != null)
        {
            _freqCombo.SelectedIndex = (int)config.Frequency;
            _cbWinTemp.Checked  = config.CleanWinTemp;
            _cbUserTemp.Checked = config.CleanUserTemp;
            _cbPrefetch.Checked = config.CleanPrefetch;
            _cbWuCache.Checked  = config.CleanWuCache;
            _cbChrome.Checked   = config.CleanChrome;
            _cbEdge.Checked     = config.CleanEdge;
            _cbFirefox.Checked  = config.CleanFirefox;
        }
        else
        {
            // Defaults: safe temp files on, browsers off
            _cbWinTemp.Checked  = true;
            _cbUserTemp.Checked = true;
            _cbPrefetch.Checked = false;
            _cbWuCache.Checked  = false;
            _cbChrome.Checked   = false;
            _cbEdge.Checked     = false;
            _cbFirefox.Checked  = false;
        }

        UpdateOptionsEnabled();
        RefreshLastRun();
    }

    private void UpdateOptionsEnabled()
    {
        bool on = _enableToggle.Checked;
        _freqCombo.Enabled   = on;
        _freqLabel.ForeColor = on ? MutedGray : Color.FromArgb(0x40, 0x40, 0x40);
        _optionsPanel.Enabled = on;
    }

    private void RefreshLastRun()
    {
        var (lastRun, summary) = AutoCleanRunner.ReadLastRun();

        if (lastRun.HasValue)
        {
            _lastRunLabel.Text  = lastRun.Value.ToString("dddd, MMMM d yyyy  ·  h:mm tt");
            _summaryLabel.Text  = summary;
        }
        else
        {
            _lastRunLabel.Text  = "Never run";
            _summaryLabel.Text  = "";
        }
    }

    private ScheduleConfig BuildConfig() => new(
        Enabled:      _enableToggle.Checked,
        Frequency:    (CleanFrequency)_freqCombo.SelectedIndex,
        CleanWinTemp:  _cbWinTemp.Checked,
        CleanUserTemp: _cbUserTemp.Checked,
        CleanPrefetch: _cbPrefetch.Checked,
        CleanWuCache:  _cbWuCache.Checked,
        CleanChrome:   _cbChrome.Checked,
        CleanEdge:     _cbEdge.Checked,
        CleanFirefox:  _cbFirefox.Checked
    );

    // ── Event handlers ────────────────────────────────────────────────────────

    private void EnableToggle_Changed(object? s, EventArgs e) => UpdateOptionsEnabled();

    private void SaveBtn_Click(object? s, EventArgs e)
    {
        var config = BuildConfig();

        if (config.Enabled)
            ScheduleManager.EnableSchedule(config.Frequency, config);
        else
            ScheduleManager.DisableSchedule();

        SetStatus("Schedule saved.", GoldColor);
    }

    private async void RunNow_Click(object? s, EventArgs e)
    {
        _runNowBtn.Enabled = false;
        SetStatus("Running auto-clean…", MutedGray);

        // Persist current category selections so AutoCleanRunner picks them up
        ScheduleManager.SaveConfig(BuildConfig());

        try
        {
            await AutoCleanRunner.RunAsync();
            RefreshLastRun();
            SetStatus("Done.", ArmyGreen);
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", DangerRed);
        }
        finally
        {
            _runNowBtn.Enabled = true;
        }
    }

    private void SetStatus(string msg, Color color)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(msg, color)); return; }
        _statusLabel.Text      = msg;
        _statusLabel.ForeColor = color;
    }

    // ── UI factory helpers ────────────────────────────────────────────────────

    private static Panel MakeCard()
    {
        var p = new FlowLayoutPanel
        {
            BackColor     = Color.FromArgb(0x14, 0x14, 0x14),
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            Padding       = new Padding(20, 16, 20, 20),
            Margin        = new Padding(0, 0, 0, 12),
            Width         = 540,
        };
        p.Paint += (s, e) =>
        {
            var pen = new Pen(Color.FromArgb(0x2A, 0x2A, 0x2A));
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            pen.Dispose();
        };
        return p;
    }

    private static Label MakeSectionHeader(string text)
    {
        var lbl = new Label
        {
            Text      = text,
            ForeColor = Color.FromArgb(0x66, 0x66, 0x66),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize  = true,
            Margin    = new Padding(0, 0, 0, 10),
        };
        return lbl;
    }

    private static Label MakeLabel(string text, float size, Color color, bool bold = false)
    {
        return new Label
        {
            Text      = text,
            ForeColor = color,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoSize  = true,
            Margin    = new Padding(0, 0, 0, 4),
        };
    }

    private static CheckBox MakeCategoryCheck(string text)
    {
        return new CheckBox
        {
            Text      = text,
            ForeColor = Color.FromArgb(0xCC, 0xCA, 0xC5),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9.5f),
            AutoSize  = true,
            Margin    = new Padding(0, 0, 0, 4),
            Cursor    = Cursors.Hand,
        };
    }
}
