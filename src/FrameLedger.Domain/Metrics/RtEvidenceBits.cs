// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// Mirror of <c>FlRtFlags</c>: every bit means "we OBSERVED this", so 0 says "no RT evidence seen" and
/// <see cref="MeasuredFields.Rt"/> is what says whether anyone looked.
/// </summary>
[Flags]
public enum RtEvidenceBits
{
    None = 0,

    /// <summary>Catches inline RayQuery, which DispatchRays alone misses.</summary>
    AsBuildObserved = 1 << 0,
    DispatchObserved = 1 << 1,

    /// <summary>Latches on: says "created ever", nothing about what is alive now.</summary>
    PsoCreatedEver = 1 << 2,
}
