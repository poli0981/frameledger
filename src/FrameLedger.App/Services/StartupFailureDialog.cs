using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;

namespace FrameLedger.App.Services;

/// <summary>
/// What a start that fails says on screen (<c>10_LOGGING</c> §Crash handling, 2026-09-15). Until then a ledger that would
/// not open or a host that would not start was a Fatal line in <c>ui-*.log</c> and a silent exit: nothing told the user
/// FrameLedger had even tried. A Win32 message box, like the crash dialog, because the shell is what failed to start.
/// It gives the reason and the log folder, and Yes opens that folder; FrameLedger exits with code 1 either way.
/// </summary>
internal static class StartupFailureDialog
{
    public static void Show(Exception exception, string logsDirectory)
    {
        if (MessageBox.Show(Body(exception, logsDirectory), Strings.StartupFailed_Title, MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.Yes) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo("explorer.exe", logsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: the logs folder could not be opened after the failed start");
        }
    }

    /// <summary>The dialog's text: the exception's own message and the folder its whole record is in.</summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture")]
    public static string Body(Exception exception, string logsDirectory)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return string.Format(CultureInfo.CurrentCulture, Strings.StartupFailed_Body_Format, exception.Message, logsDirectory);
    }
}
