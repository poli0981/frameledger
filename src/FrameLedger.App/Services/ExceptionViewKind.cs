namespace FrameLedger.App.Services;

/// <summary>D33: the states a blocked game's user-mode exception is shown in (<see cref="ExceptionView"/>).</summary>
/// <remarks>
/// D38 (2026-10-03) removed <c>TooFewSessions</c>: a grant needs no session any more, so "not yet" is no longer a state —
/// a game is eligible, being checked, or not eligible for a reason that says whether it can change.
/// </remarks>
public enum ExceptionViewKind
{
    /// <summary>An exception is on the row (in its trial or past it).</summary>
    Granted,

    /// <summary>The Agent found the game eligible; no exception has been made.</summary>
    Eligible,

    /// <summary>The Agent has not answered about this block yet (the option was just turned on, or the block changed).</summary>
    Checking,

    /// <summary>Not eligible, for the reason in the text.</summary>
    NotEligible,
}
