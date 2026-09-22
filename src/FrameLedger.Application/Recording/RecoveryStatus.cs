namespace FrameLedger.Application.Recording;

public enum RecoveryStatus
{
    /// <summary>Finalized as <c>interrupted</c> from the file's valid prefix.</summary>
    Recovered = 0,

    /// <summary>Shorter than the minimum session length; the file is gone.</summary>
    Discarded,

    /// <summary>The finalize had landed before the process died; only the file was left. Gone now.</summary>
    AlreadyStored,

    /// <summary>No readable header: nothing to recover, and the file is gone.</summary>
    Unreadable,

    /// <summary>Its game is no longer in the ledger and no row holds its executable's path: nothing written, the file is gone.</summary>
    GameRemoved,

    /// <summary>
    /// Recovering it threw. The file is set aside as <c>&lt;guid&gt;.partial.failed</c> — kept for a bug report, never
    /// retried — and recovery goes on to the next file, so one bad file can never stop the watcher from starting.
    /// </summary>
    Failed,
}
