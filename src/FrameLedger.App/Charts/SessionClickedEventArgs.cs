namespace FrameLedger.App.Charts;

/// <summary>A click on a trend landed on a session's points (beta.11, D39): which session.</summary>
public sealed class SessionClickedEventArgs(long sessionId) : EventArgs
{
    public long SessionId { get; } = sessionId;
}
