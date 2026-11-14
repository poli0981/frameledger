// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Security.Cryptography;
using System.Text;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What a session stores of advanced metrics (owner decision D102): one series per advanced reading its samples
/// carried, aligned to the session's <c>t_ms</c>, and the row that holds its facts, its statistics and its directory.
/// </summary>
/// <remarks>
/// <para>
/// <b>A tick with no reading is NaN, not −1.</b> The fixed series store −1 for a missing tick and their readers drop
/// every negative value; an advanced series may be negative — a −12 V rail — and most of its ticks have no reading at
/// all, since the sensor library is read every fifth. The <c>adv.</c> prefix is how a reader tells which rule applies.
/// </para>
/// <para>
/// <b>Nothing in, nothing out.</b> A session whose samples carry no advanced reading and that was given no context —
/// the option was off — stores no series and no row, and is byte for byte what it was before this existed.
/// </para>
/// </remarks>
public static class AdvancedSessionBuilder
{
    /// <summary>
    /// The most advanced series one session stores. A PC with a card, a processor of thirty-two threads and a board
    /// full of headers has about a hundred and fifty; what is beyond this is counted in the directory, not stored.
    /// </summary>
    public const int MaxSeries = 256;

    /// <summary>The session's advanced series as blobs, and its <c>session_advanced</c> row — nothing when there is neither a series nor data.</summary>
    /// <param name="sensors">The session's samples, in order.</param>
    /// <param name="data">What the session knew of the machine and itself, or null when the option was off.</param>
    /// <param name="codec">How a series is encoded.</param>
    /// <param name="hz">The rate the samples had (<see cref="SessionFinalizer.SampleRate"/>): stored with each series as the fallback for a reader with no <c>t_ms</c>.</param>
    public static (IReadOnlyList<SensorBlob> Blobs, AdvancedSessionRow? Row) Build(IReadOnlyList<TelemetrySample> sensors, AdvancedSessionData? data, ISeriesCodec codec, double hz = 1)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(codec);
        (List<string> names, Dictionary<string, int> indexOf, int dropped) = SeriesOf(sensors);
        if (names.Count == 0 && data is null)
        {
            return ([], null);
        }

        float[][] columns = Columns(sensors, names.Count, indexOf);
        var blobs = new List<SensorBlob>(names.Count);
        var stats = new Dictionary<string, SensorSeriesStats>(StringComparer.Ordinal);
        for (int s = 0; s < names.Count; s++)
        {
            blobs.Add(new SensorBlob { Series = names[s], Hz = hz, Codec = codec.Tag, Data = codec.EncodeSparseFloat32(columns[s]) });
            SeriesAggregates aggregates = SeriesAggregates.Of(columns[s].Select(static v => float.IsNaN(v) ? (double?)null : v));
            if (aggregates.Count > 0)
            {
                stats[names[s]] = SensorSeriesStats.From(aggregates);
            }
        }

        IReadOnlyList<AdvancedFact> machine = AdvancedJson.Storable(data?.MachineFacts ?? []);
        string? machineJson = machine.Count == 0 ? null : AdvancedJson.OfFacts(machine);
        AdvancedSeriesInfo[] directory = [.. (data?.Series ?? []).Where(i => indexOf.ContainsKey(i.Series))];
        return (blobs, new AdvancedSessionRow
        {
            MachineFactsJson = machineJson,
            MachineHash = machineJson is null ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(machineJson))),
            FactsJson = AdvancedJson.OfSession(AdvancedJson.Storable(data?.SessionFacts ?? []), data?.Sources ?? AdvancedSources.Unknown),
            StatsJson = stats.Count == 0 ? null : AdvancedJson.OfStats(stats),
            SeriesJson = AdvancedJson.OfSeries(directory, dropped),
        });
    }

    /// <summary>The series the samples carry, in the order they first appear, up to <see cref="MaxSeries"/>; and how many more there were.</summary>
    private static (List<string> Names, Dictionary<string, int> IndexOf, int Dropped) SeriesOf(IReadOnlyList<TelemetrySample> sensors)
    {
        var names = new List<string>();
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var beyond = new HashSet<string>(StringComparer.Ordinal);
        foreach (TelemetrySample sample in sensors)
        {
            foreach (AdvancedReading reading in sample.Advanced ?? [])
            {
                // Only what carries the prefix is an advanced series: a name without it could be taken for a fixed one.
                if (reading.Series is null || !reading.Series.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal) || indexOf.ContainsKey(reading.Series))
                {
                    continue;
                }

                if (names.Count < MaxSeries)
                {
                    indexOf[reading.Series] = names.Count;
                    names.Add(reading.Series);
                }
                else
                {
                    beyond.Add(reading.Series);
                }
            }
        }

        return (names, indexOf, beyond.Count);
    }

    private static float[][] Columns(IReadOnlyList<TelemetrySample> sensors, int series, Dictionary<string, int> indexOf)
    {
        float[][] columns = new float[series][];
        for (int s = 0; s < series; s++)
        {
            columns[s] = new float[sensors.Count];
            Array.Fill(columns[s], float.NaN);
        }

        for (int i = 0; i < sensors.Count; i++)
        {
            foreach (AdvancedReading reading in sensors[i].Advanced ?? [])
            {
                if (reading.Series is not null && indexOf.TryGetValue(reading.Series, out int s) && double.IsFinite(reading.Value))
                {
                    columns[s][i] = (float)reading.Value;
                }
            }
        }

        return columns;
    }
}
