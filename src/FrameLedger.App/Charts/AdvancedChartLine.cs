// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>One line of the Advanced page's chart: its legend text and its points.</summary>
/// <param name="Label">What the line is.</param>
/// <param name="Xs">Seconds from the session's start, or OLE automation dates for a live chart.</param>
/// <param name="Ys">The values, in the unit of the catalog entry.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the chart, as SensorSeries is")]
public sealed record AdvancedChartLine(string Label, double[] Xs, double[] Ys);
