using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Recording;

/// <summary>
/// D33 (owner decision 2026-09-26): what a session under a game's user-mode exception does to the exception when it ends
/// badly (<c>19_SAFETY</c> §The user-mode exception, What ends it). A finding about the game at its start or at the 30 s
/// re-scan, a safety unhook, the Overlay's own stop on another anti-cheat's module, and a crash while hooked each end it —
/// the evidence the exception rests on said "this game is safe to measure", and each of those says otherwise.
/// </summary>
/// <remarks>
/// <para>
/// <b>It only ever ends an exception.</b> Nothing here grants one or clears a block; the end turns the game's hooking off
/// while its block stands and keeps the consent stamp, as a block keeps it. The user may grant it again once the sweep
/// finds the game eligible again, through the disclosure.
/// </para>
/// <para>
/// A session that ran under no exception is left alone, and so is an ordinary end under one — the game exited, the user
/// stopped it, End task (beta.8's <c>normal</c>), a refusal that found nothing about the game.
/// </para>
/// </remarks>
public sealed class UserModeExceptionLapsePolicy
{
    private readonly IGameConsentStore _consent;

    public UserModeExceptionLapsePolicy(IGameConsentStore consent) =>
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));

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

    /// <summary>End the exception when <see cref="LapseOf"/> says so; the reason, when it was ended.</summary>
    public async ValueTask<string?> ApplyAsync(string normalisedExePath, CaptureOutcome outcome, ExitStatus exit, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);
        string? lapse = LapseOf(outcome, exit);
        if (lapse is null)
        {
            return null;
        }

        return await _consent.RevokeAntiCheatExceptionAsync(normalisedExePath, lapse, ct).ConfigureAwait(false) == ConsentWriteOutcome.Written
            ? lapse
            : null;
    }
}
