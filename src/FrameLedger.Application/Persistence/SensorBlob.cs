// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Persistence;

/// <summary>One <c>sensor_blobs</c> row.</summary>
public sealed record SensorBlob
{
    public required string Series { get; init; }

    public required double Hz { get; init; }

    public required string Codec { get; init; }

    public required ReadOnlyMemory<byte> Data { get; init; }
}
