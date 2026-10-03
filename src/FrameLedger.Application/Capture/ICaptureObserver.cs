// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Telemetry;
using FrameLedger.Shared;

namespace FrameLedger.Application.Capture;

/// <summary>
/// What a session's consumer is told while the loop runs — on the loop's own task, after each drain —
/// so the recorder can flush a <c>.partial</c> and drain its telemetry queue without a second party
/// touching the ring (<c>04_CAPTURE</c> §Threading model).
/// </summary>
/// <remarks>
/// The lists on <see cref="CaptureProgress"/> are the loop's own, append-only and live: an observer
/// may read them during the call and remember how far it got, and must not write to them or read them
/// from another thread.
/// </remarks>
public interface ICaptureObserver
{
    /// <summary>The ring was attached; the handshake names the Overlay build and, once presented, the adapter.</summary>
    void Attached(int pid, FlShmHandshake handshake);

    /// <summary>After every drain, including the last one before the ring is released.</summary>
    void Tick(CaptureProgress progress);

    /// <summary>
    /// Which process the game's own memory is read from (beta.12, D43): the pinned one as soon as the loop holds it, an
    /// unpinned hold's processes whenever their set changes, and <see cref="GameProcess.None"/> before the pin is released.
    /// The loop decides what may be opened; the observer only forwards it to the telemetry thread.
    /// </summary>
    void Measuring(GameProcess target)
    {
    }
}
