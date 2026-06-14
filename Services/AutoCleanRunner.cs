namespace CaliberClean.Services;

public static class AutoCleanRunner
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CaliberClean", "autoclean.log");

    // ── Silent run (called from --auto-clean entry point) ─────────────────────

    public static async Task RunAsync()
    {
        var config = ScheduleManager.LoadConfig();
        if (config is null)
        {
            AppendLog("Auto-clean skipped: no schedule configuration found.");
            return;
        }

        var start   = DateTime.Now;
        var lines   = new List<string>
        {
            $"=== CaliberClean Auto-Clean {start:yyyy-MM-dd HH:mm:ss} ===",
        };

        long totalFreed   = 0;
        int  totalDeleted = 0;

        // ── Temp file categories ──────────────────────────────────────────────
        var tempCleaner = new TempFileCleaner();

        foreach (var cat in TempFileCleaner.Categories)
        {
            bool should = cat.Id switch
            {
                "win_temp"  => config.CleanWinTemp,
                "user_temp" => config.CleanUserTemp,
                "prefetch"  => config.CleanPrefetch,
                "wu_cache"  => config.CleanWuCache,
                "recycle"   => false,   // NEVER touch recycle bin in auto mode
                _           => false,
            };
            if (!should) continue;

            var scan = await tempCleaner.ScanCategoryAsync(cat);
            if (scan.FileCount == 0) continue;

            var result = await tempCleaner.CleanCategoryAsync(cat);
            totalFreed   += result.BytesFreed;
            totalDeleted += result.Deleted;

            var skipNote = result.Skipped > 0 ? $", {result.Skipped} locked skipped" : "";
            lines.Add($"  [TEMP]    {cat.Name}: {result.Deleted} files, {TempFileCleaner.FormatSize(result.BytesFreed)} freed{skipNote}");
        }

        // ── Browser cache ─────────────────────────────────────────────────────
        var browserCleaner = new BrowserCacheCleaner();
        var browsers       = BrowserCacheCleaner.DetectBrowsers();

        foreach (var browser in browsers)
        {
            bool should = browser.Id switch
            {
                "chrome"  => config.CleanChrome,
                "edge"    => config.CleanEdge,
                "firefox" => config.CleanFirefox,
                _         => false,
            };
            if (!should) continue;

            var scan = await browserCleaner.ScanBrowserAsync(browser);

            if (scan.IsRunning)
            {
                lines.Add($"  [BROWSER] {browser.Name}: SKIPPED — browser is running");
                continue;
            }

            if (scan.FileCount == 0) continue;

            var result = await browserCleaner.CleanBrowserAsync(browser);
            totalFreed   += result.BytesFreed;
            totalDeleted += result.Deleted;

            var skipNote = result.Skipped > 0 ? $", {result.Skipped} locked skipped" : "";
            lines.Add($"  [BROWSER] {browser.Name}: {result.Deleted} files, {TempFileCleaner.FormatSize(result.BytesFreed)} freed{skipNote}");
        }

        var elapsed = (DateTime.Now - start).TotalSeconds;
        lines.Add($"  TOTAL: {totalDeleted} files deleted, {TempFileCleaner.FormatSize(totalFreed)} freed in {elapsed:F1}s");
        lines.Add("");

        AppendLog(string.Join(Environment.NewLine, lines));
    }

    // ── Log reader (used by the UI panel) ────────────────────────────────────

    public static (DateTime? LastRun, string Summary) ReadLastRun()
    {
        if (!File.Exists(LogPath)) return (null, "Never run");

        try
        {
            var all = File.ReadAllLines(LogPath);

            for (int i = all.Length - 1; i >= 0; i--)
            {
                if (!all[i].StartsWith("=== CaliberClean Auto-Clean ")) continue;

                var dateStr = all[i]["=== CaliberClean Auto-Clean ".Length..].TrimEnd(' ', '=');
                DateTime.TryParse(dateStr, out var dt);

                string summary = "No data";
                for (int j = i + 1; j < all.Length && j < i + 12; j++)
                {
                    var trimmed = all[j].TrimStart();
                    if (!trimmed.StartsWith("TOTAL:")) continue;
                    summary = trimmed["TOTAL: ".Length..];
                    break;
                }

                return (dt == default ? null : dt, summary);
            }
        }
        catch { }

        return (null, "No log entries");
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private static void AppendLog(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, text + Environment.NewLine);
        }
        catch { }
    }
}
