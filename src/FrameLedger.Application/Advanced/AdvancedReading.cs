// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// One value of one advanced series at one tick. A series with no value on the tick has no reading — it is never a
/// zero, and in storage it is NaN (D102).
/// </summary>
/// <param name="Series">The series' name: <see cref="AdvancedMetric.SeriesName"/>, or an instance's name for a family.</param>
/// <param name="Value">The value, in the entry's unit. It may be negative: a voltage rail is.</param>
public readonly record struct AdvancedReading(string Series, double Value);
