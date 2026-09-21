using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>FR-6.3 / FR-6.4: points per hooked session oldest first, mid-session-change sessions out by default, Displayed only where measured, markers where consecutive snapshots differ.</summary>
[Collection(StringsCultureCollection.Name)]
public sealed class TrendSeriesBuilderTests
{
    private static SessionRow Row(int day, long snapshotId, bool hooked = true, bool midSession = false, string fgMode = "none", double? native = 60, double? displayed = null, double? presented = null, double? p1 = 48, double? gpu = 70, double? factor = null, string? refusal = null) => new()
    {
        FgFactor = factor,
        FgRefusal = refusal,
        Id = day,
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = snapshotId,
        StartedAt = DateTimeOffset.UnixEpoch.AddDays(day),
        EndedAt = DateTimeOffset.UnixEpoch.AddDays(day).AddMinutes(10),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = hooked ? CaptureTier.Hooked : CaptureTier.NotHooked,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = hooked ? 1000 : 0,
        FgMode = hooked ? fgMode : "na",
        NativeFps = native,
        DisplayedFps = displayed,
        PresentedFps = presented,
        P1LowFps = p1,
        MaxGpuTemp = gpu,
        SettingsChangedMidSession = midSession,
    };

    [Fact]
    public void PointsAreHookedSessionsOldestFirstWithTheDefaultExclusion()
    {
        SessionRow[] rows =
        [
            Row(3, 1, native: 70),
            Row(1, 1, native: 60),
            Row(2, 1, hooked: false, native: null),
            Row(4, 1, midSession: true, native: 80),
        ];

        IReadOnlyList<TrendPoint> points = TrendSeriesBuilder.Points(rows, TrendMetric.Average, includeMidSessionChanges: false);
        points.Select(static p => p.Value).Should().Equal(60, 70);
        points.Should().BeInAscendingOrder(static p => p.At);
        TrendSeriesBuilder.ExcludedCount(rows).Should().Be(1);

        IReadOnlyList<TrendPoint> all = TrendSeriesBuilder.Points(rows, TrendMetric.Average, includeMidSessionChanges: true);
        all.Select(static p => p.Value).Should().Equal(60, 70, 80);
        all[2].SettingsChangedMidSession.Should().BeTrue();
    }

    [Fact]
    public void TheAverageIsPresentedWhereFgWasNotMeasuredAndDisplayedOnlyWhereItWas()
    {
        SessionRow generated = Row(1, 1, fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9);
        SessionRow none = Row(2, 1, fgMode: "none", native: 90);
        SessionRow presented = Row(3, 1, fgMode: "na", native: null, presented: 144);
        SessionRow identified = Row(4, 1, fgMode: "dlssg", native: null, displayed: null, presented: 304, refusal: "no_evaluations");

        TrendSeriesBuilder.ValueOf(generated, TrendMetric.Average).Should().Be(62);
        TrendSeriesBuilder.ValueOf(identified, TrendMetric.Average).Should().Be(304, "identified but uncounted: the Presented figure is the headline, never a Native");
        TrendSeriesBuilder.ValueOf(identified, TrendMetric.Displayed).Should().BeNull("no factor was counted, so no displayed rate exists");
        TrendSeriesBuilder.ValueOf(none, TrendMetric.Average).Should().Be(90);
        TrendSeriesBuilder.ValueOf(presented, TrendMetric.Average).Should().Be(144, "Presented FPS is the headline when FG is not measured");
        TrendSeriesBuilder.ValueOf(generated, TrendMetric.Displayed).Should().Be(118);
        TrendSeriesBuilder.ValueOf(none, TrendMetric.Displayed).Should().BeNull("a measured none has no displayed rate distinct from native");
        TrendSeriesBuilder.ValueOf(presented, TrendMetric.Displayed).Should().BeNull();
        TrendSeriesBuilder.ValueOf(none, TrendMetric.MaxGpuTemp).Should().Be(70);
        TrendSeriesBuilder.Points([generated, none, presented], TrendMetric.Displayed, true).Should().ContainSingle();
    }

    /// <summary>
    /// 2026-09-21: one game's sessions over time beyond the frame rate — the machine, and the ×FG factor. A column nobody
    /// measured is no point (never a zero on the chart), and a factor that is its session's STEADY STATE describes part of
    /// the session (CLAUDE.md rule 6), so it is left out by default and drawn marked when the partial ones are included.
    /// </summary>
    [Fact]
    public void TheMachineMetricsAreTheRowsColumnsAndASteadyStateFactorIsAPartialPoint()
    {
        SessionRow measured = Row(1, 1, fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9) with
        {
            AvgGpuLoad = 97, AvgGpuPowerW = 310, VramProcMaxMb = 9000, AvgCpuLoad = 41.5, MaxCpuTemp = 78, AvgRamMb = 18000,
        };
        SessionRow steady = Row(2, 1, fgMode: "dlssg", native: 70, displayed: 280, factor: 4) with { FgFactorScope = "steady", FgSteadyShare = 0.75, AvgCpuLoad = 50 };
        SessionRow before = Row(3, 1);

        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgGpuLoad).Should().Be(97);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgGpuPower).Should().Be(310);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.MaxVramProcess).Should().Be(9000);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgCpuLoad).Should().Be(41.5);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.MaxCpuTemp).Should().Be(78);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgRam).Should().Be(18000);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.FgFactor).Should().Be(1.9);
        TrendSeriesBuilder.ValueOf(before, TrendMetric.AvgCpuLoad).Should().BeNull("recorded before anything read the CPU");
        TrendSeriesBuilder.ValueOf(before, TrendMetric.FgFactor).Should().BeNull("a measured none has no factor to plot");

        SessionRow[] rows = [measured, steady, before];
        TrendSeriesBuilder.Points(rows, TrendMetric.AvgCpuLoad, includeMidSessionChanges: false).Select(static p => p.Value).Should().Equal([41.5, 50], "the steady state is about the FACTOR; the machine's averages are the whole session's");
        TrendSeriesBuilder.Points(rows, TrendMetric.FgFactor, includeMidSessionChanges: false).Select(static p => p.Value).Should().Equal(1.9);
        TrendSeriesBuilder.ExcludedCount(rows, TrendMetric.FgFactor).Should().Be(1);
        TrendSeriesBuilder.ExcludedCount(rows, TrendMetric.AvgCpuLoad).Should().Be(0);
        IReadOnlyList<TrendPoint> all = TrendSeriesBuilder.Points(rows, TrendMetric.FgFactor, includeMidSessionChanges: true);
        all.Select(static p => p.Value).Should().Equal(1.9, 4);
        all[1].SettingsChangedMidSession.Should().BeTrue("drawn marked: it covers a share of its session, never the session");
    }

    [Fact]
    public void MarkersAppearWhereConsecutiveSnapshotsDiffer()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            var snapshots = new Dictionary<long, HardwareSnapshot>
            {
                [1] = new() { GpuName = "RTX 5080", GpuDriver = "572.16", CpuName = "i7", DisplayRes = "2560x1440", DisplayHz = 144 },
                [2] = new() { GpuName = "RTX 5080", GpuDriver = "576.02", CpuName = "i7", DisplayRes = "2560x1440", DisplayHz = 144 },
                [3] = new() { GpuName = "RTX 5080", GpuDriver = "576.02", CpuName = "i7", DisplayRes = "3840x2160", DisplayHz = 120 },
            };
            SessionRow[] rows = [Row(1, 1), Row(2, 1), Row(3, 2), Row(4, 3), Row(5, 3)];

            IReadOnlyList<HardwareChange> changes = TrendSeriesBuilder.Changes(rows, id => snapshots.GetValueOrDefault(id));

            changes.Should().HaveCount(2);
            changes[0].At.Should().Be(rows[2].StartedAt);
            changes[0].Text.Should().Be("GPU driver: 572.16 → 576.02");
            changes[1].Text.Should().Be("Display: 2560x1440 @ 144 Hz → 3840x2160 @ 120 Hz");
            TrendSeriesBuilder.Changes([Row(1, 9), Row(2, 9)], _ => null).Should().BeEmpty("no snapshot difference, no marker");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}
