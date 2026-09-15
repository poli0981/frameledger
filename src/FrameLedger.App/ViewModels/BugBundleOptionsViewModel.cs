using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The bug report's optional crash dump (P4 PR-9, <c>10_LOGGING</c> §Bug report flow step 2): one checkbox, clear until the
/// user ticks it (<c>legal/PRIVACY_POLICY.md</c> §3), labelled with the dump's time and size, and a line saying what a
/// dump can hold.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public sealed partial class BugBundleOptionsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _includeCrashDump;

    public BugBundleOptionsViewModel(CrashDumpInfo dump)
    {
        ArgumentNullException.ThrowIfNull(dump);
        DumpLabel = string.Format(
            CultureInfo.CurrentCulture,
            Strings.BugReport_Options_Dump_Format,
            dump.WrittenAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            dump.Bytes / (1024.0 * 1024.0));
    }

    public static string Title => Strings.BugReport_Options_Title;

    public static string Intro => Strings.BugReport_Options_Intro;

    public static string DumpDetail => Strings.BugReport_Options_Dump_Detail;

    /// <summary>The checkbox's text: the dump's local time and its size in MB.</summary>
    public string DumpLabel { get; }

    /// <summary>The answer, once the dialog closed: <paramref name="continued"/> is its primary button.</summary>
    public CrashDumpChoice Choice(bool continued)
    {
        if (!continued)
        {
            return CrashDumpChoice.Cancel;
        }

        return IncludeCrashDump ? CrashDumpChoice.Include : CrashDumpChoice.LeaveOut;
    }
}
