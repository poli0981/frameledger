namespace FrameLedger.App.Services;

/// <summary>
/// The App's own "the library's rows changed" signal (beta.11, owner request 2026-10-03): an import adds entries, and a
/// page already on screen reloads. Navigating to the page that is on screen does nothing in WPF UI 4.3.0, so the Games
/// page never saw an import made from File ▸ Import library… while it was open. The Agent's own changes (a merge, a
/// finding, a new session) reach the page through its session events and the Refresh button.
/// </summary>
public sealed class LibraryChanges
{
    /// <summary>Raised on the thread that changed the library — the UI thread for every caller today.</summary>
    public event EventHandler? Changed;

    /// <summary>Say that entries were added, removed or replaced.</summary>
    public void Announce() => Changed?.Invoke(this, EventArgs.Empty);
}
