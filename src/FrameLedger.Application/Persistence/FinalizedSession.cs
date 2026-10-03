// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Persistence;

/// <summary>Everything one finalized session writes, in one transaction.</summary>
public sealed record FinalizedSession
{
    public required SessionRow Row { get; init; }

    public IReadOnlyList<SegmentRow> Segments { get; init; } = [];

    public FrameBlobs? Frames { get; init; }

    public IReadOnlyList<SensorBlob> Sensors { get; init; } = [];
}
