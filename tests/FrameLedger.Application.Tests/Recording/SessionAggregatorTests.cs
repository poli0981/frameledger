using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// The row's measured columns from a synthetic stream: numbers where something measured, null where
/// nothing did — never a 0 for an answer nobody gave (<c>06_DATA_MODEL</c>, the header of <c>0001_init.sql</c>).
/// </summary>
public sealed class SessionAggregatorTests
{
    private const FlMeasured _presentOnly = FlMeasured.OutputRes | FlMeasured.PresentArgs;

    [Fact]
    public void APresentOnlyWriterYieldsFrameStatisticsAndNothingItDidNotMeasure()
    {
        List<FlFrameRecord> records = SessionFixtures.Stream(2_000, _presentOnly);

        AggregationResult r = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(records));
        SessionRow row = r.Row;

        row.FrameCount.Should().Be(2_000);
        row.PresentedFps.Should().BeApproximately(100, 0.01);
        row.MedianFps.Should().BeApproximately(100, 0.01);
        row.P1LowFps.Should().BeApproximately(100, 0.01, "1,000+ frames: the 1 % low is published");
        row.P01LowFps.Should().BeNull("under 10,000 frames the 0.1 % low is withheld (FR-4.8)");
        row.Api.Should().Be("d3d12");
        row.PresentedQualifier.Should().Be("census_not_run");
        row.FgMode.Should().Be("na");
        row.FgSource.Should().BeNull();
        row.NativeFps.Should().BeNull();
        row.DisplayedFps.Should().BeNull();
        row.Upscaler.Should().BeNull();
        row.RenderW.Should().BeNull("UpscalerParams was never claimed");
        row.RtFlag.Should().Be("na");
        row.HdrFlag.Should().Be("na");
        row.VramProcAvgMb.Should().BeNull();
        row.ReflexActive.Should().BeNull("no latency sample was measured: not measured, not 'off'");
        row.LatencyAvgUs.Should().BeNull();
        row.StutterCount.Should().Be(0);
        row.AvgGpuTemp.Should().BeNull("no sensors");
        row.RuntimeModulesJson.Should().BeNull();
        row.NgxDriverWordsJson.Should().BeNull();
        r.FgVerdict.Should().Be(FgVerdict.NotMeasured);
        r.Segments.Should().ContainSingle().Which.DisplayedFps.Should().BeApproximately(100, 0.01);
    }

    /// <summary>
    /// The owner's rows 8 and 10 (2026-09-14): the Streamline identity named DLSS-G and the count refused. The row
    /// keeps the identity (<c>fg_mode = dlssg</c>, <c>fg_source = api</c>), publishes no number, and — since schema
    /// 0003 — says WHY in <c>fg_refusal</c>, so the UI can show "DLSS-G active — factor not counted" instead of N/A.
    /// </summary>
    [Fact]
    public void AnIdentityWithoutACountKeepsTheNameAndRecordsTheRefusal()
    {
        var writer = new FlWriterState { Status = 1, HooksInstalledMask = 0xB, RuntimeCensus = (uint)(FlRuntimeCensus.Ran | FlRuntimeCensus.SlInterposer | FlRuntimeCensus.SlDlssG) };

        // Identity claimed, the count never claimed: FgWindow refuses NotCounted.
        List<FlFrameRecord> identityOnly = [.. SessionFixtures.Stream(2_000, _presentOnly | FlMeasured.Fg).Select(static r => r with { FgMode = (byte)FlFgMode.DlssG })];
        AggregationResult a = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(identityOnly, writer));
        a.FgVerdict.Should().Be(FgVerdict.Named);
        a.Row.FgMode.Should().Be("dlssg");
        a.Row.FgSource.Should().Be("api");
        a.Row.FgFactor.Should().BeNull("nothing counted, so no factor — never 1.0 (03_METRICS §Rung 0)");
        a.Row.NativeFps.Should().BeNull();
        a.Row.DisplayedFps.Should().BeNull();
        a.Row.FgRefusal.Should().Be("not_counted");
        a.Row.PresentedFps.Should().BeApproximately(100, 0.01, "the presented rate stands alone");
        a.Row.PresentedQualifier.Should().Be("fg_runtime_loaded");

        // The count claimed on every present and zero on every present: the token hook never fired — the owner's shape.
        List<FlFrameRecord> zeroTokens = [.. SessionFixtures.Stream(2_000, _presentOnly | FlMeasured.Fg | FlMeasured.FgCounts).Select(static r => r with { FgMode = (byte)FlFgMode.DlssG, FgEvaluations = 0 })];
        SessionRow b = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(zeroTokens, writer)).Row;
        b.FgMode.Should().Be("dlssg");
        b.FgFactor.Should().BeNull();
        b.FgRefusal.Should().Be("no_evaluations");
        FgRefusalDetail.Parse(b.FgRefusalDetail).Should().BeEquivalentTo(new { Kind = "no_evaluations", Subject = "factor" }, "schema 0005 keeps the refusal's numbers beside its kind");

        // A counted factor carries no refusal.
        SessionRow counted = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(SessionFixtures.Stream(2_000, _presentOnly | FlMeasured.Fg | FlMeasured.FgCounts, fgPerBatch: 2), writer)).Row;
        counted.FgFactor.Should().BeApproximately(2, 0.01);
        counted.FgRefusal.Should().BeNull();
        counted.FgRefusalDetail.Should().BeNull("a published factor has nothing to explain");
    }

    /// <summary>
    /// 2026-09-17 (03_METRICS §Frame Generation): a real play session opens with a menu, the session-level factor is
    /// refused as non-uniform, and until now the gameplay's own number went with it — one hooked session in nine
    /// published a factor on the owner's machine. The row now carries the STEADY state, scoped and with its share,
    /// and keeps the refusal that says why it is not session-wide.
    /// </summary>
    [Fact]
    public void AMenuThenGameplayPublishesTheSteadyStateScopedAndWithItsShare()
    {
        var writer = new FlWriterState { Status = 1, HooksInstalledMask = 0xB, RuntimeCensus = (uint)(FlRuntimeCensus.Ran | FlRuntimeCensus.SlInterposer | FlRuntimeCensus.SlDlssG) };
        FlMeasured claims = _presentOnly | FlMeasured.Fg | FlMeasured.FgCounts;

        // 100 presents per second in these fixtures: 20 s of menu at x1, then 60 s of gameplay at x2.
        List<FlFrameRecord> menu = SessionFixtures.Stream(2_000, claims, fgPerBatch: 1);
        List<FlFrameRecord> play = [.. SessionFixtures.Stream(6_000, claims, fgPerBatch: 2)
            .Select((r, i) => r with { Qpc = menu[^1].Qpc + ((ulong)(i + 1) * 100_000), FrameIndex = (uint)(2_000 + i), FgMode = (byte)FlFgMode.DlssG })];

        SessionRow row = SessionAggregator.Aggregate(SessionFixtures.Skeleton(seconds: 80), SessionFixtures.Hooked([.. menu, .. play], writer)).Row;

        row.FgMode.Should().Be("dlssg");
        row.FgFactorScope.Should().Be("steady");
        row.FgFactor.Should().BeApproximately(2.0, 0.01, "the gameplay's own factor, never the 1.6 the whole session averages to");
        row.FgSteadyShare.Should().BeApproximately(0.75, 0.03);
        row.NativeFps.Should().BeApproximately(50, 1);
        row.DisplayedFps.Should().BeApproximately(100, 1);
        row.FgRefusal.Should().Be("non_uniform", "the refusal stays: it is why the number is not session-wide");
        row.FgRefusalDetail.Should().NotBeNull();
        row.DisplayedCountedBy.Should().Be("hook");

        // A uniform session is scoped 'session' and carries no share.
        SessionRow uniform = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(SessionFixtures.Stream(2_000, claims, fgPerBatch: 2), writer)).Row;
        uniform.FgFactorScope.Should().Be("session");
        uniform.FgSteadyShare.Should().BeNull();
        uniform.FgRefusal.Should().BeNull();
    }

    [Fact]
    public void ACountedNoneAndACountedFactorLandAsTheRowSpellsThem()
    {
        FlMeasured claims = _presentOnly | FlMeasured.Fg | FlMeasured.FgCounts;
        var writer = new FlWriterState { Status = 1, HooksInstalledMask = 0xB, RuntimeCensus = (uint)(FlRuntimeCensus.Ran | FlRuntimeCensus.SlInterposer) };

        SessionRow none = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(SessionFixtures.Stream(2_000, claims, fgPerBatch: 1), writer)).Row;
        SessionRow x2 = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(SessionFixtures.Stream(2_000, claims, fgPerBatch: 2), writer)).Row;

        none.FgMode.Should().Be("none");
        none.FgSource.Should().Be("none");
        none.FgFactor.Should().BeApproximately(1, 0.01);
        none.PresentedQualifier.Should().Be("no_fg_runtime");
        none.AppFrameCount.Should().Be(2_000);
        none.DisplayedCountedBy.Should().Be("hook");

        x2.FgMode.Should().Be("active", "the count says ×2 and no hooked identity named the technology");
        x2.FgSource.Should().Be("cadence");
        x2.FgFactor.Should().BeApproximately(2, 0.05);
        x2.NativeFps.Should().BeApproximately(50, 0.5);
        x2.DisplayedFps.Should().BeApproximately(100, 0.5);
        x2.AppFrameCount.Should().Be(1_000);
        x2.DisplayedFrameCount.Should().Be(2_000);
        x2.DisplayedP1LowFps.Should().NotBeNull();
    }

    [Fact]
    public void ANamedUpscalerWithParametersFillsTheExtentAndTheSegment()
    {
        FlMeasured claims = _presentOnly | FlMeasured.Upscaler | FlMeasured.UpscalerParams;
        List<FlFrameRecord> records = SessionFixtures.Stream(500, claims, upscaler: FlUpscaler.Dlss);
        var writer = new FlWriterState { Status = 1, HooksInstalledMask = 0x7 };

        AggregationResult r = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(records, writer));

        r.Row.Upscaler.Should().Be("dlss");
        r.Row.RenderW.Should().Be(1707);
        r.Row.OutputH.Should().Be(1440);
        r.Row.UpscaleRatio.Should().BeApproximately(1.5, 0.01);
        r.Row.SettingsChangedMidSession.Should().BeFalse();
        r.Segments.Should().ContainSingle().Which.Upscaler.Should().Be("dlss");
        r.Segments[0].RenderW.Should().Be(1707);
        r.Segments[0].OutputW.Should().Be(2560);
    }

    /// <summary>
    /// <c>0xFF</c> is the Overlay's "a hook ran and could not tell" for the quality byte, and every AMD dispatch carries it.
    /// Aggregated as a number it became the text "255", which the game page appends to the upscaler's name ("FSR 255").
    /// </summary>
    [Fact]
    public void AQualityTheHookCouldNotTellIsNoValueWhileAToldOneStillIs()
    {
        FlMeasured claims = _presentOnly | FlMeasured.Upscaler | FlMeasured.UpscalerParams;
        var writer = new FlWriterState { Status = 1, HooksInstalledMask = 0x7 };
        List<FlFrameRecord> notTold = [.. SessionFixtures.Stream(500, claims, upscaler: FlUpscaler.Dlss).Select(static r => r with { UpscalerQuality = 0xFF })];

        AggregationResult r = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(notTold, writer));

        r.Row.Upscaler.Should().Be("dlss");
        r.Row.UpscalerQuality.Should().BeNull("0xFF is not a preset");
        r.Segments.Should().ContainSingle().Which.UpscalerQuality.Should().BeNull();

        List<FlFrameRecord> mixed = [.. SessionFixtures.Stream(500, claims, upscaler: FlUpscaler.Dlss).Select(static (r, i) => r with { UpscalerQuality = (byte)(i % 5 == 0 ? 3 : 0xFF) })];
        AggregationResult m = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(mixed, writer));

        m.Row.UpscalerQuality.Should().Be("3", "the frames that told are the only ones that count");
        m.Segments[0].UpscalerQuality.Should().Be("3");
    }

    [Fact]
    public void AnUpscalerHookThatSawOnlyUnknownIsUnknownNotNull()
    {
        List<FlFrameRecord> records = SessionFixtures.Stream(100, _presentOnly | FlMeasured.Upscaler, upscaler: FlUpscaler.Unknown);

        SessionRow row = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(records)).Row;

        row.Upscaler.Should().Be("unknown", "a hook ran and could not identify what it saw: coverage short, not N/A");
    }

    [Fact]
    public void RayTracingVramLatencyAndSensorsAggregateWhereMeasured()
    {
        FlMeasured claims = _presentOnly | FlMeasured.Rt | FlMeasured.Vram | FlMeasured.Latency | FlMeasured.Pso;
        List<FlFrameRecord> records = SessionFixtures.Stream(1_000, claims);
        var writer = new FlWriterState { Status = 1, HooksInstalledMask = 0x7F0, RtTier = 11, RtStateObjectsCreated = 4, RasterPsoCreated = 40, VramBudgetMb = 12_000 };
        AggregationInput input = SessionFixtures.Hooked(records, writer) with { Sensors = SessionFixtures.Sensors(60) };

        SessionRow row = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), input).Row;

        row.RtFlag.Should().Be("yes");
        row.RtSource.Should().Be("measured");
        row.RtFramePct.Should().BeApproximately(50, 0.01, "every other present carried a dispatch");
        row.RaysPerPixel.Should().BeApproximately(8_294_400.0 / (2560.0 * 1440.0), 0.001);
        row.RtTier.Should().Be(11);
        row.RtPsoCount.Should().Be(4);
        row.RasterPsoCount.Should().Be(40);
        row.VramProcAvgMb.Should().BeApproximately(4004.5, 0.01);
        row.VramProcMaxMb.Should().Be(4009);
        row.ReflexActive.Should().BeTrue();
        row.LatencyAvgUs.Should().BeInRange(20_000, 20_100);
        row.LatencyP95Us.Should().BeInRange(20_000, 20_100);
        row.AvgGpuTemp.Should().BeApproximately(89.5, 0.01);
        row.MaxGpuTemp.Should().Be(119);
        row.AvgGpuLoad.Should().Be(50);
        row.VramAdapterMaxMb.Should().Be(3059);
        row.MaxGpuHotspot.Should().BeNull("no sample carried a hotspot");
        row.ThrottlePct.Should().BeNull("no sample carried throttle reasons (L3 only)");
    }

    [Fact]
    public void AGapExcludesTheIntervalIntoTheRecordFromTheStatistics()
    {
        List<FlFrameRecord> records = SessionFixtures.Stream(1_000, _presentOnly);
        // A one-second hole before record 500: without the gap it would be a 1,000 ms "frame".
        for (int i = 500; i < records.Count; i++)
        {
            records[i] = records[i] with { Qpc = records[i].Qpc + 10_000_000 };
        }

        SessionRow withGap = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(records, gaps: [500])).Row;
        SessionRow without = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), SessionFixtures.Hooked(records)).Row;

        withGap.MinFps.Should().BeApproximately(100, 0.01, "the hole is a gap, not the slowest frame");
        without.MinFps.Should().BeApproximately(1, 0.01, "unmarked, the same hole reads as a one-second frame");
        withGap.GapCount.Should().Be(0, "GapCount is the ring's torn-slot count, passed in; this fixture marked a position only");
    }

    [Fact]
    public void TheWitnessesAreStoredAsJsonOnlyWhenTheyRan()
    {
        var modules = new RuntimeModuleSet([new RuntimeModuleInfo("sl.interposer.dll", @"C:\g\sl.interposer.dll", "2,8,0,0", new Version(2, 8, 0, 0))], 2, 0);
        NgxDriverState ngx = NgxDriverState.Parse("NGXSTATE status=ANSWERED sr=0x600 rr=0x1 fg=0x5 ratio=0.0000 mode=0 preset=0 fgcount=0 fgpreset=0 fgmode=0 driver=61664");
        var markers = new ExecutableMarkers([new ExecutableMarker("ffxFsr3", "AMD FSR 3 API", true, 12)], 1_000_000, null);
        AggregationInput input = SessionFixtures.Hooked(SessionFixtures.Stream(100, _presentOnly)) with { Modules = modules, Ngx = ngx, Markers = markers };

        SessionRow row = SessionAggregator.Aggregate(SessionFixtures.Skeleton(), input).Row;

        row.RuntimeModulesJson.Should().Be("{\"sl.interposer.dll\":\"2,8,0,0\"}");
        row.SlInterposerVersion.Should().Be("2.8.0.0");
        row.ExecutableMarkersJson.Should().Be("{\"ffxFsr3\":12}");
        row.NgxDriverWordsJson.Should().Contain("\"Outcome\":\"Answered\"").And.Contain("\"Sr\":\"0x600\"").And.Contain("\"Driver\":61664");
        row.UpscalerDriverReported.Should().Be("dlss", "the driver reports SR created and evaluated");
        row.FgDriverReported.Should().BeNull("0x5 is INITIALIZED | DLL_EXISTS, not created");
    }
}
