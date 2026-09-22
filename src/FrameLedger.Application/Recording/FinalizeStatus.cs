namespace FrameLedger.Application.Recording;

public enum FinalizeStatus
{
    /// <summary>Row, segments and blobs are in the ledger, in one transaction.</summary>
    Saved = 0,

    /// <summary>Shorter than the minimum session length: dropped, log only (<c>04_CAPTURE</c> §Discard rule).</summary>
    Discarded,

    /// <summary>A session with this guid was already stored — recovery ran after a finalize that did land.</summary>
    AlreadyStored,

    /// <summary>
    /// No <c>games</c> row owns the session any more (2026-09-23): its entry was removed with its sessions while it ran,
    /// and no row holds its executable's path either. Nothing is written; the caller deletes the <c>.partial</c> as it does
    /// for <see cref="Discarded"/>. Until this value existed the insert died on the foreign key and the session FAULTED.
    /// </summary>
    GameRemoved,
}
