// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Controls;

/// <summary>One segment of <see cref="FgMixBar"/> (beta.24, D62): a row of the stored mix, as its kind, factor and share.</summary>
/// <param name="Kind">A state, no frame generation, or the time no state claims.</param>
/// <param name="Factor">A state's factor, which picks its colour; null otherwise.</param>
/// <param name="Share">Its share of the session's presenting time (0..1); the bar draws the segments in proportion.</param>
public sealed record FgMixSegment(FgMixKind Kind, double? Factor, double Share);
