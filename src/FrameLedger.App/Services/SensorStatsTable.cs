// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.Services;

/// <summary>
/// The summary's statistics table (beta.12; owner request "median, mean"): every series the session stored statistics for,
/// in <see cref="SensorSeriesCatalog"/>'s order, each in its own unit — °C, %, W, or memory as Task Manager states it — with
/// the label its chart line carries, so "this game", "whole graphics card" and "whole PC" read the same in both places.
/// </summary>
public static class SensorStatsTable
{
    /// <summary>The rows for one session's stored statistics; empty for a session recorded before beta.12.</summary>
    public static IReadOnlyList<SensorStatRowModel> Rows(IReadOnlyDictionary<string, SensorSeriesStats> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var rows = new List<SensorStatRowModel>();
        foreach ((string name, _) in SensorSeriesCatalog.All)
        {
            if (!stats.TryGetValue(name, out SensorSeriesStats? s) || s.N == 0)
            {
                continue;
            }

            Func<double?, string> format = FormatOf(name);
            rows.Add(new SensorStatRowModel(Label(name), format(s.Mean), format(s.Median), format(s.Min), format(s.Max), s.N.ToString("N0", CultureInfo.CurrentCulture)));
        }

        return rows;
    }

    /// <summary>The label a series carries on the Sensors charts and in this table.</summary>
    public static string Label(string series) => series switch
    {
        SensorSeriesCatalog.GpuTemp => Strings.Sensors_Series_GpuTemp,
        SensorSeriesCatalog.GpuHotspot => Strings.Sensors_Series_GpuHotspot,
        SensorSeriesCatalog.GpuLoad => Strings.Sensors_Series_GpuLoad,
        SensorSeriesCatalog.GpuPower => Strings.Sensors_Series_GpuPower,
        SensorSeriesCatalog.CpuTemp => Strings.Sensors_Series_CpuTemp,
        SensorSeriesCatalog.CpuLoad => Strings.Sensors_Series_CpuLoad,
        SensorSeriesCatalog.VramAdapter => Strings.Sensors_Series_VramAdapter,
        SensorSeriesCatalog.RamSystem => Strings.Sensors_Series_Ram,
        SensorSeriesCatalog.GameVramDedicated => Strings.Sensors_Series_GameVram,
        SensorSeriesCatalog.GameVramShared => Strings.Sensors_Series_GameVramShared,
        SensorSeriesCatalog.GameRamPrivate => Strings.Sensors_Series_GameRam,
        SensorSeriesCatalog.GameRamWorkingSet => Strings.Sensors_Series_GameRamWs,
        SensorSeriesCatalog.GameCommit => Strings.Sensors_Series_GameCommit,
        _ => series,
    };

    private static Func<double?, string> FormatOf(string series) => series switch
    {
        SensorSeriesCatalog.GpuTemp or SensorSeriesCatalog.GpuHotspot or SensorSeriesCatalog.CpuTemp => Formats.Temperature,
        SensorSeriesCatalog.GpuLoad or SensorSeriesCatalog.CpuLoad => Formats.Percent,
        SensorSeriesCatalog.GpuPower => Formats.Power,
        _ => Formats.Memory,
    };
}
