using System.Runtime.InteropServices;

namespace CaliberClean.Services;

// ── Category model ────────────────────────────────────────────────────────────
public record TempCategory(string Id, string Name, string FolderPath);

// ── Per-category results ──────────────────────────────────────────────────────
public record ScanResult(string CategoryId, int FileCount, long TotalBytes);
public record CleanResult(string CategoryId, int Deleted, int Skipped, long BytesFreed);

public class TempFileCleaner
{
    // ── Shell P/Invoke ────────────────────────────────────────────────────────
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int    cbSize;
        public long   i64Size;
        public long   i64NumItems;
    }

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI   = 0x00000002;
    private const uint SHERB_NOSOUND        = 0x00000004;

    // ── Category catalog ──────────────────────────────────────────────────────
    public static readonly TempCategory[] Categories =
    [
        new("win_temp",  "Windows Temp",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),

        new("user_temp", "User Temp",
            Environment.GetEnvironmentVariable("TEMP")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")),

        new("prefetch",  "Prefetch",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch")),

        new("recycle",   "Recycle Bin",   ""),   // handled via Shell API

        new("wu_cache",  "Windows Update Cache",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                         "SoftwareDistribution", "Download")),
    ];

    // ── Scan ──────────────────────────────────────────────────────────────────
    public async Task<ScanResult> ScanCategoryAsync(
        TempCategory cat,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report($"Scanning {cat.Name}…");

            if (cat.Id == "recycle")
                return ScanRecycleBin(cat.Id);

            if (!Directory.Exists(cat.FolderPath))
                return new ScanResult(cat.Id, 0, 0);

            int count = 0;
            long bytes = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(cat.FolderPath, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try { var fi = new FileInfo(file); bytes += fi.Length; count++; }
                    catch { }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return new ScanResult(cat.Id, count, bytes);
        }, ct);
    }

    private static ScanResult ScanRecycleBin(string categoryId)
    {
        try
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            if (SHQueryRecycleBin(null, ref info) == 0)
                return new ScanResult(categoryId, (int)info.i64NumItems, info.i64Size);
        }
        catch { }
        return new ScanResult(categoryId, 0, 0);
    }

    // ── Clean ─────────────────────────────────────────────────────────────────
    public async Task<CleanResult> CleanCategoryAsync(
        TempCategory cat,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report($"Cleaning {cat.Name}…");

            if (cat.Id == "recycle")
                return CleanRecycleBin(cat.Id);

            if (!Directory.Exists(cat.FolderPath))
                return new CleanResult(cat.Id, 0, 0, 0);

            int deleted = 0, skipped = 0;
            long freed = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(cat.FolderPath, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(file);
                        long size = fi.Length;
                        fi.Delete();
                        freed += size;
                        deleted++;
                    }
                    catch { skipped++; }
                }

                // Prune empty subdirectories (deepest first so parents empty out)
                foreach (var dir in Directory.EnumerateDirectories(cat.FolderPath, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.Length))
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return new CleanResult(cat.Id, deleted, skipped, freed);
        }, ct);
    }

    private static CleanResult CleanRecycleBin(string categoryId)
    {
        // Snapshot before so we can report what was freed
        var before = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        try { SHQueryRecycleBin(null, ref before); } catch { }

        try { SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND); }
        catch { }

        return new CleanResult(categoryId, (int)before.i64NumItems, 0, before.i64Size);
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
