using System.Diagnostics;
using CaliberClean.Panels;

namespace CaliberClean;

/// Rebuilt from scratch (2026-08-04) to match CaliberHQ's CaliberClean modal
/// exactly — colors/spacing pulled from CaliberCommandCenter.html + caliber-themes.js,
/// not eyeballed. See Theme.cs for the palette source.
public class MainForm : Form
{
    private static readonly (string Title, NavIconKind Icon)[] Sections =
    [
        ("Dashboard", NavIconKind.Dashboard),
        ("Scheduled Clean", NavIconKind.ScheduledClean),
        ("Disk Usage", NavIconKind.DiskUsage),
        ("Startup Manager", NavIconKind.StartupManager),
        ("Uninstall Manager", NavIconKind.UninstallManager),
        ("Duplicate Finder", NavIconKind.DuplicateFinder),
        ("Large Files", NavIconKind.LargeFiles),
        ("Temp Files", NavIconKind.TempFiles),
        ("Browser Cache", NavIconKind.BrowserCache),
    ];

    private static readonly string HeaderIconPath = Path.Combine(Application.StartupPath, "Assets", "header-icon.png");
    private static readonly string InstallerFolderPath = @"C:\Projects\CaliberClean\installer\Output";

    private Palette _pal = Palette.DarkArmy;
    private bool _isDark = true;
    private int _selectedNav;

    private Panel _accentStrip = null!;
    private Panel _headerContent = null!;
    private Panel _navRail = null!;
    private Panel _contentArea = null!;
    private Panel _footer = null!;
    private Button _themeToggle = null!;
    private readonly List<NavButton> _navButtons = [];

    public MainForm()
    {
        BuildUI();
        SelectSection(0);
    }

    private void BuildUI()
    {
        SuspendLayout();
        Controls.Clear();
        _navButtons.Clear();

        Text = "CaliberClean";
        Size = new Size(1020, 760);
        MinimumSize = new Size(820, 560);
        BackColor = _pal.Surface;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;

        try { Icon = new Icon(Path.Combine(Application.StartupPath, "CaliberClean.ico")); } catch { }

        var contentArea = BuildContentArea();
        var navRail = BuildNavRail();
        var topBar = BuildTopBar();
        var footer = BuildFooter();

        Controls.Add(contentArea);
        Controls.Add(navRail);
        Controls.Add(topBar);
        Controls.Add(footer);

        ResumeLayout();
    }

    // ── Top bar: 2px army accent strip + header row ──────────────────────────

    private Panel BuildTopBar()
    {
        var topBar = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = _pal.Panel2 };

        _accentStrip = new Panel { Dock = DockStyle.Top, Height = 2, BackColor = _pal.Army };

        _headerContent = new Panel { Dock = DockStyle.Fill, BackColor = _pal.Panel2 };

        PictureBox icon = new()
        {
            Location = new Point(20, 14),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
        };
        try { icon.Image = Image.FromFile(HeaderIconPath); } catch { }

        var title = new TwoToneLabel
        {
            PartA = "CALIBER",
            ColorA = _pal.Gold,
            PartB = "CLEAN",
            ColorB = _pal.Army,
            Location = new Point(76, 20),
            Font = Fonts.Display(21f),
        };
        title.Measure();

        var subtitle = new Label
        {
            Text = "PC Cleanup Utility",
            AutoSize = true,
            Location = new Point(76, 44),
            ForeColor = _pal.Muted,
            BackColor = Color.Transparent,
            Font = Fonts.Body(12f),
        };

        _themeToggle = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = _pal.Panel2,
            ForeColor = _pal.White,
            Font = Fonts.UI(10f),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            Cursor = Cursors.Hand,
            Text = _isDark ? "\U0001F319  Dark Army" : "\u2600  Light Army",
        };
        _themeToggle.FlatAppearance.BorderColor = _pal.Border2;
        _themeToggle.FlatAppearance.BorderSize = 1;
        _themeToggle.Click += (_, _) => ToggleTheme();
        _headerContent.SizeChanged += (_, _) =>
            _themeToggle.Location = new Point(_headerContent.Width - _themeToggle.Width - 12, 12);

        _headerContent.Controls.Add(icon);
        _headerContent.Controls.Add(title);
        _headerContent.Controls.Add(subtitle);
        _headerContent.Controls.Add(_themeToggle);

        topBar.Controls.Add(_headerContent);
        topBar.Controls.Add(_accentStrip);
        return topBar;
    }

    // ── Nav rail ───────────────────────────────────────────────────────────

    private Panel BuildNavRail()
    {
        _navRail = new Panel { Dock = DockStyle.Left, Width = 198, BackColor = _pal.Panel2, Padding = new Padding(9) };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        // Added in reverse: for stacked Dock=Top children, the LAST control
        // added ends up closest to the container's true top edge — confirmed
        // by screenshot, the opposite of what the naive reading suggests.
        for (int i = Sections.Length - 1; i >= 0; i--)
        {
            var (title, icon) = Sections[i];
            var btn = new NavButton(i, icon, title, _pal) { IsActive = i == _selectedNav };
            btn.Click += (_, _) => SelectSection(btn.SectionIndex);
            _navButtons.Add(btn);
            inner.Controls.Add(btn);
        }

        _navRail.Controls.Add(inner);
        _navRail.Paint += (_, e) => e.Graphics.DrawLine(new Pen(_pal.Border2), _navRail.Width - 1, 0, _navRail.Width - 1, _navRail.Height);
        return _navRail;
    }

    // ── Footer: Close + Open Installer Folder ─────────────────────────────

    private Panel BuildFooter()
    {
        _footer = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = _pal.Panel2 };
        _footer.Paint += (_, e) => e.Graphics.DrawLine(new Pen(_pal.Border2), 0, 0, _footer.Width, 0);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 8, 14, 8),
            BackColor = Color.Transparent,
        };

        var closeBtn = MakeFooterButton("Close", primary: false);
        closeBtn.Click += (_, _) => Close();

        var openInstallerBtn = MakeFooterButton("\U0001F4C2  Open Installer Folder", primary: true);
        openInstallerBtn.Click += (_, _) => OpenInstallerFolder();
        openInstallerBtn.Margin = new Padding(8, 0, 0, 0);

        flow.Controls.Add(closeBtn);
        flow.Controls.Add(openInstallerBtn);
        _footer.Controls.Add(flow);
        return _footer;
    }

    private Button MakeFooterButton(string text, bool primary)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(12f, FontStyle.Bold),
            Padding = new Padding(16, 8, 16, 8),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Cursor = Cursors.Hand,
            BackColor = primary ? _pal.Gold : _pal.Panel2,
            ForeColor = primary ? _pal.Bg : _pal.White,
        };
        btn.FlatAppearance.BorderColor = primary ? _pal.Gold : _pal.Border2;
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }

    private void OpenInstallerFolder()
    {
        try
        {
            if (Directory.Exists(InstallerFolderPath))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{InstallerFolderPath}\"") { UseShellExecute = true });
            else
                MessageBox.Show($"Installer folder not found:\n{InstallerFolderPath}", "CaliberClean",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { }
    }

    // ── Content area / section switching ──────────────────────────────────

    private Panel BuildContentArea() =>
        _contentArea = new Panel { Dock = DockStyle.Fill, BackColor = _pal.Surface };

    private void SelectSection(int idx)
    {
        _selectedNav = idx;
        foreach (var btn in _navButtons)
        {
            btn.IsActive = btn.SectionIndex == idx;
            btn.Invalidate();
        }
        LoadSection(idx);
    }

    private void LoadSection(int idx)
    {
        _contentArea.Controls.Clear();
        Control panel = idx switch
        {
            0 => new DashboardPanel(_pal),
            1 => new ScheduledCleanPanel(_pal),
            2 => new DiskUsagePanel(_pal),
            3 => new StartupManagerPanel(_pal),
            _ => new StubPanel(Sections[idx].Title, Sections[idx].Icon, _pal),
        };
        _contentArea.Controls.Add(panel);
    }

    // ── Theme toggle ───────────────────────────────────────────────────────

    private void ToggleTheme()
    {
        _isDark = !_isDark;
        _pal = _isDark ? Palette.DarkArmy : Palette.LightArmy;
        BuildUI();
        SelectSection(_selectedNav);
    }
}
