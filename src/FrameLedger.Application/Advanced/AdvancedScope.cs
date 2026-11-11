// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// What an advanced metric belongs to (owner decision D101, <c>docs/18_GPU_VENDOR_APIS.md</c> §Advanced metrics): the
/// machine, or one game session. It decides where the value is stored and on which tab it is shown.
/// </summary>
public enum AdvancedScope
{
    /// <summary>Not stated. No entry of the catalog carries it.</summary>
    None = 0,

    /// <summary>A fact about the PC that no session changes: stored once in <c>machine_profiles</c>, which a session points to.</summary>
    MachineFact = 1,

    /// <summary>A fact about one session — the game's display, the game's driver profile: <c>session_advanced.facts_json</c>.</summary>
    SessionFact = 2,

    /// <summary>
    /// A value sampled over time. During a session it is stored with that session (<c>sensor_blobs</c>, under
    /// <see cref="AdvancedMetric.SeriesName"/>); with no session it is shown while the page is open and stored nowhere.
    /// </summary>
    Series = 3,
}
