using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class TempFilesPanel : UserControl
{
    private static readonly Color BgColor = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color DangerRed = Color.FromArgb(0xC0, 0x39, 0x2B);

    private readonly TempFileCleaner _cleaner = new();
    private List<TempFileEntry> _entries = [];
    private CancellationTokenSource? _cts;

    private ListView _listView = null!;
    private Button _scanBtn = null!;
    private Button _cleanBtn = null!;
    private Button _selectAllBtn = null!;
    private Label _summaryLabel = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;

    public TempFilesPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
    }

    private void BuildUI()
    {
        SuspendLayout();

        // --- Top toolbar ---
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
        _selectAllBtn.Margin = new Padding(8, 0, 0, 0);
        _selectAllBtn.Enabled = false;
        _selectAllBtn.Click += (s, e) => ToggleSelectAll();

        _summaryLabel = new Label
        {
            Text = "Click SCAN to find temporary files",
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

        _listView.Columns.Add("Name", 380);
        _listView.Columns.Add("Size", 90, HorizontalAlignment.Right);
        _listView.Columns.Add("Modified", 140);
        _listView.Columns.Add("Type", 80);
        _listView.ItemChecked += (s, e) => UpdateCleanButton();

        StyleListViewHeader();

        // --- Bottom action bar ---
        var actionBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = PanelColor,
            Padding = new Padding(16, 0, 16, 0),
        };
        actionBar.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, 0, actionBar.Width, 0);

        _cleanBtn = MakeButton("CLEAN SELECTED", DangerRed, 160);
        _cleanBtn.Dock = DockStyle.Right;
        _cleanBtn.Enabled = false;
        _cleanBtn.Click += CleanBtn_Click;

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 0,
            BackColor = PanelColor,
            ForeColor = GoldColor,
            Height = 4,
            Visible = false,
        };

        _statusLabel = new Label
        {
            Text = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
            Padding = new Padding(0, 0, 12, 0),
        };

        actionBar.Controls.Add(_cleanBtn);
        actionBar.Controls.Add(_statusLabel);

        Controls.Add(_listView);
        Controls.Add(toolbar);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    private void StyleListViewHeader()
    {
        _listView.HandleCreated += (s, e) =>
        {
            // Native header color via owner-draw would require P/Invoke;
            // instead just ensure consistent column widths on resize
        };
        _listView.Resize += (s, e) =>
        {
            int available = _listView.Width - _listView.Columns[1].Width - _listView.Columns[2].Width - _listView.Columns[3].Width - 32;
            if (available > 100) _listView.Columns[0].Width = available;
        };
    }

    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetScanning(true);
        _listView.Items.Clear();
        _entries = [];

        var progress = new Progress<string>(msg => SetStatus(msg));

        try
        {
            _entries = await _cleaner.ScanAsync(progress, _cts.Token);
            PopulateList(_entries);
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
        var toDelete = _listView.CheckedItems
            .Cast<ListViewItem>()
            .Select(lvi => lvi.Tag as TempFileEntry)
            .Where(t => t != null)
            .Cast<TempFileEntry>()
            .ToList();

        if (toDelete.Count == 0) return;

        var confirm = MessageBox.Show(
            $"Delete {toDelete.Count} item(s)?\nThis cannot be undone.",
            "CALIBER CLEAN — Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetCleaning(true);

        var progress = new Progress<string>(msg => SetStatus(msg));

        try
        {
            var (deleted, skipped, freed) = await _cleaner.CleanAsync(toDelete, progress, _cts.Token);

            // Remove deleted items from list
            var remaining = _listView.Items.Cast<ListViewItem>()
                .Where(lvi => lvi.Tag is TempFileEntry entry && !toDelete.Contains(entry))
                .ToList();

            _listView.Items.Clear();
            foreach (var item in remaining)
                _listView.Items.Add(item);

            _entries = remaining
                .Select(lvi => lvi.Tag as TempFileEntry)
                .Where(t => t != null)
                .Cast<TempFileEntry>()
                .ToList();

            string skippedNote = skipped > 0 ? $" ({skipped} locked/skipped)" : "";
            SetStatus($"Freed {TempFileCleaner.FormatSize(freed)} — {deleted} deleted{skippedNote}");
            UpdateSummary();
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

    private void PopulateList(List<TempFileEntry> entries)
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry.Path);
            if (string.IsNullOrEmpty(name)) name = entry.Path;

            var lvi = new ListViewItem(name)
            {
                Tag = entry,
                ForeColor = entry.IsDirectory ? ArmyGreen : TextColor,
                BackColor = BgColor,
                ToolTipText = entry.Path,
            };
            lvi.SubItems.Add(TempFileCleaner.FormatSize(entry.SizeBytes));
            lvi.SubItems.Add(entry.LastModified.ToString("yyyy-MM-dd HH:mm"));
            lvi.SubItems.Add(entry.IsDirectory ? "Folder" : "File");
            _listView.Items.Add(lvi);
        }

        _listView.EndUpdate();
        _selectAllBtn.Enabled = entries.Count > 0;
    }

    private void UpdateSummary()
    {
        long total = _entries.Sum(e => e.SizeBytes);
        int count = _entries.Count;
        _summaryLabel.Text = count == 0
            ? "No temporary files found."
            : $"{count} items found — {TempFileCleaner.FormatSize(total)} recoverable";
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
                .Sum(lvi => (lvi.Tag as TempFileEntry)?.SizeBytes ?? 0);
            _cleanBtn.Text = $"CLEAN {checkedCount} ITEMS ({TempFileCleaner.FormatSize(checkedSize)})";
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
