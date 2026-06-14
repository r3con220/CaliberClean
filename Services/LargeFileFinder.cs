namespace CaliberClean.Services;

public record LargeFileEntry(string FilePath, string FileName, long Size, DateTime LastModified);

public class LargeFileFinder
{
    private static readonly string[] DefaultSystemFolders =
    [
        @"C:\Windows",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\ProgramData",
    ];

    public async Task<(LargeFileEntry[] Files, long TotalSize)> ScanAsync(
        string root,
        bool includeSystem,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report($"Scanning {root}…");

            var results = new List<LargeFileEntry>();
            int scanned = 0;

            void Walk(string dir)
            {
                ct.ThrowIfCancellationRequested();

                if (!includeSystem && IsSystemFolder(dir))
                    return;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(file);
                            results.Add(new LargeFileEntry(file, fi.Name, fi.Length, fi.LastWriteTime));
                        }
                        catch { }
                        scanned++;
                        if (scanned % 500 == 0)
                            progress?.Report($"Scanned {scanned:N0} files…");
                    }
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        try { Walk(sub); }
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            Walk(root);

            var top = results
                .OrderByDescending(f => f.Size)
                .Take(50)
                .ToArray();

            long total = top.Sum(f => f.Size);
            return (top, total);
        }, ct);
    }

    private static bool IsSystemFolder(string dir)
    {
        foreach (var sf in DefaultSystemFolders)
            if (dir.StartsWith(sf, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F2} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }
}
