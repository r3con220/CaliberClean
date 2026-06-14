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
}
