namespace FrameLedger.App.Services;

/// <summary>D33: how a request to grant or withdraw one game's user-mode exception ended.</summary>
public enum AntiCheatExceptionOutcome
{
    /// <summary>The Agent wrote the grant (<c>AntiCheatExceptionAck.granted = true</c>). Hooking is still off until FR-2.1.</summary>
    Granted,

    /// <summary>The exception is not in force any more (<c>granted = false</c> on a withdrawal).</summary>
    Withdrawn,

    /// <summary>The user closed the disclosure without accepting; nothing was sent.</summary>
    Declined,

    /// <summary>The Agent's own facts did not make the game eligible (<c>Refused</c>).</summary>
    Refused,

    /// <summary>The Agent grants against another version of the disclosure than this app shows; the dialog was not opened.</summary>
    VersionMismatch,

    /// <summary>The option is off in Settings (<c>Error ExceptionsOff</c>); nothing was granted.</summary>
    OptionOff,

    /// <summary>No connected Agent; nothing was sent.</summary>
    AgentUnavailable,

    /// <summary>The Agent answered and did not grant (a store outcome, an <c>Error</c>, a timeout); <c>Detail</c> says which.</summary>
    Failed,
}
