using System.Diagnostics;
using System.Text.Json;

namespace CaliberClean.Services;

public enum CleanFrequency { Daily, Weekly, Monthly }

public record ScheduleStatus(bool IsEnabled, CleanFrequency? Frequency);

public record ScheduleConfig(
    bool             Enabled,
    CleanFrequency   Frequency,
    // Temp file categories
    bool CleanWinTemp,
    bool CleanUserTemp,
    bool CleanPrefetch,
    bool CleanWuCache,
    // Browser categories
    bool CleanChrome,
    bool CleanEdge,
    bool CleanFirefox
);

public static class ScheduleManager
{
    private const string TaskName = "CaliberClean_AutoClean";

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CaliberClean", "schedule.json");

    // ── Public API ────────────────────────────────────────────────────────────

    public static (bool Success, string Error) EnableSchedule(CleanFrequency frequency, ScheduleConfig config)
    {
        EnsureDir();
        SaveConfig(config with { Enabled = true, Frequency = frequency });

        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(exePath)) return (false, "Could not determine the running exe path.");

        // Remove stale task first (ignore errors — it may not exist yet)
        Schtasks($"/delete /tn \"{TaskName}\" /f");

        var schedArgs = frequency switch
        {
            CleanFrequency.Daily   => "/sc DAILY /st 03:00",
            CleanFrequency.Weekly  => "/sc WEEKLY /d MON /st 03:00",
            CleanFrequency.Monthly => "/sc MONTHLY /d 1 /st 03:00",
            _                      => "/sc DAILY /st 03:00",
        };

        // /rl HIGHEST requires the *creating* process to be elevated, not just
        // the account the task runs as — Windows Task Scheduler enforces this
        // regardless of whose account /ru names. Surface that instead of the
        // previous silent no-op so callers know the task genuinely wasn't made.
        //
        // /ru must be a resolved "%USERDOMAIN%\%USERNAME%" literal never
        // expands here — ProcessStartInfo launches schtasks.exe directly with
        // no cmd.exe in between, so %VAR% tokens pass through unexpanded and
        // schtasks fails with "No mapping between account names and security
        // IDs was done." Resolve the account name in C# instead.
        var (exitCode, output) = Schtasks(
            $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\" --auto-clean\" " +
            $"{schedArgs} /ru \"{Environment.UserDomainName}\\{Environment.UserName}\" /rl HIGHEST /f");

        if (exitCode != 0)
        {
            var friendly = output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
                ? "Access denied — registering a scheduled task with elevated privileges requires CaliberClean to run as Administrator at least once."
                : output.Trim();
            return (false, friendly);
        }

        return (true, "");
    }

    public static (bool Success, string Error) DisableSchedule()
    {
        var (exitCode, output) = Schtasks($"/delete /tn \"{TaskName}\" /f");

        var cfg = LoadConfig();
        if (cfg != null) SaveConfig(cfg with { Enabled = false });

        // "Cannot find" just means there was nothing to delete — already the
        // desired end state, not a real failure. Deleting a task that was
        // created with /rl HIGHEST also requires an elevated caller (verified
        // via testing, same constraint as creating it) — surface that case.
        if (exitCode != 0 && !output.Contains("cannot find", StringComparison.OrdinalIgnoreCase))
        {
            var friendly = output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
                ? "Access denied — removing this scheduled task requires CaliberClean to run as Administrator (it was created with elevated privileges)."
                : output.Trim();
            return (false, friendly);
        }

        return (true, "");
    }

    public static ScheduleStatus GetScheduleStatus()
    {
        var cfg = LoadConfig();
        bool exists = TaskExists();

        if (!exists || cfg is null || !cfg.Enabled)
            return new ScheduleStatus(false, null);

        return new ScheduleStatus(true, cfg.Frequency);
    }

    public static DateTime? GetNextRun()
    {
        var status = GetScheduleStatus();
        if (!status.IsEnabled || status.Frequency == null) return null;

        var now = DateTime.Now;
        return status.Frequency switch
        {
            CleanFrequency.Daily   => now.Date.AddDays(1).AddHours(3),
            CleanFrequency.Weekly  => NextWeekday(now, DayOfWeek.Monday).AddHours(3),
            CleanFrequency.Monthly => new DateTime(now.Year, now.Month, 1).AddMonths(1).AddHours(3),
            _                      => null,
        };
    }

    private static DateTime NextWeekday(DateTime from, DayOfWeek dow)
    {
        int daysUntil = ((int)dow - (int)from.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7;
        return from.Date.AddDays(daysUntil);
    }

    public static ScheduleConfig? LoadConfig()
    {
        if (!File.Exists(ConfigPath)) return null;
        try   { return JsonSerializer.Deserialize<ScheduleConfig>(File.ReadAllText(ConfigPath)); }
        catch { return null; }
    }

    public static void SaveConfig(ScheduleConfig config)
    {
        EnsureDir();
        File.WriteAllText(ConfigPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TaskExists()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", $"/query /tn \"{TaskName}\"")
            {
                CreateNoWindow        = true,
                UseShellExecute       = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static (int ExitCode, string Output) Schtasks(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow        = true,
                UseShellExecute       = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return (-1, "Could not start schtasks.exe.");
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, string.IsNullOrEmpty(stderr) ? stdout : stderr);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private static void EnsureDir() =>
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
}
