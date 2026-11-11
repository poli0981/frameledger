// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// One row of a table-valued fact (<see cref="AdvancedUnit.Table"/>) — a driver setting, a memory module.
/// </summary>
/// <param name="Name">What the row is, in the vendor's own words; shown as it is.</param>
/// <param name="Value">Its value, in the vendor's own notation; shown as it is.</param>
/// <param name="Token">A word the App translates, saying something about the row — who set the value, say; or null.</param>
public sealed record AdvancedRow(string Name, string Value, string? Token = null);
