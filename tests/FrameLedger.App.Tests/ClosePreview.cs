using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// The bug report flow's three ports, scripted (P4 PR-3): a preview that answers what the test says, a browser and a
/// clipboard that record. The optional items' question answers <paramref name="options"/> (nothing ticked by default) and
/// records each offer.
/// </summary>
internal sealed class ClosePreview(BugReportChoice choice = BugReportChoice.Close, BugBundleOptions? options = null) : IBugReportPreview
{
    public List<BugReportPreviewModel> Shown { get; } = [];

    public List<BugBundleOffer> Offers { get; } = [];

    public Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default)
    {
        Shown.Add(model);
        return Task.FromResult(choice);
    }

    public Task<BugBundleOptions> AskOptionsAsync(BugBundleOffer offer, CancellationToken ct = default)
    {
        Offers.Add(offer);
        return Task.FromResult(options ?? BugBundleOptions.NothingTicked);
    }
}
