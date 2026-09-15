namespace FrameLedger.App.Services;

/// <summary>The bug report's dialogs as a port, so <see cref="BugReportFlow"/> is testable without a window.</summary>
public interface IBugReportPreview
{
    /// <summary>Step 3: every entry of the written zip, then step 4's choice.</summary>
    Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default);

    /// <summary>
    /// Step 2's optional items: a checkbox for each item <paramref name="offer"/> has, clear until the user ticks it
    /// (<c>legal/PRIVACY_POLICY.md</c> §3). Closing the dialog is <see cref="BugBundleOptions.Cancel"/>.
    /// </summary>
    Task<BugBundleOptions> AskOptionsAsync(BugBundleOffer offer, CancellationToken ct = default);
}
