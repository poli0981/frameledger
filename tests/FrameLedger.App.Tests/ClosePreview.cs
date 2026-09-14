using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>The bug report flow's three ports, scripted (P4 PR-3): a preview that answers what the test says, a browser and a clipboard that record.</summary>
internal sealed class ClosePreview(BugReportChoice choice = BugReportChoice.Close) : IBugReportPreview
{
    public List<BugReportPreviewModel> Shown { get; } = [];

    public Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default)
    {
        Shown.Add(model);
        return Task.FromResult(choice);
    }
}
