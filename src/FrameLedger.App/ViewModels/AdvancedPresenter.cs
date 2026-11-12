// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Turns what the Agent read — or a session stored — into what the Advanced page shows (owner decision D101): facts
/// under their groups, series under their labels, and every catalog entry with whether it is read or why not. Pure:
/// no I/O, no state, so every rule here has a test.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class AdvancedPresenter
{
    /// <summary>Above this many members a family is drawn as its lowest, average and highest — more lines than that cannot be told apart.</summary>
    public const int MaxLines = 8;

    private static readonly Dictionary<string, int> _order = AdvancedMetricCatalog.All
        .Select(static (m, i) => (m.Id, i))
        .ToDictionary(static p => p.Id, static p => p.i, StringComparer.Ordinal);

    /// <summary>
    /// The facts under their groups, in the catalog's order. A fact whose id this build does not know — stored by a
    /// newer one — is shown under its own id, last, rather than dropped.
    /// </summary>
    public static IReadOnlyList<AdvancedFactGroup> Groups(IReadOnlyList<AdvancedFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var groups = new SortedDictionary<int, (string Title, List<(int Order, string Instance, AdvancedFactRow Row)> Rows)>();
        foreach (AdvancedFact fact in facts)
        {
            AdvancedMetric? metric = AdvancedMetricCatalog.Find(fact.Id);
            int group = metric is null ? int.MaxValue : (int)metric.Group;
            if (!groups.TryGetValue(group, out (string Title, List<(int, string, AdvancedFactRow)> Rows) bucket))
            {
                bucket = (metric is null ? Strings.Common_NotAvailable : AdvancedLabels.Group(metric.Group), []);
                groups[group] = bucket;
            }

            string label = AdvancedLabels.Member(metric is null ? fact.Id : AdvancedLabels.Of(metric), fact.Instance);
            AdvancedDetailRow[] details = [.. (fact.Value.Rows ?? []).Select(static r => new AdvancedDetailRow(r.Name, AdvancedLabels.RowValue(r)))];
            var row = new AdvancedFactRow(label, AdvancedLabels.Value(fact.Value, metric?.Unit ?? AdvancedUnit.None), details);
            bucket.Rows.Add((OrderOf(fact.Id), fact.Instance ?? string.Empty, row));
        }

        return [.. groups.Values.Select(static g => new AdvancedFactGroup(
            g.Title,
            [.. g.Rows.OrderBy(static r => r.Order).ThenBy(static r => r.Instance, StringComparer.Ordinal).Select(static r => r.Row)]))];
    }

    /// <summary>
    /// The catalog entry a series belongs to and its label: the family's with the hardware's own name for a member, as
    /// the directory gives it; the entry's for a series that is one. A series nothing names keeps its own name.
    /// </summary>
    public static (AdvancedMetric? Metric, string Label) Resolve(string series, IReadOnlyList<AdvancedSeriesInfo> directory)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(directory);
        foreach (AdvancedSeriesInfo info in directory)
        {
            if (string.Equals(info.Series, series, StringComparison.Ordinal) && AdvancedMetricCatalog.Find(info.MetricId) is { } family)
            {
                return (family, AdvancedLabels.Member(AdvancedLabels.Of(family), info.Label));
            }
        }

        if (!series.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal))
        {
            return (null, series);
        }

        string id = series[AdvancedMetric.SeriesPrefix.Length..];
        if (AdvancedMetricCatalog.Find(id) is { } exact)
        {
            return (exact, AdvancedLabels.Of(exact));
        }

        // A member whose directory entry is gone: the family is the longest id the name starts with.
        AdvancedMetric? owner = null;
        foreach (AdvancedMetric metric in AdvancedMetricCatalog.All)
        {
            if (metric.IsFamily && id.StartsWith(metric.Id + ".", StringComparison.Ordinal) && (owner is null || metric.Id.Length > owner.Id.Length))
            {
                owner = metric;
            }
        }

        return owner is null ? (null, series) : (owner, AdvancedLabels.Member(AdvancedLabels.Of(owner), id[(owner.Id.Length + 1)..]));
    }

    /// <summary>Every series as it reads now, in the catalog's order: its value in its unit and the time it was read at.</summary>
    public static IReadOnlyList<AdvancedLiveRow> Live(AdvancedMachineView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Values
            .Select(pair => (pair.Key, Resolved: Resolve(pair.Key, view.Series), Point: pair.Value))
            .OrderBy(static r => r.Resolved.Metric is null ? int.MaxValue : OrderOf(r.Resolved.Metric.Id))
            .ThenBy(static r => r.Resolved.Label, StringComparer.CurrentCulture)
            .Select(static r => new AdvancedLiveRow(
                r.Resolved.Label,
                AdvancedLabels.Number(r.Point.Value, r.Resolved.Metric?.Unit ?? AdvancedUnit.None),
                r.Point.At.ToLocalTime().ToString("T", CultureInfo.CurrentCulture)))];
    }

    /// <summary>The catalog entries that have at least one of these series, in the catalog's order — what the chart's selector offers.</summary>
    public static IReadOnlyList<AdvancedMetricOption> Metrics(IEnumerable<string> series, IReadOnlyList<AdvancedSeriesInfo> directory)
    {
        ArgumentNullException.ThrowIfNull(series);
        return [.. series
            .Select(s => Resolve(s, directory).Metric)
            .OfType<AdvancedMetric>()
            .DistinctBy(static m => m.Id, StringComparer.Ordinal)
            .OrderBy(static m => OrderOf(m.Id))
            .Select(static m => new AdvancedMetricOption(m.Id, AdvancedLabels.Of(m)))];
    }

    /// <summary>
    /// The lines of one catalog entry: one per series of it — or, above <see cref="MaxLines"/>, the lowest, the
    /// average and the highest across them at each moment, which is what can still be read off a chart.
    /// </summary>
    /// <returns>The lines, and whether they are that band.</returns>
    public static (IReadOnlyList<AdvancedChartLine> Lines, bool Band) Lines(string metricId, IEnumerable<SensorSeries> series, IReadOnlyList<AdvancedSeriesInfo> directory)
    {
        ArgumentNullException.ThrowIfNull(metricId);
        ArgumentNullException.ThrowIfNull(series);
        var members = new List<AdvancedChartLine>();
        foreach (SensorSeries one in series)
        {
            (AdvancedMetric? metric, string label) = Resolve(one.Name, directory);
            if (metric is not null && string.Equals(metric.Id, metricId, StringComparison.Ordinal) && one.Values.Length > 0)
            {
                members.Add(new AdvancedChartLine(label, one.TimesS, one.Values));
            }
        }

        members.Sort(static (a, b) => StringComparer.CurrentCulture.Compare(a.Label, b.Label));
        return members.Count <= MaxLines ? (members, false) : (Band(members), true);
    }

    /// <summary>The statistics a session stored, one row a series, in the catalog's order.</summary>
    public static IReadOnlyList<AdvancedStatRow> Stats(IReadOnlyDictionary<string, SensorSeriesStats> stats, IReadOnlyList<AdvancedSeriesInfo> directory)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return [.. stats
            .Select(pair => (Resolved: Resolve(pair.Key, directory), pair.Value))
            .OrderBy(static r => r.Resolved.Metric is null ? int.MaxValue : OrderOf(r.Resolved.Metric.Id))
            .ThenBy(static r => r.Resolved.Label, StringComparer.CurrentCulture)
            .Select(static r =>
            {
                AdvancedUnit unit = r.Resolved.Metric?.Unit ?? AdvancedUnit.None;
                return new AdvancedStatRow(
                    r.Resolved.Label,
                    r.Value.N.ToString("N0", CultureInfo.CurrentCulture),
                    NumberOrNone(r.Value.Min, unit),
                    NumberOrNone(r.Value.Mean, unit),
                    NumberOrNone(r.Value.Max, unit));
            })];
    }

    /// <summary>
    /// Every entry of the catalog with whether it is read, or why it is not. <paramref name="now"/> is the sources'
    /// state where it is known — the page is live — and null where it is not: then an entry the Agent reads says
    /// when it is read rather than claiming that it is being read.
    /// </summary>
    public static IReadOnlyList<AdvancedSourceRow> Sources(AdvancedSources? now) =>
        [.. AdvancedMetricCatalog.All.Select(m => new AdvancedSourceRow(
            AdvancedLabels.Of(m),
            AdvancedLabels.Scope(m.Scope),
            AdvancedLabels.Source(m.Source),
            StatusOf(m, now),
            m.IsRead))];

    /// <summary>The three sources' states in one sentence.</summary>
    public static string SourcesLine(AdvancedSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        string line = string.Format(
            CultureInfo.CurrentCulture,
            Strings.Advanced_SourcesLine,
            AdvancedLabels.Status(sources.Nvapi),
            AdvancedLabels.Status(sources.Sensors),
            AdvancedLabels.Status(sources.ElevatedSensors));

        // A session's row also says what became of the entries read inside the game; the machine's live state and a
        // row stored before 0.1.0-beta.40 do not, and then the sentence is not there.
        if (sources.InGame is { } inGame)
        {
            line += " " + string.Format(CultureInfo.CurrentCulture, Strings.Advanced_InGameLine, AdvancedLabels.InGame(inGame));
        }

        // The two calls hooked only for Advanced metrics (0.1.0-beta.43): for each, whether it was read, hooked and
        // never made, or not hooked - so that "nothing here" says which of the two it is. A row stored before, and a
        // session not measured inside the game, carry neither.
        if (sources.StreamlineConstants is { } streamline && sources.FsrFrameGeneration is { } frameGeneration)
        {
            line += " " + string.Format(
                CultureInfo.CurrentCulture,
                Strings.Advanced_InGameCallsLine,
                AdvancedLabels.InGameCall(streamline),
                AdvancedLabels.InGameCall(frameGeneration));
        }

        // The two questions about a feature (0.1.0-beta.44), worded for calls a game makes while it starts. A row
        // stored before carries neither.
        if (sources.StreamlineFeatureVersion is { } version && sources.StreamlineFeatureRequirements is { } requirements)
        {
            line += " " + string.Format(
                CultureInfo.CurrentCulture,
                Strings.Advanced_InGameFeatureCallsLine,
                AdvancedLabels.InGameStartupCall(version),
                AdvancedLabels.InGameStartupCall(requirements));
        }

        return line;
    }

    private static string StatusOf(AdvancedMetric metric, AdvancedSources? now)
    {
        if (!metric.IsRead)
        {
            return AdvancedLabels.Reach(metric.Reach);
        }

        if (metric.Reach == AdvancedReach.Game)
        {
            // Not a state of the PC: it is read inside a game being measured, when that game makes the call.
            return Strings.Advanced_Status_InGame;
        }

        AdvancedSourceStatus? state = now is null ? null : (metric.Source, metric.Reach) switch
        {
            (AdvancedSource.Nvapi, _) => now.Nvapi,
            (AdvancedSource.Lhm, AdvancedReach.AgentElevated) => now.ElevatedSensors,
            (AdvancedSource.Lhm, _) => now.Sensors,
            _ => null,
        };
        return state switch
        {
            AdvancedSourceStatus.Reading => Strings.Advanced_Status_Read,
            null or AdvancedSourceStatus.Unknown => Strings.Advanced_Status_Readable,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.Advanced_Status_SourceSilent, AdvancedLabels.Status(state.Value)),
        };
    }

    private static List<AdvancedChartLine> Band(List<AdvancedChartLine> members)
    {
        var at = new SortedDictionary<double, (double Min, double Max, double Sum, int N)>();
        foreach (AdvancedChartLine line in members)
        {
            for (int i = 0; i < line.Xs.Length && i < line.Ys.Length; i++)
            {
                double y = line.Ys[i];
                at[line.Xs[i]] = at.TryGetValue(line.Xs[i], out (double Min, double Max, double Sum, int N) seen)
                    ? (Math.Min(seen.Min, y), Math.Max(seen.Max, y), seen.Sum + y, seen.N + 1)
                    : (y, y, y, 1);
            }
        }

        double[] xs = [.. at.Keys];
        return
        [
            new AdvancedChartLine(Strings.Advanced_Min, xs, [.. at.Values.Select(static v => v.Min)]),
            new AdvancedChartLine(Strings.Advanced_Mean, xs, [.. at.Values.Select(static v => v.Sum / v.N)]),
            new AdvancedChartLine(Strings.Advanced_Max, xs, [.. at.Values.Select(static v => v.Max)]),
        ];
    }

    private static string NumberOrNone(double? value, AdvancedUnit unit) => value is double v ? AdvancedLabels.Number(v, unit) : Strings.Common_NotAvailable;

    private static int OrderOf(string id) => _order.GetValueOrDefault(id, int.MaxValue);
}
