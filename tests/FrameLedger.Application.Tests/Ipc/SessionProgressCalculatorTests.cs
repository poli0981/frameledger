using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Telemetry;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Ipc;

/// <summary>
/// The live card's numbers over a synthetic ring (<c>SessionFixtures.Stream</c>: 100 fps, 10 ms apart), through
/// the same calculators the row uses — so the assertions are the row's own expectations, five seconds at a time.
/// </summary>
public sealed class SessionProgressCalculatorTests
{
    private static readonly Guid _guid = Guid.NewGuid();

    private static CaptureProgress Progress(IReadOnlyList<FlFrameRecord> records, IReadOnlyList<int>? gaps = null, uint census = (uint)FlRuntimeCensus.Ran) => new()
    {
        Records = records,
        GapBefore = gaps ?? [],
        WriterState = new FlWriterState { Status = (uint)FlStatus.Ready, HooksInstalledMask = 0x1, RuntimeCensus = census },
        TotalDropped = 0,
        TotalGaps = gaps?.Count ?? 0,
        DrainTicks = 10,
        ForegroundTicks = 10,
        GuardTicksPublished = 1,
        TouchQpc = [],
        RuntimeModules = RuntimeModuleSet.Empty,
        NgxDriver = NgxDriverState.NotRun,
    };

    [Fact]
    public void OnlyTheLastFiveSecondsCountAndThePresentedRateStandsAloneWithItsQualifier()
    {
        // 10 s of records; the window is the last 5 s.
        List<FlFrameRecord> records = SessionFixtures.Stream(1000, FlMeasured.OutputRes | FlMeasured.PresentArgs);
        SessionProgressEvent e = SessionProgressCalculator.Compute(_guid, Progress(records), SessionFixtures.QpcFrequency, 12.5, gpu: null);

        e.SessionGuid.Should().Be(_guid);
        e.ElapsedS.Should().Be(12.5);
        e.Presents5s.Should().BeInRange(495, 505);
        e.PresentedFps5s.Should().BeApproximately(100, 1);
        e.PresentedQualifier.Should().Be("no_fg_runtime", "the census ran and named no frame-generation runtime");
        e.NativeFps5s.Should().BeNull("frame generation was not measured, so no Native number may appear (rule 6)");
        e.DisplayedFps5s.Should().BeNull();
        e.FgFactor.Should().BeNull();
        e.FgMode.Should().Be("na");
        e.Upscaler.Should().BeNull("no upscaler hook ran");
        e.RtActive.Should().BeNull("no record claimed a ray-tracing measurement");
        e.VramProcMb.Should().BeNull();
        e.LatencyUs.Should().BeNull();
        e.CpuTempC.Should().BeNull();
    }

    [Fact]
    public void AMeasuredFrameGenerationFactorSplitsNativeFromDisplayed()
    {
        List<FlFrameRecord> records = SessionFixtures.Stream(800, FlMeasured.OutputRes | FlMeasured.Fg | FlMeasured.FgCounts, fgPerBatch: 2);
        SessionProgressEvent e = SessionProgressCalculator.Compute(_guid, Progress(records), SessionFixtures.QpcFrequency, 8, gpu: null);

        e.FgFactor.Should().BeApproximately(2.0, 0.05);
        e.NativeFps5s.Should().BeApproximately(50, 1.5);
        e.DisplayedFps5s.Should().BeApproximately(100, 1.5);
        e.FgMode.Should().Be("active", "the count says generated, and no hooked identity names the technology");
    }

    [Fact]
    public void UpscalerIdentityExtentRayTracingVramAndLatencyComeFromTheRecordsThatClaimThem()
    {
        List<FlFrameRecord> records = SessionFixtures.Stream(600,
            FlMeasured.OutputRes | FlMeasured.Upscaler | FlMeasured.UpscalerParams | FlMeasured.Rt | FlMeasured.Vram | FlMeasured.Latency,
            upscaler: FlUpscaler.Dlss);
        var gpu = new GpuSample { TakenAt = DateTimeOffset.UtcNow, Layer = TelemetryLayer.Lhm, TempCoreC = 61.5 };

        SessionProgressEvent e = SessionProgressCalculator.Compute(_guid, Progress(records), SessionFixtures.QpcFrequency, 6, gpu);

        e.Upscaler.Should().Be("dlss");
        e.RenderW.Should().Be(1707);
        e.RenderH.Should().Be(960);
        e.OutputW.Should().Be(2560);
        e.OutputH.Should().Be(1440);
        e.RtActive.Should().BeTrue("every second record carries DispatchObserved");
        e.VramProcMb.Should().Be((int)records[^1].VramUsedMb);
        e.LatencyUs.Should().Be((int)records[^1].ReflexLatencyUs);
        e.GpuTempC.Should().Be(61.5);
    }

    [Fact]
    public void AnRtClaimWithNoEvidenceIsAMeasuredNoNotAnUnknown()
    {
        List<FlFrameRecord> records = SessionFixtures.Stream(300, FlMeasured.OutputRes | FlMeasured.Rt);
        for (int i = 0; i < records.Count; i++)
        {
            FlFrameRecord r = records[i];
            r.RtFlags = 0;
            r.DispatchRaysVolume = 0;
            records[i] = r;
        }

        SessionProgressCalculator.Compute(_guid, Progress(records), SessionFixtures.QpcFrequency, 3, gpu: null).RtActive.Should().BeFalse();
    }

    [Fact]
    public void NoRecordsYetIsAnHonestEmptyProgressNotAZero()
    {
        SessionProgressEvent e = SessionProgressCalculator.Compute(_guid, Progress([], census: 0), SessionFixtures.QpcFrequency, 0.3, gpu: null);

        e.Presents5s.Should().Be(0);
        e.PresentedFps5s.Should().BeNull();
        e.PresentedQualifier.Should().Be("census_not_run");
        e.FgMode.Should().Be("na");
        e.Upscaler.Should().BeNull();
    }

    [Fact]
    public void AGapInsideTheWindowReadsAsPresentsThatAreNotThereNotAsAFasterRate()
    {
        // 50 records dropped from the middle of the last 5 s, with the gap marked at the record that follows:
        // the presented rate is the presents that exist over the window (the row's PresentedFps rule), so it
        // reads 90, and the window says so with its count rather than stretching the survivors to 100.
        List<FlFrameRecord> records = SessionFixtures.Stream(1000, FlMeasured.OutputRes);
        records.RemoveRange(700, 50);
        SessionProgressEvent e = SessionProgressCalculator.Compute(_guid, Progress(records, gaps: [700]), SessionFixtures.QpcFrequency, 10, gpu: null);

        e.Presents5s.Should().BeInRange(445, 455);
        e.PresentedFps5s.Should().BeApproximately(90, 1.5, "450 presents over 5 s — the missing frames are missing, not invented");
    }
}
