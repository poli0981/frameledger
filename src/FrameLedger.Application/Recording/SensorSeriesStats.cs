// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text.Json;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Recording;

/// <summary>
/// One series' statistics as <c>sessions.sensor_stats_json</c> stores them (beta.12; owner request "median, mean"):
/// how many ticks carried a value, and their mean, median, minimum and maximum — computed once at finalize from the same
/// samples the blob holds, so the summary reads stored values (<c>08_UI</c>: stat cards are never recomputed) and they
/// survive a retention sweep of the raw series.
/// </summary>
/// <param name="N">Ticks that carried a value.</param>
/// <param name="Mean">The mean, or null when <paramref name="N"/> is 0.</param>
/// <param name="Median">The linear-interpolation median (<c>Percentile.Linear</c> at 0.5).</param>
/// <param name="Min">The smallest value.</param>
/// <param name="Max">The largest value.</param>
public sealed record SensorSeriesStats(int N, double? Mean, double? Median, double? Min, double? Max)
{
    /// <summary>The statistics of one series' aggregates.</summary>
    public static SensorSeriesStats From(SeriesAggregates a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new SensorSeriesStats(a.Count, a.Average, a.Median, a.Min, a.Max);
    }

    /// <summary>Every catalogued series that carried at least one value, as the column's JSON; null when none did.</summary>
    public static string? JsonOf(IReadOnlyList<Telemetry.TelemetrySample> sensors)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        var stats = new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal);
        foreach ((string name, Func<Telemetry.TelemetrySample, double?> select) in SensorSeriesCatalog.All)
        {
            SeriesAggregates a = SeriesAggregates.Of(sensors.Select(select));
            if (a.Count > 0)
            {
                stats[name] = From(a);
            }
        }

        return stats.Count == 0 ? null : JsonSerializer.Serialize(stats, RecordingJsonContext.Default.DictionaryStringSensorSeriesStats);
    }

    /// <summary>The column read back; an empty map for null or text this build cannot parse (never a throw into the UI).</summary>
    public static IReadOnlyDictionary<string, SensorSeriesStats> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize(json, RecordingJsonContext.Default.DictionaryStringSensorSeriesStats)
                ?? new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal);
        }
    }
}
