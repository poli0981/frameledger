using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Services;

/// <summary>FR-8.3's override dialog as a question; null when cancelled.</summary>
public interface ITriStateOverridePrompt
{
    Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default);
}
