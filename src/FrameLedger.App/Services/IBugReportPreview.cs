namespace FrameLedger.App.Services;

/// <summary>The bug report's dialogs as a port, so <see cref="BugReportFlow"/> is testable without a window.</summary>
public interface IBugReportPreview
{
    /// <summary>Step 3: every entry of the written zip, then step 4's choice.</summary>
    Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default);

    /// <summary>
    /// Step 2's optional item (P4 PR-9): the crash dump's checkbox, clear until the user ticks it
    /// (<c>legal/PRIVACY_POLICY.md</c> §3). Closing the dialog is <see cref="CrashDumpChoice.Cancel"/>.
    /// </summary>
    Task<CrashDumpChoice> AskCrashDumpAsync(CrashDumpInfo dump, CancellationToken ct = default);
}
