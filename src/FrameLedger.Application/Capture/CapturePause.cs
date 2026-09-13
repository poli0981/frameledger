namespace FrameLedger.Application.Capture;

/// <summary>
/// The one global pause flag (FR-3.9): flipped by the pipe's <c>PauseCapture</c> / <c>ResumeCapture</c>, read by
/// every running session on its next tick. In memory only — a pause is a moment's intent, not a setting, and
/// an Agent that restarts resumes.
/// </summary>
public sealed class CapturePause : ICapturePauseSource
{
    private volatile bool _paused;

    public bool IsPaused => _paused;

    /// <summary>Returns the new state.</summary>
    public bool Set(bool paused)
    {
        _paused = paused;
        return paused;
    }
}
