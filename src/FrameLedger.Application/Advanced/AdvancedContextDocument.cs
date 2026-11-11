// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>An <see cref="AdvancedSessionData"/> as the <c>.partial</c> carries it, so a session that is recovered keeps its facts.</summary>
internal sealed record AdvancedContextDocument
{
    public List<AdvancedFactDocument> Machine { get; init; } = [];

    public List<AdvancedFactDocument> Session { get; init; } = [];

    public List<AdvancedSeriesInfoDocument> Series { get; init; } = [];

    public AdvancedSources? Sources { get; init; }
}
