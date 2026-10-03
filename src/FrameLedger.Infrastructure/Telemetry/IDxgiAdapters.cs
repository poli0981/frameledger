// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Telemetry;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>What <see cref="BaselineTelemetrySource"/> enumerates through. <see cref="DxgiAdapters"/> is the real one.</summary>
public interface IDxgiAdapters
{
    /// <summary>
    /// Every adapter DXGI lists, hardware and software alike, in DXGI's high-performance
    /// preference order. Identity only — nothing here is polled, so nothing is kept open.
    /// Throws when the factory itself cannot be created; the source counts that as a fault.
    /// </summary>
    IReadOnlyList<GpuAdapterIdentity> Enumerate();
}
