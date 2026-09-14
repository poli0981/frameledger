using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>FR-1.3's edit dialog as a question: the current metadata in, the edited metadata out, or null when cancelled.</summary>
public interface IEditGamePrompt
{
    Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default);
}
