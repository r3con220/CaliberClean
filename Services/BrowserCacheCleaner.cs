using System.Diagnostics;

namespace CaliberClean.Services;

// ── Category model ────────────────────────────────────────────────────────────
public record BrowserCategory(
    string   Id,
    string   Name,
    string   ProcessName,
    string[] CacheFolders);   // all cache dirs to scan/clean for this browser

// ── Per-browser results ───────────────────────────────────────────────────────
public record BrowserScanResult(string BrowserId, int FileCount, long TotalBytes, bool IsRunning);
public record BrowserCleanResult(string BrowserId, int Deleted, int Skipped, long BytesFreed);

public class BrowserCacheCleaner
{
    private static readonly string Local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    // ── Browser detection ─────────────────────────────────────────────────────
    // Returns only browsers that have at least one existing cache folder on disk.
    public static BrowserCategory[] DetectBrowsers()
    {
        var list = new List<BrowserCategory>();

        TryAdd(list, "chrome",  "Google Chrome",   "chrome",
            ChromiumFolders(Path.Combine(Local, "Google", "Chrome", "User Data", "Default")));

        TryAdd(list, "edge",    "Microsoft Edge",  "msedge",
            ChromiumFolders(Path.Combine(Local, "Microsoft", "Edge", "User Data", "Default")));

        TryAdd(list, "firefox", "Mozilla Firefox", "firefox",
            FirefoxFolders());

        return [.. list];
    }

    private static void TryAdd(List<BrowserCategory> list, string id, string name, string proc, string[] folders)
    {
        if (folders.Length > 0)
            list.Add(new BrowserCategory(id, name, proc, folders));
    }

    // Chrome / Edge: sum Cache, Cache2\entries, Code Cache, GPUCache under Default profile
    private static string[] ChromiumFolders(string defaultProfile)
    {
        string[] candidates =
        [
            Path.Combine(defaultProfile, "Cache"),
            Path.Combine(defaultProfile, "Cache2", "entries"),
            Path.Combine(defaultProfile, "Code Cache"),
            Path.Combine(defaultProfile, "GPUCache"),
        ];
        return candidates.Where(Directory.Exists).ToArray();
    }

    // Firefox: parse profiles.ini → locate each profile → include cache2 if present
    private static string[] FirefoxFolders()
    {
        var ini = Path.Combine(Roaming, "Mozilla", "Firefox", "profiles.ini");
        if (!File.Exists(ini)) return [];

        var folders    = new List<string>();
        string? path   = null;
        bool isRelative = true;

        void Flush()
        {
            if (path is null) return;
            var dir = isRelative
                ? Path.Combine(Roaming, "Mozilla", "Firefox", path.Replace('/', Path.DirectorySeparatorChar))
                : path;
            var cache = Path.Combine(dir, "cache2");
            if (Directory.Exists(cache)) folders.Add(cache);
            path = null; isRelative = true;
        }

        try
        {
            foreach (var raw in File.ReadAllLines(ini))
            {
                var line = raw.Trim();
                if (line.StartsWith('[')) Flush();
                else if (line.StartsWith("Path=",       StringComparison.OrdinalIgnoreCase)) path       = line[5..];
                else if (line.StartsWith("IsRelative=", StringComparison.OrdinalIgnoreCase)) isRelative = line[11..] == "1";
            }
            Flush();
        }
        catch { }

        return [.. folders];
    }

    // ── Scan ──────────────────────────────────────────────────────────────────
    public async Task<BrowserScanResult> ScanBrowserAsync(
        BrowserCategory browser,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report($"Scanning {browser.Name}…");
            bool running = Process.GetProcessesByName(browser.ProcessName).Length > 0;
            int count = 0; long bytes = 0;

            foreach (var folder in browser.CacheFolders)
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        try { var fi = new FileInfo(file); bytes += fi.Length; count++; } catch { }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            return new BrowserScanResult(browser.Id, count, bytes, running);
        }, ct);
    }

    // ── Clean ─────────────────────────────────────────────────────────────────
    // Deletes files inside each cache folder; never removes the folders themselves.
    public async Task<BrowserCleanResult> CleanBrowserAsync(
        BrowserCategory browser,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report($"Cleaning {browser.Name}…");
            int deleted = 0, skipped = 0; long freed = 0;

            foreach (var folder in browser.CacheFolders)
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(file);
                            long size = fi.Length;
                            fi.Delete();
                            freed += size; deleted++;
                        }
                        catch { skipped++; }
                    }
                    // Prune empty subdirs — keep the root cache folder itself
                    foreach (var dir in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
                                                 .OrderByDescending(d => d.Length))
                    {
                        try { Directory.Delete(dir, false); } catch { }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            return new BrowserCleanResult(browser.Id, deleted, skipped, freed);
        }, ct);
    }

    // ── Formatting ────────────────────────────────────────────────────────────
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }
}
