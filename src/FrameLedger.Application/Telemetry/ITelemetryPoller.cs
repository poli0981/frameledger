// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// The 1 Hz telemetry thread (<c>18_GPU_VENDOR_APIS</c> §Runtime policy: <i>"Poll at 1 Hz on a
/// dedicated Agent thread. Never from the game process, never from the Overlay."</i>).
/// </summary>
/// <remarks>
/// <para>
/// One producer, one consumer. The poller's own thread reads the composite source once per
/// interval and queues a <see cref="TelemetrySample"/>; the session loop — the only other
/// party — takes the queue's contents each drain tick through <see cref="Drain"/>. Nothing
/// here touches the ring, and nothing in the session loop waits on a vendor call.
/// </para>
/// <para>
/// <see cref="Descriptor"/> is what <c>sessions.telemetry_source</c> stores: the layers still
/// standing, lowest first, joined with <c>+</c> (<c>l1+lhm+nvapi</c>). ~~It is read at finalize
/// time, so a layer disabled mid-session drops out of the descriptor the session is stored
/// under.~~ **It is read when the session starts** (<c>SessionRecorder</c> puts it in the
/// <c>.partial</c> header, and that is what is stored) — corrected 2026-10-03: the sentence said
/// finalize, which the recorder never did, so a layer disabled mid-session stays in the descriptor.
/// </para>
/// </remarks>
public interface ITelemetryPoller : IDisposable
{
    string Descriptor { get; }

    /// <summary>Samples queued and never drained because the queue was full. A stall figure, like the ring's.</summary>
    long Dropped { get; }

    /// <summary>Start the thread. Idempotent.</summary>
    void Start();

    /// <summary>Move every queued sample into <paramref name="into"/>, oldest first. Returns how many.</summary>
    int Drain(ICollection<TelemetrySample> into);

    /// <summary>
    /// The game process whose own memory the thread reads from now on (beta.12, D43) — told by the capture loop, which owns
    /// what may be opened (<see cref="GameProcess"/>). A poller with no game source ignores it.
    /// </summary>
    void Follow(GameProcess target) { }

    /// <summary>
    /// The adapter the game presents on, by the LUID the Overlay publishes at its first present (beta.13): the layers follow
    /// it. False when the source cannot follow adapters or did not list this one — a poller without layers says false.
    /// </summary>
    bool FollowAdapter(ulong luid) => false;
}
