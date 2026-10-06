// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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

        IReadOnlyList<TrendPoint> points = TrendSeriesBuilder.Points(rows, TrendMetric.PresentedFps, includeMidSessionChanges: false);
        points.Select(static p => p.Value).Should().Equal(60, 70);
        points.Should().BeInAscendingOrder(static p => p.At);
        TrendSeriesBuilder.ExcludedCount(rows).Should().Be(1);

        IReadOnlyList<TrendPoint> all = TrendSeriesBuilder.Points(rows, TrendMetric.PresentedFps, includeMidSessionChanges: true);
        all.Select(static p => p.Value).Should().Equal(60, 70, 80);
        all[2].SettingsChangedMidSession.Should().BeTrue();
    }

    /// <summary>
    /// beta.8: "Average" mixed two rates in one line. Native FPS is the game's own frames where frame generation was measured;
    /// Presented FPS is on the sessions that counted no generated frame — never on one that did, whose presented rate
    /// includes them (CLAUDE.md rule 6).
    /// </summary>
    [Fact]
    public void NativeAndPresentedAreTwoMetricsAndNeitherIsAnInflatedNumber()
    {
        SessionRow generated = Row(1, 1, fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9);
        SessionRow none = Row(2, 1, fgMode: "none", native: 90);
        SessionRow presented = Row(3, 1, fgMode: "na", native: null, presented: 144);
        SessionRow identified = Row(4, 1, fgMode: "dlssg", native: null, displayed: null, presented: 304, refusal: "no_evaluations");

        TrendSeriesBuilder.ValueOf(generated, TrendMetric.NativeFps).Should().Be(62);
        TrendSeriesBuilder.ValueOf(generated, TrendMetric.PresentedFps).Should().BeNull("its presented rate counts generated frames: the inflated number");
        TrendSeriesBuilder.ValueOf(identified, TrendMetric.PresentedFps).Should().Be(304, "identified but uncounted: the Presented figure is the headline, never a Native");
        TrendSeriesBuilder.ValueOf(identified, TrendMetric.NativeFps).Should().BeNull();
        TrendSeriesBuilder.ValueOf(identified, TrendMetric.Displayed).Should().BeNull("no factor was counted, so no displayed rate exists");
        TrendSeriesBuilder.ValueOf(none, TrendMetric.NativeFps).Should().Be(90);
        TrendSeriesBuilder.ValueOf(none, TrendMetric.PresentedFps).Should().Be(90, "a measured none: the presents are the application's frames");
        TrendSeriesBuilder.ValueOf(presented, TrendMetric.PresentedFps).Should().Be(144, "Presented FPS is the headline when FG is not measured");
        TrendSeriesBuilder.ValueOf(presented, TrendMetric.NativeFps).Should().BeNull();
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
            AvgGpuLoad = 97,
            AvgGpuPowerW = 310,
            GameVramDedicatedMedianMb = 6144,
            GameVramDedicatedMaxMb = 9216,
            GameRamPrivateMedianMb = 3072,
            GameRamPrivateMaxMb = 4096,
            GameRamWorkingSetMaxMb = 5120,
            AvgCpuLoad = 41.5,
            MaxCpuTemp = 78,
            AvgRamMb = 18432,
        };
        SessionRow steady = Row(2, 1, fgMode: "dlssg", native: 70, displayed: 280, factor: 4) with { FgFactorScope = "steady", FgSteadyShare = 0.75, AvgCpuLoad = 50 };
        SessionRow before = Row(3, 1);

        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgGpuLoad).Should().Be(97);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgGpuPower).Should().Be(310);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.GameVramMedian).Should().Be(6, "GB, 1 GB = 1024 MiB");
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.GameVramPeak).Should().Be(9);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.GameRamMedian).Should().Be(3);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.GameRamPeak).Should().Be(4);
        TrendSeriesBuilder.ValueOf(measured with { GameRamPrivateMaxMb = null }, TrendMetric.GameRamPeak).Should().BeNull("the working set is another quantity and never takes the private working set's place on a line");
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgCpuLoad).Should().Be(41.5);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.MaxCpuTemp).Should().Be(78);
        TrendSeriesBuilder.ValueOf(measured, TrendMetric.AvgRam).Should().Be(18);
        TrendSeriesBuilder.Counts(Row(5, 1, hooked: false), TrendMetric.GameVramPeak).Should().BeTrue("the game's memory is read from outside it: a session that was not hooked has it too");
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

    /// <summary>
    /// beta.8: a machine metric is every session's telemetry, Tier 2 included — a game never hooked had no trend at all;
    /// a frame-rate metric stays the hooks'. A steady state is partial for the rates beside the factor too.
    /// </summary>
    [Fact]
    public void MachineMetricsCountEverySessionAndASteadyStatesRatesArePartial()
    {
        SessionRow hooked = Row(1, 1, gpu: 70);
        SessionRow tier2 = Row(2, 1, hooked: false, native: null, gpu: 64);
        SessionRow steady = Row(3, 1, fgMode: "dlssg", native: 70, displayed: 280, factor: 4) with { FgFactorScope = "steady", FgSteadyShare = 0.75 };

        TrendSeriesBuilder.Points([hooked, tier2], TrendMetric.MaxGpuTemp, includeMidSessionChanges: false).Select(static p => p.Value).Should().Equal(70, 64);
        TrendSeriesBuilder.Points([hooked, tier2], TrendMetric.PresentedFps, includeMidSessionChanges: false).Should().ContainSingle("the hooks' rate");
        TrendSeriesBuilder.IsPartial(steady, TrendMetric.NativeFps).Should().BeTrue();
        TrendSeriesBuilder.IsPartial(steady, TrendMetric.Displayed).Should().BeTrue();
        TrendSeriesBuilder.IsPartial(steady, TrendMetric.MaxGpuTemp).Should().BeFalse("the machine's numbers are the whole session's");
        TrendSeriesBuilder.ExcludedCount([hooked, steady], TrendMetric.NativeFps).Should().Be(1);
        TrendSeriesBuilder.ExcludedCount([hooked, steady], TrendMetric.PresentedFps).Should().Be(0, "a steady session has no presented point to exclude");
    }

    /// <summary>beta.8: two differences on one day are one marker with both lines; two labels at one place were drawn over each other.</summary>
    [Fact]
    public void ChangesOnOneDayAreOneMarker()
    {
        DateTimeOffset day = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);
        IReadOnlyList<HardwareChange> merged = TrendSeriesBuilder.MergedByDay(
        [
            new HardwareChange(day, "GPU driver: 572.16 → 576.02"),
            new HardwareChange(day.AddMinutes(1), "OS: 26100 → 26200"),
            new HardwareChange(day.AddDays(3), "GPU: A → B"),
        ]);

        merged.Should().HaveCount(2);
        merged[0].Text.Should().Be("GPU driver: 572.16 → 576.02" + Environment.NewLine + "OS: 26100 → 26200");
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

    private static SessionRow Shown(int day, bool hooked, Domain.Display.DisplaySummary? display, bool midSession = false) =>
        Application.Recording.SessionDisplayColumns.WithDisplay(Row(day, 1, hooked: hooked, midSession: midSession), display);

    /// <summary>
    /// beta.10 (<c>03_METRICS</c> §Display mode): the display shares are every session's — the window is read out of process
    /// whether or not anything was hooked — and a session whose settings changed is NOT left out: switching between fullscreen
    /// and a window resizes the swap chain, which is exactly what marks it, and the share is the whole session's by construction.
    /// </summary>
    [Fact]
    public void DisplaySharesAreEverySessionsAndAModeSwitchIsNotAMidSessionChange()
    {
        SessionRow[] rows =
        [
            Shown(1, hooked: true, new() { ExclusiveMs = 25_000, BorderlessMs = 75_000, Source = "swapchain" }, midSession: true),
            Shown(2, hooked: false, new() { CoversMs = 80_000, WindowedMs = 20_000, Source = "window" }),
            Shown(3, hooked: true, display: null),
        ];

        TrendSeriesBuilder.Points(rows, TrendMetric.DisplayExclusiveShare, includeMidSessionChanges: false).Select(static p => p.Value).Should().Equal(25.0);
        TrendSeriesBuilder.Points(rows, TrendMetric.DisplayBorderlessShare, includeMidSessionChanges: false).Select(static p => p.Value).Should().Equal(75.0);
        TrendSeriesBuilder.Points(rows, TrendMetric.DisplayWindowedShare, includeMidSessionChanges: false).Select(static p => p.Value).Should().Equal(0.0, 20.0);
        TrendSeriesBuilder.ExcludedCount(rows, TrendMetric.DisplayExclusiveShare).Should().Be(0);
        TrendSeriesBuilder.Points(rows, TrendMetric.DisplayExclusiveShare, includeMidSessionChanges: false).Single().SettingsChangedMidSession.Should().BeFalse();
    }

    [Fact]
    public void ExclusiveAndBorderlessHaveNoPointWhereTheyCouldNotBeToldApart()
    {
        SessionRow covered = Shown(1, hooked: false, new() { CoversMs = 60_000, Source = "window" });
        SessionRow partly = Shown(2, hooked: true, new() { BorderlessMs = 50_000, CoversMs = 10_000, Source = "swapchain" });

        foreach (SessionRow row in new[] { covered, partly })
        {
            TrendSeriesBuilder.ValueOf(row, TrendMetric.DisplayExclusiveShare).Should().BeNull("a share of time that could have been either would be a guess");
            TrendSeriesBuilder.ValueOf(row, TrendMetric.DisplayBorderlessShare).Should().BeNull();
            TrendSeriesBuilder.ValueOf(row, TrendMetric.DisplayWindowedShare).Should().Be(0.0, "windowed is told from the window alone");
        }

        TrendSeriesBuilder.ValueOf(Shown(3, hooked: true, new() { NoWindowMs = 9_000 }), TrendMetric.DisplayWindowedShare)
            .Should().BeNull("no window was ever read, so there is no share of anything");
    }
    /// <summary>
    /// beta.14 (D49): the nine new metrics are schema 0019's columns in their own units. Pacing and efficiency are the hooks',
    /// so a Tier-2 session has no point there even with a value; the card's clock, memory temperature and power limit are
    /// telemetry, every session's; a session recorded before beta.14 has no point at all.
    /// </summary>
    [Fact]
    public void TheBetaFourteenMetricsAreTheRowsColumnsInTheirOwnUnits()
    {
        SessionRow hooked = Row(1, 1) with
        {
            TimeBelow60Pct = 9.6,
            TimeBelowRefreshPct = 3.5,
            FrametimeDeltaMeanMs = 0.9,
            UpscaleRatio = 1.5,
            VsyncPresentPct = 100,
            AvgGpuCoreClockMhz = 2655,
            MaxGpuMemTemp = 78,
            PowerLimitPct = 33,
            AppFramesPerJoule = 0.31,
        };
        SessionRow tier2 = Row(2, 1, hooked: false) with { TimeBelow60Pct = 50, AvgGpuCoreClockMhz = 2400, MaxGpuMemTemp = 70, PowerLimitPct = 10 };
        SessionRow[] rows = [hooked, tier2, Row(3, 1)];

        TrendSeriesBuilder.Points(rows, TrendMetric.TimeBelow60, false).Select(static p => p.Value).Should().Equal(9.6);
        TrendSeriesBuilder.Points(rows, TrendMetric.GpuCoreClock, false).Select(static p => p.Value).Should().Equal(2655, 2400);
        TrendSeriesBuilder.Points(rows, TrendMetric.MaxGpuMemTemp, false).Select(static p => p.Value).Should().Equal(78, 70);
        TrendSeriesBuilder.Points(rows, TrendMetric.PowerLimitShare, false).Select(static p => p.Value).Should().Equal(33, 10);
        TrendSeriesBuilder.Points(rows, TrendMetric.RenderScale, false).Single().Value.Should().BeApproximately(66.67, 0.01, "100 / upscale_ratio");
        TrendSeriesBuilder.Points(rows, TrendMetric.Efficiency, false).Select(static p => p.Value).Should().Equal(0.31);
        TrendSeriesBuilder.Points(rows, TrendMetric.VsyncShare, false).Select(static p => p.Value).Should().Equal(100);
        TrendSeriesBuilder.Points(rows, TrendMetric.FrameToFrame, false).Select(static p => p.Value).Should().Equal(0.9);
        TrendSeriesBuilder.Points(rows, TrendMetric.TimeBelowRefresh, false).Select(static p => p.Value).Should().Equal(3.5);

        (TrendSeriesBuilder.UnitOf(TrendMetric.FrameToFrame), TrendSeriesBuilder.UnitOf(TrendMetric.GpuCoreClock), TrendSeriesBuilder.UnitOf(TrendMetric.Efficiency))
            .Should().Be((Strings.Trend_Unit_Ms, Strings.Trend_Unit_Mhz, Strings.Trend_Unit_FpsPerWatt));
        (TrendSeriesBuilder.UnitOf(TrendMetric.MaxGpuMemTemp), TrendSeriesBuilder.UnitOf(TrendMetric.TimeBelow60), TrendSeriesBuilder.UnitOf(TrendMetric.RenderScale))
            .Should().Be((Strings.Trend_Unit_Celsius, Strings.Trend_Unit_Percent, Strings.Trend_Unit_Percent));
    }
}
