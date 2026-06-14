using System.Drawing.Text;
using CaliberClean.Panels;

namespace CaliberClean;

public class MainForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray = Color.FromArgb(0x66, 0x66, 0x66);

    private Panel _navRail = null!;
    private Panel _contentArea = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;
    private int _selectedNav = 0;

    private readonly (string Title, string Icon)[] _sections =
    [
        ("Temp Files", "🗑"),
        ("Browser Cache", "🌐"),
        ("Startup Manager", "⚡"),
        ("Disk Usage", "💾"),
        ("Scheduled Clean", "🕐"),
    ];

    public MainForm()
    {
        InitializeComponent();
        SelectSection(0);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "CALIBER CLEAN";
        Size = new Size(960, 640);
        MinimumSize = new Size(800, 520);
        BackColor = BgColor;
        ForeColor = TextColor;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;

        try
        {
            Icon = new Icon(Path.Combine(Application.StartupPath, "CaliberClean.ico"));
        }
        catch { }

        // --- Title bar strip ---
        var titleStrip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = PanelColor,
            Padding = new Padding(16, 0, 16, 0),
        };
        titleStrip.Paint += (s, e) =>
        {
            e.Graphics.DrawLine(new Pen(BorderColor), 0, titleStrip.Height - 1, titleStrip.Width, titleStrip.Height - 1);
        };

        _titleLabel = new Label
        {
            Text = "CALIBER CLEAN",
            ForeColor = GoldColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 0, 0),
        };
        ApplyBebasNeue(_titleLabel, 26f);
        titleStrip.Controls.Add(_titleLabel);

        var versionChip = new Label
        {
            Text = "v0.4.0",
            ForeColor = ArmyGreen,
            BackColor = Color.FromArgb(0x20, 0x20, 0x20),
            AutoSize = true,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(8, 0, 8, 0),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
        };
        titleStrip.Controls.Add(versionChip);

        // --- Nav rail ---
        _navRail = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = PanelColor,
        };
        _navRail.Paint += (s, e) =>
        {
            e.Graphics.DrawLine(new Pen(BorderColor), _navRail.Width - 1, 0, _navRail.Width - 1, _navRail.Height);
        };

        for (int i = 0; i < _sections.Length; i++)
        {
            var idx = i;
            var btn = CreateNavButton(idx);
            _navRail.Controls.Add(btn);
        }

        // --- Content area ---
        _contentArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Padding = new Padding(32),
        };

        // --- Status bar ---
        var statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = PanelColor,
        };
        statusBar.Paint += (s, e) =>
        {
            e.Graphics.DrawLine(new Pen(BorderColor), 0, 0, statusBar.Width, 0);
        };

        _statusLabel = new Label
        {
            Text = "CaliberClean v0.4.0 — Caliber Media LLC",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Right,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
        };
        statusBar.Controls.Add(_statusLabel);

        Controls.Add(_contentArea);
        Controls.Add(_navRail);
        Controls.Add(titleStrip);
        Controls.Add(statusBar);

        ResumeLayout();
    }

    private Button CreateNavButton(int idx)
    {
        var (title, icon) = _sections[idx];
        var btn = new Button
        {
            Text = $"  {icon}  {title}",
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(0xAA, 0xA8, 0xA2),
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Height = 48,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Cursor = Cursors.Hand,
            Tag = idx,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x22, 0x22, 0x22);
        btn.Click += (s, e) => SelectSection(idx);
        return btn;
    }

    private void SelectSection(int idx)
    {
        _selectedNav = idx;

        // Update nav button states
        foreach (Control ctrl in _navRail.Controls)
        {
            if (ctrl is Button btn && btn.Tag is int btnIdx)
            {
                bool active = btnIdx == idx;
                btn.ForeColor = active ? GoldColor : Color.FromArgb(0xAA, 0xA8, 0xA2);
                btn.BackColor = active ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.Transparent;
                if (active)
                    ApplyBebasNeue(btn, 11f);
                else
                    btn.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            }
        }

        LoadSection(idx);
    }

    private void LoadSection(int idx)
    {
        _contentArea.Controls.Clear();

        if (idx == 0)
        {
            _contentArea.Padding = new Padding(0);
            _contentArea.Controls.Add(new TempFilesPanel());
            return;
        }

        if (idx == 1)
        {
            _contentArea.Padding = new Padding(0);
            _contentArea.Controls.Add(new BrowserCachePanel());
            return;
        }

        if (idx == 2)
        {
            _contentArea.Padding = new Padding(0);
            _contentArea.Controls.Add(new StartupManagerPanel());
            return;
        }

        if (idx == 3)
        {
            _contentArea.Padding = new Padding(0);
            _contentArea.Controls.Add(new DiskUsagePanel());
            return;
        }

        if (idx == 4)
        {
            _contentArea.Padding = new Padding(0);
            _contentArea.Controls.Add(new ScheduledCleanPanel());
            return;
        }

        _contentArea.Padding = new Padding(32);
        ShowPlaceholder(idx);
    }

    private void ShowPlaceholder(int idx)
    {
        _contentArea.Controls.Clear();

        var (title, icon) = _sections[idx];

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            RowCount = 3,
            ColumnCount = 1,
        };
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var iconLbl = new Label
        {
            Text = icon,
            Font = new Font("Segoe UI Emoji", 40f),
            ForeColor = Color.FromArgb(0x33, 0x33, 0x33),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom,
            TextAlign = ContentAlignment.BottomCenter,
        };

        var titleLbl = new Label
        {
            Text = title.ToUpperInvariant(),
            ForeColor = TextColor,
            AutoSize = true,
            Anchor = AnchorStyles.Top,
            TextAlign = ContentAlignment.TopCenter,
        };
        ApplyBebasNeue(titleLbl, 28f);

        var comingSoon = new Label
        {
            Text = "Coming soon",
            ForeColor = MutedGray,
            AutoSize = true,
            Anchor = AnchorStyles.Top,
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 11f, FontStyle.Italic),
            Padding = new Padding(0, 8, 0, 0),
        };

        container.Controls.Add(iconLbl, 0, 0);
        container.Controls.Add(titleLbl, 0, 1);
        container.Controls.Add(comingSoon, 0, 2);

        _contentArea.Controls.Add(container);
    }

    private static void ApplyBebasNeue(Control ctrl, float size)
    {
        using var fonts = new InstalledFontCollection();
        bool hasBebas = fonts.Families.Any(f => f.Name.Equals("Bebas Neue", StringComparison.OrdinalIgnoreCase));
        ctrl.Font = hasBebas
            ? new Font("Bebas Neue", size, FontStyle.Regular)
            : new Font("Arial", size, FontStyle.Bold);
    }
}
