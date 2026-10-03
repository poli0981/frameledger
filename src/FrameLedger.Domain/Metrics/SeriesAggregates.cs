// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// <c>03_METRICS</c> §Sensor aggregates: the mean of the non-null samples, their median, minimum and maximum, and
/// <b>N/A — never 0 — when there are none</b>. The median is the linear-interpolation 50th percentile
/// (<see cref="Percentile.Linear"/>): the middle sample of an odd count, the mean of the two middle ones of an even count.
/// </summary>
/// <remarks>The median and the minimum were added in beta.12 (owner request: "median, mean" for the game's memory and the sensors).</remarks>
public sealed record SeriesAggregates
{
    public required double? Average { get; init; }

    public required double? Median { get; init; }

    public required double? Min { get; init; }

    public required double? Max { get; init; }

    /// <summary>Samples that carried a value.</summary>
    public required int Count { get; init; }

    public static SeriesAggregates Of(IEnumerable<double?> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var values = new List<double>();
        double sum = 0;
        foreach (double? s in samples)
        {
            if (s is not double v)
            {
                continue;
            }

            values.Add(v);
            sum += v;
        }

        if (values.Count == 0)
        {
            return new SeriesAggregates { Average = null, Median = null, Min = null, Max = null, Count = 0 };
        }

        values.Sort();
        return new SeriesAggregates
        {
            Average = sum / values.Count,
            Median = Percentile.Linear(values, 0.5),
            Min = values[0],
            Max = values[^1],
            Count = values.Count,
        };
    }
}
