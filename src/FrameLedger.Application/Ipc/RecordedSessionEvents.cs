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

    public static void PublishAll(IIpcEventPublisher pipe, RecordedSession session, SessionStartedInfo info)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(info);

        CaptureOutcome o = session.Outcome;
        Guid guid = session.SessionGuid;
        switch (Classify(o.Reason))
        {
            case Kind.Refused:
                pipe.Publish(IpcMessageType.CaptureRefused,
                    new CaptureRefusedEvent(info.GameId, info.GameName, o.Reason.ToString(), NullIfEmpty(o.Verdict.Family), NullIfEmpty(o.Verdict.Signal), o.HookingTurnedOff));
                break;
            case Kind.SafetyUnhook:
                pipe.Publish(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(guid, NullIfEmpty(o.Verdict.Family), NullIfEmpty(o.Verdict.Signal), o.HookingTurnedOff));
                break;
            case Kind.Degraded:
                pipe.Publish(IpcMessageType.CaptureDegraded, new CaptureDegradedEvent(guid, From: 1, To: 2, o.Reason.ToString()));
                break;
            case Kind.AttachError:
                pipe.Publish(IpcMessageType.CaptureError,
                    new CaptureErrorEvent(guid, RingCode(o.AttachRefusal), $"the ring could not be attached: {o.AttachRefusal}"));
                break;
            case Kind.TargetError:
                pipe.Publish(IpcMessageType.CaptureError, new CaptureErrorEvent(guid, CaptureErrorCode.InjectFailed, o.Reason.ToString()));
                break;
            default:
                break;
        }

        pipe.Publish(IpcMessageType.SessionCompleted, new SessionCompletedEvent(
            guid,
            session.Finalize.SessionId,
            Vocabulary.ExitStatusText(session.ExitStatus),
            (int)session.Row.Tier,
            FinalizeText(session.Finalize.Status),
            o.Reason.ToString()));
    }

    public static string FinalizeText(FinalizeStatus status) => status switch
    {
        FinalizeStatus.Saved => "saved",
        FinalizeStatus.Discarded => "discarded",
        FinalizeStatus.AlreadyStored => "already_stored",
        _ => status.ToString(),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
