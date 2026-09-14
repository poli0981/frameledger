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
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
