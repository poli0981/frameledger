namespace FrameLedger.Application.Recording;

/// <summary>
/// One session, end to end: the port the Agent's orchestrator and console verbs drive (P2 PR-F), so a
/// watcher can be tested against a fake recorder while <see cref="SessionRecorder"/> stays the one implementation.
/// </summary>
public interface ISessionRecorder
{
    /// <summary>Run one session for <paramref name="request"/> and store what it yielded.</summary>
    Task<RecordedSession> RecordAsync(RecordRequest request, CancellationToken ct = default);
}
