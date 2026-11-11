// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// Everything the advanced reader knows at one tick: the readings of that tick, the PC's facts, the directory of the
/// series that are members of a family, and the state of each source. Immutable — the reader publishes a new one and
/// a consumer keeps whichever it took, so nobody waits on the reader's thread.
/// </summary>
/// <param name="TakenAt">When the tick was read.</param>
/// <param name="Readings">
/// One value per series that was READ on this tick. A series that was not is not here — and not every source is read
/// on every tick: a slow one is read every few. Its last value is not repeated on the ticks between, so whoever
/// shows "the value now" keeps the last one it saw, and whoever stores a series stores a tick with no reading.
/// </param>
/// <param name="MachineFacts">The facts of the PC, read when the reader opened.</param>
/// <param name="Series">Every member of a family this reader has read, with the hardware's own labels — on every tick, read or not.</param>
/// <param name="Sources">Which sources answer, and why the others do not.</param>
public sealed record AdvancedSnapshot(
    DateTimeOffset TakenAt,
    IReadOnlyList<AdvancedReading> Readings,
    IReadOnlyList<AdvancedFact> MachineFacts,
    IReadOnlyList<AdvancedSeriesInfo> Series,
    AdvancedSources Sources);
