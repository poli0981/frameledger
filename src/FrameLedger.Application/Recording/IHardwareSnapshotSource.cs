// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What this machine is right now, for <c>hardware_snapshots</c> (<c>06_DATA_MODEL</c> §Hardware change
/// markers): the GPU from DXGI's identity, the rest from the OS. Taken once per session, before the
/// capture, and deduplicated by hash on the way in.
/// </summary>
public interface IHardwareSnapshotSource
{
    HardwareSnapshot Take();
}
