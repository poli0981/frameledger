namespace FrameLedger.App.Services;

/// <summary>
/// The answer to the optional items (<c>legal/PRIVACY_POLICY.md</c> §3: included only when ticked): the dialog closed, or
/// which boxes were ticked. Every box starts clear.
/// </summary>
public sealed record BugBundleOptions(bool Cancelled, bool IncludeCrashDump, bool IncludeLastSession)
{
    /// <summary>The dialog was closed: no bundle is written.</summary>
    public static BugBundleOptions Cancel { get; } = new(Cancelled: true, IncludeCrashDump: false, IncludeLastSession: false);

    /// <summary>Continue with every box clear, and what an empty offer means.</summary>
    public static BugBundleOptions NothingTicked { get; } = new(Cancelled: false, IncludeCrashDump: false, IncludeLastSession: false);
}
