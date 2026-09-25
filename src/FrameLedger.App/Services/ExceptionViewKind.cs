namespace FrameLedger.App.Services;

/// <summary>D33: the states a blocked game's user-mode exception is shown in (<see cref="ExceptionView"/>).</summary>
public enum ExceptionViewKind
{
    /// <summary>An exception is on the row.</summary>
    Granted,

    /// <summary>The Agent found the game eligible; no exception has been made.</summary>
    Eligible,

    /// <summary>The guard would let the family through, and the game has fewer than two successful hooked sessions.</summary>
    TooFewSessions,

    /// <summary>The Agent has not answered about this block yet (the option was just turned on, or the block changed).</summary>
    Checking,

    /// <summary>Not eligible, for the reason in the text.</summary>
    NotEligible,
}
