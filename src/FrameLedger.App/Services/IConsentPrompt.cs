namespace FrameLedger.App.Services;

/// <summary>
/// FR-2.1's dialog as a question: shows the reviewed disclosure for one game and answers whether the user typed
/// the acknowledgement and chose to enable. The WPF implementation is <see cref="ConsentPrompt"/>; a test
/// substitutes an answer.
/// </summary>
public interface IConsentPrompt
{
    Task<bool> ShowAsync(string gameName, CancellationToken ct = default);
}
