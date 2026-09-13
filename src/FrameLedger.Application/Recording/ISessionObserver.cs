using FrameLedger.Application.Capture;
using FrameLedger.Application.Telemetry;
using FrameLedger.Shared;

namespace FrameLedger.Application.Recording;

/// <summary>
/// The recorder's second listener (P3 PR-1): what a session does, as it does it, for something that is not the
/// <c>.partial</c> — today the pipe's event publisher. Every call is made on the session's own task, after the
/// recorder has done its own work for that step (<c>04_CAPTURE</c> §Threading model), and the lists handed over
/// are the loop's own, valid for the duration of the call and not to be kept.
/// </summary>
/// <remarks>
/// An implementation must not throw: a throw here would end the session for a reason that has nothing to do
/// with the target. It must not block either — it runs between two drains of a ring that holds ~16 s.
/// </remarks>
public interface ISessionObserver
{
    /// <summary>The row's identity exists; nothing is attached yet.</summary>
    void Started(SessionStartedInfo info);

    /// <summary>The ring is attached: Tier 1 from here.</summary>
    void Attached(Guid sessionGuid, int pid, FlShmHandshake handshake);

    /// <summary>After every drain, with the telemetry samples drained on this same tick (possibly none).</summary>
    void Tick(Guid sessionGuid, CaptureProgress progress, IReadOnlyList<TelemetrySample> drained);

    /// <summary>Finalized — stored or discarded.</summary>
    void Ended(RecordedSession session);

    /// <summary>The session's task threw; the <c>.partial</c> stays for recovery and no row was written.</summary>
    void Faulted(Guid sessionGuid, Exception exception);
}
