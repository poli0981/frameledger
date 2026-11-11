// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// The one reader of advanced metrics in the Agent (owner decisions D101 and D107): shared by every session and by
/// the page, running only while somebody holds it, and never read on a caller's thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one, and why shared.</b> The Agent records several sessions at once, each with a telemetry poller of its
/// own; a reader per session, plus one for the page, would open the motherboard's sensor chip as many times over.
/// One reader on a thread of its own serves them all, and a slow or stuck sensor holds up that thread and nothing
/// else — not a session's 1 Hz samples, and not the pipe.
/// </para>
/// <para>
/// <b>Nothing is read while nobody holds it</b> (D107): the first <see cref="Acquire"/> opens the sources, and some
/// seconds after the last holder lets go they are closed again. A PC idling with FrameLedger in the tray reads
/// no sensor for this (NFR-2).
/// </para>
/// </remarks>
public interface IAdvancedHardware
{
    /// <summary>
    /// Keeps the reader running until the returned handle is disposed. Returns at once; the first snapshot follows
    /// when the sources have opened.
    /// </summary>
    IDisposable Acquire();

    /// <summary>The last tick, without waiting; null while the reader is not running or has not read yet.</summary>
    AdvancedSnapshot? Latest { get; }

    /// <summary>
    /// The facts of one session, read now, on the caller's thread — it can take some hundred milliseconds, so a
    /// session asks beside its launch and never on a sampling loop. Empty while the reader is not running.
    /// </summary>
    IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target);
}
