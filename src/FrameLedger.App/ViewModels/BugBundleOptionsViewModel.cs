using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The bug report's optional items (<c>10_LOGGING</c> §Bug report flow step 2): a checkbox for each item there is — the
/// crash dump (P4 PR-9) and the last session's summary — each clear until the user ticks it
/// (<c>legal/PRIVACY_POLICY.md</c> §3), each with a line saying what it holds.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class BugBundleOptionsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _includeCrashDump;

    [ObservableProperty]
    private bool _includeLastSession;

    public BugBundleOptionsViewModel(BugBundleOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        HasCrashDump = offer.CrashDump is not null;
        HasLastSession = offer.LastSession is not null;
        if (offer.CrashDump is { } dump)
        {
            DumpLabel = string.Format(
                CultureInfo.CurrentCulture,
                Strings.BugReport_Options_Dump_Format,
                dump.WrittenAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                dump.Bytes / (1024.0 * 1024.0));
        }

        if (offer.LastSession is { } session)
        {
            SessionLabel = string.Format(
                CultureInfo.CurrentCulture,
                Strings.BugReport_Options_Session_Format,
                session.Game,
                session.StartedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        }
    }

    public static string Title => Strings.BugReport_Options_Title;

    public static string Intro => Strings.BugReport_Options_Intro;

    public static string DumpDetail => Strings.BugReport_Options_Dump_Detail;

    public static string SessionDetail => Strings.BugReport_Options_Session_Detail;

    public bool HasCrashDump { get; }

    public bool HasLastSession { get; }

    /// <summary>The crash dump's checkbox text: its local time and size in MB; empty when there is no dump.</summary>
    public string DumpLabel { get; } = string.Empty;

    /// <summary>The last session's checkbox text: the game and the local start time; empty when there is no session.</summary>
    public string SessionLabel { get; } = string.Empty;

    /// <summary>The answer, once the dialog closed: <paramref name="continued"/> is its primary button.</summary>
    public BugBundleOptions Options(bool continued) => continued
        ? new BugBundleOptions(Cancelled: false, IncludeCrashDump: HasCrashDump && IncludeCrashDump, IncludeLastSession: HasLastSession && IncludeLastSession)
        : BugBundleOptions.Cancel;
}
