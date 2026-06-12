using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class BrowserCachePanel : UserControl
{
    private static readonly Color BgColor     = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor  = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color RowAltColor = Color.FromArgb(0x11, 0x11, 0x11);
    private static readonly Color TextColor   = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor   = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen   = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray   = Color.FromArgb(0x66, 0x66, 0x66);

    private readonly BrowserCacheCleaner _cleaner = new();

    private BrowserRow[]             _rows    = [];
    private CancellationTokenSource? _cts;
    private bool                     _scanDone;

    private Button      _scanBtn   = null!;
    private Button      _cleanBtn  = null!;
    private Label       _summary   = null!;
    private Label       _status    = null!;
    private ProgressBar _progress  = null!;
    private Panel       _rowsPanel = null!;

    public BrowserCachePanel()
    {
        Dock      = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
    }

    private void BuildUI()
    {
        SuspendLayout();

        // ── Top toolbar ────────────────────────────────────────────────────────
        var toolbar = MakeBar(DockStyle.Top, 54, isTop: true);
        toolbar.Padding = new Padding(14, 10, 14, 10);

        _scanBtn = MakeBtn("⟳  SCAN", GoldColor, 110);
        _scanBtn.Click += ScanBtn_Click;

        _summary = new Label
        {
            Text      = "Click SCAN to detect installed browsers",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9.5f),
            Padding   = new Padding(14, 0, 0, 0),
        };

        toolbar.Controls.Add(_summary);
        toolbar.Controls.Add(_scanBtn);

        // ── Column header strip ────────────────────────────────────────────────
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 26,
            BackColor = Color.FromArgb(0x10, 0x10, 0x10),
        };
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        AddColHeader(header, "Browser",  80,  ContentAlignment.MiddleLeft);
        AddColHeader(header, "Files",   220,  ContentAlignment.MiddleRight);
        AddColHeader(header, "Size",    310,  ContentAlignment.MiddleRight);
        AddColHeader(header, "Status",  430,  ContentAlignment.MiddleLeft);

        // ── Scrollable rows area ───────────────────────────────────────────────
        _rowsPanel = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = BgColor,
        };
        ShowEmpty("Click SCAN to detect installed browsers.");

        // ── Progress bar ───────────────────────────────────────────────────────
        _progress = new ProgressBar
        {
            Dock                  = DockStyle.Bottom,
            Height                = 3,
            Style                 = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 0,
            BackColor             = PanelColor,
            ForeColor             = GoldColor,
            Visible               = false,
        };

        // ── Bottom action bar ──────────────────────────────────────────────────
        var actionBar = MakeBar(DockStyle.Bottom, 52, isTop: false);
        actionBar.Padding = new Padding(14, 9, 14, 9);

        _cleanBtn = MakeBtn("CLEAN SELECTED", ArmyGreen, 170);
        _cleanBtn.Dock    = DockStyle.Right;
        _cleanBtn.Enabled = false;
        _cleanBtn.Click  += CleanBtn_Click;

        _status = new Label
        {
            Text      = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9f),
        };

        actionBar.Controls.Add(_cleanBtn);
        actionBar.Controls.Add(_status);

        // DockStyle stacking: Fill goes first, then Top items in reverse display order
        Controls.Add(_rowsPanel);
        Controls.Add(header);
        Controls.Add(toolbar);
        Controls.Add(_progress);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    // ── Scan ──────────────────────────────────────────────────────────────────
    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts      = new CancellationTokenSource();
        _scanDone = false;

        SetBusy(true, "Detecting browsers…");

        var browsers = BrowserCacheCleaner.DetectBrowsers();
        RebuildRows(browsers);

        if (browsers.Length == 0)
        {
            ShowEmpty("No supported browsers found on this machine.");
            SetBusy(false, null);
            UpdateSummaryBar();
            return;
        }

        var progress = new Progress<string>(SetStatus);
        try
        {
            foreach (var row in _rows)
            {
                _cts.Token.ThrowIfCancellationRequested();
                row.SetScanning();
                var result = await _cleaner.ScanBrowserAsync(row.Browser, progress, _cts.Token);
                row.ApplyScanResult(result);
                UpdateSummaryBar();
            }

            _scanDone = true;

            // Default all enabled rows to checked
            foreach (var r in _rows)
                if (r.CheckBox.Enabled) r.CheckBox.Checked = true;

            SetStatus($"Scan complete — {_rows.Length} browser(s) detected");
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
            SetBusy(false, null);
            UpdateSummaryBar();
        }
    }

    // ── Clean ─────────────────────────────────────────────────────────────────
    private async void CleanBtn_Click(object? sender, EventArgs e)
    {
        var toClean = _rows.Where(r => r.CheckBox.Checked && r.ScanResult != null).ToArray();
        if (toClean.Length == 0) return;

        long totalBytes = toClean.Sum(r => r.ScanResult!.TotalBytes);
        bool anyRunning = toClean.Any(r => r.ScanResult!.IsRunning);

        string runNote = anyRunning
            ? "\n\n⚠ One or more browsers are currently running.\nClose them first for best results."
            : "";

        var confirm = MessageBox.Show(
            $"This will permanently delete cache files for {toClean.Length} " +
            $"browser{(toClean.Length == 1 ? "" : "s")} " +
            $"({BrowserCacheCleaner.FormatSize(totalBytes)}).\n\n" +
            "Locked or in-use files will be skipped automatically." +
            runNote + "\n\nContinue?",
            "CALIBER CLEAN — Confirm Cache Clear",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        SetBusy(true, "Cleaning…");

        var progress = new Progress<string>(SetStatus);
        try
        {
            foreach (var row in toClean)
            {
                _cts.Token.ThrowIfCancellationRequested();
                row.SetCleaning();
                var result = await _cleaner.CleanBrowserAsync(row.Browser, progress, _cts.Token);
                row.ApplyCleanResult(result);
            }

            long freed = toClean.Sum(r => r.CleanResult?.BytesFreed ?? 0);
            int  del   = toClean.Sum(r => r.CleanResult?.Deleted    ?? 0);
            int  skip  = toClean.Sum(r => r.CleanResult?.Skipped    ?? 0);
            string skipNote = skip > 0 ? $", {skip} skipped (in use)" : "";
            SetStatus($"Done — {BrowserCacheCleaner.FormatSize(freed)} freed, {del} files deleted{skipNote}");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Clean cancelled.");
        }
        finally
        {
            SetBusy(false, null);
            UpdateSummaryBar();
        }
    }

    // ── Row management ────────────────────────────────────────────────────────
    private void RebuildRows(BrowserCategory[] browsers)
    {
        if (InvokeRequired) { Invoke(() => RebuildRows(browsers)); return; }

        _rowsPanel.Controls.Clear();
        _rows = [];

        if (browsers.Length == 0) return;

        _rows = browsers.Select((b, i) => new BrowserRow(b, i)).ToArray();

        // DockStyle.Top: last-added appears at top — add in reverse for correct order
        foreach (var row in _rows.Reverse())
        {
            var panel = row.BuildPanel();
            row.CheckBox.CheckedChanged += (_, _) => UpdateSummaryBar();
            _rowsPanel.Controls.Add(panel);
        }
    }

    private void ShowEmpty(string message)
    {
        if (InvokeRequired) { Invoke(() => ShowEmpty(message)); return; }
        _rowsPanel.Controls.Clear();
        _rowsPanel.Controls.Add(new Label
        {
            Text      = message,
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 10f, FontStyle.Italic),
        });
    }

    private void UpdateSummaryBar()
    {
        if (InvokeRequired) { Invoke(UpdateSummaryBar); return; }

        var selected = _rows.Where(r => r.CheckBox.Checked).ToArray();
        long bytes   = selected.Sum(r => r.ScanResult?.TotalBytes ?? 0);
        int  items   = selected.Sum(r => r.ScanResult?.FileCount  ?? 0);

        if (!_scanDone || _rows.Length == 0)
        {
            _summary.Text      = "Click SCAN to detect installed browsers";
            _summary.ForeColor = MutedGray;
        }
        else if (selected.Length == 0)
        {
            _summary.Text      = "No browsers selected";
            _summary.ForeColor = MutedGray;
        }
        else
        {
            _summary.Text      = $"{items:N0} files selected — {BrowserCacheCleaner.FormatSize(bytes)} to free";
            _summary.ForeColor = TextColor;
        }

        _cleanBtn.Enabled = _scanDone && selected.Length > 0;
    }

    private void SetBusy(bool busy, string? msg)
    {
        if (InvokeRequired) { Invoke(() => SetBusy(busy, msg)); return; }
        _scanBtn.Enabled  = !busy;
        _scanBtn.Text     = busy ? "…" : "⟳  SCAN";
        _cleanBtn.Enabled = !busy && _scanDone;
        _progress.Visible = busy;
        _progress.MarqueeAnimationSpeed = busy ? 30 : 0;
        if (msg != null) SetStatus(msg);
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _status.Text = text;
    }

    // ── UI factories ──────────────────────────────────────────────────────────
    private static Panel MakeBar(DockStyle dock, int height, bool isTop)
    {
        var p = new Panel { Dock = dock, Height = height, BackColor = PanelColor };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor);
            int y = isTop ? p.Height - 1 : 0;
            e.Graphics.DrawLine(pen, 0, y, p.Width, y);
        };
        return p;
    }

    private static void AddColHeader(Panel parent, string text, int x, ContentAlignment align)
    {
        parent.Controls.Add(new Label
        {
            Text      = text.ToUpperInvariant(),
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize  = false,
            Width = 110, Height = 26, Left = x, Top = 0,
            TextAlign = align,
        });
    }

    private static Button MakeBtn(string text, Color backColor, int width)
    {
        var btn = new Button
        {
            Text      = text,
            Width     = width,
            Height    = 34,
            Anchor    = AnchorStyles.Left | AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = backColor.GetBrightness() < 0.5f ? Color.White : Color.FromArgb(0x0D, 0x0D, 0x0D),
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        btn.FlatAppearance.BorderSize         = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.08f);
        return btn;
    }

    // ── BrowserRow nested class ───────────────────────────────────────────────
    private sealed class BrowserRow(BrowserCategory browser, int index)
    {
        public BrowserCategory     Browser     { get; } = browser;
        public BrowserScanResult?  ScanResult  { get; private set; }
        public BrowserCleanResult? CleanResult { get; private set; }
        public CheckBox            CheckBox    { get; } = new() { Enabled = false, Checked = false };

        private Label _countLabel  = null!;
        private Label _sizeLabel   = null!;
        private Label _statusLabel = null!;

        private static readonly Color TxtColor  = Color.FromArgb(0xF0, 0xED, 0xE6);
        private static readonly Color Gold      = Color.FromArgb(0xFF, 0xCC, 0x01);
        private static readonly Color Army      = Color.FromArgb(0x8B, 0x9E, 0x6B);
        private static readonly Color Muted     = Color.FromArgb(0x66, 0x66, 0x66);
        private static readonly Color Warn      = Color.FromArgb(0xFF, 0xA5, 0x00);
        private static readonly Color RowBg     = Color.FromArgb(0x0D, 0x0D, 0x0D);
        private static readonly Color RowAlt    = Color.FromArgb(0x11, 0x11, 0x11);
        private static readonly Color Border    = Color.FromArgb(0x2A, 0x2A, 0x2A);

        private static readonly Dictionary<string, (string Icon, Color Accent)> Meta = new()
        {
            ["chrome"]  = ("🌐", Color.FromArgb(0x42, 0xA5, 0xF5)),
            ["edge"]    = ("🔵", Color.FromArgb(0x00, 0x78, 0xD4)),
            ["firefox"] = ("🦊", Color.FromArgb(0xFF, 0x77, 0x22)),
        };

        public Panel BuildPanel()
        {
            var (icon, accent) = Meta.GetValueOrDefault(Browser.Id, ("🌐", Color.FromArgb(0x4A, 0x9E, 0xFF)));

            var p = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = index % 2 == 0 ? RowBg : RowAlt,
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(Border);
                e.Graphics.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
            };

            // Checkbox
            CheckBox.Left = 14; CheckBox.Top = 18;
            CheckBox.Width = 20; CheckBox.Height = 20;
            CheckBox.BackColor = Color.Transparent;
            p.Controls.Add(CheckBox);

            // Accent badge
            p.Controls.Add(new Label
            {
                Text      = icon,
                BackColor = Color.FromArgb(30, accent.R, accent.G, accent.B),
                ForeColor = accent,
                Font      = new Font("Segoe UI Emoji", 14f),
                AutoSize  = false, Width = 34, Height = 34, Left = 40, Top = 12,
                TextAlign = ContentAlignment.MiddleCenter,
            });

            // Browser name
            p.Controls.Add(new Label
            {
                Text      = Browser.Name,
                ForeColor = TxtColor,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoSize  = false, Left = 82, Top = 9, Width = 200, Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
            });

            // Folder count hint
            p.Controls.Add(new Label
            {
                Text      = $"{Browser.CacheFolders.Length} cache folder{(Browser.CacheFolders.Length == 1 ? "" : "s")}",
                ForeColor = Muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 7.5f),
                AutoSize  = false, Left = 82, Top = 31, Width = 200, Height = 16,
                TextAlign = ContentAlignment.MiddleLeft,
            });

            // File count
            _countLabel = new Label
            {
                Text      = "—",
                ForeColor = Muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9f),
                AutoSize  = false, Left = 220, Top = 10, Width = 100, Height = 36,
                TextAlign = ContentAlignment.MiddleRight,
            };
            p.Controls.Add(_countLabel);

            // Size
            _sizeLabel = new Label
            {
                Text      = "—",
                ForeColor = Muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize  = false, Left = 315, Top = 10, Width = 100, Height = 36,
                TextAlign = ContentAlignment.MiddleRight,
            };
            p.Controls.Add(_sizeLabel);

            // Status
            _statusLabel = new Label
            {
                Text      = "—",
                ForeColor = Muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                AutoSize  = false, Left = 430, Top = 10, Width = 300, Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            p.Controls.Add(_statusLabel);

            return p;
        }

        public void SetScanning() => SafeSet(_statusLabel, "Scanning…", Muted);
        public void SetCleaning() => SafeSet(_statusLabel, "Cleaning…", Muted);

        public void ApplyScanResult(BrowserScanResult r)
        {
            ScanResult = r;

            SafeSet(_countLabel,
                r.FileCount > 0 ? $"{r.FileCount:N0} files" : "Empty",
                r.FileCount > 0 ? TxtColor : Muted);

            SafeSet(_sizeLabel,
                r.TotalBytes > 0 ? BrowserCacheCleaner.FormatSize(r.TotalBytes) : "—",
                r.TotalBytes > 0 ? Gold : Muted);

            SafeSet(_statusLabel,
                r.IsRunning ? $"⚠ Close {Browser.Name.Split(' ')[0]} for best results" : "Ready",
                r.IsRunning ? Warn : Army);

            SafeEnable(CheckBox, r.FileCount > 0 || r.TotalBytes > 0);
        }

        public void ApplyCleanResult(BrowserCleanResult r)
        {
            CleanResult = r;
            string skipNote = r.Skipped > 0 ? $", {r.Skipped} skipped" : "";
            string msg = r.Deleted == 0 && r.Skipped == 0
                ? "Nothing to clean"
                : $"Freed {BrowserCacheCleaner.FormatSize(r.BytesFreed)}, {r.Deleted:N0} files deleted{skipNote}";

            SafeSet(_statusLabel, msg, Army);
            SafeSet(_countLabel,  "0 files", Muted);
            SafeSet(_sizeLabel,   "—",       Muted);

            ScanResult = new BrowserScanResult(Browser.Id, 0, 0, false);
            SafeEnable(CheckBox, false);
        }

        private static void SafeSet(Label lbl, string text, Color color)
        {
            if (lbl is null) return;
            if (lbl.InvokeRequired) { lbl.Invoke(() => SafeSet(lbl, text, color)); return; }
            lbl.Text = text; lbl.ForeColor = color;
        }

        private static void SafeEnable(CheckBox cb, bool enabled)
        {
            if (cb.InvokeRequired) { cb.Invoke(() => SafeEnable(cb, enabled)); return; }
            cb.Enabled = enabled;
        }
    }
}
