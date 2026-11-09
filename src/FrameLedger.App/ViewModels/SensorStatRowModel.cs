// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One row of the summary's statistics table (beta.12): a series' stored mean, median, minimum and peak as text in its own
/// unit, and how many readings they are over — from <c>sessions.sensor_stats_json</c>, never recomputed.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public sealed record SensorStatRowModel(string Series, string Mean, string Median, string Min, string Max, string Readings)
{
    /// <summary>The row as a screen reader reads it (beta.35, D88), each number with what it is.</summary>
    public override string ToString() =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.A11y_SensorStat_Format, Series, Mean, Median, Min, Max, Readings);
}
