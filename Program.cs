using CaliberClean.Services;

namespace CaliberClean;

static class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        // Silent auto-clean mode — no UI, write log, exit.
        if (args.Contains("--auto-clean", StringComparer.OrdinalIgnoreCase))
        {
            await AutoCleanRunner.RunAsync();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
