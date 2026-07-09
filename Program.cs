using System.Diagnostics;
using System.Text.Json;
using AutoUpdaterDotNET;
using CaliberClean.Services;

namespace CaliberClean;

static class Program
{
    // Shared with the manual "Check for Updates" button in DashboardPanel.
    public const string UpdateCheckUrl = "https://raw.githubusercontent.com/r3con220/CaliberClean/master/update.xml";

    [STAThread]
    static async Task Main(string[] args)
    {
        // Silent auto-clean mode — no UI, write log, exit.
        if (args.Contains("--auto-clean", StringComparer.OrdinalIgnoreCase))
        {
            await AutoCleanRunner.RunAsync();
            return;
        }

        // Silent, single-action mode — no UI, print JSON result, exit.
        // Used by CaliberHQ's web control panel to trigger individual features.
        var actionArg = args.FirstOrDefault(a => a.StartsWith("--action=", StringComparison.OrdinalIgnoreCase));
        if (actionArg is not null)
        {
            await RunActionAsync(actionArg["--action=".Length..]);
            return;
        }

        // Silent, non-blocking check — AutoUpdater shows its own dialog only
        // if a newer version is actually available; otherwise this is a no-op.
        AutoUpdater.Start(UpdateCheckUrl);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static async Task RunActionAsync(string actionAndParam)
    {
        // Actions that take a parameter are written "name=value" (e.g.
        // delete-large-file=C:\path\to\file) — split on the first '=' only,
        // since a file path can legitimately contain '=' later on.
        var parts = actionAndParam.Split('=', 2);
        var action = parts[0];
        var param = parts.Length > 1 ? parts[1] : null;

        switch (action.ToLowerInvariant())
        {
            case "empty-temp":
                await RunEmptyTempAsync();
                break;

            case "clear-browser-cache":
                await RunClearBrowserCacheAsync();
                break;

            case "scan-large-files":
                await RunScanLargeFilesAsync();
                break;

            case "delete-large-file":
                await RunDeleteLargeFileAsync(param);
                break;

            case "scan-duplicates":
                await RunScanDuplicatesAsync();
                break;

            case "disk-usage":
                RunDiskUsage();
                break;

            case "list-startup-items":
                RunListStartupItems();
                break;

            case "toggle-startup-item":
                RunToggleStartupItem(param);
                break;

            case "list-installed-programs":
                RunListInstalledPrograms();
                break;

            case "uninstall":
                RunUninstall(param);
                break;

            case "get-schedule":
                RunGetSchedule();
                break;

            case "set-schedule":
                RunSetSchedule(param);
                break;

            default:
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Unknown action: {action}" }));
                break;
        }
    }

    private static async Task RunEmptyTempAsync()
    {
        try
        {
            // Same category loop + service call as the "Quick Clean" button uses
            // for temp files (MainForm.RunQuickClean) — just temp categories, headless.
            var tempCleaner = new TempFileCleaner();
            long freed = 0;

            foreach (var cat in TempFileCleaner.Categories)
            {
                var result = await tempCleaner.CleanCategoryAsync(cat);
                freed += result.BytesFreed;
            }

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, freedBytes = freed }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static async Task RunClearBrowserCacheAsync()
    {
        try
        {
            // Same detect/scan/clean calls BrowserCacheCleaner exposes for the
            // "Quick Clean" button (MainForm.RunQuickClean) — headless here.
            var browserCleaner = new BrowserCacheCleaner();
            long freed = 0;
            var skipped = new List<string>();

            foreach (var browser in BrowserCacheCleaner.DetectBrowsers())
            {
                var scan = await browserCleaner.ScanBrowserAsync(browser);
                if (scan.IsRunning)
                {
                    skipped.Add(browser.Name);
                    continue;
                }

                var result = await browserCleaner.CleanBrowserAsync(browser);
                freed += result.BytesFreed;
            }

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, freedBytes = freed, skippedBrowsers = skipped }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static async Task RunScanLargeFilesAsync()
    {
        try
        {
            // Same LargeFileFinder.ScanAsync call the Large Files panel's SCAN
            // button uses (top 50 by size, system folders excluded by default).
            var finder = new LargeFileFinder();
            var (files, totalSize) = await finder.ScanAsync(@"C:\", includeSystem: false);

            var payload = files.Select(f => new
            {
                path = f.FilePath,
                name = f.FileName,
                size = f.Size,
                lastModified = f.LastModified.ToString("yyyy-MM-dd"),
            });

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, files = payload, totalSize }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static Task RunDeleteLargeFileAsync(string? filePath)
    {
        // Generic single-file-by-path delete — same operation the Large Files
        // panel's DELETE SELECTED button performs per row. Duplicate Finder's
        // per-row delete reuses this exact action too; deleting one file by
        // path is the same operation regardless of which scan surfaced it.
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "No file path given." }));
            return Task.CompletedTask;
        }

        try
        {
            var fi = new FileInfo(filePath);
            if (!fi.Exists)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "File not found." }));
                return Task.CompletedTask;
            }

            long size = fi.Length;
            fi.Delete();
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, freedBytes = size }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
        return Task.CompletedTask;
    }

    private static async Task RunScanDuplicatesAsync()
    {
        try
        {
            // Same DuplicateFileFinder.ScanAsync call the Duplicate Finder
            // panel's SCAN button uses. The panel defaults its folder box to
            // the user's Downloads folder — same default here, resolved
            // portably rather than the panel's hardcoded dev-machine path.
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var finder = new DuplicateFileFinder();
            var (groups, totalRecoverableBytes) = await finder.ScanAsync(downloads);

            var payload = groups.Select(g => new
            {
                hash = g.Hash,
                fileSize = g.FileSize,
                paths = g.Paths,
            });

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, groups = payload, totalRecoverableBytes, scannedFolder = downloads }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static void RunDiskUsage()
    {
        try
        {
            // Same DriveInfo query DashboardPanel.MakeDriveCard and the Disk
            // Usage panel use for their live used/total/free numbers.
            var payload = DiskUsageAnalyzer.GetDrives().Select(d => new
            {
                name = d.Name,
                totalBytes = d.TotalSize,
                freeBytes = d.AvailableFreeSpace,
                usedBytes = d.TotalSize - d.AvailableFreeSpace,
            });

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, drives = payload }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static void RunListStartupItems()
    {
        try
        {
            // Same StartupManager.GetEntries() call the Startup Manager
            // panel's list uses (registry Run keys + Startup folders).
            var manager = new StartupManager();
            var payload = manager.GetEntries().Select(e => new
            {
                name = e.Name,
                command = e.Command,
                location = e.Location.ToString(),
                isEnabled = e.IsEnabled,
                canToggle = e.CanToggle,
            });

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, items = payload }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static void RunToggleStartupItem(string? paramJson)
    {
        // Param is a small JSON object identifying the entry — {"name":...,
        // "location":...} — rather than a delimited string, since a startup
        // command can contain almost any character including '='.
        if (string.IsNullOrWhiteSpace(paramJson))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "No entry specified." }));
            return;
        }

        try
        {
            var target = JsonSerializer.Deserialize<ToggleStartupItemParam>(paramJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (target is null || string.IsNullOrEmpty(target.Name) || string.IsNullOrEmpty(target.Location))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Expected {\"name\":..., \"location\":...}." }));
                return;
            }

            if (!Enum.TryParse<StartupLocation>(target.Location, out var location))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Unknown location: {target.Location}" }));
                return;
            }

            var manager = new StartupManager();
            var entry = manager.GetEntries()
                .FirstOrDefault(e => e.Name == target.Name && e.Location == location);
            if (entry is null)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Startup entry not found — it may have changed since the list was loaded." }));
                return;
            }

            var (ok, error) = manager.ToggleEntry(entry);
            if (!ok)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error }));
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, isEnabled = !entry.IsEnabled }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private sealed record ToggleStartupItemParam(string? Name, string? Location);

    private static void RunListInstalledPrograms()
    {
        try
        {
            // Same UninstallManager.GetInstalledPrograms() call the Uninstall
            // Manager panel's list uses (Uninstall registry keys, both hives).
            var payload = UninstallManager.GetInstalledPrograms().Select(p => new
            {
                displayName = p.DisplayName,
                publisher = p.Publisher,
                installDate = p.InstallDate,
                estimatedSizeKb = p.EstimatedSizeKb,
                installLocation = p.InstallLocation,
                hasUninstaller = !string.IsNullOrWhiteSpace(p.UninstallString),
            });

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, programs = payload }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static void RunUninstall(string? displayName)
    {
        // This only LAUNCHES the program's own uninstaller (same quoted-path
        // parsing as UninstallManagerPanel.LaunchUninstaller) — most vendor
        // uninstallers are interactive GUIs, not silent, so there is no
        // "wait for completion" here. Success means the uninstaller window
        // was opened, not that the program is gone. If that uninstaller's
        // own manifest requires admin, Windows will show its own UAC prompt
        // for that process regardless of whether this exe is elevated.
        if (string.IsNullOrWhiteSpace(displayName))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "No program specified." }));
            return;
        }

        try
        {
            var program = UninstallManager.GetInstalledPrograms()
                .FirstOrDefault(p => p.DisplayName == displayName);
            if (program is null)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Program not found — the installed list may have changed." }));
                return;
            }
            if (string.IsNullOrWhiteSpace(program.UninstallString))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "This program has no uninstaller registered." }));
                return;
            }

            var str = program.UninstallString.Trim();
            Process? proc;
            if (str.StartsWith('"'))
            {
                var end = str.IndexOf('"', 1);
                var exe = str[1..end];
                var uargs = end + 1 < str.Length ? str[(end + 1)..].Trim() : "";
                proc = Process.Start(new ProcessStartInfo(exe, uargs) { UseShellExecute = true });
            }
            else
            {
                // cmd /c blocks until the invoked uninstaller returns, so this
                // process's exit is still gated on the real uninstaller work
                // (or its cancellation) even though it isn't the uninstaller itself.
                proc = Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{str}\"", UseShellExecute = true });
            }

            // Process.Start not throwing does NOT mean the uninstall is really
            // proceeding — verified via testing that cancelling the UAC prompt
            // on a heuristic-elevated (non-manifest) uninstaller does not throw
            // here, unlike the documented behavior for manifest-based elevation.
            // A real interactive uninstaller wizard stays open for a while; one
            // that exits almost instantly very likely means the user cancelled
            // the elevation prompt. This is a heuristic, not a certainty — a
            // genuinely fast/silent uninstaller could false-positive here.
            if (proc is not null && proc.WaitForExit(1500))
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"The uninstaller closed almost immediately (exit code {proc.ExitCode}) — it likely didn't run, possibly because the elevation prompt was cancelled.",
                }));
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(new { success = true, launched = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static void RunGetSchedule()
    {
        try
        {
            // Same status/config/last-run calls the Scheduled Clean panel
            // reads on load (ScheduleManager + AutoCleanRunner.ReadLastRun).
            var status = ScheduleManager.GetScheduleStatus();
            var config = ScheduleManager.LoadConfig();
            var nextRun = ScheduleManager.GetNextRun();
            var (lastRun, lastRunSummary) = AutoCleanRunner.ReadLastRun();

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                enabled = status.IsEnabled,
                frequency = status.Frequency?.ToString(),
                nextRun = nextRun?.ToString("o"),
                lastRun = lastRun?.ToString("o"),
                lastRunSummary,
                config = config is null ? null : new
                {
                    cleanWinTemp = config.CleanWinTemp,
                    cleanUserTemp = config.CleanUserTemp,
                    cleanPrefetch = config.CleanPrefetch,
                    cleanWuCache = config.CleanWuCache,
                    cleanChrome = config.CleanChrome,
                    cleanEdge = config.CleanEdge,
                    cleanFirefox = config.CleanFirefox,
                },
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static void RunSetSchedule(string? paramJson)
    {
        // Same SaveBtn_Click logic from the Scheduled Clean panel: build a
        // ScheduleConfig from the submitted fields, then either register the
        // scheduled task (EnableSchedule) or remove it (DisableSchedule).
        // EnableSchedule's schtasks.exe /rl HIGHEST call requires the calling
        // process itself to be elevated (confirmed via testing — Windows
        // enforces this for /rl HIGHEST regardless of the /ru account), and
        // server.js does not spawn CaliberClean.exe elevated, so this branch
        // will surface EnableSchedule's "Access denied" error in that case.
        if (string.IsNullOrWhiteSpace(paramJson))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "No schedule config given." }));
            return;
        }

        try
        {
            var input = JsonSerializer.Deserialize<SetScheduleParam>(paramJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (input is null || string.IsNullOrEmpty(input.Frequency))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Expected { enabled, frequency, cleanWinTemp, ... }." }));
                return;
            }
            if (!Enum.TryParse<CleanFrequency>(input.Frequency, ignoreCase: true, out var frequency))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Unknown frequency: {input.Frequency}" }));
                return;
            }

            var config = new ScheduleConfig(
                Enabled: input.Enabled,
                Frequency: frequency,
                CleanWinTemp: input.CleanWinTemp,
                CleanUserTemp: input.CleanUserTemp,
                CleanPrefetch: input.CleanPrefetch,
                CleanWuCache: input.CleanWuCache,
                CleanChrome: input.CleanChrome,
                CleanEdge: input.CleanEdge,
                CleanFirefox: input.CleanFirefox);

            if (config.Enabled)
            {
                var (ok, error) = ScheduleManager.EnableSchedule(config.Frequency, config);
                if (!ok)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error }));
                    return;
                }
            }
            else
            {
                var (ok, error) = ScheduleManager.DisableSchedule();
                if (!ok)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error }));
                    return;
                }
            }

            Console.WriteLine(JsonSerializer.Serialize(new { success = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private sealed record SetScheduleParam(
        bool Enabled,
        string? Frequency,
        bool CleanWinTemp,
        bool CleanUserTemp,
        bool CleanPrefetch,
        bool CleanWuCache,
        bool CleanChrome,
        bool CleanEdge,
        bool CleanFirefox);
}
