using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class BrowserCachePanel : UserControl
{
    private static readonly Color BgColor = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color DangerRed = Color.FromArgb(0xC0, 0x39, 0x2B);

    private readonly BrowserCacheCleaner _cleaner = new();
    private List<BrowserProfile> _profiles = [];
    private CancellationTokenSource? _cts;

    private ListView _listView = null!;
    private Button _scanBtn = null!;
    private Button _cleanBtn = null!;
    private Button _selectAllBtn = null!;
    private Label _summaryLabel = null!;
    private Label _statusLabel = null!;

    public BrowserCachePanel()
    {
        Dock = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
    }

    private void BuildUI()
    {
        SuspendLayout();

        // --- Toolbar ---
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = PanelColor,
            Padding = new Padding(16, 0, 16, 0),
        };
        toolbar.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);

        _scanBtn = MakeButton("SCAN", GoldColor, 110);
        _scanBtn.Dock = DockStyle.Left;
        _scanBtn.Click += ScanBtn_Click;

        _selectAllBtn = MakeButton("SELECT ALL", Color.FromArgb(0x44, 0x44, 0x44), 110);
        _selectAllBtn.Dock = DockStyle.Left;
        _selectAllBtn.Enabled = false;
        _selectAllBtn.Click += (s, e) => ToggleSelectAll();

        _summaryLabel = new Label
        {
            Text = "Click SCAN to detect browser caches",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(16, 0, 0, 0),
        };

        toolbar.Controls.Add(_summaryLabel);
        toolbar.Controls.Add(_selectAllBtn);
        toolbar.Controls.Add(_scanBtn);

        // --- Warning banner (browsers must be closed) ---
        var warnPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(0x33, 0x28, 0x00),
        };
        warnPanel.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(Color.FromArgb(0xFF, 0xCC, 0x01, 0x40)), 0, warnPanel.Height - 1, warnPanel.Width, warnPanel.Height - 1);

        var warnLabel = new Label
        {
            Text = "⚠  Close all browsers before cleaning cache",
            ForeColor = Color.FromArgb(0xFF, 0xCC, 0x01),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
        };
        warnPanel.Controls.Add(warnLabel);

        // --- List view ---
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            CheckBoxes = true,
            BackColor = BgColor,
            ForeColor = TextColor,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9f),
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };

        _listView.Columns.Add("Browser", 160);
        _listView.Columns.Add("Profile", 140);
        _listView.Columns.Add("Cache Size", 100, HorizontalAlignment.Right);
        _listView.Columns.Add("Cache Path", 400);
        _listView.ItemChecked += (s, e) => UpdateCleanButton();
        _listView.Resize += (s, e) =>
        {
            int fixed_ = _listView.Columns[0].Width + _listView.Columns[1].Width + _listView.Columns[2].Width + 32;
            int avail = _listView.Width - fixed_;
            if (avail > 80) _listView.Columns[3].Width = avail;
        };

        // --- Action bar ---
        var actionBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = PanelColor,
            Padding = new Padding(16, 0, 16, 0),
        };
        actionBar.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, 0, actionBar.Width, 0);

        _cleanBtn = MakeButton("CLEAN SELECTED", DangerRed, 170);
        _cleanBtn.Dock = DockStyle.Right;
        _cleanBtn.Enabled = false;
        _cleanBtn.Click += CleanBtn_Click;

        _statusLabel = new Label
        {
            Text = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
        };

        actionBar.Controls.Add(_cleanBtn);
        actionBar.Controls.Add(_statusLabel);

        Controls.Add(_listView);
        Controls.Add(warnPanel);
        Controls.Add(toolbar);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        SetScanning(true);
        _listView.Items.Clear();
        _profiles = [];

        var progress = new Progress<string>(msg => SetStatus(msg));

        try
        {
            _profiles = await _cleaner.ScanAsync(progress, _cts.Token);
            PopulateList(_profiles);
            UpdateSummary();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Scan error: {ex.Message}");
        }
        finally
        {
            SetScanning(false);
        }
    }

    private async void CleanBtn_Click(object? sender, EventArgs e)
    {
        var toClean = _listView.CheckedItems
            .Cast<ListViewItem>()
            .Select(lvi => lvi.Tag as BrowserProfile)
            .Where(p => p != null)
            .Cast<BrowserProfile>()
            .ToList();

        if (toClean.Count == 0) return;

        long totalSize = toClean.Sum(p => p.SizeBytes);
        var confirm = MessageBox.Show(
            $"Clear cache for {toClean.Count} browser profile(s)?\n" +
            $"Approximately {BrowserCacheCleaner.FormatSize(totalSize)} will be deleted.\n\n" +
            "Make sure all affected browsers are closed.",
            "CALIBER CLEAN — Confirm Cache Clear",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        SetCleaning(true);

        var progress = new Progress<string>(msg => SetStatus(msg));

        try
        {
            var (cleaned, skipped, freed) = await _cleaner.CleanAsync(toClean, progress, _cts.Token);

            // Re-scan sizes for cleaned profiles and update rows
            SetStatus("Refreshing sizes…");
            _profiles = await _cleaner.ScanAsync(null, _cts.Token);
            PopulateList(_profiles);
            UpdateSummary();

            string skippedNote = skipped > 0 ? $" ({skipped} file(s) skipped — browser may still be open)" : "";
            SetStatus($"Freed {BrowserCacheCleaner.FormatSize(freed)} across {cleaned} profile(s){skippedNote}");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Clean cancelled.");
        }
        finally
        {
            SetCleaning(false);
        }
    }

    private void PopulateList(List<BrowserProfile> profiles)
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        string? lastBrowser = null;
        foreach (var profile in profiles)
        {
            var lvi = new ListViewItem(profile.BrowserName)
            {
                Tag = profile,
                BackColor = BgColor,
                ForeColor = TextColor,
                ToolTipText = profile.CachePath,
            };

            // Dim the browser name on repeated rows for same browser
            if (profile.BrowserName == lastBrowser)
                lvi.ForeColor = MutedGray;

            lvi.SubItems.Add(profile.ProfileName);
            lvi.SubItems.Add(BrowserCacheCleaner.FormatSize(profile.SizeBytes));
            lvi.SubItems[2].ForeColor = profile.SizeBytes > 104_857_600 ? DangerRed
                : profile.SizeBytes > 10_485_760 ? GoldColor
                : ArmyGreen;
            lvi.SubItems.Add(ShortenPath(profile.CachePath));

            _listView.Items.Add(lvi);
            lastBrowser = profile.BrowserName;
        }

        _listView.EndUpdate();
        _selectAllBtn.Enabled = profiles.Count > 0;
    }

    private void UpdateSummary()
    {
        long total = _profiles.Sum(p => p.SizeBytes);
        int count = _profiles.Count;
        _summaryLabel.Text = count == 0
            ? "No browser caches found (or no browsers installed)."
            : $"{count} profile(s) found — {BrowserCacheCleaner.FormatSize(total)} total cache";
        _summaryLabel.ForeColor = count > 0 ? TextColor : MutedGray;
    }

    private void UpdateCleanButton()
    {
        int checkedCount = _listView.CheckedItems.Count;
        _cleanBtn.Enabled = checkedCount > 0;
        if (checkedCount > 0)
        {
            long checkedSize = _listView.CheckedItems
                .Cast<ListViewItem>()
                .Sum(lvi => (lvi.Tag as BrowserProfile)?.SizeBytes ?? 0);
            _cleanBtn.Text = $"CLEAN {checkedCount} ({BrowserCacheCleaner.FormatSize(checkedSize)})";
        }
        else
        {
            _cleanBtn.Text = "CLEAN SELECTED";
        }
    }

    private void ToggleSelectAll()
    {
        bool anyUnchecked = _listView.Items.Cast<ListViewItem>().Any(i => !i.Checked);
        _listView.BeginUpdate();
        foreach (ListViewItem item in _listView.Items)
            item.Checked = anyUnchecked;
        _listView.EndUpdate();
        _selectAllBtn.Text = anyUnchecked ? "DESELECT ALL" : "SELECT ALL";
    }

    private void SetScanning(bool scanning)
    {
        _scanBtn.Enabled = !scanning;
        _scanBtn.Text = scanning ? "SCANNING…" : "SCAN";
        _selectAllBtn.Enabled = !scanning && _listView.Items.Count > 0;
        _cleanBtn.Enabled = false;
        if (!scanning) SetStatus("");
    }

    private void SetCleaning(bool cleaning)
    {
        _cleanBtn.Enabled = !cleaning;
        _scanBtn.Enabled = !cleaning;
        _selectAllBtn.Enabled = !cleaning;
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _statusLabel.Text = text;
    }

    private static string ShortenPath(string path)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (path.StartsWith(local, StringComparison.OrdinalIgnoreCase))
            return "%LOCALAPPDATA%" + path[local.Length..];
        if (path.StartsWith(roaming, StringComparison.OrdinalIgnoreCase))
            return "%APPDATA%" + path[roaming.Length..];
        return path;
    }

    private static Button MakeButton(string text, Color backColor, int width)
    {
        bool isDark = backColor.GetBrightness() < 0.5f;
        var btn = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = isDark ? Color.White : Color.FromArgb(0x0D, 0x0D, 0x0D),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.1f);
        return btn;
    }
}
