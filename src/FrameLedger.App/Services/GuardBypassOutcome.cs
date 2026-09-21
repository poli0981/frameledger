namespace FrameLedger.App.Services;

/// <summary>What happened when the user moved a game's guard-bypass switch (owner decision 2026-09-21).</summary>
public enum GuardBypassOutcome
{
    /// <summary>The Agent recorded the acknowledgement: the bypass is on for this game.</summary>
    Enabled,

    /// <summary>The Agent withdrew it: the guard is a hard gate for this game again.</summary>
    Disabled,

    /// <summary>The disclosure was closed without both acts. Nothing was sent.</summary>
    Declined,

    /// <summary>No Agent to record it; nothing changed.</summary>
    AgentUnavailable,

    /// <summary>The Agent answered with something other than a written acknowledgement; <c>Detail</c> says what.</summary>
    Failed,
}
