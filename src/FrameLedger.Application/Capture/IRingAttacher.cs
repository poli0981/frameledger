// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Shared;

namespace FrameLedger.Application.Capture;

/// <summary>
/// One attempt to open the ring a pid publishes. The <c>Infrastructure</c> adapter wraps
/// <c>ShmRingReader.TryAttach</c> with the Agent's own build id; the session retries only
/// <see cref="ShmAttachRefusal.Incomplete"/>, on its own clock.
/// </summary>
public interface IRingAttacher
{
    /// <summary>A sink the caller now owns, or null with the refusal that stands.</summary>
    (ICaptureSink? Sink, ShmAttachRefusal Refusal) TryAttach(int pid);
}
