namespace CaliberClean.Services;

internal class DiskNode
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; init; }
    public List<DiskNode> Children { get; } = [];
}

internal class DiskUsageAnalyzer
{
    public async Task<DiskNode> ScanDirectoryAsync(
        string rootPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => ScanDirectory(rootPath, depth: 0, maxDepth: 2, progress, ct), ct);
    }

    private static DiskNode ScanDirectory(
        string path, int depth, int maxDepth,
        IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var node = new DiskNode
        {
            Path = path,
            Name = System.IO.Path.GetFileName(path) is { Length: > 0 } n ? n : path,
            IsDirectory = true,
        };

        if (depth == 0)
            progress?.Report($"Scanning {path}…");

        try
        {
            // Count direct files
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    node.SizeBytes += fi.Length;
                }
                catch { }
            }

            // Recurse into subdirectories
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    DiskNode child;
                    if (depth < maxDepth)
                    {
                        if (depth == 0) progress?.Report($"Scanning {dir}…");
                        child = ScanDirectory(dir, depth + 1, maxDepth, progress, ct);
                    }
                    else
                    {
                        // At max depth just tally the size without building children
                        child = new DiskNode
                        {
                            Path = dir,
                            Name = System.IO.Path.GetFileName(dir),
                            IsDirectory = true,
                            SizeBytes = GetDirectorySizeFlat(dir, ct),
                        };
                    }
                    node.SizeBytes += child.SizeBytes;
                    node.Children.Add(child);
                }
                catch { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        node.Children.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
        return node;
    }

    private static long GetDirectorySizeFlat(string path, CancellationToken ct)
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

    public static DriveInfo[] GetDrives() =>
        DriveInfo.GetDrives().Where(d => d.IsReady).ToArray();

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }
}
