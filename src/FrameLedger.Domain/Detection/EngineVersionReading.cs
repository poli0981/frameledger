// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>An engine version and the witness it rests on (<see cref="EngineVersionSource"/>); both null when nothing was established.</summary>
/// <param name="Version">The version as the product shows it (<c>5.4.4</c>, <c>5.1</c>), or null.</param>
/// <param name="Source">An <see cref="EngineVersionSource"/> id, or null with a null version.</param>
public readonly record struct EngineVersionReading(string? Version, string? Source)
{
    /// <summary>Nothing established: the stored value, if any, stays (detection never erases).</summary>
    public static EngineVersionReading None => default;
}
