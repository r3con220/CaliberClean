using System.Diagnostics;
using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class LargeFilesPanel : UserControl
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

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly LargeFileFinder                  _finder = new();
    private CancellationTokenSource?                  _cts;
    private readonly List<(CheckBox Cb, LargeFileEntry Entry)> _rows = new();

    // ── Controls ──────────────────────────────────────────────────────────────
    private ComboBox    _rootCombo      = null!;
    private CheckBox    _sysToggle      = null!;
    private Button      _scanBtn        = null!;
    private Button      _deleteBtn      = null!;
    private Label       _summary        = null!;
    private Label       _status         = null!;
    private ProgressBar _progress       = null!;
    private Panel       _rowsPanel      = null!;

    public LargeFilesPanel()
    {
        Dock      = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
    }

    private void BuildUI()
    {
        SuspendLayout();

        // ── Toolbar ───────────────────────────────────────────────────────────
        var toolbar = MakeBar(DockStyle.Top, 54, isTop: true);
        toolbar.Padding = new Padding(12, 10, 12, 10);

        _scanBtn = MakeBtn("⟳  SCAN", GoldColor, 100);
        _scanBtn.Dock   = DockStyle.Right;
        _scanBtn.Click += ScanBtn_Click;

        // System folders toggle — sits right of combo
        _sysToggle = new CheckBox
        {
            Text      = "Include system folders",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Right,
            Width     = 176,
            CheckAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 8.5f),
            Checked   = false,
            Padding   = new Padding(8, 0, 8, 0),
        };

        _rootCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock          = DockStyle.Right,
            Width         = 90,
            BackColor     = Color.FromArgb(0x1E, 0x1E, 0x1E),
            ForeColor     = TextColor,
            Font          = new Font("Segoe UI", 9f),
            FlatStyle     = FlatStyle.Flat,
        };
        PopulateDrives();

        var lbl = new Label
        {
            Text      = "Scan drive:",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9f),
        };

        toolbar.Controls.Add(lbl);
        toolbar.Controls.Add(_rootCombo);
        toolbar.Controls.Add(_sysToggle);
        toolbar.Controls.Add(_scanBtn);

        // ── Progress ──────────────────────────────────────────────────────────
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

        // ── Column headers ────────────────────────────────────────────────────
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
        AddColHeader(header, "File Name",      44);
        AddColHeader(header, "Size",          310, ContentAlignment.MiddleRight);
        AddColHeader(header, "Last Modified", 400);
        AddColHeader(header, "Path",          520);

        // ── Scrollable rows ───────────────────────────────────────────────────
        _rowsPanel = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = BgColor,
        };
        ShowEmptyState("Select a drive and click SCAN to find the largest files.");

        // ── Status strip ──────────────────────────────────────────────────────
        _status = new Label
        {
            Text      = "",
            ForeColor = MutedGray,
            BackColor = PanelColor,
            Dock      = DockStyle.Bottom,
            Height    = 18,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 8f),
            Padding   = new Padding(12, 0, 0, 0),
        };

        // ── Bottom action bar ─────────────────────────────────────────────────
        var actionBar = MakeBar(DockStyle.Bottom, 52, isTop: false);
        actionBar.Padding = new Padding(12, 9, 12, 9);

        _deleteBtn = MakeBtn("DELETE SELECTED", DangerRed, 160);
        _deleteBtn.Dock    = DockStyle.Right;
        _deleteBtn.Enabled = false;
        _deleteBtn.Click  += DeleteBtn_Click;

        _summary = new Label
        {
            Text      = "No scan run",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9f),
        };

        actionBar.Controls.Add(_deleteBtn);
        actionBar.Controls.Add(_summary);

        Controls.Add(_rowsPanel);
        Controls.Add(header);
        Controls.Add(_progress);
        Controls.Add(toolbar);
        Controls.Add(_status);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    private void PopulateDrives()
    {
        _rootCombo.Items.Clear();
        // Always offer C:\ and D:\; also detect real drives
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in new[] { @"C:\", @"D:\" })
        {
            if (seen.Add(drive)) _rootCombo.Items.Add(drive);
        }
        foreach (var di in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var root = di.RootDirectory.FullName;
            if (seen.Add(root)) _rootCombo.Items.Add(root);
        }
        if (_rootCombo.Items.Count > 0)
            _rootCombo.SelectedIndex = 0;
    }

    // ── Scan ──────────────────────────────────────────────────────────────────
    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        var root = _rootCombo.SelectedItem?.ToString() ?? @"C:\";
        if (!Directory.Exists(root))
        {
            SetStatus("Selected drive not found.");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        SetBusy(true, $"Scanning {root} — this may take a moment…");
        ShowEmptyState("Scanning…");
        _rows.Clear();

        var progress = new Progress<string>(SetStatus);
        try
        {
            var (files, totalSize) = await _finder.ScanAsync(
                root, _sysToggle.Checked, progress, _cts.Token);

            if (files.Length == 0)
            {
                ShowEmptyState("No files found.");
                SetSummary("No results.");
            }
            else
            {
                RebuildRows(files);
                SetSummary($"Top {files.Length} files using {LargeFileFinder.FormatSize(totalSize)} total");
                _deleteBtn.Enabled = true;
            }
            SetStatus($"Scan complete — {files.Length} file(s) listed.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    // ── Build rows ────────────────────────────────────────────────────────────
    private void RebuildRows(LargeFileEntry[] files)
    {
        if (InvokeRequired) { Invoke(() => RebuildRows(files)); return; }

        _rowsPanel.SuspendLayout();
        _rowsPanel.Controls.Clear();
        _rows.Clear();

        // Reverse order for correct DockStyle.Top stacking
        for (int i = files.Length - 1; i >= 0; i--)
        {
            var row = BuildFileRow(files[i], i);
            _rowsPanel.Controls.Add(row);
        }

        _rowsPanel.ResumeLayout();
    }

    private Panel BuildFileRow(LargeFileEntry entry, int idx)
    {
        var row = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 50,
            BackColor = idx % 2 == 0 ? BgColor : RowAltColor,
        };
        row.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };

        var cb = new CheckBox
        {
            Left = 12, Top = 16, Width = 20, Height = 20,
            BackColor = Color.Transparent,
            Checked   = false,
        };
        cb.CheckedChanged += (_, _) => UpdateDeleteBtn();
        _rows.Add((cb, entry));
        row.Controls.Add(cb);

        // File name
        row.Controls.Add(new Label
        {
            Text      = entry.FileName,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            AutoSize  = false, Left = 38, Top = 8, Width = 260, Height = 18,
            TextAlign = ContentAlignment.MiddleLeft,
        });

        // Directory path (truncated)
        var dir = Path.GetDirectoryName(entry.FilePath) ?? "";
        row.Controls.Add(new Label
        {
            Text      = dir,
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 7.5f),
            AutoSize  = false, Left = 38, Top = 28, Width = 400, Height = 14,
            TextAlign = ContentAlignment.MiddleLeft,
        });

        // Size (gold, right-aligned at ~x=310)
        row.Controls.Add(new Label
        {
            Text      = LargeFileFinder.FormatSize(entry.Size),
            ForeColor = GoldColor,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            AutoSize  = false, Left = 280, Top = 8, Width = 90, Height = 18,
            TextAlign = ContentAlignment.MiddleRight,
        });

        // Last modified
        row.Controls.Add(new Label
        {
            Text      = entry.LastModified.ToString("yyyy-MM-dd"),
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8f),
            AutoSize  = false, Left = 382, Top = 8, Width = 90, Height = 18,
            TextAlign = ContentAlignment.MiddleLeft,
        });

        // Open in Explorer button
        var openBtn = new Button
        {
            Text      = "📂",
            Width     = 30, Height = 30,
            Anchor    = AnchorStyles.Right | AnchorStyles.Top,
            Left      = row.Width - 44, Top = 10,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0x22, 0x22, 0x22),
            ForeColor = MutedGray,
            Font      = new Font("Segoe UI Emoji", 11f),
            Cursor    = Cursors.Hand,
            Tag       = entry.FilePath,
        };
        openBtn.FlatAppearance.BorderSize = 0;
        openBtn.Click += (s, _) => OpenInExplorer((string)((Button)s!).Tag!);
        // Anchor to right side
        openBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        row.Controls.Add(openBtn);

        return row;
    }

    private void UpdateDeleteBtn()
    {
        if (InvokeRequired) { Invoke(UpdateDeleteBtn); return; }
        _deleteBtn.Enabled = _rows.Any(r => r.Cb.Checked);
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    private async void DeleteBtn_Click(object? sender, EventArgs e)
    {
        var toDelete = _rows.Where(r => r.Cb.Checked).ToArray();
        if (toDelete.Length == 0) return;

        long totalSize = toDelete.Sum(r => r.Entry.Size);
        var confirm = MessageBox.Show(
            $"Permanently delete {toDelete.Length} file{(toDelete.Length == 1 ? "" : "s")} " +
            $"({LargeFileFinder.FormatSize(totalSize)})?\n\nThis cannot be undone.\n\nContinue?",
            "CALIBER CLEAN — Confirm Delete Large Files",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        SetBusy(true, "Deleting…");
        int deleted = 0, skipped = 0;
        long freed = 0;

        await Task.Run(() =>
        {
            foreach (var (_, entry) in toDelete)
            {
                try
                {
                    var fi = new FileInfo(entry.FilePath);
                    long sz = fi.Length;
                    fi.Delete();
                    freed += sz; deleted++;
                }
                catch { skipped++; }
            }
        });

        string skipNote = skipped > 0 ? $", {skipped} skipped (in use)" : "";
        SetStatus($"Done — {deleted} file{(deleted == 1 ? "" : "s")} deleted, {LargeFileFinder.FormatSize(freed)} freed{skipNote}");
        SetBusy(false, null);

        // Visually disable rows for deleted files
        foreach (var (cb, entry) in toDelete.Where(r => !File.Exists(r.Entry.FilePath)))
            if (cb.InvokeRequired) cb.Invoke(() => cb.Enabled = false);
            else cb.Enabled = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void OpenInExplorer(string path)
    {
        try { Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
    }

    private void ShowEmptyState(string message)
    {
        if (InvokeRequired) { Invoke(() => ShowEmptyState(message)); return; }
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

    private void SetBusy(bool busy, string? msg)
    {
        if (InvokeRequired) { Invoke(() => SetBusy(busy, msg)); return; }
        _scanBtn.Enabled  = !busy;
        _scanBtn.Text     = busy ? "…" : "⟳  SCAN";
        _rootCombo.Enabled   = !busy;
        _sysToggle.Enabled   = !busy;
        _progress.Visible    = busy;
        _progress.MarqueeAnimationSpeed = busy ? 30 : 0;
        if (!busy) UpdateDeleteBtn();
        if (msg != null) SetStatus(msg);
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _status.Text = text;
    }

    private void SetSummary(string text)
    {
        if (InvokeRequired) { Invoke(() => SetSummary(text)); return; }
        _summary.Text      = text;
        _summary.ForeColor = TextColor;
    }

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

    private static void AddColHeader(Panel parent, string text, int x,
        ContentAlignment align = ContentAlignment.MiddleLeft)
    {
        parent.Controls.Add(new Label
        {
            Text      = text.ToUpperInvariant(),
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize  = false,
            Width = 160, Height = 26, Left = x, Top = 0,
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
}
