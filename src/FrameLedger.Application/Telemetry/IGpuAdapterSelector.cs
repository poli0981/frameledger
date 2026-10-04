// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// A telemetry layer that can be pointed at one graphics adapter (beta.13): L1 reads DXGI's identity and the adapter-wide
/// memory counter for the adapter it is told, by the LUID the Overlay publishes in the handshake at the game's first
/// present. Until then it describes the first hardware adapter in DXGI's high-performance order.
/// </summary>
public interface IGpuAdapterSelector
{
    /// <summary>The adapter being described; null before one is known.</summary>
    GpuAdapterIdentity? Selected { get; }

    /// <summary>Switch to the adapter with this LUID. False — and no change — for 0, or for a LUID it did not list.</summary>
    bool SelectAdapter(ulong luid);
}
