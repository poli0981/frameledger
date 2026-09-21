namespace FrameLedger.App.Services;

/// <summary>
/// The guard-bypass disclosure (owner decision 2026-09-21, <c>19_SAFETY</c> §The user's bypass): true only when the user
/// ticked the acknowledgement AND typed the confirmation phrase AND pressed the primary button. A seam so the flow is
/// testable without a window.
/// </summary>
public interface IGuardBypassPrompt
{
    /// <summary>Shows the disclosure for one game and answers whether both acts were performed.</summary>
    /// <param name="gameName">The game the bypass is for; it applies to no other.</param>
    /// <param name="finding">What the guard has recorded for this game (the row's block reason), or null when nothing yet.</param>
    /// <param name="ct">Cancels the dialog; a cancelled disclosure is a declined one.</param>
    Task<bool> ShowAsync(string gameName, string? finding, CancellationToken ct = default);
}
