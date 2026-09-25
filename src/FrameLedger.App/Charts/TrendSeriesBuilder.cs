using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Charts;

/// <summary>
/// The Trend tab's data (FR-6.3, FR-6.4): one point per session that has the metric, oldest first, with the points that
/// describe only part of their session excluded unless asked for; and a marker wherever two consecutive sessions'
/// hardware snapshots differ in what a reader would want to know about.
/// </summary>
/// <remarks>
/// <b>Which sessions (beta.8).</b> A frame-rate metric is the hooks' and comes from hooked sessions only; a machine metric
/// — temperatures, loads, power, memory — is the telemetry every session records, Tier 2 included, which the trend left
/// out until 2026-09-25 (a game never hooked had no trend at all).
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public static class TrendSeriesBuilder
{
    public static IReadOnlyList<TrendPoint> Points(IReadOnlyList<SessionRow> rows, TrendMetric metric, bool includeMidSessionChanges)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var points = new List<TrendPoint>();
        foreach (SessionRow row in rows.Where(r => Counts(r, metric)).OrderBy(static r => r.StartedAt))
        {
            bool partial = IsPartial(row, metric);
            if (partial && !includeMidSessionChanges)
            {
                continue;
            }

            double? value = ValueOf(row, metric);
            if (value is double v)
            {
                points.Add(new TrendPoint(row.StartedAt, v, row.Id, partial));
            }
        }

        return points;
    }

    /// <summary>How many sessions FR-6.4 leaves out at the default setting.</summary>
    public static int ExcludedCount(IReadOnlyList<SessionRow> rows) => ExcludedCount(rows, TrendMetric.PresentedFps);

    /// <summary>Whether a session can have a point for <paramref name="metric"/>: a frame rate needs the hooks; the machine is every session's.</summary>
    public static bool Counts(SessionRow row, TrendMetric metric)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Tier == CaptureTier.Hooked || IsMachineMetric(metric);
    }

    /// <summary>The metrics that are telemetry, recorded by every session whatever its tier.</summary>
    public static bool IsMachineMetric(TrendMetric metric) => metric is TrendMetric.MaxGpuTemp or TrendMetric.AvgGpuLoad or TrendMetric.AvgGpuPower
        or TrendMetric.AvgCpuLoad or TrendMetric.MaxCpuTemp or TrendMetric.AvgRam;

    public static int ExcludedCount(IReadOnlyList<SessionRow> rows, TrendMetric metric)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Count(r => Counts(r, metric) && IsPartial(r, metric) && ValueOf(r, metric) is not null);
    }

    /// <summary>
    /// A point that describes only part of its session: the settings changed mid-session (FR-6.4), or — for the factor
    /// — the number is the steady state's and covers a share of the session (rule 6: never shown as the session's).
    /// </summary>
    public static bool IsPartial(SessionRow row, TrendMetric metric)
    {
        ArgumentNullException.ThrowIfNull(row);
        // A steady state (CLAUDE.md rule 6, 2026-09-17) is the factor's AND the Native / Displayed rates beside it: all three
        // describe the state the session spent most of its generating time in, never the whole session (beta.8).
        return row.SettingsChangedMidSession
            || (metric is TrendMetric.FgFactor or TrendMetric.NativeFps or TrendMetric.Displayed
                && string.Equals(row.FgFactorScope, "steady", StringComparison.Ordinal));
    }

    /// <summary>The metric's value on a row, or null when that row cannot have it (FR-4.9: never an estimate).</summary>
    public static double? ValueOf(SessionRow row, TrendMetric metric)
    {
        ArgumentNullException.ThrowIfNull(row);
        FpsReadoutModel readout = FpsPresentation.FromRow(row);
        return metric switch
        {
            TrendMetric.NativeFps => readout.Kind is FpsReadoutKind.Generated or FpsReadoutKind.None ? readout.Native : null,
            TrendMetric.PresentedFps => readout.Kind switch
            {
                // A measured none: the presents are the application's frames, so the rate is both.
                FpsReadoutKind.None => readout.Native,
                FpsReadoutKind.Presented or FpsReadoutKind.IdentifiedUncounted => readout.Presented,
                _ => null,
            },
            TrendMetric.Displayed => readout.Kind == FpsReadoutKind.Generated ? readout.Displayed : null,
            TrendMetric.P1Low => row.P1LowFps,
            TrendMetric.P01Low => row.P01LowFps,
            TrendMetric.MaxGpuTemp => row.MaxGpuTemp,
            TrendMetric.AvgGpuLoad => row.AvgGpuLoad,
            TrendMetric.AvgGpuPower => row.AvgGpuPowerW,
            TrendMetric.MaxVramProcess => row.VramProcMaxMb,
            TrendMetric.AvgCpuLoad => row.AvgCpuLoad,
            TrendMetric.MaxCpuTemp => row.MaxCpuTemp,
            TrendMetric.AvgRam => row.AvgRamMb,
            TrendMetric.FgFactor => readout.Kind == FpsReadoutKind.Generated ? readout.Factor : null,
            _ => null,
        };
    }

    /// <summary>
    /// One marker per day that changed (beta.8): every difference found on that day in one label, line after line — two
    /// labels at one position were drawn over each other.
    /// </summary>
    public static IReadOnlyList<HardwareChange> MergedByDay(IReadOnlyList<HardwareChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return [.. changes.GroupBy(static c => c.At.LocalDateTime.Date)
            .Select(static g => new HardwareChange(g.First().At, string.Join(Environment.NewLine, g.Select(static c => c.Text).Distinct(StringComparer.Ordinal))))];
    }

    /// <summary>Markers between consecutive sessions (oldest first) whose snapshots differ; <paramref name="snapshotOf"/> resolves a row's snapshot id.</summary>
    public static IReadOnlyList<HardwareChange> Changes(IReadOnlyList<SessionRow> rows, Func<long, HardwareSnapshot?> snapshotOf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(snapshotOf);
        var changes = new List<HardwareChange>();
        SessionRow? previous = null;
        foreach (SessionRow row in rows.OrderBy(static r => r.StartedAt))
        {
            if (previous is not null && previous.SnapshotId != row.SnapshotId)
            {
                HardwareSnapshot? before = snapshotOf(previous.SnapshotId);
                HardwareSnapshot? after = snapshotOf(row.SnapshotId);
                if (before is not null && after is not null)
                {
                    changes.AddRange(Describe(before, after).Select(text => new HardwareChange(row.StartedAt, text)));
                }
            }

            previous = row;
        }

        return changes;
    }

    /// <summary>The fields a trend reader cares about, each as "label: before → after" when it changed.</summary>
    public static IReadOnlyList<string> Describe(HardwareSnapshot before, HardwareSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var texts = new List<string>();
        Add(texts, Strings.Trend_Change_GpuDriver, before.GpuDriver, after.GpuDriver);
        Add(texts, Strings.Trend_Change_Gpu, before.GpuName, after.GpuName);
        Add(texts, Strings.Trend_Change_Cpu, before.CpuName, after.CpuName);
        Add(texts, Strings.Trend_Change_Display, Display(before), Display(after));
        Add(texts, Strings.Trend_Change_Os, before.OsBuild, after.OsBuild);
        return texts;
    }

    private static string? Display(HardwareSnapshot s) =>
        s.DisplayRes is null ? null : s.DisplayHz is double hz ? s.DisplayRes + " @ " + hz.ToString("0", CultureInfo.InvariantCulture) + " Hz" : s.DisplayRes;

    private static void Add(List<string> texts, string label, string? before, string? after)
    {
        if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
        {
            texts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Trend_Change_Format, label, before ?? Strings.Common_NotAvailable, after ?? Strings.Common_NotAvailable));
        }
    }
}
