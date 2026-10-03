// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
