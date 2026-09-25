using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.AntiCheat;

/// <summary>
/// D33 (owner decision 2026-09-26): the managed rules of the user-mode exception — what a grant needs before the guard is
/// even asked to tolerate a family, and the words its end is recorded in. The guard's own rules (which family is user-mode,
/// what is never excepted, scanning past what it lets through) are native and are not repeated here
/// (<c>19_SAFETY</c> §The user-mode exception).
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here can only narrow.</b> <see cref="CoveredFamily"/> answers null unless every managed condition holds,
/// and a non-null answer is a NAME the guard may still decline — it honours only a family whose every signal is
/// user-mode, and it refuses on anything else it finds. There is no path from this class to an injection that the
/// guard did not pass.
/// </para>
/// </remarks>
public static class UserModeExceptionRules
{
    /// <summary>The owner's threshold: successful Tier-1 sessions of the game before its exception may be granted.</summary>
    /// <remarks>
    /// "Successful" is <c>ISessionRepository.CountSuccessfulHookedAsync</c>'s: hooked, frames recorded, <c>exit_status =
    /// normal</c> — which since beta.8 includes End task's exit code 1 and a user's stop, and never a crash, a safety
    /// unhook, a degraded or an interrupted session.
    /// </remarks>
    public const int RequiredSessions = 2;

    /// <summary>
    /// The family the game's exception covers for a session against <paramref name="observed"/>, or null: the option is
    /// on, the row carries a grant, the grant was made on this executable, and the block on the row is a module, file or
    /// folder finding naming that same family.
    /// </summary>
    /// <param name="record">What the store returned.</param>
    /// <param name="observed">The executable as it is on disk right now.</param>
    /// <param name="optionOn"><c>hooking.usermode_ac_exceptions</c> as read just now; off suspends every exception.</param>
    public static string? CoveredFamily(GameConsentRecord record, ExecutableFingerprint observed, bool optionOn)
    {
        if (!optionOn || !record.IsFromStore || record.Exception is not { } grant || !grant.Covers(observed))
        {
            return null;
        }

        return StoredBlock.Parse(record.BlockedReason) is { IsExceptionable: true } block
               && string.Equals(block.Family, grant.Family, StringComparison.Ordinal)
            ? grant.Family
            : null;
    }

    /// <summary>
    /// Whether <paramref name="verdict"/> — a tolerant pre-scan asked about <paramref name="block"/>'s family — makes the
    /// game eligible, given <paramref name="sessions"/> successful Tier-1 sessions: the guard passed and named that same
    /// family as what it let through, and the evidence reaches <see cref="RequiredSessions"/>.
    /// </summary>
    public static bool IsEligible(StoredBlock? block, AntiCheatVerdict verdict, int sessions) =>
        block is { IsExceptionable: true } b
        && verdict.RanUnderException
        && string.Equals(verdict.Family, b.Family, StringComparison.Ordinal)
        && sessions >= RequiredSessions;
}
