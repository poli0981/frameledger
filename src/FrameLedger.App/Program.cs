// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
    /// <summary>The exit code of a <c>--data-dir</c> refused before any window (beta.15, D52).</summary>
    internal const int ExitDataFolderRefused = 2;

    [STAThread]
    private static int Main()
    {
        VelopackHooks.Configure(VelopackApp.Build()).Run();

        // D52 (beta.15): --data-dir makes this App a viewer over a copy, decided before the single-instance key, the log
        // or the ledger reads a path. A refusal is said in a message box (a WinExe has no console) and opens nothing.
        string[] args = Environment.GetCommandLineArgs()[1..];
        DataFolderChoice folder = DataFolderArgument.Read(args, UiPaths.ProfileDirectory);
        if (folder.Kind == DataFolderChoiceKind.Refused)
        {
            _ = System.Windows.MessageBox.Show(folder.Problem, Strings.DataDir_Refused_Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return ExitDataFolderRefused;
        }

        if (folder.Kind == DataFolderChoiceKind.Viewer)
        {
            UiPaths.UseViewerFolder(folder.Folder!);
        }

        // One App per data folder (2026-09-21): a second start hands over to the first and exits. --diag opens no window
        // and must work beside a running App, so it never claims. A viewer's folder is its own key, so it runs beside the
        // profile's App.
        SingleInstance? single = null;
        try
        {
            if (!DiagReport.Requested(args)
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
