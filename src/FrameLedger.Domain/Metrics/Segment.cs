// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>One contiguous run of presents with one output size, on one stream.</summary>
public sealed record Segment
{
    public required uint SwapchainId { get; init; }

    public required ushort OutputW { get; init; }

    public required ushort OutputH { get; init; }

    public required IReadOnlyList<FrameSample> Samples { get; init; }
}
