using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-temp exactly — a completely different shape from
/// every panel built so far (Uninstall Manager / Duplicate Finder / Large Files all use
/// the header-card + list-card pattern; this one doesn't use CardChrome at all). It's a
/// centered single-action feature block: icon, title, subtitle, one "Run Now" button,
/// and a result .clean-status-pill below it — checked the HTML rather than assuming it'd
/// match the list-panel shape of everything built before it.
public sealed class TempFilesPanel : UserControl
{
    private readonly Palette _pal;
    private readonly Label _icon;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Button _runBtn;
    private Label? _resultPill;
    private readonly TempFileCleaner _cleaner = new();

    public TempFilesPanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;

        _icon = new Label
        {
            Text = "\U0001F4C1", // 📁
            AutoSize = true,
            Font = new Font("Segoe UI Emoji", 24f),
            ForeColor = _pal.White,
        };

        _title = new Label
        {
            Text = "Temp Files",
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = _pal.White,
            Font = Fonts.UI(15f, FontStyle.Bold),
        };

        _subtitle = new Label
        {
            Text = "Clear Windows/app temp directories",
            AutoSize = true,
            ForeColor = _pal.Muted,
            Font = Fonts.Body(12f),
        };

        _runBtn = new Button
        {
            Text = "Run Now",
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            Font = Fonts.UI(12f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            BackColor = _pal.Gold,
            ForeColor = _pal.Bg,
        };
        _runBtn.FlatAppearance.BorderColor = _pal.Gold;
        _runBtn.FlatAppearance.BorderSize = 1;
        _runBtn.Click += async (_, _) => await RunAsync();

        Controls.Add(_icon);
        Controls.Add(_title);
        Controls.Add(_subtitle);
        Controls.Add(_runBtn);
        Resize += (_, _) => LayoutCentered();
        LayoutCentered();
    }

    private void LayoutCentered()
    {
        int cx = Width / 2;
        int y = 40;
        _icon.Location = new Point(cx - _icon.Width / 2, y); y += _icon.Height + 6;
        _title.Location = new Point(cx - _title.Width / 2, y); y += _title.Height + 4;
        _subtitle.Location = new Point(cx - _subtitle.Width / 2, y); y += _subtitle.Height + 14;
        _runBtn.Location = new Point(cx - _runBtn.Width / 2, y); y += _runBtn.Height + 10;
        if (_resultPill != null)
            _resultPill.Location = new Point(cx - _resultPill.Width / 2, y);
    }

    // ── .clean-status-pill — same shape as StartupManagerPanel's NewPillLabel, not
    // shared via CardChrome (pill styling isn't centralized there either). ──────────
    private Label NewResultPill(string text, bool success)
    {
        Color c = success ? _pal.Green : _pal.Red;
        var pill = new Label
        {
            Text = text,
            UseMnemonic = false,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            ForeColor = c,
            BackColor = success ? Palette.Blend(_pal.Green, _pal.Panel, 0.1) : Color.Transparent,
            Font = Fonts.UI(9f, FontStyle.Bold),
        };
        pill.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(c), 0, 0, pill.Width - 1, pill.Height - 1);
        return pill;
    }

    private async Task RunAsync()
    {
        _runBtn.Enabled = false;
        _runBtn.Text = "Running…";
        if (_resultPill != null) { Controls.Remove(_resultPill); _resultPill = null; }

        try
        {
            // Same category loop the --action=empty-temp CLI handler uses
            // (Program.cs RunEmptyTempAsync) — not reimplemented here.
            long freed = 0;
            foreach (var cat in TempFileCleaner.Categories)
            {
                var result = await _cleaner.CleanCategoryAsync(cat);
                freed += result.BytesFreed;
            }
            _resultPill = NewResultPill($"✓ Freed {TempFileCleaner.FormatSize(freed)}", success: true);
        }
        catch (Exception ex)
        {
            _resultPill = NewResultPill($"✗ {ex.Message}", success: false);
        }
        finally
        {
            _runBtn.Enabled = true;
            _runBtn.Text = "Run Now";
        }

        Controls.Add(_resultPill);
        LayoutCentered();
    }
}
