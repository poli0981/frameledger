namespace FrameLedger.App.Services;

/// <summary>
/// The optional items a bug report can offer (<c>10_LOGGING</c> §Bug report flow step 2), each null when there is none:
/// the newest crash dump of the last seven days (P4 PR-9) and the last session's summary.
/// </summary>
public sealed record BugBundleOffer(CrashDumpInfo? CrashDump, LastSessionInfo? LastSession)
{
    /// <summary>Nothing to offer, so no question is asked.</summary>
    public bool IsEmpty => CrashDump is null && LastSession is null;
}
