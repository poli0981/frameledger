using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// The bug report flow's three ports, scripted (P4 PR-3): a preview that answers what the test says, a browser and a
/// clipboard that record. The crash dump question (P4 PR-9) answers <paramref name="dumpChoice"/> and records each offer.
/// </summary>
internal sealed class ClosePreview(BugReportChoice choice = BugReportChoice.Close, CrashDumpChoice dumpChoice = CrashDumpChoice.LeaveOut) : IBugReportPreview
{
    public List<BugReportPreviewModel> Shown { get; } = [];

    public List<CrashDumpInfo> DumpsOffered { get; } = [];

    public Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default)
    {
        Shown.Add(model);
        return Task.FromResult(choice);
    }

    public Task<CrashDumpChoice> AskCrashDumpAsync(CrashDumpInfo dump, CancellationToken ct = default)
    {
        DumpsOffered.Add(dump);
        return Task.FromResult(dumpChoice);
    }
}
