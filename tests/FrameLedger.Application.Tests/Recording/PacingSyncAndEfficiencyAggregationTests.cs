// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// beta.14 (D49), the Agent's half: pacing, what the presents asked for, the card's clocks and limits, and efficiency — each
/// over the frames rule 6 allows, and null wherever nothing measured it.
/// </summary>
public sealed class PacingSyncAndEfficiencyAggregationTests
{
    private const FlMeasured _presentOnly = FlMeasured.OutputRes | FlMeasured.PresentArgs;
    private const FlMeasured _counted = _presentOnly | FlMeasured.Fg | FlMeasured.FgCounts;

    private static readonly FlWriterState _noFgRuntime = new() { Status = 1, HooksInstalledMask = 0x1, RuntimeCensus = (uint)FlRuntimeCensus.Ran };

    private static readonly FlWriterState _slDlssG = new()
    {
        Status = 1,
        HooksInstalledMask = 0xB,
        RuntimeCensus = (uint)(FlRuntimeCensus.Ran | FlRuntimeCensus.SlInterposer | FlRuntimeCensus.SlDlssG),
    };

    /// <summary>The card at 200 W: core clock 2500 MHz rising by one each tick, power-limited on every other tick.</summary>
    private static List<TelemetrySample> Card(int seconds) =>
    [
        .. SessionFixtures.Sensors(seconds).Select(static (s, i) => s with
        {
            Sample = s.Sample with
            {
                PowerW = 200,
                CoreClockMhz = 2500 + i,
                MemClockMhz = 14000,
                FanRpm = 1500,
                TempMemoryC = 70 + i,
                ThrottleReasons = i % 2 == 0 ? GpuLimitReasons.Power : 0u,
            },
        }),
    ];

    private static SessionRow Aggregate(List<FlFrameRecord> records, FlWriterState? writer = null, int? refreshHz = null, List<TelemetrySample>? sensors = null) =>
        SessionAggregator.Aggregate(SessionFixtures.Skeleton() with { DisplayMonitorHz = refreshHz },
            SessionFixtures.Hooked(records, writer) with { Sensors = sensors ?? [] }).Row;

    [Fact]
    public void PacingIsOverTheFramesAndTheRefreshShareNeedsARate()
    {
        // 100 FPS held, on a 144 Hz display.
        List<FlFrameRecord> records = SessionFixtures.Stream(2_000, _presentOnly);

        SessionRow row = Aggregate(records, refreshHz: 144);

        (row.TimeBelow30Pct, row.TimeBelow60Pct).Should().Be((0.0, 0.0));
        row.TimeBelowRefreshPct.Should().BeApproximately(100, 1e-9, "100 presents a second never fed a 144 Hz display at its rate");
        row.FrametimeDeltaMeanMs.Should().Be(0, "every frame took 10 ms");
        Aggregate(records).TimeBelowRefreshPct.Should().BeNull("no refresh rate was read — a recovered session has none");
    }

    [Fact]
    public void AGeneratingSessionIsBelowSixtyByItsApplicationFrames()
    {
        // ×2 counted: application frames at 50 FPS, presents at 100, on a 100 Hz display.
        SessionRow row = Aggregate(SessionFixtures.Stream(2_200, _counted, fgPerBatch: 2), _slDlssG, refreshHz: 100);

        row.FgFactor.Should().BeApproximately(2, 0.01);
        row.TimeBelow60Pct.Should().BeApproximately(100, 1e-9, "the game ran at 50; the generated frames do not make it 60 (rule 6)");
        row.TimeBelowRefreshPct.Should().Be(0, "the display received a frame every refresh — over presents, as the display sees them");
    }

    [Fact]
    public void TheSyncSharesAndModeAreWhatThePresentsAskedFor()
    {
        SessionRow on = Aggregate([.. SessionFixtures.Stream(100, _presentOnly).Select(static r => r with { SyncInterval = 1 })]);
        (on.VsyncPresentPct, on.TearingAllowedPct, on.SyncIntervalMode).Should().Be((100.0, 0.0, "on"));

        List<FlFrameRecord> menuThenGame = [.. SessionFixtures.Stream(100, _presentOnly)
            .Select(static (r, i) => r with { SyncInterval = (ushort)(i < 25 ? 1 : 0), PresentFlags = i < 25 ? 0u : 0x200u })];
        SessionRow mixed = Aggregate(menuThenGame);
        (mixed.VsyncPresentPct, mixed.TearingAllowedPct, mixed.SyncIntervalMode).Should().Be((25.0, 75.0, "mixed"));

        Aggregate(SessionFixtures.Stream(100, _presentOnly)).SyncIntervalMode.Should().Be("off");

        SessionRow vulkan = Aggregate(SessionFixtures.Stream(100, FlMeasured.OutputRes, api: FlApi.Vulkan));
        (vulkan.VsyncPresentPct, vulkan.TearingAllowedPct, vulkan.SyncIntervalMode)
            .Should().Be(((double?)null, (double?)null, (string?)null), "a Vulkan present carries no arguments: N/A, never 0 %");
    }

    [Fact]
    public void EfficiencyIsTheApplicationRateOverTheCardsPowerAndNeverTheDisplayedRate()
    {
        List<FlFrameRecord> plain = SessionFixtures.Stream(2_000, _presentOnly);

        SessionRow noRuntime = Aggregate(plain, _noFgRuntime, sensors: Card(10));
        noRuntime.PresentedQualifier.Should().Be("no_fg_runtime");
        noRuntime.AppFramesPerJoule.Should().BeApproximately(0.5, 1e-9, "100 presents a second, every one an application frame, at 200 W");

        Aggregate(plain, sensors: Card(10)).AppFramesPerJoule.Should().BeNull("census_not_run: the presents may include generated frames");
        Aggregate(plain, _slDlssG, sensors: Card(10)).AppFramesPerJoule.Should().BeNull("a frame-generation runtime was loaded and nothing counted");

        SessionRow counted = Aggregate(SessionFixtures.Stream(2_200, _counted, fgPerBatch: 2), _slDlssG, sensors: Card(10));
        counted.AppFramesPerJoule.Should().BeApproximately(0.25, 1e-3, "50 application frames a second at 200 W — the 100 displayed are never the numerator");

        Aggregate(plain, _noFgRuntime).AppFramesPerJoule.Should().BeNull("no power was read");
    }

    [Fact]
    public void TheCardsClocksFanMemoryTemperatureAndLimitsAreAggregatedOnBothTiers()
    {
        SessionRow tier2 = SessionAggregator.WithSensorAggregates(SessionFixtures.Skeleton(tier: CaptureTier.NotHooked), Card(4));

        tier2.AvgGpuCoreClockMhz.Should().BeApproximately(2501.5, 1e-9);
        (tier2.AvgGpuMemClockMhz, tier2.AvgGpuFanRpm, tier2.MaxGpuMemTemp).Should().Be((14000.0, 1500.0, 73.0));
        (tier2.PowerLimitPct, tier2.ThermalLimitPct).Should().Be((50.0, 0.0));
        tier2.AppFramesPerJoule.Should().BeNull("a Tier-2 session has no frames");
        SensorSeriesStats.Parse(tier2.SensorStatsJson)[SensorSeriesCatalog.GpuPowerLimit].Mean.Should().Be(50, "the stored series' mean is the same share");

        SessionRow noReasons = SessionAggregator.WithSensorAggregates(SessionFixtures.Skeleton(), SessionFixtures.Sensors(4));
        (noReasons.PowerLimitPct, noReasons.ThermalLimitPct, noReasons.AvgGpuCoreClockMhz)
            .Should().Be(((double?)null, (double?)null, (double?)null), "no layer reported a reason or a clock: N/A, never 'not limited'");
    }

    [Fact]
    public void TheStoredSeriesNamesNeverChangeAndBeta14AppendsSix() =>
        SensorSeriesCatalog.All.Select(static s => s.Name).Should().Equal(
            "gpu_temp", "gpu_hotspot", "gpu_load", "gpu_power", "vram_adapter", "cpu_load", "cpu_temp", "ram_mb",
            "game_vram_dedicated", "game_vram_shared", "game_ram_private", "game_ram_ws", "game_commit",
            "gpu_core_clock", "gpu_mem_clock", "gpu_fan", "gpu_mem_temp", "gpu_power_limit", "gpu_thermal_limit");

    [Theory]
    [InlineData(0x2u, 100.0, 0.0)]
    [InlineData(0x1u, 0.0, 100.0)]
    [InlineData(0x3u, 100.0, 100.0)]
    [InlineData(0x8000_0000u, 0.0, 0.0)]
    [InlineData(0u, 0.0, 0.0)]
    public void EachLimitIsItsOwnBit(uint reasons, double power, double thermal) =>
        (GpuLimitReasons.PowerLimited(reasons), GpuLimitReasons.ThermalLimited(reasons)).Should().Be(((double?)power, (double?)thermal));

    [Fact]
    public void ATickWithNoReasonsIsNoAnswer() =>
        (GpuLimitReasons.PowerLimited(null), GpuLimitReasons.ThermalLimited(null)).Should().Be(((double?)null, (double?)null));
}
