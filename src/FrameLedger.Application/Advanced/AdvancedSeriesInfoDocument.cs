// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>An <see cref="AdvancedSeriesInfo"/> as it is stored — nullable throughout, for the same reason as <see cref="AdvancedRowDocument"/>.</summary>
internal sealed record AdvancedSeriesInfoDocument
{
    public string? Series { get; init; }

    public string? MetricId { get; init; }

    public string? Label { get; init; }
}
