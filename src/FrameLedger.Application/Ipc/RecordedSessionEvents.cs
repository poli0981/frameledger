using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// What a finished session tells the UI (<c>07_IPC</c> §Messages, the Agent → UI rows): one safety or error
/// event when the loop's reason warrants one, then always <c>SessionCompleted</c>. The classification is a
/// table over <see cref="SessionEndReason"/>, so the UI's "never a toast" rule (<c>08_UI</c> §Safety events)
/// is decided here once rather than by every consumer.
/// </summary>
public static class RecordedSessionEvents
{
    /// <summary>The safety/error event a reason maps to, or nothing (a Tier-2 row that needs no explaining, or a normal end).</summary>
    public enum Kind
    {
        None = 0,

        /// <summary>The gate or the guard refused before injection: <c>CaptureRefused</c>.</summary>
        Refused,

        /// <summary>Our own guard fired mid-session: <c>SafetyUnhook</c>.</summary>
        SafetyUnhook,

        /// <summary>Measurement stopped mid-session for a reason that is not the guard's: <c>CaptureDegraded</c>.</summary>
        Degraded,

        /// <summary>The ring could not be attached: <c>CaptureError</c> with a ring code.</summary>
        AttachError,

        /// <summary>The target could not be reached at all: <c>CaptureError</c> <c>InjectFailed</c>.</summary>
        TargetError,
    }

    public static Kind Classify(SessionEndReason reason) => reason switch
    {
        SessionEndReason.RefusedByGuard or SessionEndReason.RefusedPreviouslyBlocked
            or SessionEndReason.PreScanCouldNotVerify or SessionEndReason.RefusedKillSwitch
            // A persistent notice that says why, not a toast with a code: the user enabled hooking, started the game
            // and would otherwise see nothing at all.
            or SessionEndReason.TargetUnreadable => Kind.Refused,
        SessionEndReason.SafetyUnhook => Kind.SafetyUnhook,
        SessionEndReason.SupervisionLost or SessionEndReason.WriterSelfDisabled or SessionEndReason.WriterStoppedBlocklisted
            or SessionEndReason.WriterNeverInstalledHooks or SessionEndReason.SupervisionFaulted
            or SessionEndReason.KillSwitchEngaged => Kind.Degraded,
        SessionEndReason.AttachRefused => Kind.AttachError,
        SessionEndReason.LaunchCannotStart or SessionEndReason.TargetCannotBePinned or SessionEndReason.ExecutableUnreadable
            or SessionEndReason.TargetAmbiguous or SessionEndReason.TargetNotRunning => Kind.TargetError,
        _ => Kind.None,
    };

    /// <summary>A layout, record-size or build-id refusal is the "restart the game after an update" case; the rest is the ring itself.</summary>
    public static string RingCode(ShmAttachRefusal refusal) => refusal switch
    {
        ShmAttachRefusal.LayoutVersionMismatch or ShmAttachRefusal.RecordSizeMismatch or ShmAttachRefusal.BuildIdMismatch
            => CaptureErrorCode.RingVersionMismatch,
        _ => CaptureErrorCode.AttachRefused,
    };

    /// <summary>
    /// A finished session's events: its reason's safety/error event — unless <paramref name="reasonAlreadyPublished"/>, because
    /// a held session said it when its hold began (2026-09-23) — then always <c>SessionCompleted</c>, naming the entry the
    /// session was stored under.
    /// </summary>
    public static void PublishAll(IIpcEventPublisher pipe, RecordedSession session, SessionStartedInfo info, bool reasonAlreadyPublished = false)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(info);

        if (!reasonAlreadyPublished)
        {
            PublishReason(pipe, session.SessionGuid, session.Outcome, info);
        }

        CaptureOutcome o = session.Outcome;
        pipe.Publish(IpcMessageType.SessionCompleted, new SessionCompletedEvent(
            session.SessionGuid,
            session.Finalize.SessionId,
            Vocabulary.ExitStatusText(session.ExitStatus),
            (int)session.Row.Tier,
            FinalizeText(session.Finalize.Status),
            o.Reason.ToString(),
            session.Finalize.GameId ?? info.GameId,
            info.GameName));
    }

    /// <summary>
    /// The one safety or error event <paramref name="outcome"/>'s reason warrants, or nothing. At the end of a session, or —
    /// for a session held unhooked (2026-09-23) — the moment its hold begins, so a refusal is said while the game runs and
    /// not when it exits.
    /// </summary>
    public static void PublishReason(IIpcEventPublisher pipe, Guid sessionGuid, CaptureOutcome outcome, SessionStartedInfo info)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(info);

        CaptureOutcome o = outcome;
        switch (Classify(o.Reason))
        {
            case Kind.Refused:
                pipe.Publish(IpcMessageType.CaptureRefused,
                    new CaptureRefusedEvent(info.GameId, info.GameName, o.Reason.ToString(), NullIfEmpty(o.Verdict.Family), NullIfEmpty(o.Verdict.Signal), o.HookingTurnedOff));
                break;
            case Kind.SafetyUnhook:
                pipe.Publish(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(sessionGuid, NullIfEmpty(o.Verdict.Family), NullIfEmpty(o.Verdict.Signal), o.HookingTurnedOff));
                break;
            case Kind.Degraded:
                pipe.Publish(IpcMessageType.CaptureDegraded, new CaptureDegradedEvent(sessionGuid, From: 1, To: 2, o.Reason.ToString()));
                break;
            case Kind.AttachError:
                pipe.Publish(IpcMessageType.CaptureError,
                    new CaptureErrorEvent(sessionGuid, RingCode(o.AttachRefusal), $"the ring could not be attached: {o.AttachRefusal}"));
                break;
            case Kind.TargetError:
                pipe.Publish(IpcMessageType.CaptureError, new CaptureErrorEvent(sessionGuid, CaptureErrorCode.InjectFailed, o.Reason.ToString()));
                break;
            default:
                break;
        }
    }

    /// <summary><c>SessionCompleted.finalize</c> for a session whose task threw before it could finalize (2026-09-23).</summary>
    public const string FaultedFinalize = "faulted";

    /// <summary>
    /// The words a held session's <c>capture_notes</c> will carry, for the wire (2026-09-23): the loop's reason and, when the
    /// guard said anything but a plain allow, its reason, family and signal.
    /// </summary>
    public static SessionHold HoldOf(CaptureOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        Domain.AntiCheat.AntiCheatVerdict v = outcome.Verdict;
        bool guardSpoke = v.Reason != Domain.AntiCheat.AntiCheatRefusalReason.Allow;
        return new SessionHold(
            outcome.Reason.ToString(),
            guardSpoke ? v.Reason.ToString() : null,
            guardSpoke ? NullIfEmpty(v.Family) : null,
            guardSpoke ? NullIfEmpty(v.Signal) : null,
            outcome.HookingTurnedOff);
    }

    public static string FinalizeText(FinalizeStatus status) => status switch
    {
        FinalizeStatus.Saved => "saved",
        FinalizeStatus.Discarded => "discarded",
        FinalizeStatus.AlreadyStored => "already_stored",
        FinalizeStatus.GameRemoved => "game_removed",
        _ => status.ToString(),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
