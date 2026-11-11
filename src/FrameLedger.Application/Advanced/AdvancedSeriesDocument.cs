// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// <c>session_advanced.series_json</c>: what each stored series that is a member of a family is, and how many series
/// were left out because the session had more than the most one session stores.
/// </summary>
internal sealed record AdvancedSeriesDocument
{
    public List<AdvancedSeriesInfoDocument> Series { get; init; } = [];

    public int Dropped { get; init; }
}
