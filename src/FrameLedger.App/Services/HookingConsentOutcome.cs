namespace FrameLedger.App.Services;

/// <summary>How a request to enable or disable hooking for one game ended.</summary>
public enum HookingConsentOutcome
{
    /// <summary>The Agent stamped the consent (<c>HookEnabledAck.enabled = true</c>).</summary>
    Enabled,

    /// <summary>The Agent revoked it.</summary>
    Disabled,

    /// <summary>The user closed the dialog without the acknowledgement; nothing was sent.</summary>
    Declined,

    /// <summary>The Agent's pre-scan refused (<c>Refused</c>): a block is on the row, and there is no way past it (<c>19_SAFETY</c> §What a finding does to the game).</summary>
    Refused,

    /// <summary>The Agent stamps against another version of the disclosure than this app shows; the dialog was not opened (D14).</summary>
    VersionMismatch,

    /// <summary>No connected Agent; nothing was sent.</summary>
    AgentUnavailable,

    /// <summary>The Agent answered but did not stamp (a store outcome other than <c>Written</c>, an <c>Error</c>, a timeout); <c>Detail</c> says which.</summary>
    Failed,
}
