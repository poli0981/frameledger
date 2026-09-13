namespace FrameLedger.Application.Watch;

/// <summary>What <see cref="CaptureOrchestrator.LaunchAsync"/> decided.</summary>
public enum LaunchOutcome
{
    /// <summary>Nobody decided. The zero value, never a result.</summary>
    NotEvaluated = 0,

    /// <summary>The session task is running; <see cref="LaunchResult.SessionGuid"/> names it.</summary>
    Accepted,

    /// <summary>No <c>games</c> row has that id.</summary>
    UnknownGame,

    /// <summary>A session for that game is already running (one at a time).</summary>
    SessionRunning,

    /// <summary>This orchestrator was composed without launch mode (the console's election-only instance, or a test).</summary>
    Unavailable,
}
