namespace CaliberClean.Services;

internal record TempFileEntry(string Path, long SizeBytes, DateTime LastModified, bool IsDirectory);

internal class TempFileCleaner
{
    private static readonly string[] ScanRoots =
    [
        Path.GetTempPath(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
    ];

    private static readonly string PrefetchPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

    public async Task<List<TempFileEntry>> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var results = new List<TempFileEntry>();

        await Task.Run(() =>
        {
            var roots = new HashSet<string>(ScanRoots, StringComparer.OrdinalIgnoreCase);

            // Also add Prefetch if accessible
            if (Directory.Exists(PrefetchPath))
                roots.Add(PrefetchPath);

            foreach (var root in roots)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(root)) continue;

                progress?.Report($"Scanning {root}…");

                try
                {
                    // Top-level files
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(file);
                            results.Add(new TempFileEntry(fi.FullName, fi.Length, fi.LastWriteTime, false));
                        }
                        catch { }
                    }

                    // Top-level subdirectories (listed as single entries — we'll recurse size)
                    foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var di = new DirectoryInfo(dir);
                            long size = GetDirectorySize(di, ct);
                            results.Add(new TempFileEntry(di.FullName, size, di.LastWriteTime, true));
                        }
                        catch { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }, ct);

        return results.OrderByDescending(e => e.SizeBytes).ToList();
    }

    public async Task<(int Deleted, int Skipped, long BytesFreed)> CleanAsync(
        IEnumerable<TempFileEntry> entries,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int deleted = 0, skipped = 0;
        long freed = 0;

        await Task.Run(() =>
        {
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Deleting {Path.GetFileName(entry.Path)}…");
                try
                {
                    if (entry.IsDirectory)
                        Directory.Delete(entry.Path, recursive: true);
                    else
                        File.Delete(entry.Path);

                    freed += entry.SizeBytes;
                    deleted++;
                }
                catch
                {
                    skipped++;
                }
            }
        }, ct);

        return (deleted, skipped, freed);
    }

    private static long GetDirectorySize(DirectoryInfo dir, CancellationToken ct)
    {
        long size = 0;
        try
        {
            foreach (var fi in dir.EnumerateFiles("*", SearchOption.AllDirectories))
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
