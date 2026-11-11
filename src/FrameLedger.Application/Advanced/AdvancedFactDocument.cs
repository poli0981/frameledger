// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>One <see cref="AdvancedFact"/> as it is stored: its id, which member of a family, and its value in whichever one shape it has.</summary>
internal sealed record AdvancedFactDocument
{
    public string Id { get; init; } = string.Empty;

    public string? Instance { get; init; }

    public double? Number { get; init; }

    public string? Text { get; init; }

    public List<string>? Tokens { get; init; }

    public List<AdvancedRowDocument>? Rows { get; init; }
}
