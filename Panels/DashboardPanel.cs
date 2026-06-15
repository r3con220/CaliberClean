using System.Diagnostics;
using CaliberClean.Services;

namespace CaliberClean.Panels;

public class DashboardPanel : UserControl
{
    private static readonly Color BgColor     = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor  = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor   = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor   = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen   = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray   = Color.FromArgb(0x66, 0x66, 0x66);

    public event Action? QuickCleanRequested;
    public event Action? FullScanRequested;

    public DashboardPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = BgColor;
        Padding = new Padding(32);
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };

        var inner = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
        };
        inner.ClientSizeChanged += (s, e) => inner.Width = scroll.ClientSize.Width;
        scroll.SizeChanged += (s, e) => inner.Width = scroll.ClientSize.Width;

        // Header
        inner.Controls.Add(MakeHeading("DASHBOARD", GoldColor, 28f));
        inner.Controls.Add(MakeSpacer(8));

        // --- Disk Space Section ---
        inner.Controls.Add(MakeSectionLabel("DISK SPACE"));
        inner.Controls.Add(MakeSpacer(8));

        foreach (var drive in GetDrives())
            inner.Controls.Add(MakeDriveCard(drive));

        inner.Controls.Add(MakeSpacer(24));

        // --- Last Cleanup Section ---
        inner.Controls.Add(MakeSectionLabel("LAST CLEANUP"));
        inner.Controls.Add(MakeSpacer(8));
        inner.Controls.Add(MakeLastCleanupCard());
        inner.Controls.Add(MakeSpacer(24));

        // --- Scheduled Cleaning Section ---
        inner.Controls.Add(MakeSectionLabel("SCHEDULED CLEANING"));
        inner.Controls.Add(MakeSpacer(8));
        inner.Controls.Add(MakeScheduleCard());
        inner.Controls.Add(MakeSpacer(24));

        // --- Quick Actions Section ---
        inner.Controls.Add(MakeSectionLabel("QUICK ACTIONS"));
        inner.Controls.Add(MakeSpacer(8));
        inner.Controls.Add(MakeActionsCard());
        inner.Controls.Add(MakeSpacer(24));

        // --- About Section ---
        inner.Controls.Add(MakeSectionLabel("ABOUT"));
        inner.Controls.Add(MakeSpacer(8));
        inner.Controls.Add(MakeAboutCard());

        scroll.Controls.Add(inner);
        Controls.Add(scroll);
    }

    // ---------- Cards ----------

    private Panel MakeDriveCard(DriveInfo drive)
    {
        long used = drive.TotalSize - drive.AvailableFreeSpace;
        double pct = drive.TotalSize > 0 ? (double)used / drive.TotalSize : 0;

        var card = MakeCard(80);

        var nameLbl = new Label
        {
            Text = $"{drive.Name}  ({drive.DriveFormat})",
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 10),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        };

        var sizeLbl = new Label
        {
            Text = $"{FormatBytes(used)} used of {FormatBytes(drive.TotalSize)}  —  {FormatBytes(drive.AvailableFreeSpace)} free",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 30),
            Font = new Font("Segoe UI", 8.5f),
        };

        var barBg = new Panel
        {
            BackColor = Color.FromArgb(0x22, 0x22, 0x22),
            Height = 8,
            Left = 16,
            Top = 52,
        };
        card.SizeChanged += (s, e) => barBg.Width = card.Width - 32;
        barBg.Width = 400;

        var pctColor = pct > 0.9 ? Color.FromArgb(0xC0, 0x39, 0x2B)
                     : pct > 0.7 ? Color.FromArgb(0xE6, 0x7E, 0x22)
                     : ArmyGreen;

        var barFill = new Panel
        {
            BackColor = pctColor,
            Height = 8,
            Left = 0,
            Top = 0,
        };
        barBg.SizeChanged += (s, e) => barFill.Width = (int)(barBg.Width * pct);
        barFill.Width = (int)(400 * pct);

        barBg.Controls.Add(barFill);
        card.Controls.Add(nameLbl);
        card.Controls.Add(sizeLbl);
        card.Controls.Add(barBg);
        return card;
    }

    private Panel MakeLastCleanupCard()
    {
        var card = MakeCard(60);

        var history = CleanHistory.Load();
        var lastDate = history.LastCleanDate;
        var lastFreed = history.LastCleanFreedBytes;

        string dateText = lastDate == DateTime.MinValue
            ? "No cleanup recorded yet"
            : $"Last cleaned: {lastDate:yyyy-MM-dd HH:mm}";

        string freedText = lastFreed > 0
            ? $"Freed: {FormatBytes(lastFreed)}"
            : "";

        var dateLbl = new Label
        {
            Text = dateText,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 12),
            Font = new Font("Segoe UI", 10f),
        };

        var freedLbl = new Label
        {
            Text = freedText,
            ForeColor = ArmyGreen,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 34),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };

        card.Controls.Add(dateLbl);
        card.Controls.Add(freedLbl);
        return card;
    }

    private Panel MakeScheduleCard()
    {
        var card = MakeCard(56);

        var sched = ScheduleManager.GetNextRun();
        string schedText = sched == null
            ? "Scheduled cleaning is not configured"
            : $"Next scheduled run: {sched.Value:yyyy-MM-dd HH:mm}";

        var lbl = new Label
        {
            Text = schedText,
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 16),
            Font = new Font("Segoe UI", 10f),
        };

        card.Controls.Add(lbl);
        return card;
    }

    private Panel MakeActionsCard()
    {
        var card = MakeCard(64);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 14, 16, 14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var quickBtn = new Button
        {
            Text = "⚡  QUICK CLEAN",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Black,
            BackColor = GoldColor,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        quickBtn.FlatAppearance.BorderSize = 0;
        quickBtn.Click += (s, e) => QuickCleanRequested?.Invoke();

        var fullBtn = new Button
        {
            Text = "🔍  FULL SCAN",
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            BackColor = Color.FromArgb(0x22, 0x22, 0x22),
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        fullBtn.FlatAppearance.BorderColor = BorderColor;
        fullBtn.FlatAppearance.BorderSize = 1;
        fullBtn.Click += (s, e) => FullScanRequested?.Invoke();

        table.Controls.Add(quickBtn, 0, 0);
        table.Controls.Add(fullBtn, 1, 0);
        card.Controls.Add(table);
        return card;
    }

    private Panel MakeAboutCard()
    {
        var card = MakeCard(72);

        var nameLbl = new Label
        {
            Text = "CaliberClean v0.6.0 — Built by Caliber Media LLC",
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(16, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        };
        card.SizeChanged += (s, e) => nameLbl.Width = card.Width - 32;

        var linkLbl = new LinkLabel
        {
            Text = "calibervoice.com",
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(16, 36),
            Font = new Font("Segoe UI", 9f),
            LinkColor = GoldColor,
            ActiveLinkColor = Color.White,
        };
        linkLbl.LinkClicked += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("https://calibervoice.com") { UseShellExecute = true }); } catch { }
        };

        card.Controls.Add(nameLbl);
        card.Controls.Add(linkLbl);
        return card;
    }

    // ---------- Helpers ----------

    private static Panel MakeCard(int height)
    {
        return new Panel
        {
            BackColor = PanelColor,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Top,
            Height = height,
            Margin = new Padding(0, 0, 0, 8),
        };
    }

    private static Label MakeHeading(string text, Color color, float size)
    {
        var lbl = new Label
        {
            Text = text,
            ForeColor = color,
            BackColor = Color.Transparent,
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4),
        };
        return lbl;
    }

    private static Label MakeSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 2),
        };
    }

    private static Panel MakeSpacer(int height)
    {
        return new Panel
        {
            Height = height,
            Dock = DockStyle.Top,
            BackColor = Color.Transparent,
        };
    }

    private static DriveInfo[] GetDrives()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .ToList();

        // Always ensure C: and D: are included if present
        var letters = drives.Select(d => d.Name.ToUpperInvariant()).ToHashSet();
        foreach (var letter in new[] { @"C:\", @"D:\" })
        {
            if (!letters.Contains(letter.ToUpperInvariant()))
            {
                try
                {
                    var di = new DriveInfo(letter);
                    if (di.IsReady) drives.Insert(0, di);
                }
                catch { }
            }
        }

        return drives.OrderBy(d => d.Name).ToArray();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F0} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }
}
