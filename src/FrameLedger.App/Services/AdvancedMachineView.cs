// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Advanced;

namespace FrameLedger.App.Services;

/// <summary>
/// "This PC, now" as the App last learnt it from the Agent (owner decision D101). Immutable: the feed publishes a new
/// one after each request and a page keeps whichever it took.
/// </summary>
/// <param name="State">What the last request came to.</param>
/// <param name="TakenAt">The Agent's tick the newest values are of; null before the first snapshot.</param>
/// <param name="Facts">The PC's facts, as the ledger would store them.</param>
/// <param name="Values">
/// The last value seen of each series, with the tick it was read on. A slow source is read every few ticks and its
/// value is not repeated between, so "the value now" is the last one seen — and its age is part of it.
/// </param>
/// <param name="Series">The members of the catalog's families the reader has met, with the hardware's own labels.</param>
/// <param name="Sources">Which sources answer, and why the others do not.</param>
public sealed record AdvancedMachineView(
    AdvancedFeedState State,
    DateTimeOffset? TakenAt,
    IReadOnlyList<AdvancedFact> Facts,
    IReadOnlyDictionary<string, AdvancedLivePoint> Values,
    IReadOnlyList<AdvancedSeriesInfo> Series,
    AdvancedSources Sources)
{
    /// <summary>Nothing asked, nothing known.</summary>
    public static AdvancedMachineView Stopped { get; } = new(
        AdvancedFeedState.Stopped,
        TakenAt: null,
        [],
        new Dictionary<string, AdvancedLivePoint>(StringComparer.Ordinal),
        [],
        AdvancedSources.Unknown);
}
