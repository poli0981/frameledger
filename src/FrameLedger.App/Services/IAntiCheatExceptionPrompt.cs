namespace FrameLedger.App.Services;

/// <summary>
/// D33's disclosure as a question: shows the versioned text for one game and answers whether the user accepted the risk
/// and chose to make the exception. The WPF implementation is <see cref="AntiCheatExceptionPrompt"/>; a test substitutes an
/// answer.
/// </summary>
public interface IAntiCheatExceptionPrompt
{
    Task<bool> ShowAsync(AntiCheatExceptionFacts facts, CancellationToken ct = default);
}
