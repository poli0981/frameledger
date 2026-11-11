// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// Whether one source of advanced metrics is being read on this PC, or why not — what the page's Sources tab says
/// beside every entry of that source that reads N/A.
/// </summary>
public enum AdvancedSourceStatus
{
    /// <summary>Nobody asked the source yet. The zero a forgetful writer publishes; it reads as "not known", never as "read".</summary>
    Unknown = 0,

    /// <summary>The source answers.</summary>
    Reading = 1,

    /// <summary>The source is not on this PC: no NVIDIA driver, or it lists no graphics card.</summary>
    NotPresent = 2,

    /// <summary>The Agent does not run as administrator (D103).</summary>
    NotElevated = 3,

    /// <summary>The Agent runs as administrator and PawnIO is not installed (D103).</summary>
    PawnIoMissing = 4,

    /// <summary>The source failed twice and is off until the reader is opened again (<c>18_GPU_VENDOR_APIS</c> §Runtime policy).</summary>
    Faulted = 5,
}
