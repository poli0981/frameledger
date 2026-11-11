// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// One fact that was read: the catalog entry it is the value of, which member of a family it is, and the value.
/// </summary>
/// <param name="Id">An <see cref="AdvancedMetric.Id"/> whose scope is a fact.</param>
/// <param name="Instance">
/// For a family — a set of facts per display — which one: <c>1</c>, <c>2</c>… in the order the source listed them.
/// An ordinal on purpose: the source's own handle for a display changes from boot to boot and identifies nothing
/// worth storing (D104). Null for an entry that is one value.
/// </param>
/// <param name="Value">The value.</param>
public sealed record AdvancedFact(string Id, string? Instance, AdvancedValue Value);
