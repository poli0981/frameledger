namespace FrameLedger.App.Services;

/// <summary>How a <c>DeleteSessions</c> ask ended (beta.11, D40).</summary>
public enum SessionDeletionOutcome
{
    /// <summary>The Agent deleted them; the counts say how many.</summary>
    Deleted,

    /// <summary>A session it would delete under is being recorded: nothing was deleted.</summary>
    SessionRunning,

    /// <summary>The capture agent is not connected: nothing was asked.</summary>
    AgentUnavailable,

    /// <summary>The Agent does not know the request (an older build): nothing was deleted.</summary>
    AgentTooOld,

    /// <summary>Anything else; the detail says what.</summary>
    Failed,
}
