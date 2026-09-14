namespace FrameLedger.App.Services;

/// <summary>FR-6.2's blocking question: the selected sessions measured different things — compare across tiers anyway?</summary>
public interface IMixedTierPrompt
{
    Task<bool> AcknowledgeAsync(CancellationToken ct = default);
}
