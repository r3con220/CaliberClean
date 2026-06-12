using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class DiskUsagePanel : UserControl
{
    private static readonly Color BgColor = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color DangerRed = Color.FromArgb(0xC0, 0x39, 0x2B);
    private static readonly Color BarFill = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color BarFillLarge = Color.FromArgb(0xC0, 0x39, 0x2B);
    private static readonly Color BarBg = Color.FromArgb(0x22, 0x22, 0x22);

    private readonly DiskUsageAnalyzer _analyzer = new();
    private CancellationTokenSource? _cts;

    // Navigation stack: each entry is the DiskNode being displayed
    private readonly Stack<DiskNode> _navStack = new();
    private DiskNode? _currentNode;

    private ComboBox _driveCombo = null!;
    private Button _scanBtn = null!;
    private Button _upBtn = null!;
    private Label _breadcrumb = null!;
    private Label _summaryLabel = null!;
    private Label _statusLabel = null!;
    private Panel _driveBar = null!;    // Drive free/used visual bar
    private Panel _listPanel = null!;   // Scrollable rows

    public DiskUsagePanel()
    {
        Dock = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
        PopulateDrives();
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

        _driveCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Height = 34,
            Dock = DockStyle.Left,
            BackColor = Color.FromArgb(0x22, 0x22, 0x22),
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 9.5f),
            FlatStyle = FlatStyle.Flat,
        };

        _scanBtn = MakeButton("SCAN", GoldColor, 100);
        _scanBtn.Dock = DockStyle.Left;
        _scanBtn.Click += ScanBtn_Click;

        _upBtn = MakeButton("▲ UP", Color.FromArgb(0x44, 0x44, 0x44), 80);
        _upBtn.Dock = DockStyle.Left;
        _upBtn.Enabled = false;
        _upBtn.Click += (s, e) => NavigateUp();

        _summaryLabel = new Label
        {
            Text = "Select a drive and click SCAN",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(16, 0, 0, 0),
        };

        toolbar.Controls.Add(_summaryLabel);
        toolbar.Controls.Add(_upBtn);
        toolbar.Controls.Add(_scanBtn);
        toolbar.Controls.Add(_driveCombo);

        // --- Drive space bar ---
        _driveBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(0x10, 0x10, 0x10),
            Visible = false,
        };
        _driveBar.Paint += DriveBar_Paint;

        // --- Breadcrumb bar ---
        var breadcrumbPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(0x11, 0x11, 0x11),
        };
        breadcrumbPanel.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, breadcrumbPanel.Height - 1, breadcrumbPanel.Width, breadcrumbPanel.Height - 1);

        _breadcrumb = new Label
        {
            Text = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f),
            Padding = new Padding(16, 0, 0, 0),
        };
        breadcrumbPanel.Controls.Add(_breadcrumb);

        // --- Scrollable list ---
        _listPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            AutoScroll = true,
            Padding = new Padding(0),
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

        _statusLabel = new Label
        {
            Text = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
        };
        actionBar.Controls.Add(_statusLabel);

        Controls.Add(_listPanel);
        Controls.Add(breadcrumbPanel);
        Controls.Add(_driveBar);
        Controls.Add(toolbar);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    private void PopulateDrives()
    {
        _driveCombo.Items.Clear();
        foreach (var drive in DiskUsageAnalyzer.GetDrives())
        {
            string label = string.IsNullOrEmpty(drive.VolumeLabel)
                ? drive.Name
                : $"{drive.Name} ({drive.VolumeLabel})";
            _driveCombo.Items.Add(new DriveItem(label, drive));
        }
        if (_driveCombo.Items.Count > 0)
            _driveCombo.SelectedIndex = 0;
    }

    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        if (_driveCombo.SelectedItem is not DriveItem selected) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetScanning(true);
        _navStack.Clear();
        _currentNode = null;
        ClearList();

        var progress = new Progress<string>(msg => SetStatus(msg));

        try
        {
            var root = await _analyzer.ScanDirectoryAsync(selected.Drive.RootDirectory.FullName, progress, _cts.Token);
            _currentNode = root;
            ShowDriveBar(selected.Drive);
            RenderNode(root);
            UpdateSummary(root, selected.Drive);
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

    private void RenderNode(DiskNode node)
    {
        _currentNode = node;
        ClearList();

        UpdateBreadcrumb();

        if (node.Children.Count == 0)
        {
            var empty = new Label
            {
                Text = "No subdirectories to display.",
                ForeColor = MutedGray,
                Font = new Font("Segoe UI", 10f, FontStyle.Italic),
                AutoSize = true,
                Location = new Point(24, 24),
            };
            _listPanel.Controls.Add(empty);
            return;
        }

        long maxSize = node.Children[0].SizeBytes; // already sorted descending
        int y = 0;

        foreach (var child in node.Children)
        {
            var row = BuildRow(child, maxSize, node.SizeBytes);
            row.Top = y;
            row.Left = 0;
            row.Width = _listPanel.ClientSize.Width;
            row.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _listPanel.Controls.Add(row);
            y += row.Height + 1;
        }

        _listPanel.Resize += (s, e) =>
        {
            foreach (Control ctrl in _listPanel.Controls)
                ctrl.Width = _listPanel.ClientSize.Width;
        };

        _upBtn.Enabled = _navStack.Count > 0;
    }

    private Panel BuildRow(DiskNode node, long maxSize, long parentSize)
    {
        const int rowH = 52;
        var row = new Panel
        {
            Height = rowH,
            BackColor = BgColor,
            Cursor = node.IsDirectory ? Cursors.Hand : Cursors.Default,
            Tag = node,
        };
        row.Paint += (s, e) => DrawRow(e.Graphics, row, node, maxSize, parentSize);

        if (node.IsDirectory)
        {
            row.Click += (s, e) => DrillDown(node);
            row.MouseEnter += (s, e) => { row.BackColor = Color.FromArgb(0x18, 0x18, 0x18); row.Invalidate(); };
            row.MouseLeave += (s, e) => { row.BackColor = BgColor; row.Invalidate(); };
        }

        return row;
    }

    private void DrawRow(Graphics g, Panel row, DiskNode node, long maxSize, long parentSize)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = row.Width;
        int h = row.Height;

        // Separator
        g.DrawLine(new Pen(BorderColor), 0, h - 1, w, h - 1);

        // Size bar background
        int barX = w - 260;
        int barW = 220;
        int barH = 8;
        int barY = (h - barH) / 2;
        g.FillRectangle(new SolidBrush(BarBg), barX, barY, barW, barH);

        // Size bar fill — color by % of parent
        double pct = parentSize > 0 ? (double)node.SizeBytes / parentSize : 0;
        int fillW = (int)(barW * Math.Min((double)node.SizeBytes / Math.Max(maxSize, 1), 1.0));
        var fillColor = pct > 0.3 ? DangerRed : pct > 0.1 ? GoldColor : ArmyGreen;
        if (fillW > 0)
            g.FillRectangle(new SolidBrush(fillColor), barX, barY, fillW, barH);

        // Percent label
        string pctStr = pct >= 0.01 ? $"{pct * 100:F0}%" : "<1%";
        using var mutedFont = new Font("Segoe UI", 7.5f);
        var pctSize = g.MeasureString(pctStr, mutedFont);
        g.DrawString(pctStr, mutedFont, new SolidBrush(MutedGray),
            barX + barW + 6, barY - (pctSize.Height / 2) + barH / 2);

        // Name
        string icon = node.IsDirectory ? "📁" : "📄";
        string displayName = node.Name.Length > 48 ? node.Name[..45] + "…" : node.Name;
        using var nameFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        g.DrawString($"{icon}  {displayName}", nameFont,
            new SolidBrush(node.IsDirectory ? TextColor : MutedGray),
            new RectangleF(16, 0, barX - 100, h),
            new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });

        // Size text
        string sizeStr = DiskUsageAnalyzer.FormatSize(node.SizeBytes);
        using var sizeFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        var sizeSize = g.MeasureString(sizeStr, sizeFont);
        g.DrawString(sizeStr, sizeFont,
            new SolidBrush(fillColor),
            barX - sizeSize.Width - 12, (h - sizeSize.Height) / 2);

        // Drill arrow for directories
        if (node.IsDirectory)
        {
            using var arrowFont = new Font("Segoe UI", 8f);
            g.DrawString("›", arrowFont, new SolidBrush(Color.FromArgb(0x33, 0x33, 0x33)),
                w - 14, (h - 14) / 2);
        }
    }

    private void DrillDown(DiskNode node)
    {
        if (_currentNode == null) return;
        _navStack.Push(_currentNode);
        RenderNode(node);
    }

    private void NavigateUp()
    {
        if (_navStack.Count == 0) return;
        var parent = _navStack.Pop();
        RenderNode(parent);
    }

    private void UpdateBreadcrumb()
    {
        if (_currentNode == null) { _breadcrumb.Text = ""; return; }
        var parts = _navStack.Reverse().Select(n => n.Name).ToList();
        parts.Add(_currentNode.Name);
        _breadcrumb.Text = "  ›  " + string.Join("  ›  ", parts);
    }

    private void ShowDriveBar(DriveInfo drive)
    {
        _driveBar.Tag = drive;
        _driveBar.Visible = true;
        _driveBar.Invalidate();
    }

    private void DriveBar_Paint(object? sender, PaintEventArgs e)
    {
        if (_driveBar.Tag is not DriveInfo drive) return;
        var g = e.Graphics;
        int w = _driveBar.Width - 32;
        int h = 10;
        int x = 16;
        int y = (_driveBar.Height - h) / 2;

        long total = drive.TotalSize;
        long free = drive.AvailableFreeSpace;
        long used = total - free;

        g.FillRectangle(new SolidBrush(BarBg), x, y, w, h);

        double usedPct = total > 0 ? (double)used / total : 0;
        int usedW = (int)(w * usedPct);
        var barColor = usedPct > 0.9 ? DangerRed : usedPct > 0.7 ? GoldColor : ArmyGreen;
        if (usedW > 0) g.FillRectangle(new SolidBrush(barColor), x, y, usedW, h);

        string info = $"{DiskUsageAnalyzer.FormatSize(used)} used of {DiskUsageAnalyzer.FormatSize(total)}  —  {DiskUsageAnalyzer.FormatSize(free)} free  ({usedPct * 100:F0}%)";
        using var font = new Font("Segoe UI", 8f);
        g.DrawString(info, font, new SolidBrush(MutedGray), x, y + h + 3);
    }

    private void UpdateSummary(DiskNode root, DriveInfo drive)
    {
        int topCount = root.Children.Count;
        _summaryLabel.Text = $"{topCount} top-level folders — {DiskUsageAnalyzer.FormatSize(root.SizeBytes)} scanned";
        _summaryLabel.ForeColor = TextColor;
        SetStatus("");
    }

    private void ClearList()
    {
        _listPanel.Controls.Clear();
    }

    private void SetScanning(bool scanning)
    {
        _scanBtn.Enabled = !scanning;
        _scanBtn.Text = scanning ? "SCANNING…" : "SCAN";
        _driveCombo.Enabled = !scanning;
        _upBtn.Enabled = false;
        if (!scanning) SetStatus("");
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _statusLabel.Text = text;
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

    private record DriveItem(string Label, DriveInfo Drive)
    {
        public override string ToString() => Label;
    }
}
