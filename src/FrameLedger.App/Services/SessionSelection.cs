namespace FrameLedger.App.Services;

/// <summary>
/// The session the shell's File ▸ Export acts on (P4 PR-3): the one selected on a game's Sessions tab, or the one
/// whose summary window was opened last — whichever happened most recently. A singleton the page and the opener
/// write and the shell reads; null until the user has picked anything.
/// </summary>
public sealed class SessionSelection
{
    public long? SessionId { get; private set; }

    public event EventHandler? Changed;

    public void Set(long? sessionId)
    {
        if (SessionId == sessionId)
        {
            return;
        }

        SessionId = sessionId;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
