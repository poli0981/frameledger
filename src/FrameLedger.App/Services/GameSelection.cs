namespace FrameLedger.App.Services;

/// <summary>
/// Which game the detail page shows. Pages are transient and navigated by type only (<c>16_WPFUI_SYNTAX</c>
/// §Navigation), so the selection travels through this singleton: the grid sets it, then navigates; the detail
/// page's view model reads it in its constructor.
/// </summary>
public sealed class GameSelection
{
    public long? GameId { get; set; }
}
