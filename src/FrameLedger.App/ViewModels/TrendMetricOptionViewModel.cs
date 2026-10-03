// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Charts;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One metric the Trend can draw beside the selected one (beta.11, owner decision D39: several metrics at once). Disabled
/// when it is the selected metric itself, or when two units are already on the chart and it is in neither.
/// </summary>
public sealed partial class TrendMetricOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled = true;

    public TrendMetricOptionViewModel(TrendMetric metric, string label, string unit)
    {
        Metric = metric;
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
    }

    public TrendMetric Metric { get; }

    public string Label { get; }

    /// <summary>The axis it is drawn against (<see cref="TrendSeriesBuilder.UnitOf"/>).</summary>
    public string Unit { get; }
}
