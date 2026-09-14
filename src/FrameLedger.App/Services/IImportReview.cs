using FrameLedger.Application.Import;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-1.2's review checklist as a port (P4 PR-4): every candidate the stores found, the user ticks what to add,
/// and the answer is the ticked subset — or null for cancel. The WPF implementation is a <c>ContentDialog</c>
/// (<see cref="ImportReviewPrompt"/>); the flow is tested without it.
/// </summary>
public interface IImportReview
{
    Task<IReadOnlyList<ImportCandidate>?> ReviewAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default);
}
