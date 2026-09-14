namespace FrameLedger.App.Services;

/// <summary>Opens the session summary window for one session (a row click, a recent-list click, later the post-session toast).</summary>
public interface ISessionSummaryOpener
{
    void Open(long sessionId);
}
