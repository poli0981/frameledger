// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// A telemetry layer that reads its own GPU rather than one named by LUID (beta.13) — LibreHardwareMonitor's GPU node,
/// NVAPI's physical GPU — told the PCI vendor of the adapter the game presents on, so it describes that vendor's GPU or
/// nothing. A layer whose GPU is not the game's contributes no field: a confident figure from the wrong card is worse
/// than N/A (<c>FlShmHandshake.AdapterLuid</c>'s own rule).
/// </summary>
public interface IGpuVendorFollower
{
    /// <summary>The game's adapter is this PCI vendor's (<c>0x10DE</c> NVIDIA, <c>0x1002</c> AMD, <c>0x8086</c> Intel, or another).</summary>
    void FollowVendor(uint vendorId);
}
