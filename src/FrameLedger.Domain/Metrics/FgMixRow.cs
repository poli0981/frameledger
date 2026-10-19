// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>One row of <see cref="FgMix"/>.</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Factor">A state's pooled <c>Σ displayed ÷ Σ tokens</c>; null for the other kinds.</param>
/// <param name="NativeFps"><c>Σ tokens ÷ seconds</c> over the row's windows; null for <see cref="FgMixKind.Unclassified"/>.</param>
/// <param name="DisplayedFps"><c>Σ displayed ÷ seconds</c>; null for <see cref="FgMixKind.Unclassified"/>.</param>
/// <param name="Seconds">The time the row's windows cover.</param>
/// <param name="Share">That time over the time of every window — the rows' shares add up to one.</param>
/// <param name="Windows">How many windows the row holds.</param>
public sealed record FgMixRow(FgMixKind Kind, double? Factor, double? NativeFps, double? DisplayedFps, double Seconds, double Share, int Windows);
