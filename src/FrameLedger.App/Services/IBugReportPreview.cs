namespace FrameLedger.App.Services;

/// <summary>Step 3's preview dialog as a port, so <see cref="BugReportFlow"/> is testable without a window.</summary>
public interface IBugReportPreview
{
    Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default);
}
