using CaliberClean.Services;

namespace CaliberClean;

/// Shared UAC-relaunch prompt for any action that needs administrator privileges
/// (hosts blocklist, scheduled-task registration). HostsBlocklistService.IsElevated()/
/// RelaunchElevated() are general process-elevation checks despite living on that
/// class — reused here rather than duplicated.
public static class Elevation
{
    public static void PromptIfNeeded(string reason)
    {
        var result = MessageBox.Show(
            $"{reason}\n\nRestart CaliberClean as administrator now?",
            "Administrator Required", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        if (HostsBlocklistService.RelaunchElevated())
        {
            Application.Exit();
            return;
        }

        MessageBox.Show("Elevation was cancelled — no changes were made.", "Administrator Required",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
