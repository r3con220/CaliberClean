using CaliberClean.Services;

namespace CaliberClean.Panels;

internal class StartupManagerPanel : UserControl
{
    private static readonly Color BgColor = Color.FromArgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromArgb(0x14, 0x14, 0x14);
    private static readonly Color TextColor = Color.FromArgb(0xF0, 0xED, 0xE6);
    private static readonly Color GoldColor = Color.FromArgb(0xFF, 0xCC, 0x01);
    private static readonly Color ArmyGreen = Color.FromArgb(0x8B, 0x9E, 0x6B);
    private static readonly Color BorderColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
    private static readonly Color MutedGray = Color.FromArgb(0x66, 0x66, 0x66);
    private static readonly Color DangerRed = Color.FromArgb(0xC0, 0x39, 0x2B);
    private static readonly Color DisabledTint = Color.FromArgb(0x44, 0x44, 0x44);

    private readonly StartupManager _manager = new();
    private List<StartupEntry> _entries = [];

    private ListView _listView = null!;
    private Button _refreshBtn = null!;
    private Button _toggleBtn = null!;
    private Button _deleteBtn = null!;
    private Label _summaryLabel = null!;
    private Label _statusLabel = null!;

    public StartupManagerPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = BgColor;
        BuildUI();
        LoadEntries();
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

        _refreshBtn = MakeButton("REFRESH", GoldColor, 100);
        _refreshBtn.Dock = DockStyle.Left;
        _refreshBtn.Click += (s, e) => LoadEntries();

        _toggleBtn = MakeButton("DISABLE", Color.FromArgb(0x44, 0x44, 0x44), 100);
        _toggleBtn.Dock = DockStyle.Left;
        _toggleBtn.Enabled = false;
        _toggleBtn.Click += ToggleBtn_Click;

        _deleteBtn = MakeButton("DELETE", DangerRed, 100);
        _deleteBtn.Dock = DockStyle.Left;
        _deleteBtn.Enabled = false;
        _deleteBtn.Click += DeleteBtn_Click;

        _summaryLabel = new Label
        {
            Text = "Loading startup entries…",
            ForeColor = MutedGray,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(16, 0, 0, 0),
        };

        toolbar.Controls.Add(_summaryLabel);
        toolbar.Controls.Add(_deleteBtn);
        toolbar.Controls.Add(_toggleBtn);
        toolbar.Controls.Add(_refreshBtn);

        // --- Info banner ---
        var infoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(0x0D, 0x1A, 0x0D),
        };
        infoPanel.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(Color.FromArgb(0x8B, 0x9E, 0x6B, 0x60)), 0, infoPanel.Height - 1, infoPanel.Width, infoPanel.Height - 1);

        var infoLabel = new Label
        {
            Text = "ℹ  Changes take effect on next Windows restart",
            ForeColor = ArmyGreen,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f),
        };
        infoPanel.Controls.Add(infoLabel);

        // --- List view ---
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BackColor = BgColor,
            ForeColor = TextColor,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9f),
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };

        _listView.Columns.Add("Status", 80);
        _listView.Columns.Add("Name", 200);
        _listView.Columns.Add("Command / Path", 380);
        _listView.Columns.Add("Location", 160);

        _listView.SelectedIndexChanged += ListView_SelectedIndexChanged;
        _listView.Resize += (s, e) =>
        {
            int fixed_ = _listView.Columns[0].Width + _listView.Columns[1].Width + _listView.Columns[3].Width + 32;
            int avail = _listView.Width - fixed_;
            if (avail > 80) _listView.Columns[2].Width = avail;
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

        Controls.Add(_listView);
        Controls.Add(infoPanel);
        Controls.Add(toolbar);
        Controls.Add(actionBar);

        ResumeLayout();
    }

    private void LoadEntries()
    {
        _refreshBtn.Enabled = false;
        _listView.Items.Clear();
        _entries = [];

        try
        {
            _entries = _manager.GetEntries();
            PopulateList();
            UpdateSummary();
            SetStatus("");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading entries: {ex.Message}");
        }
        finally
        {
            _refreshBtn.Enabled = true;
        }
    }

    private void PopulateList()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        foreach (var entry in _entries)
        {
            bool enabled = entry.IsEnabled;

            var lvi = new ListViewItem(enabled ? "● Enabled" : "○ Disabled")
            {
                Tag = entry,
                BackColor = BgColor,
                ForeColor = enabled ? ArmyGreen : DisabledTint,
                ToolTipText = entry.Command,
            };

            lvi.SubItems.Add(entry.Name);
            lvi.SubItems[1].ForeColor = enabled ? TextColor : MutedGray;

            lvi.SubItems.Add(ShortenCommand(entry.Command));
            lvi.SubItems[2].ForeColor = MutedGray;

            lvi.SubItems.Add(StartupManager.LocationLabel(entry.Location));
            lvi.SubItems[3].ForeColor = Color.FromArgb(0x55, 0x55, 0x55);

            _listView.Items.Add(lvi);
        }

        _listView.EndUpdate();
    }

    private void ListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = SelectedEntry();
        if (selected == null)
        {
            _toggleBtn.Enabled = false;
            _deleteBtn.Enabled = false;
            return;
        }

        _deleteBtn.Enabled = true;

        if (selected.CanToggle)
        {
            _toggleBtn.Enabled = true;
            _toggleBtn.Text = selected.IsEnabled ? "DISABLE" : "ENABLE";
            _toggleBtn.BackColor = selected.IsEnabled
                ? Color.FromArgb(0x44, 0x44, 0x44)
                : ArmyGreen;
        }
        else
        {
            _toggleBtn.Enabled = false;
            _toggleBtn.Text = "DISABLE";
        }
    }

    private void ToggleBtn_Click(object? sender, EventArgs e)
    {
        var entry = SelectedEntry();
        if (entry == null) return;

        var (success, error) = _manager.ToggleEntry(entry);
        if (!success)
        {
            SetStatus($"Error: {error}");
            return;
        }

        string action = entry.IsEnabled ? "disabled" : "enabled";
        SetStatus($"'{entry.Name}' {action}. Changes take effect on next restart.");
        LoadEntries();
    }

    private void DeleteBtn_Click(object? sender, EventArgs e)
    {
        var entry = SelectedEntry();
        if (entry == null) return;

        var confirm = MessageBox.Show(
            $"Remove '{entry.Name}' from startup?\n\nThis permanently removes the startup entry (not the application itself).",
            "CALIBER CLEAN — Confirm Remove",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        var (success, error) = _manager.DeleteEntry(entry);
        if (!success)
        {
            SetStatus($"Error: {error}");
            return;
        }

        SetStatus($"'{entry.Name}' removed from startup.");
        LoadEntries();
    }

    private void UpdateSummary()
    {
        int total = _entries.Count;
        int enabled = _entries.Count(e => e.IsEnabled);
        int disabled = total - enabled;
        _summaryLabel.Text = total == 0
            ? "No startup entries found."
            : $"{total} entries — {enabled} enabled, {disabled} disabled";
        _summaryLabel.ForeColor = total > 0 ? TextColor : MutedGray;
    }

    private StartupEntry? SelectedEntry()
    {
        if (_listView.SelectedItems.Count == 0) return null;
        return _listView.SelectedItems[0].Tag as StartupEntry;
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _statusLabel.Text = text;
    }

    private static string ShortenCommand(string cmd)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (cmd.StartsWith(local, StringComparison.OrdinalIgnoreCase))
            return "%LOCALAPPDATA%" + cmd[local.Length..];
        if (cmd.StartsWith(pf, StringComparison.OrdinalIgnoreCase))
            return "%PF%" + cmd[pf.Length..];
        if (cmd.StartsWith(pf86, StringComparison.OrdinalIgnoreCase))
            return "%PF86%" + cmd[pf86.Length..];
        return cmd;
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
