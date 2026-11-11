// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary><c>session_advanced.facts_json</c>: one session's facts and the state of each source while it ran.</summary>
internal sealed record AdvancedSessionDocument
{
    public List<AdvancedFactDocument> Facts { get; init; } = [];

    public AdvancedSources? Sources { get; init; }
}
