namespace FrameLedger.Application.AntiCheat;

/// <summary>
/// D33: why a user-mode exception ended — <c>games.ac_exception_lapsed_reason</c>, one of these words, which the App
/// says in the user's language (<c>19_SAFETY</c> §The user-mode exception, What ends it).
/// </summary>
public static class UserModeExceptionLapse
{
    /// <summary>The user withdrew it.</summary>
    public const string Withdrawn = "Withdrawn";

    /// <summary>The executable is not the one the grant was made on: a game update, or <i>Change executable</i>.</summary>
    public const string ExecutableChanged = "ExecutableChanged";

    /// <summary>The guard no longer lets the game through under it: a new finding, a <c>.sys</c>, the family turned kernel-level, too few sessions.</summary>
    public const string NoLongerEligible = "NoLongerEligible";

    /// <summary>A finding about the game at a session's start, at the 30 s re-scan or when hooking was turned on.</summary>
    public const string NewFinding = "NewFinding";

    /// <summary>A session under it was unhooked for safety by the 30 s re-scan.</summary>
    public const string SafetyUnhook = "SafetyUnhook";

    /// <summary>The Overlay stopped observing on its own: another anti-cheat's module loaded in the game.</summary>
    public const string OverlayStopped = "OverlayStopped";

    /// <summary>A session under it ended in a crash.</summary>
    public const string SessionCrashed = "SessionCrashed";
}
