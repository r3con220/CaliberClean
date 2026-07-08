using AutoUpdaterDotNET;
using CaliberClean.Services;

namespace CaliberClean;

static class Program
{
    // Shared with the manual "Check for Updates" button in DashboardPanel.
    public const string UpdateCheckUrl = "https://raw.githubusercontent.com/r3con220/CaliberClean/master/update.xml";

    [STAThread]
    static async Task Main(string[] args)
    {
        // Silent auto-clean mode — no UI, write log, exit.
        if (args.Contains("--auto-clean", StringComparer.OrdinalIgnoreCase))
        {
            await AutoCleanRunner.RunAsync();
            return;
        }

        // Silent, non-blocking check — AutoUpdater shows its own dialog only
        // if a newer version is actually available; otherwise this is a no-op.
        AutoUpdater.Start(UpdateCheckUrl);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
