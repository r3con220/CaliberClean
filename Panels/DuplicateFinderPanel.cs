using System.Diagnostics;
using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class DuplicateFinderPanel : UserControl
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
    private static readonly Color GroupHdr    = Color.FromArgb(0x1A, 0x1A, 0x1A);

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly DuplicateFileFinder _finder = new();
    private CancellationTokenSource?     _cts;
    private readonly List<(CheckBox Cb, string Path)> _fileChecks = new();

    // ── Controls ──────────────────────────────────────────────────────────────
    private TextBox     _folderBox  = null!;
    private Button      _browseBtn  = null!;
    private Button      _scanBtn    = null!;
    private Button      _deleteBtn  = null!;
    private Label       _summary    = null!;
    private Label       _status     = null!;
    private ProgressBar _progress   = null!;
    private Panel       _resultsPanel = null!;

    public DuplicateFinderPanel()
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

        _browseBtn = MakeBtn("Browse…", Color.FromArgb(0x33, 0x33, 0x33), 84);
        _browseBtn.Dock   = DockStyle.Right;
        _browseBtn.Margin = new Padding(0, 0, 6, 0);
        _browseBtn.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description         = "Select folder to scan for duplicates",
                SelectedPath        = _folderBox.Text,
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                _folderBox.Text = dlg.SelectedPath;
        };

        _folderBox = new TextBox
        {
            Text      = @"C:\Users\jason\Downloads",
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E),
            ForeColor = TextColor,
            Font      = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.FixedSingle,
        };

        toolbar.Controls.Add(_folderBox);
        toolbar.Controls.Add(_browseBtn);
        toolbar.Controls.Add(_scanBtn);

        // ── Progress bar ──────────────────────────────────────────────────────
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
        AddColHeader(header, "File / Path", 44);
        AddColHeader(header, "Size", 340, ContentAlignment.MiddleRight);
        AddColHeader(header, "Action", 430);

        // ── Results area ──────────────────────────────────────────────────────
        _resultsPanel = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = BgColor,
        };
        ShowEmptyState("Click SCAN to find duplicate files.");

        // ── Bottom action bar ─────────────────────────────────────────────────
        var actionBar = MakeBar(DockStyle.Bottom, 52, isTop: false);
        actionBar.Padding = new Padding(12, 9, 12, 9);

        _deleteBtn = MakeBtn("DELETE SELECTED", Color.FromArgb(0xC0, 0x39, 0x2B), 160);
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

        _status = new Label
        {
            Text      = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Bottom,
            Height    = 18,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 8f),
            Padding   = new Padding(12, 0, 0, 0),
        };

        actionBar.Controls.Add(_deleteBtn);
        actionBar.Controls.Add(_summary);

        Controls.Add(_resultsPanel);
        Controls.Add(header);
        Controls.Add(_progress);
        Controls.Add(toolbar);
        Controls.Add(_status);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    // ── Scan ──────────────────────────────────────────────────────────────────
    private async void ScanBtn_Click(object? sender, EventArgs e)
    {
        var folder = _folderBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            SetStatus("Folder not found.");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        SetBusy(true, "Scanning…");
        _fileChecks.Clear();
        ShowEmptyState("Scanning…");

        var progress = new Progress<string>(SetStatus);
        try
        {
            var (groups, recoverable) = await _finder.ScanAsync(folder, progress, _cts.Token);

            if (groups.Length == 0)
            {
                ShowEmptyState("No duplicate files found.");
                SetSummary("No duplicates found.");
            }
            else
            {
                RebuildResults(groups);
                SetSummary($"{groups.Length} duplicate group{(groups.Length == 1 ? "" : "s")} — {DuplicateFileFinder.FormatSize(recoverable)} recoverable");
                _deleteBtn.Enabled = true;
            }
            SetStatus($"Scan complete — {groups.Length} group(s) found.");
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

    // ── Build results ─────────────────────────────────────────────────────────
    private void RebuildResults(DuplicateGroup[] groups)
    {
        if (InvokeRequired) { Invoke(() => RebuildResults(groups)); return; }

        _resultsPanel.SuspendLayout();
        _resultsPanel.Controls.Clear();
        _fileChecks.Clear();

        // DockStyle.Top stacks in reverse: add last group first
        for (int gi = groups.Length - 1; gi >= 0; gi--)
        {
            var group   = groups[gi];
            var groupPnl = BuildGroupPanel(group, gi);
            _resultsPanel.Controls.Add(groupPnl);
        }

        _resultsPanel.ResumeLayout();
    }

    private Panel BuildGroupPanel(DuplicateGroup group, int groupIdx)
    {
        int fileRowH = 46;
        int totalH   = 28 + group.Paths.Length * fileRowH;

        var pnl = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = totalH,
            BackColor = BgColor,
        };
        pnl.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(0x22, 0x22, 0x22));
            e.Graphics.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
        };

        // Group header
        var hdr = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 28,
            BackColor = GroupHdr,
        };
        hdr.Controls.Add(new Label
        {
            Text      = $"⬡  {group.Paths.Length} copies · {DuplicateFileFinder.FormatSize(group.FileSize)} each · recoverable: {DuplicateFileFinder.FormatSize(group.FileSize * (group.Paths.Length - 1))}",
            ForeColor = GoldColor,
            BackColor = Color.Transparent,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(12, 0, 0, 0),
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
        });
        pnl.Controls.Add(hdr);

        // File rows — first is "keep" (unchecked), rest are "delete" (checked)
        // DockStyle.Top in pnl: add in reverse so first path appears at top after header
        for (int fi = group.Paths.Length - 1; fi >= 0; fi--)
        {
            var path     = group.Paths[fi];
            bool isFirst = fi == 0;
            int  fIdx    = fi;

            var row = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = fileRowH,
                BackColor = (groupIdx + fi) % 2 == 0 ? BgColor : RowAltColor,
            };
            row.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderColor);
                e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            };

            var cb = new CheckBox
            {
                Checked   = !isFirst,     // keep first, mark extras for deletion
                Left      = 12,
                Top       = 14,
                Width     = 20,
                Height    = 20,
                BackColor = Color.Transparent,
            };
            _fileChecks.Add((cb, path));
            row.Controls.Add(cb);

            // Keep / Delete badge
            var badge = new Label
            {
                Text      = isFirst ? "KEEP" : "DELETE",
                ForeColor = isFirst ? ArmyGreen : Color.FromArgb(0xC0, 0x39, 0x2B),
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 7f, FontStyle.Bold),
                AutoSize  = false, Left = 36, Top = 6, Width = 46, Height = 14,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            row.Controls.Add(badge);

            // File name
            var fileName = new Label
            {
                Text      = Path.GetFileName(path),
                ForeColor = TextColor,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize  = false, Left = 36, Top = 20, Width = 280, Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            row.Controls.Add(fileName);

            // Size
            var sizeLabel = new Label
            {
                Text      = DuplicateFileFinder.FormatSize(group.FileSize),
                ForeColor = MutedGray,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8f),
                AutoSize  = false, Left = 320, Top = 10, Width = 80, Height = 26,
                TextAlign = ContentAlignment.MiddleRight,
            };
            row.Controls.Add(sizeLabel);

            // Full path
            var pathLabel = new Label
            {
                Text      = path,
                ForeColor = MutedGray,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 7.5f),
                AutoSize  = false, Left = 36, Top = 6, Width = 600, Height = 14,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            // Shift path below name — adjust top
            pathLabel.Top  = 6;
            fileName.Top   = 22;
            badge.Top      = 8;
            badge.Left     = 410;
            pathLabel.Left = 36;
            pathLabel.Width = 360;

            row.Controls.Add(pathLabel);

            // Open in Explorer button
            var openBtn = new Button
            {
                Text      = "📂",
                Width     = 28, Height = 28,
                Left      = 780, Top = 9,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x22, 0x22, 0x22),
                ForeColor = MutedGray,
                Font      = new Font("Segoe UI Emoji", 10f),
                Cursor    = Cursors.Hand,
                Tag       = path,
            };
            openBtn.FlatAppearance.BorderSize = 0;
            openBtn.Click += (s, _) => OpenInExplorer((string)((Button)s!).Tag!);
            row.Controls.Add(openBtn);

            pnl.Controls.Add(row);
        }

        return pnl;
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    private async void DeleteBtn_Click(object? sender, EventArgs e)
    {
        var toDelete = _fileChecks.Where(x => x.Cb.Checked).Select(x => x.Path).ToArray();
        if (toDelete.Length == 0) return;

        long totalSize = toDelete.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        var confirm = MessageBox.Show(
            $"Permanently delete {toDelete.Length} duplicate file{(toDelete.Length == 1 ? "" : "s")} " +
            $"({DuplicateFileFinder.FormatSize(totalSize)})?\n\nThis cannot be undone.\n\nContinue?",
            "CALIBER CLEAN — Confirm Delete Duplicates",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        SetBusy(true, "Deleting…");
        int deleted = 0, skipped = 0;
        long freed = 0;

        await Task.Run(() =>
        {
            foreach (var path in toDelete)
            {
                try
                {
                    var fi = new FileInfo(path);
                    long sz = fi.Length;
                    fi.Delete();
                    freed += sz;
                    deleted++;
                }
                catch { skipped++; }
            }
        });

        string skipNote = skipped > 0 ? $", {skipped} skipped (in use)" : "";
        SetStatus($"Done — {deleted} file{(deleted == 1 ? "" : "s")} deleted, {DuplicateFileFinder.FormatSize(freed)} freed{skipNote}");
        SetBusy(false, null);

        // Remove deleted entries from UI and disable their checkboxes
        foreach (var (cb, path) in _fileChecks.Where(x => x.Cb.Checked && !File.Exists(x.Path)).ToArray())
            cb.Enabled = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void OpenInExplorer(string path)
    {
        try { Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
    }

    private void ShowEmptyState(string message)
    {
        if (InvokeRequired) { Invoke(() => ShowEmptyState(message)); return; }
        _resultsPanel.Controls.Clear();
        _resultsPanel.Controls.Add(new Label
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
        _browseBtn.Enabled = !busy;
        _progress.Visible = busy;
        _progress.MarqueeAnimationSpeed = busy ? 30 : 0;
        if (!busy) _deleteBtn.Enabled = _fileChecks.Any(x => x.Cb.Enabled);
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
            Width = 140, Height = 26, Left = x, Top = 0,
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
