using System.Diagnostics;
using CaliberClean.Services;

namespace CaliberClean.Panels;

public class UninstallManagerPanel : UserControl
{
    private static readonly Color BgColor      = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor   = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor    = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor    = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color BorderColor  = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray    = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color RedColor     = Color.FromArgb(0xC0, 0x39, 0x2B);

    private TextBox _searchBox = null!;
    private Button _refreshBtn = null!;
    private Label _countLabel = null!;
    private Panel _listPanel = null!;
    private Label _statusLabel = null!;

    private InstalledProgram[] _allPrograms = [];
    private InstalledProgram[] _filtered    = [];

    public UninstallManagerPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
        LoadPrograms();
    }

    private void BuildUI()
    {
        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = PanelColor,
            Padding = new Padding(16, 0, 16, 0),
        };
        toolbar.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);

        _refreshBtn = new Button
        {
            Text = "⟳  REFRESH",
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextColor,
            BackColor = Color.FromArgb(0x22, 0x22, 0x22),
            Dock = DockStyle.Right,
            Width = 110,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        _refreshBtn.FlatAppearance.BorderColor = BorderColor;
        _refreshBtn.FlatAppearance.BorderSize = 1;
        _refreshBtn.Click += (s, e) => LoadPrograms();

        _countLabel = new Label
        {
            Text = "",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Right,
            AutoSize = false,
            Width = 130,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9f),
        };

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E),
            ForeColor = TextColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 11f),
            PlaceholderText = "Search programs…",
        };
        _searchBox.TextChanged += (s, e) => ApplyFilter();

        toolbar.Controls.Add(_searchBox);
        toolbar.Controls.Add(_countLabel);
        toolbar.Controls.Add(_refreshBtn);

        // Status bar
        var statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = PanelColor,
        };
        statusBar.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, 0, statusBar.Width, 0);

        _statusLabel = new Label
        {
            Text = "Loading installed programs…",
            ForeColor = MutedGray,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Font = new Font("Segoe UI", 8.5f),
        };
        statusBar.Controls.Add(_statusLabel);

        // Scrollable list
        _listPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            AutoScroll = true,
        };

        Controls.Add(_listPanel);
        Controls.Add(toolbar);
        Controls.Add(statusBar);
    }

    private void LoadPrograms()
    {
        _refreshBtn.Enabled = false;
        _statusLabel.Text = "Scanning registry…";
        _listPanel.Controls.Clear();

        Task.Run(() =>
        {
            var programs = UninstallManager.GetInstalledPrograms();
            Invoke(() =>
            {
                _allPrograms = programs;
                _refreshBtn.Enabled = true;
                ApplyFilter();
            });
        });
    }

    private void ApplyFilter()
    {
        var query = _searchBox.Text.Trim();
        _filtered = string.IsNullOrEmpty(query)
            ? _allPrograms
            : _allPrograms.Where(p =>
                p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))
              .ToArray();

        _countLabel.Text = $"{_filtered.Length} program{(_filtered.Length == 1 ? "" : "s")}";
        _statusLabel.Text = $"{_allPrograms.Length} installed programs found";
        RenderList();
    }

    private void RenderList()
    {
        _listPanel.SuspendLayout();
        _listPanel.Controls.Clear();

        // Build rows in reverse so DockStyle.Top stacks correctly
        for (int i = _filtered.Length - 1; i >= 0; i--)
        {
            var prog = _filtered[i];
            _listPanel.Controls.Add(MakeRow(prog, i));
        }

        _listPanel.ResumeLayout();
    }

    private Panel MakeRow(InstalledProgram prog, int idx)
    {
        var row = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = idx % 2 == 0 ? BgColor : Color.FromArgb(0x11, 0x11, 0x11),
            Padding = new Padding(16, 0, 16, 0),
        };
        row.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(BorderColor), 0, row.Height - 1, row.Width, row.Height - 1);

        // Right-side buttons
        var btnPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 240,
            BackColor = Color.Transparent,
        };

        if (!string.IsNullOrWhiteSpace(prog.InstallLocation) && Directory.Exists(prog.InstallLocation))
        {
            var openBtn = MakeSmallButton("📂  OPEN FOLDER", Color.FromArgb(0x22, 0x22, 0x22), 120);
            openBtn.Dock = DockStyle.Right;
            openBtn.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", prog.InstallLocation); } catch { }
            };
            btnPanel.Controls.Add(openBtn);
        }

        if (!string.IsNullOrWhiteSpace(prog.UninstallString))
        {
            var uninstBtn = MakeSmallButton("✕  UNINSTALL", RedColor, 110);
            uninstBtn.Dock = DockStyle.Left;
            uninstBtn.Click += (s, e) => LaunchUninstaller(prog);
            btnPanel.Controls.Add(uninstBtn);
        }

        // Info labels
        var infoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
        };

        var nameLbl = new Label
        {
            Text = prog.DisplayName,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding = new Padding(0, 4, 0, 0),
        };

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prog.Publisher)) metaParts.Add(prog.Publisher);
        if (!string.IsNullOrWhiteSpace(prog.InstallDate)) metaParts.Add(prog.InstallDate);
        var sizeStr = UninstallManager.FormatSize(prog.EstimatedSizeKb);
        if (!string.IsNullOrWhiteSpace(sizeStr)) metaParts.Add(sizeStr);

        var metaLbl = new Label
        {
            Text = string.Join("   ·   ", metaParts),
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 8.5f),
        };

        infoPanel.Controls.Add(metaLbl);
        infoPanel.Controls.Add(nameLbl);

        row.Controls.Add(infoPanel);
        row.Controls.Add(btnPanel);
        return row;
    }

    private static Button MakeSmallButton(string text, Color back, int width)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = back,
            Width = width,
            Height = 30,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private static void LaunchUninstaller(InstalledProgram prog)
    {
        var result = MessageBox.Show(
            $"Launch the uninstaller for:\n\n{prog.DisplayName}\n\nThis will run the program's own uninstaller.",
            "Uninstall",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        try
        {
            var str = prog.UninstallString.Trim();
            // Handle quoted paths
            if (str.StartsWith('"'))
            {
                var end = str.IndexOf('"', 1);
                var exe = str[1..end];
                var args = end + 1 < str.Length ? str[(end + 1)..].Trim() : "";
                Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{str}\"", UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not launch uninstaller:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
