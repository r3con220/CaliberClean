using CaliberClean.Services;

namespace CaliberClean.Panels;

/// Matches CaliberHQ's #clean-pane-browser-cache exactly — same centered single-action
/// shape as Temp Files (no CardChrome), plus one addition the HTML's runClearBrowserCache
/// has that Temp Files doesn't: a muted "Skipped (running): ..." sub-line under the result
/// pill when a detected browser was left alone because it's currently open.
public sealed class BrowserCachePanel : UserControl
{
    private readonly Palette _pal;
    private readonly Label _icon;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Button _runBtn;
    private Label? _resultPill;
    private Label? _skippedLbl;
    private readonly BrowserCacheCleaner _cleaner = new();

    public BrowserCachePanel(Palette palette)
    {
        _pal = palette;
        Dock = DockStyle.Fill;
        BackColor = _pal.Surface;

        _icon = new Label
        {
            Text = "\U0001F310", // 🌐
            AutoSize = true,
            Font = new Font("Segoe UI Emoji", 24f),
            ForeColor = _pal.White,
        };

        _title = new Label
        {
            Text = "Browser Cache",
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = _pal.White,
            Font = Fonts.UI(15f, FontStyle.Bold),
        };

        _subtitle = new Label
        {
            Text = "Chrome/Edge cache + history sweep",
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
        {
            _resultPill.Location = new Point(cx - _resultPill.Width / 2, y);
            y += _resultPill.Height + 4;
        }
        if (_skippedLbl != null)
            _skippedLbl.Location = new Point(cx - _skippedLbl.Width / 2, y);
    }

    // Same .clean-status-pill shape as TempFilesPanel/StartupManagerPanel — not shared
    // via CardChrome, pill styling is duplicated per panel throughout this codebase.
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
        if (_skippedLbl != null) { Controls.Remove(_skippedLbl); _skippedLbl = null; }

        try
        {
            // Same detect/scan/clean loop the --action=clear-browser-cache CLI handler
            // uses (Program.cs RunClearBrowserCacheAsync) — not reimplemented here.
            long freed = 0;
            var skipped = new List<string>();

            foreach (var browser in BrowserCacheCleaner.DetectBrowsers())
            {
                var scan = await _cleaner.ScanBrowserAsync(browser);
                if (scan.IsRunning)
                {
                    skipped.Add(browser.Name);
                    continue;
                }

                var result = await _cleaner.CleanBrowserAsync(browser);
                freed += result.BytesFreed;
            }

            _resultPill = NewResultPill($"✓ Freed {BrowserCacheCleaner.FormatSize(freed)}", success: true);
            if (skipped.Count > 0)
            {
                _skippedLbl = new Label
                {
                    Text = $"Skipped (running): {string.Join(", ", skipped)}",
                    AutoSize = true,
                    ForeColor = _pal.Muted,
                    Font = Fonts.Body(8.5f),
                };
                Controls.Add(_skippedLbl);
            }
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
