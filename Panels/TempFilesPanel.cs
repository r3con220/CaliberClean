using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class TempFilesPanel : UserControl
{
    // ── Theme ─────────────────────────────────────────────────────────────────
    private static readonly Color BgColor     = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor  = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color RowAltColor = Color.FromArgb(0x11, 0x11, 0x11);
    private static readonly Color TextColor   = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor   = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen   = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray   = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color DangerRed   = Color.FromArgb(0xC0, 0x39, 0x2B);
    private static readonly Color GreenDim    = Color.FromArgb(0x1A, 0x2E, 0x18);

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly TempFileCleaner       _cleaner = new();
    private readonly CategoryRow[]         _rows;
    private CancellationTokenSource?       _cts;
    private bool                           _scanDone;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private Button _scanBtn   = null!;
    private Button _cleanBtn  = null!;
    private Label  _summary   = null!;
    private Label  _status    = null!;
    private ProgressBar _progress = null!;

    public TempFilesPanel()
    {
        Dock      = DockStyle.Fill;
        BackColor = BgColor;
        _rows     = TempFileCleaner.Categories.Select((c, i) => new CategoryRow(c, i)).ToArray();
        BuildUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI construction
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        SuspendLayout();

        // ── Top toolbar ───────────────────────────────────────────────────────
        var toolbar = MakeBar(DockStyle.Top, 54, top: true);

        _scanBtn = MakeButton("⟳  SCAN", GoldColor, 110);
        _scanBtn.Click += ScanBtn_Click;

        _summary = new Label
        {
            Text      = "Click SCAN to analyse temporary files",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9.5f),
            Padding   = new Padding(14, 0, 0, 0),
        };

        toolbar.Controls.Add(_summary);   // fill (added first so Dock works)
        toolbar.Controls.Add(_scanBtn);

        // ── Column header strip ───────────────────────────────────────────────
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
        AddHeaderLabel(header, "Category",   130, ContentAlignment.MiddleLeft,  leftPad: 44);
        AddHeaderLabel(header, "Files",      220, ContentAlignment.MiddleRight, rightPad: 8);
        AddHeaderLabel(header, "Size",       310, ContentAlignment.MiddleRight, rightPad: 8);
        AddHeaderLabel(header, "Status",     420, ContentAlignment.MiddleLeft);

        // ── Category rows (scrollable) ────────────────────────────────────────
        var scroll = new Panel
        {
            Dock        = DockStyle.Fill,
            AutoScroll  = true,
            BackColor   = BgColor,
        };

        // DockStyle.Top stacks in reverse z-order — add last row first
        foreach (var row in _rows.Reverse())
        {
            var rowPanel = row.BuildPanel(BgColor, RowAltColor, BorderColor, TextColor, MutedGray, GoldColor, ArmyGreen);
            rowPanel.Tag = row;
            row.CheckBox.CheckedChanged += (s, e) => UpdateSummaryBar();
            scroll.Controls.Add(rowPanel);
        }

        // ── Bottom action bar ─────────────────────────────────────────────────
        var actionBar = MakeBar(DockStyle.Bottom, 52, top: false);

        _progress = new ProgressBar
        {
            Dock                  = DockStyle.Top,
            Height                = 3,
            Style                 = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 0,
            BackColor             = PanelColor,
            ForeColor             = GoldColor,
            Visible               = false,
        };

        _cleanBtn = MakeButton("CLEAN SELECTED", ArmyGreen, 170);
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

        Controls.Add(scroll);
        Controls.Add(header);
        Controls.Add(toolbar);
        Controls.Add(_progress);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scan
    // ─────────────────────────────────────────────────────────────────────────
    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts      = new CancellationTokenSource();
        _scanDone = false;

        SetBusy(true, "Scanning…");
        foreach (var r in _rows) r.Reset();
        UpdateSummaryBar();

        var progress = new Progress<string>(msg => SetStatus(msg));
        try
        {
            foreach (var row in _rows)
            {
                _cts.Token.ThrowIfCancellationRequested();
                row.SetScanning();
                var result = await _cleaner.ScanCategoryAsync(row.Category, progress, _cts.Token);
                row.ApplyScanResult(result);
                UpdateSummaryBar();
            }

            // Default Recycle Bin unchecked; everything else checked
            foreach (var r in _rows)
                r.CheckBox.Checked = r.Category.Id != "recycle";

            _scanDone = true;
            SetStatus($"Scan complete — {_rows.Sum(r => r.ScanResult?.FileCount ?? 0):N0} items found");
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

    // ─────────────────────────────────────────────────────────────────────────
    // Clean
    // ─────────────────────────────────────────────────────────────────────────
    private async void CleanBtn_Click(object? sender, EventArgs e)
    {
        var toClean = _rows.Where(r => r.CheckBox.Checked && r.ScanResult != null).ToArray();
        if (toClean.Length == 0) return;

        long totalBytes = toClean.Sum(r => r.ScanResult!.TotalBytes);
        var confirm = MessageBox.Show(
            $"This will permanently delete files in {toClean.Length} selected " +
            $"categor{(toClean.Length == 1 ? "y" : "ies")} ({TempFileCleaner.FormatSize(totalBytes)}).\n\n" +
            "Locked or in-use files will be skipped automatically.\n\nContinue?",
            "CALIBER CLEAN — Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetBusy(true, "Cleaning…");
        var progress = new Progress<string>(msg => SetStatus(msg));

        try
        {
            foreach (var row in toClean)
            {
                _cts.Token.ThrowIfCancellationRequested();
                row.SetCleaning();
                var result = await _cleaner.CleanCategoryAsync(row.Category, progress, _cts.Token);
                row.ApplyCleanResult(result);
            }

            long totalFreed = toClean.Sum(r => r.CleanResult?.BytesFreed ?? 0);
            int  totalDel   = toClean.Sum(r => r.CleanResult?.Deleted    ?? 0);
            int  totalSkip  = toClean.Sum(r => r.CleanResult?.Skipped    ?? 0);
            string skipped  = totalSkip > 0 ? $", {totalSkip} skipped (in use)" : "";
            SetStatus($"Done — {TempFileCleaner.FormatSize(totalFreed)} freed, {totalDel} deleted{skipped}");
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

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateSummaryBar()
    {
        if (InvokeRequired) { Invoke(UpdateSummaryBar); return; }

        var checked_ = _rows.Where(r => r.CheckBox.Checked).ToArray();
        long bytes   = checked_.Sum(r => r.ScanResult?.TotalBytes ?? 0);
        int  items   = checked_.Sum(r => r.ScanResult?.FileCount  ?? 0);

        if (!_scanDone)
        {
            _summary.Text      = "Click SCAN to analyse temporary files";
            _summary.ForeColor = MutedGray;
        }
        else if (checked_.Length == 0)
        {
            _summary.Text      = "No categories selected";
            _summary.ForeColor = MutedGray;
        }
        else
        {
            _summary.Text      = $"{items:N0} items selected — {TempFileCleaner.FormatSize(bytes)} to free";
            _summary.ForeColor = TextColor;
        }

        _cleanBtn.Enabled = _scanDone && checked_.Length > 0;
    }

    private void SetBusy(bool busy, string? statusText)
    {
        if (InvokeRequired) { Invoke(() => SetBusy(busy, statusText)); return; }
        _scanBtn.Enabled              = !busy;
        _scanBtn.Text                 = busy ? "…" : "⟳  SCAN";
        _cleanBtn.Enabled             = !busy && _scanDone;
        _progress.Visible             = busy;
        _progress.MarqueeAnimationSpeed = busy ? 30 : 0;
        if (statusText != null) SetStatus(statusText);
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _status.Text = text;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI factories
    // ─────────────────────────────────────────────────────────────────────────
    private static Panel MakeBar(DockStyle dock, int height, bool top)
    {
        var p = new Panel { Dock = dock, Height = height, BackColor = PanelColor };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor);
            int y = top ? p.Height - 1 : 0;
            e.Graphics.DrawLine(pen, 0, y, p.Width, y);
        };
        return p;
    }

    private static void AddHeaderLabel(Panel parent, string text, int x, ContentAlignment align, int leftPad = 0, int rightPad = 0)
    {
        parent.Controls.Add(new Label
        {
            Text      = text.ToUpperInvariant(),
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize  = false,
            Width     = 100,
            Height    = 26,
            Left      = x,
            Top       = 0,
            TextAlign = align,
            Padding   = new Padding(leftPad, 0, rightPad, 0),
        });
    }

    private static Button MakeButton(string text, Color backColor, int width)
    {
        bool dark = backColor.GetBrightness() < 0.5f;
        var btn = new Button
        {
            Text      = text,
            Width     = width,
            Height    = 34,
            Anchor    = AnchorStyles.Left | AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = dark ? Color.White : Color.FromArgb(0x0D, 0x0D, 0x0D),
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 8, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        btn.FlatAppearance.BorderSize         = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.08f);
        return btn;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CategoryRow — one per TempCategory
    // ─────────────────────────────────────────────────────────────────────────
    private sealed class CategoryRow(TempCategory category, int index)
    {
        public TempCategory Category   { get; } = category;
        public ScanResult?  ScanResult { get; private set; }
        public CleanResult? CleanResult { get; private set; }

        // Controls updated during scan/clean
        public CheckBox CheckBox      { get; } = new() { Enabled = false };
        private Label   _countLabel   = null!;
        private Label   _sizeLabel    = null!;
        private Label   _statusLabel  = null!;

        private static readonly Color TextColor  = Color.FromArgb(0xF0, 0xED, 0xE6);
        private static readonly Color GoldColor  = Color.FromArgb(0xFF, 0xCC, 0x01);
        private static readonly Color ArmyGreen  = Color.FromArgb(0x8B, 0x9E, 0x6B);
        private static readonly Color MutedGray  = Color.FromArgb(0x66, 0x66, 0x66);
        private static readonly Color GreenDim   = Color.FromArgb(0x1A, 0x2E, 0x18);
        private static readonly Color OrangeDim  = Color.FromArgb(0xE6, 0x7E, 0x22);

        public Panel BuildPanel(Color bg, Color altBg, Color border, Color text, Color muted, Color gold, Color army)
        {
            var p = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = index % 2 == 0 ? bg : altBg,
                Padding   = new Padding(0),
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(border);
                e.Graphics.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
            };

            // ── Checkbox ──────────────────────────────────────────────────────
            CheckBox.Left      = 14;
            CheckBox.Top       = 18;
            CheckBox.Width     = 20;
            CheckBox.Height    = 20;
            CheckBox.BackColor = Color.Transparent;
            CheckBox.ForeColor = text;
            CheckBox.Checked   = false;
            CheckBox.Enabled   = false;
            p.Controls.Add(CheckBox);

            // ── Name label ────────────────────────────────────────────────────
            var nameLabel = new Label
            {
                Text      = Category.Name,
                ForeColor = text,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoSize  = false, Left = 42, Top = 10, Width = 200, Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            p.Controls.Add(nameLabel);

            // ── Path label ────────────────────────────────────────────────────
            var pathLabel = new Label
            {
                Text      = string.IsNullOrEmpty(Category.FolderPath) ? "Shell API" : Category.FolderPath,
                ForeColor = muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 7.5f),
                AutoSize  = false, Left = 42, Top = 30, Width = 260, Height = 16,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            p.Controls.Add(pathLabel);

            // ── Count label (right-aligned at x=230) ─────────────────────────
            _countLabel = new Label
            {
                Text      = "—",
                ForeColor = muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9f),
                AutoSize  = false, Left = 220, Top = 10, Width = 100, Height = 36,
                TextAlign = ContentAlignment.MiddleRight,
            };
            p.Controls.Add(_countLabel);

            // ── Size label (right-aligned at x=330) ──────────────────────────
            _sizeLabel = new Label
            {
                Text      = "—",
                ForeColor = muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize  = false, Left = 318, Top = 10, Width = 100, Height = 36,
                TextAlign = ContentAlignment.MiddleRight,
            };
            p.Controls.Add(_sizeLabel);

            // ── Status label ──────────────────────────────────────────────────
            _statusLabel = new Label
            {
                Text      = "—",
                ForeColor = muted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                AutoSize  = false, Left = 430, Top = 10, Width = 260, Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            p.Controls.Add(_statusLabel);

            return p;
        }

        public void Reset()
        {
            ScanResult  = null;
            CleanResult = null;
            CheckBox.Enabled = false;
            CheckBox.Checked = false;
            SafeSet(_countLabel,  "—",  MutedGray);
            SafeSet(_sizeLabel,   "—",  MutedGray);
            SafeSet(_statusLabel, "—",  MutedGray);
        }

        public void SetScanning()   => SafeSet(_statusLabel, "Scanning…", MutedGray);
        public void SetCleaning()   => SafeSet(_statusLabel, "Cleaning…", MutedGray);

        public void ApplyScanResult(ScanResult r)
        {
            ScanResult = r;
            SafeSet(_countLabel, r.FileCount > 0 ? $"{r.FileCount:N0} files" : "Empty", r.FileCount > 0 ? TextColor : MutedGray);
            SafeSet(_sizeLabel,  r.TotalBytes > 0 ? TempFileCleaner.FormatSize(r.TotalBytes) : "—",
                    r.TotalBytes > 0 ? GoldColor : MutedGray);
            SafeSet(_statusLabel, "Ready", ArmyGreen);
            SafeEnable(CheckBox, r.FileCount > 0 || r.TotalBytes > 0);
        }

        public void ApplyCleanResult(CleanResult r)
        {
            CleanResult = r;
            string skipped = r.Skipped > 0 ? $", {r.Skipped} skipped" : "";
            string msg = r.Deleted == 0 && r.Skipped == 0
                ? "Nothing to clean"
                : $"Cleaned {r.Deleted:N0} files, {TempFileCleaner.FormatSize(r.BytesFreed)} freed{skipped}";
            SafeSet(_statusLabel, msg, ArmyGreen);
            SafeSet(_countLabel,  "0 files", MutedGray);
            SafeSet(_sizeLabel,   "—",       MutedGray);
            ScanResult = new ScanResult(Category.Id, 0, 0);
            SafeEnable(CheckBox, false);
        }

        // Thread-safe label updates
        private static void SafeSet(Label lbl, string text, Color color)
        {
            if (lbl is null) return;
            if (lbl.InvokeRequired) { lbl.Invoke(() => SafeSet(lbl, text, color)); return; }
            lbl.Text      = text;
            lbl.ForeColor = color;
        }

        private static void SafeEnable(CheckBox cb, bool enabled)
        {
            if (cb.InvokeRequired) { cb.Invoke(() => SafeEnable(cb, enabled)); return; }
            cb.Enabled = enabled;
        }
    }
}
