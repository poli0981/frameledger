// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>One session's percentile curve on the Compare overlay: its legend label and the 101 points.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the chart; ScottPlot draws double[]")]
public sealed record CompareCurve(string Label, double[] Xs, double[] Ys);
