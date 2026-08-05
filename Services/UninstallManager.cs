using System.Diagnostics;
using Microsoft.Win32;

namespace CaliberClean.Services;

public record InstalledProgram(
    string DisplayName,
    string Publisher,
    string InstallDate,
    long EstimatedSizeKb,
    string UninstallString,
    string InstallLocation
);

public static class UninstallManager
{
    private static readonly string[] UninstallKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    public static InstalledProgram[] GetInstalledPrograms()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<InstalledProgram>();

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var keyPath in UninstallKeys)
                ReadKey(hive, keyPath, seen, results);

        return results
            .OrderByDescending(p => p.EstimatedSizeKb)
            .ToArray();
    }

    private static void ReadKey(RegistryKey hive, string keyPath, HashSet<string> seen, List<InstalledProgram> results)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath);
            if (key == null) return;

            foreach (var subName in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(subName);
                    if (sub == null) continue;

                    var name = sub.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (!seen.Add(name)) continue;

                    // Skip system components and updates
                    var systemComponent = sub.GetValue("SystemComponent");
                    if (systemComponent is int sc && sc == 1) continue;

                    var uninstall = sub.GetValue("UninstallString") as string ?? "";
                    var publisher = sub.GetValue("Publisher") as string ?? "";
                    var installDate = FormatDate(sub.GetValue("InstallDate") as string);
                    var location = sub.GetValue("InstallLocation") as string ?? "";

                    long sizeKb = 0;
                    var sizeVal = sub.GetValue("EstimatedSize");
                    if (sizeVal is int sizeInt) sizeKb = sizeInt;

                    results.Add(new InstalledProgram(name, publisher, installDate, sizeKb, uninstall, location));
                }
                catch { }
            }
        }
        catch { }
    }

    private static string FormatDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length != 8) return "";
        if (DateTime.TryParseExact(raw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw;
    }

    public static string FormatSize(long kb)
    {
        if (kb <= 0) return "";
        if (kb >= 1_048_576) return $"{kb / 1_048_576.0:F1} GB";
        if (kb >= 1_024)     return $"{kb / 1_024.0:F1} MB";
        return $"{kb} KB";
    }

    // Moved here from Program.cs's --action=uninstall CLI handler so the GUI's
    // Uninstall Manager panel can call the exact same logic in-process instead
    // of duplicating the quoted-path parsing and cancel-detection heuristic.
    public static (bool Success, string Error) LaunchUninstaller(string uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return (false, "This program has no uninstaller registered.");

        try
        {
            var str = uninstallString.Trim();
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
                return (false,
                    $"The uninstaller closed almost immediately (exit code {proc.ExitCode}) — it likely didn't run, possibly because the elevation prompt was cancelled.");
            }

            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
