using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Recording;

/// <summary>
/// D33 (owner decision 2026-09-26) as amended by D38 (2026-10-03): what a session under a game's user-mode exception does to
/// the exception when it ends (<c>19_SAFETY</c> §The user-mode exception, What ends it). A finding about the game at its
/// start or at the 30 s re-scan, a safety unhook, the Overlay's own stop on another anti-cheat's module, and a crash while
/// hooked each end it — the trust the exception rests on said "this game is safe to measure", and each of those says
/// otherwise. D38: during the grant's trial (its first <see cref="UserModeExceptionRules.TrialSessions"/> successful
/// sessions) a hooked session that recorded no frame or did not end normally ends it too, and any end during the trial
/// marks the game so that no exception can be made for it again.
/// </summary>
/// <remarks>
/// <para>
/// <b>It only ever ends an exception.</b> Nothing here grants one or clears a block; the end turns the game's hooking off
/// while its block stands and keeps the consent stamp, as a block keeps it. After the trial the user may grant it again
/// once the sweep finds the game eligible again, through the disclosure; after an end during the trial, never.
/// </para>
/// <para>
/// A session that ran under no exception is left alone, and so is an ordinary end under one — the game exited, the user
/// stopped it, End task (beta.8's <c>normal</c>), a refusal that found nothing about the game — and a session too short to
/// be stored, which proves nothing either way.
/// </para>
/// </remarks>
public sealed class UserModeExceptionLapsePolicy
{
    private readonly IGameConsentStore _consent;
    private readonly ISessionRepository? _sessions;

    /// <summary>The policy over the store that holds the exception, and the sessions its trial is counted in.</summary>
    /// <param name="consent">Where the exception lives.</param>
    /// <param name="sessions">Where a grant's trial is counted (D38); without it no end is judged against a trial.</param>
    public UserModeExceptionLapsePolicy(IGameConsentStore consent, ISessionRepository? sessions = null)
    {
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _sessions = sessions;
    }

    /// <summary>Why <paramref name="outcome"/> ends the exception it ran under (<see cref="UserModeExceptionLapse"/>), or null.</summary>
    public static string? LapseOf(CaptureOutcome outcome, ExitStatus exit)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.ExceptionFamily is null)
        {
            return null;
        }

        if (outcome.HookingTurnedOff)
        {
            return UserModeExceptionLapse.NewFinding;
        }

        if (outcome.Reason == SessionEndReason.SafetyUnhook)
        {
            return UserModeExceptionLapse.SafetyUnhook;
        }

        if (outcome.Reason == SessionEndReason.WriterStoppedBlocklisted)
        {
            return UserModeExceptionLapse.OverlayStopped;
        }

        // A crash counts only while something of ours was in the process.
        return outcome.AttachRefusal == ShmAttachRefusal.Ok && exit == ExitStatus.Crashed
            ? UserModeExceptionLapse.SessionCrashed
            : null;
    }

    /// <summary>
    /// D38: whether the session in <paramref name="session"/> succeeded as a trial counts it — stored hooked, frames
    /// recorded, <c>normal</c> (<c>ISessionRepository.CountSuccessfulUnderGrantAsync</c>'s rule, for the row just written).
    /// </summary>
    public static bool Succeeded(SessionForTrial session, ExitStatus exit) =>
        session.StoredHooked && exit == ExitStatus.Normal && session.FrameCount > 0;

    /// <summary>
    /// The end <paramref name="outcome"/> brings its exception, given whether the grant is still in its trial: a reason
    /// from <see cref="LapseOf"/>, or <see cref="UserModeExceptionLapse.TrialFailed"/> for a stored hooked session that did
    /// not succeed during the trial, or null. Pure, for the tests and for <see cref="ApplyAsync"/>.
    /// </summary>
    public static string? EndOf(CaptureOutcome outcome, ExitStatus exit, SessionForTrial? session, bool inTrial)
    {
        string? lapse = LapseOf(outcome, exit);
        if (lapse is not null || !inTrial || session is not { } s || outcome.ExceptionFamily is null)
        {
            return lapse;
        }

        return s.StoredHooked && !Succeeded(s, exit) ? UserModeExceptionLapse.TrialFailed : null;
    }

    /// <summary>
    /// End the exception when <see cref="EndOf"/> says so — marking the game when the end came during the trial — and
    /// return the reason when it was ended. <paramref name="session"/> is the row the recorder just wrote (null when it
    /// wrote none it can describe).
    /// </summary>
    public async ValueTask<string?> ApplyAsync(string normalisedExePath, CaptureOutcome outcome, ExitStatus exit, SessionForTrial? session = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.ExceptionFamily is null)
        {
            return null;
        }

        bool inTrial = await InTrialAsync(normalisedExePath, session, exit, ct).ConfigureAwait(false);
        string? lapse = EndOf(outcome, exit, session, inTrial);
        if (lapse is null)
        {
            return null;
        }

        return await _consent.RevokeAntiCheatExceptionAsync(normalisedExePath, lapse, trialFailed: inTrial, ct).ConfigureAwait(false) == ConsentWriteOutcome.Written
            ? lapse
            : null;
    }

    /// <summary>
    /// D38: the grant in force right now is still in its trial — fewer than <see cref="UserModeExceptionRules.TrialSessions"/>
    /// successful sessions under it besides this one. No grant, no count: not in a trial.
    /// </summary>
    private async ValueTask<bool> InTrialAsync(string normalisedExePath, SessionForTrial? session, ExitStatus exit, CancellationToken ct)
    {
        if (_sessions is null || session is not { } s)
        {
            return false;
        }

        GameConsentRecord record = await _consent.FindAsync(normalisedExePath, ct).ConfigureAwait(false);
        if (record.Exception is not { } grant)
        {
            return false;
        }

        int counted = await _sessions.CountSuccessfulUnderGrantAsync(s.GameId, grant.Family, grant.GrantedAt, ct).ConfigureAwait(false);
        int before = Succeeded(s, exit) ? Math.Max(0, counted - 1) : counted;
        return UserModeExceptionRules.InTrial(before);
    }
}
