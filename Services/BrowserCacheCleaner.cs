namespace CaliberClean.Services;

internal record BrowserProfile(string BrowserName, string ProfileName, string CachePath, long SizeBytes);

internal class BrowserCacheCleaner
{
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    // Each entry: (browser display name, base data dir, relative cache path pattern)
    // Cache path may contain {profile} placeholder replaced per discovered profile folder
    private static readonly (string Name, string BaseDir, string CacheSubPath, bool MultiProfile)[] BrowserDefs =
    [
        ("Google Chrome",   Path.Combine(Local,   "Google", "Chrome", "User Data"),   "Cache\\Cache_Data", true),
        ("Microsoft Edge",  Path.Combine(Local,   "Microsoft", "Edge", "User Data"),  "Cache\\Cache_Data", true),
        ("Brave",           Path.Combine(Local,   "BraveSoftware", "Brave-Browser", "User Data"), "Cache\\Cache_Data", true),
        ("Opera",           Path.Combine(Roaming, "Opera Software", "Opera Stable"),  "Cache\\Cache_Data", false),
        ("Vivaldi",         Path.Combine(Local,   "Vivaldi", "User Data"),            "Cache\\Cache_Data", true),
        ("Firefox",         Path.Combine(Roaming, "Mozilla", "Firefox", "Profiles"),  "cache2\\entries",   true),
    ];

    public async Task<List<BrowserProfile>> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var results = new List<BrowserProfile>();

        await Task.Run(() =>
        {
            foreach (var (name, baseDir, cacheSub, multiProfile) in BrowserDefs)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(baseDir)) continue;

                progress?.Report($"Scanning {name}…");

                if (!multiProfile)
                {
                    // Single-profile browsers (Opera): baseDir IS the profile dir
                    var cachePath = Path.Combine(baseDir, cacheSub);
                    if (Directory.Exists(cachePath))
                    {
                        long size = GetDirSize(cachePath, ct);
                        results.Add(new BrowserProfile(name, "Default", cachePath, size));
                    }
                    continue;
                }

                // Multi-profile: enumerate profile folders inside baseDir
                try
                {
                    foreach (var profileDir in Directory.EnumerateDirectories(baseDir))
                    {
                        ct.ThrowIfCancellationRequested();
                        var dirName = Path.GetFileName(profileDir);

                        // Chrome/Edge/Brave: "Default" and "Profile 1", "Profile 2", etc.
                        // Firefox: random hash dirs, all valid
                        bool isChromiumProfile = dirName == "Default" || dirName.StartsWith("Profile ");
                        bool isFirefox = name == "Firefox";

                        if (!isChromiumProfile && !isFirefox) continue;

                        var cachePath = Path.Combine(profileDir, cacheSub);
                        if (!Directory.Exists(cachePath)) continue;

                        long size = GetDirSize(cachePath, ct);
                        if (size == 0) continue;

                        // Firefox profile names look like "abc123.default-release" — clean them up
                        string profileLabel = isFirefox
                            ? (dirName.Contains('.') ? dirName[(dirName.IndexOf('.') + 1)..] : dirName)
                            : dirName;

                        results.Add(new BrowserProfile(name, profileLabel, cachePath, size));
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }, ct);

        return results.OrderByDescending(p => p.SizeBytes).ToList();
    }

    public async Task<(int Cleaned, int Skipped, long BytesFreed)> CleanAsync(
        IEnumerable<BrowserProfile> profiles,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int cleaned = 0, skipped = 0;
        long freed = 0;

        await Task.Run(() =>
        {
            foreach (var profile in profiles)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Cleaning {profile.BrowserName} — {profile.ProfileName}…");

                long beforeSize = GetDirSize(profile.CachePath, ct);
                int filesDeleted = 0;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(profile.CachePath, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        try { File.Delete(file); filesDeleted++; }
                        catch { skipped++; }
                    }

                    // Remove empty subdirectories
                    foreach (var dir in Directory.EnumerateDirectories(profile.CachePath, "*", SearchOption.AllDirectories).Reverse())
                    {
                        try { Directory.Delete(dir); } catch { }
                    }

                    freed += beforeSize;
                    cleaned++;
                }
                catch
                {
                    skipped++;
                }
            }
        }, ct);

        return (cleaned, skipped, freed);
    }

    private static long GetDirSize(string path, CancellationToken ct)
    {
        long size = 0;
        try
        {
            foreach (var fi in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { size += fi.Length; } catch { }
            }
        }
        catch { }
        return size;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }
}
