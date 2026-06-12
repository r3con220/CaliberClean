using Microsoft.Win32;

namespace CaliberClean.Services;

internal enum StartupLocation
{
    RegistryCurrentUser,
    RegistryLocalMachine,
    RegistryCurrentUserDisabled,
    RegistryLocalMachineDisabled,
    StartupFolder,
}

internal record StartupEntry(
    string Name,
    string Command,
    StartupLocation Location,
    bool IsEnabled,
    bool CanToggle  // false for startup folder entries (we just delete them)
);

internal class StartupManager
{
    // Chromium/Windows stores disabled registry entries under this key
    private const string DisabledSuffix = "_disabled_by_CaliberClean";

    private static readonly (string Path, StartupLocation Loc)[] RegistryKeys =
    [
        (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            StartupLocation.RegistryCurrentUser),
        (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            StartupLocation.RegistryLocalMachine),
    ];

    public List<StartupEntry> GetEntries()
    {
        var entries = new List<StartupEntry>();
        entries.AddRange(ReadRegistryKey(Registry.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            StartupLocation.RegistryCurrentUser, enabled: true));
        entries.AddRange(ReadRegistryKey(Registry.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run_disabled_by_CaliberClean",
            StartupLocation.RegistryCurrentUserDisabled, enabled: false));
        entries.AddRange(ReadRegistryKey(Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            StartupLocation.RegistryLocalMachine, enabled: true));
        entries.AddRange(ReadRegistryKey(Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run_disabled_by_CaliberClean",
            StartupLocation.RegistryLocalMachineDisabled, enabled: false));
        entries.AddRange(ReadStartupFolders());
        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public (bool Success, string Error) ToggleEntry(StartupEntry entry)
    {
        if (!entry.CanToggle)
            return (false, "Startup folder entries cannot be toggled — delete them instead.");

        try
        {
            if (entry.IsEnabled)
                DisableEntry(entry);
            else
                EnableEntry(entry);
            return (true, "");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Access denied. Try running CaliberClean as Administrator.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Success, string Error) DeleteEntry(StartupEntry entry)
    {
        try
        {
            if (entry.Location == StartupLocation.StartupFolder)
            {
                File.Delete(entry.Command);
                return (true, "");
            }

            var (hive, keyPath, _) = GetRegistryDetails(entry);
            using var key = hive.OpenSubKey(keyPath, writable: true);
            if (key == null) return (false, "Registry key not found.");
            key.DeleteValue(entry.Name, throwOnMissingValue: false);
            return (true, "");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Access denied. Try running CaliberClean as Administrator.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void DisableEntry(StartupEntry entry)
    {
        var (hive, enabledPath, disabledPath) = GetRegistryDetails(entry);

        using var enabledKey = hive.OpenSubKey(enabledPath, writable: true);
        if (enabledKey == null) return;

        var value = enabledKey.GetValue(entry.Name);
        if (value == null) return;

        using var disabledKey = hive.CreateSubKey(disabledPath);
        disabledKey.SetValue(entry.Name, value);
        enabledKey.DeleteValue(entry.Name);
    }

    private void EnableEntry(StartupEntry entry)
    {
        var (hive, enabledPath, disabledPath) = GetRegistryDetails(entry);

        using var disabledKey = hive.OpenSubKey(disabledPath, writable: true);
        if (disabledKey == null) return;

        var value = disabledKey.GetValue(entry.Name);
        if (value == null) return;

        using var enabledKey = hive.CreateSubKey(enabledPath);
        enabledKey.SetValue(entry.Name, value);
        disabledKey.DeleteValue(entry.Name);
    }

    private static (RegistryKey Hive, string EnabledPath, string DisabledPath) GetRegistryDetails(StartupEntry entry)
    {
        bool isHklm = entry.Location is StartupLocation.RegistryLocalMachine
                                     or StartupLocation.RegistryLocalMachineDisabled;
        var hive = isHklm ? Registry.LocalMachine : Registry.CurrentUser;
        const string runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string disabledPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run_disabled_by_CaliberClean";
        return (hive, runPath, disabledPath);
    }

    private static IEnumerable<StartupEntry> ReadRegistryKey(
        RegistryKey hive, string keyPath, StartupLocation location, bool enabled)
    {
        var results = new List<StartupEntry>();
        try
        {
            using var key = hive.OpenSubKey(keyPath);
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var value = key.GetValue(name)?.ToString() ?? "";
                    results.Add(new StartupEntry(name, value, location, enabled, CanToggle: true));
                }
            }
        }
        catch { }
        return results;
    }

    private static IEnumerable<StartupEntry> ReadStartupFolders()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
        };

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                yield return new StartupEntry(
                    Path.GetFileNameWithoutExtension(file),
                    file,
                    StartupLocation.StartupFolder,
                    IsEnabled: true,
                    CanToggle: false);
            }
        }
    }

    public static string LocationLabel(StartupLocation loc) => loc switch
    {
        StartupLocation.RegistryCurrentUser => "HKCU\\Run",
        StartupLocation.RegistryLocalMachine => "HKLM\\Run",
        StartupLocation.RegistryCurrentUserDisabled => "HKCU\\Run (disabled)",
        StartupLocation.RegistryLocalMachineDisabled => "HKLM\\Run (disabled)",
        StartupLocation.StartupFolder => "Startup Folder",
        _ => "Unknown",
    };
}
