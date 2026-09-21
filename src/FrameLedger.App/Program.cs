using FrameLedger.App.Services;
using FrameLedger.App.Update;
using Velopack;

namespace FrameLedger.App;

/// <summary>
/// The entry point, hand-written so Velopack runs first (<c>11_UPDATER</c>): its install, update and uninstall
/// launches re-enter this executable with their own arguments and must be answered before any window, host or
/// ledger exists — <see cref="VelopackApp.Run"/> handles them and exits the process; on an ordinary launch it
/// returns and the WPF application starts as it always did. <c>StartupObject</c> in the project file names this
/// class over the generated <c>App.Main</c>.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        VelopackHooks.Configure(VelopackApp.Build()).Run();

        // One App per data folder (2026-09-22): a second start hands over to the first and exits. --diag opens no window
        // and must work beside a running App, so it never claims.
        SingleInstance? single = null;
        try
        {
            if (!DiagReport.Requested(Environment.GetCommandLineArgs())
                && SingleInstance.TryClaim(SingleInstance.KeyOf(UiPaths.DataDirectory), out single) == SingleInstance.Claim.AnotherInstanceIsRunning)
            {
                return 0;
            }

            var app = new App { Instance = single };
            app.InitializeComponent();
            return app.Run();
        }
        finally
        {
            single?.Dispose();
        }
    }
}
