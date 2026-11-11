// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Persistence;

/// <summary>
/// What a session stores of advanced metrics beside its series (owner decision D102, schema 0023): one row of
/// <c>session_advanced</c>, and the row of <c>machine_profiles</c> it points to.
/// </summary>
/// <remarks>
/// A session recorded with the option off has none — that absence is how the page knows to say "the option was off
/// when this was recorded" rather than "nothing answered". Every member is JSON in <c>Application.Advanced.AdvancedJson</c>'s form.
/// </remarks>
public sealed record AdvancedSessionRow
{
    /// <summary>The PC's facts, or null when no source had any. Stored once per distinct <see cref="MachineHash"/>.</summary>
    public string? MachineFactsJson { get; init; }

    /// <summary>SHA-256 of <see cref="MachineFactsJson"/>, lower-case hexadecimal; null with it.</summary>
    public string? MachineHash { get; init; }

    /// <summary>The session's own facts and the state of each source while it ran.</summary>
    public required string FactsJson { get; init; }

    /// <summary>Count, mean, median, minimum and maximum of each stored series; null when the session stored none.</summary>
    public string? StatsJson { get; init; }

    /// <summary>What each stored series that is a member of a family is, and how many series were left out.</summary>
    public required string SeriesJson { get; init; }
}
