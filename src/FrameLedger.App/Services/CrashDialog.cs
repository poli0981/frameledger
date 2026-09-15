using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;

namespace FrameLedger.App.Services;

/// <summary>
/// The crash dialog of <c>10_LOGGING</c> §Crash handling (P4 PR-9). A Win32 message box on purpose, not a WPF UI one: it
/// runs after an exception nobody handled, when the shell's dialog host, the theme and the dispatcher's state are exactly
/// what cannot be trusted. Yes opens the bug-report flow; No closes FrameLedger.
/// </summary>
internal static class CrashDialog
{
    public static bool AskToReport(string? dump) =>
        MessageBox.Show(Body(dump), Strings.Crash_Title, MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No) == MessageBoxResult.Yes;

    /// <summary>The dialog's text: what happened, where the dump is (or that there is none), and what the report does.</summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture")]
    public static string Body(string? dump)
    {
        string where = dump is null ? Strings.Crash_NoDump : string.Format(CultureInfo.CurrentCulture, Strings.Crash_Dump_Format, dump);
        return string.Format(CultureInfo.CurrentCulture, Strings.Crash_Body_Format, where);
    }
}
