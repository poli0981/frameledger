// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// What one session knows of advanced metrics beside its series (owner decision D102): the PC's facts as they were
/// when it ran, its own facts, what each series is, and which sources answered.
/// </summary>
/// <param name="MachineFacts">The facts of the PC — stored once per distinct set, in <c>machine_profiles</c>.</param>
/// <param name="SessionFacts">The facts of this session — <c>session_advanced.facts_json</c>.</param>
/// <param name="Series">What each series that is a member of a family is, with the hardware's own label.</param>
/// <param name="Sources">Which sources answered while it ran, and why the others did not.</param>
public sealed record AdvancedSessionData(
    IReadOnlyList<AdvancedFact> MachineFacts,
    IReadOnlyList<AdvancedFact> SessionFacts,
    IReadOnlyList<AdvancedSeriesInfo> Series,
    AdvancedSources Sources);
