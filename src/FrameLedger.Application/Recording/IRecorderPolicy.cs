namespace FrameLedger.Application.Recording;

/// <summary>
/// Resolves the options one session runs under, at that session's start. D16: the Agent re-reads its settings
/// keys at each session start rather than being told they changed, so the recorder asks this once per
/// <see cref="SessionRecorder.RecordAsync"/> and the answer is fixed for the session's life.
/// </summary>
public interface IRecorderPolicy
{
    /// <summary>The options for the session about to start, derived from <paramref name="baseline"/> (the composition's).</summary>
    ValueTask<RecorderOptions> ResolveAsync(RecorderOptions baseline, CancellationToken ct = default);
}
