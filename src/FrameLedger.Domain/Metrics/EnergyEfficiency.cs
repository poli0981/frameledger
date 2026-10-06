// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// <c>03_METRICS</c> §Efficiency (beta.14, D49): application frames per joule of the graphics card's power — frames per
/// second over watts, since a watt is a joule per second.
/// </summary>
/// <remarks>
/// Which frame rate is the caller's to decide, and it is never the displayed one (CLAUDE.md rule 6): a generated frame costs
/// the card a fraction of a rendered one, and counting it would sell frame generation as efficiency. The power is the card's
/// board power as its sensor reports it — not the processor's, not the whole PC's.
/// </remarks>
public static class EnergyEfficiency
{
    /// <summary><paramref name="applicationFps"/> / <paramref name="averageWatts"/>; null when either is missing or not positive.</summary>
    public static double? FramesPerJoule(double? applicationFps, double? averageWatts) =>
        applicationFps is > 0 && averageWatts is > 0 ? applicationFps.Value / averageWatts.Value : null;
}
