using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace CaliberClean.Services;

public record BlocklistStatus(bool IsEnabled, int DomainCount, DateTime? EnabledAt, DateTime? LastRefreshedAt, string? Error);

/// <summary>
/// System-wide ad/tracker blocking via the Windows hosts file. Downloads
/// StevenBlack's community-maintained unified hosts list
/// (https://github.com/StevenBlack/hosts) and appends its domains, each
/// mapped to 0.0.0.0, inside a clearly marked block — so enabling/disabling
/// never touches anything the user added to hosts themselves.
///
/// Status is always derived live from the hosts file rather than trusted
/// from a cached flag, so the UI can never drift from what's actually
/// installed (e.g. if the user hand-edits hosts outside the app).
/// </summary>
public static class HostsBlocklistService
{
    private const string SourceUrl  = "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts";
    private const string MarkerStart = "# CALIBER-CLEAN-BLOCKLIST-START";
    private const string MarkerEnd   = "# CALIBER-CLEAN-BLOCKLIST-END";

    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaliberClean");

    private static readonly string BackupPath = Path.Combine(AppDataDir, "hosts.original.backup");
    private static readonly string MetaPath   = Path.Combine(AppDataDir, "blocklist-meta.json");

    // EnabledAt is set only by a true Enable transition; LastRefreshedAt is
    // touched by both Enable (the initial download counts as a refresh too)
    // and Refresh — kept distinct so the UI can show both facts honestly.
    private sealed record Meta(DateTime? EnabledAt, DateTime? LastRefreshedAt);

    // ── Elevation ─────────────────────────────────────────────────────────────

    public sealed class ElevationRequiredException : Exception
    {
        public ElevationRequiredException()
            : base("Administrator privileges are required to modify the hosts file.") { }
    }

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// Relaunches CaliberClean elevated via a UAC prompt. Returns false
    /// (rather than throwing) if the user cancels the prompt, so callers can
    /// show a clear message instead of an unhandled exception.
    public static bool RelaunchElevated()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return false;

            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // ERROR_CANCELLED — user declined the UAC prompt.
            return false;
        }
    }

    private static void RequireElevated()
    {
        if (!IsElevated()) throw new ElevationRequiredException();
    }

    /// Maps common failure modes (network, permissions/locking) to messages a
    /// non-technical user can act on. Falls back to the raw exception message
    /// for anything unexpected, so nothing is ever silently swallowed.
    public static string FriendlyError(Exception ex) => ex switch
    {
        HttpRequestException or TaskCanceledException or OperationCanceledException =>
            "Could not download the block list — check your internet connection and try again.",
        UnauthorizedAccessException =>
            "Access to the hosts file was denied — another program (often antivirus software) may be protecting it.",
        IOException =>
            "The hosts file is in use by another program and couldn't be updated — close any hosts editors or security tools and try again.",
        ElevationRequiredException =>
            ex.Message,
        _ => $"Could not update the hosts file: {ex.Message}",
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public static BlocklistStatus GetStatus()
    {
        try
        {
            if (!File.Exists(HostsPath))
                return new BlocklistStatus(false, 0, null, null, null);

            var lines = File.ReadAllLines(HostsPath);

            if (HasUnterminatedBlock(lines))
                return new BlocklistStatus(false, 0, null, null,
                    "Hosts file has an incomplete Caliber block (likely from an interrupted update) — click Refresh or toggle off to repair it.");

            int count = CountBlockedDomains(lines);
            var meta = LoadMeta();
            return new BlocklistStatus(count > 0, count, meta.EnabledAt, meta.LastRefreshedAt, null);
        }
        catch (Exception ex)
        {
            return new BlocklistStatus(false, 0, null, null, FriendlyError(ex));
        }
    }

    public static async Task EnableAsync(IProgress<string>? progress = null)
    {
        RequireElevated();

        progress?.Report("Backing up hosts file…");
        EnsureBackup();

        progress?.Report("Downloading block list…");
        var domains = await DownloadDomainsAsync();

        progress?.Report("Applying block list…");
        ApplyBlock(domains);
        // A true Enable also counts as the first refresh — both timestamps
        // start together, then diverge if Refresh is used later without a
        // full disable/enable cycle.
        SaveMeta(new Meta(DateTime.Now, DateTime.Now));

        progress?.Report("Flushing DNS cache…");
        FlushDns();
    }

    /// Re-downloads and re-applies the block list in place — same
    /// atomic-write and marker-boundary logic as Enable, but only touches
    /// LastRefreshedAt (leaves EnabledAt and the one-time backup alone).
    /// Only meaningful while already enabled; callers should gate the
    /// "Refresh List" button on IsEnabled.
    public static async Task RefreshAsync(IProgress<string>? progress = null)
    {
        RequireElevated();

        progress?.Report("Downloading block list…");
        var domains = await DownloadDomainsAsync();

        progress?.Report("Applying block list…");
        ApplyBlock(domains);

        var existing = LoadMeta();
        SaveMeta(existing with { LastRefreshedAt = DateTime.Now });

        progress?.Report("Flushing DNS cache…");
        FlushDns();
    }

    // Used by GetStatus() to detect a truncated marker pair (e.g. from manual
    // editing, or a pre-atomic-write interruption) rather than silently
    // misreporting the domain count — the UI surfaces this via status.Error.
    private static bool HasUnterminatedBlock(string[] lines)
    {
        bool inBlock = false;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (trimmed == MarkerStart)
            {
                if (inBlock) return true; // duplicate START before an END — also malformed
                inBlock = true;
            }
            else if (trimmed == MarkerEnd)
            {
                inBlock = false;
            }
        }
        return inBlock;
    }

    public static void Disable()
    {
        RequireElevated();
        if (!File.Exists(HostsPath)) return;

        var lines = File.ReadAllLines(HostsPath);
        var kept = RemoveBlock(lines).ToList();
        WriteHostsFileAtomic(kept);

        FlushDns();
    }

    // ── Hosts file parsing/editing ───────────────────────────────────────────

    private static async Task<List<string>> DownloadDomainsAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CaliberClean/0.7.0 (+https://calibervoice.com)");
        var text = await http.GetStringAsync(SourceUrl);

        var domains = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (!line.StartsWith("0.0.0.0 ", StringComparison.Ordinal)) continue;

            var domain = line[8..].Trim();
            var hashIdx = domain.IndexOf('#');
            if (hashIdx >= 0) domain = domain[..hashIdx].Trim();

            // Skip the list's own self-referential "0.0.0.0 0.0.0.0" header line.
            if (domain.Length == 0 || domain == "0.0.0.0") continue;

            if (seen.Add(domain)) domains.Add(domain);
        }

        if (domains.Count == 0)
            throw new InvalidOperationException("Downloaded block list but found no domains — source format may have changed.");

        return domains;
    }

    private static void ApplyBlock(List<string> domains)
    {
        var existing = File.Exists(HostsPath) ? File.ReadAllLines(HostsPath) : Array.Empty<string>();
        var lines = RemoveBlock(existing).ToList();

        lines.Add("");
        lines.Add(MarkerStart);
        lines.Add($"# {domains.Count:N0} domains — Caliber Media LLC — source: {SourceUrl}");
        lines.Add($"# Last updated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        foreach (var domain in domains)
            lines.Add($"0.0.0.0 {domain}");
        lines.Add(MarkerEnd);

        WriteHostsFileAtomic(lines);
    }

    /// Writes to a temp file first, then atomically swaps it into place, so a
    /// crash, kill, disk-full, or antivirus lock mid-write can never leave the
    /// real hosts file half-written — worst case, the swap fails and the
    /// previous good hosts content is untouched.
    private static void WriteHostsFileAtomic(IEnumerable<string> lines)
    {
        var tempPath = HostsPath + ".calibertmp";
        File.WriteAllLines(tempPath, lines, new UTF8Encoding(false));

        try
        {
            if (File.Exists(HostsPath))
                File.Replace(tempPath, HostsPath, null);
            else
                File.Move(tempPath, HostsPath);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    /// Strips everything between (and including) the Caliber markers,
    /// leaving the user's own hosts entries untouched. Also trims any blank
    /// lines left dangling at the end.
    private static IEnumerable<string> RemoveBlock(string[] lines)
    {
        var result = new List<string>();
        bool inBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (trimmed == MarkerStart) { inBlock = true; continue; }
            if (trimmed == MarkerEnd)   { inBlock = false; continue; }
            if (inBlock) continue;
            result.Add(line);
        }

        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);

        return result;
    }

    private static int CountBlockedDomains(string[] lines)
    {
        bool inBlock = false;
        int count = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (trimmed == MarkerStart) { inBlock = true; continue; }
            if (trimmed == MarkerEnd)   { inBlock = false; continue; }
            if (inBlock && trimmed.StartsWith("0.0.0.0 ", StringComparison.Ordinal)) count++;
        }

        return count;
    }

    private static void EnsureBackup()
    {
        Directory.CreateDirectory(AppDataDir);
        if (!File.Exists(BackupPath) && File.Exists(HostsPath))
            File.Copy(HostsPath, BackupPath);
    }

    private static void FlushDns()
    {
        try
        {
            var psi = new ProcessStartInfo("ipconfig.exe", "/flushdns")
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch { /* non-fatal — worst case, cached entries expire on their own TTL */ }
    }

    private static Meta LoadMeta()
    {
        try
        {
            if (File.Exists(MetaPath))
                return JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath)) ?? new Meta(null, null);
        }
        catch { }
        return new Meta(null, null);
    }

    private static void SaveMeta(Meta meta)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
