using System.Security.Cryptography;

namespace CaliberClean.Services;

public record DuplicateGroup(string Hash, long FileSize, string[] Paths);

public class DuplicateFileFinder
{
    private const long MinBytes = 1024;

    public async Task<(DuplicateGroup[] Groups, long TotalRecoverableBytes)> ScanAsync(
        string folder,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            progress?.Report("Collecting files…");

            if (!Directory.Exists(folder))
                return (Array.Empty<DuplicateGroup>(), 0L);

            // Collect all qualifying files
            var allFiles = new List<string>();
            try
            {
                foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try { if (new FileInfo(f).Length >= MinBytes) allFiles.Add(f); } catch { }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            // Pre-filter: only files sharing the same size can be duplicates
            var sizeCandidates = allFiles
                .GroupBy(f => { try { return new FileInfo(f).Length; } catch { return -1L; } })
                .Where(g => g.Key > 0 && g.Count() > 1)
                .ToList();

            int totalToHash = sizeCandidates.Sum(g => g.Count());
            progress?.Report($"Hashing {totalToHash:N0} candidate files…");

            var hashMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            int done = 0;

            foreach (var sizeGroup in sizeCandidates)
            {
                foreach (var file in sizeGroup)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var hash = ComputeMD5(file);
                        if (!hashMap.TryGetValue(hash, out var list))
                            hashMap[hash] = list = new List<string>();
                        list.Add(file);
                    }
                    catch { }
                    done++;
                    if (done % 25 == 0)
                        progress?.Report($"Hashing {done:N0} / {totalToHash:N0}…");
                }
            }

            var groups = hashMap
                .Where(kv => kv.Value.Count > 1)
                .Select(kv =>
                {
                    long sz = 0;
                    try { sz = new FileInfo(kv.Value[0]).Length; } catch { }
                    return new DuplicateGroup(kv.Key, sz, kv.Value.ToArray());
                })
                .OrderByDescending(g => g.FileSize * (g.Paths.Length - 1))
                .ToArray();

            long recoverable = groups.Sum(g => g.FileSize * (g.Paths.Length - 1));
            return (groups, recoverable);
        }, ct);
    }

    private static string ComputeMD5(string path)
    {
        using var md5 = MD5.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        return Convert.ToHexString(md5.ComputeHash(stream));
    }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }
}
