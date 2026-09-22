using System.Reflection;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// <c>04_CAPTURE</c> §Finalizing: the FULL blob set, the discard rule, one insert, the sweep. The column
/// list is read off <see cref="FrameBlobs"/> by reflection and checked against what the finalizer filled,
/// so a column added to the DTO without a producer here turns this red.
/// </summary>
public sealed class SessionFinalizerTests
{
    private const FlMeasured _everything = FlMeasured.OutputRes | FlMeasured.PresentArgs | FlMeasured.Upscaler | FlMeasured.UpscalerParams
        | FlMeasured.Fg | FlMeasured.FgCounts | FlMeasured.Rt | FlMeasured.Pso | FlMeasured.Vram | FlMeasured.Latency | FlMeasured.Hdr;

    private static (SessionFinalizer Finalizer, FakeSessionRepository Repo) Make()
    {
        var repo = new FakeSessionRepository();
        return (new SessionFinalizer(repo, new RawSeriesCodec()), repo);
    }

    [Fact]
    public void EveryFrameBlobColumnIsFilledWhenEverythingWasMeasured()
    {
        (SessionFinalizer finalizer, _) = Make();
        List<FlFrameRecord> records = SessionFixtures.Stream(200, _everything, fgPerBatch: 2, upscaler: FlUpscaler.Dlss);
        records[100] = records[100] with { RenderW = 1280, RenderH = 720 };    // the render size moves once, so render_res is stored

        FinalizedSession built = finalizer.Build(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(), Hooked = SessionFixtures.Hooked(records) });

        FrameBlobs frames = built.Frames!;
        frames.SampleCount.Should().Be(200);
        frames.Codec.Should().Be("raw-le-test");
        foreach (PropertyInfo p in typeof(FrameBlobs).GetProperties().Where(p => p.PropertyType == typeof(ReadOnlyMemory<byte>?) && !string.Equals(p.Name, nameof(FrameBlobs.SwapchainIds), StringComparison.Ordinal)))
        {
            var value = (ReadOnlyMemory<byte>?)p.GetValue(frames);
            value.Should().NotBeNull($"{p.Name} was measured, so 06_DATA_MODEL's column must be filled — not skipped");
        }

        frames.SwapchainIds.Should().BeNull("one stream: the schema stores the ids only when the session held more than one");
        RawSeriesCodec.Floats(frames.FrameTimes)[1].Should().BeApproximately(10f, 0.001f);
        RawSeriesCodec.Floats(frames.FrameTimes)[0].Should().Be(0, "no interval into the first record");
        byte[] flags = frames.FrameFlags.ToArray();
        ((FrameFlagBits)flags[0]).Should().HaveFlag(FrameFlagBits.Gap);
        ((FrameFlagBits)flags[1]).Should().HaveFlag(FrameFlagBits.Generated, "record 1 carried no evaluation at ×2");
        ((FrameFlagBits)flags[2]).Should().NotHaveFlag(FrameFlagBits.Generated);
        RawSeriesCodec.UInt16s(frames.RenderRes!.Value).Should().HaveCount(800, "two pairs per frame");
        RawSeriesCodec.UInt32s(frames.LatencyUs!.Value)[0].Should().Be(20_000, "Reflex latency is in the set, not omitted");
    }

    [Fact]
    public void WhatWasNotMeasuredStaysNullAndOneStreamStoresNoSwapchainIds()
    {
        (SessionFinalizer finalizer, _) = Make();
        List<FlFrameRecord> records = SessionFixtures.Stream(100, FlMeasured.OutputRes | FlMeasured.PresentArgs);

        FrameBlobs frames = finalizer.Build(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(), Hooked = SessionFixtures.Hooked(records) }).Frames!;

        frames.SwapchainIds.Should().BeNull();
        frames.RtFlags.Should().BeNull();
        frames.RenderRes.Should().BeNull("parameters never measured");
        frames.DispatchRays.Should().BeNull();
        frames.PsoCreated.Should().BeNull();
        frames.VramProc.Should().BeNull();
        frames.LatencyUs.Should().BeNull();
        frames.FrameIndex.Should().NotBeNull("the writer's counter is always there");
    }

    [Fact]
    public void AConstantRenderResolutionIsNotStoredPerFrame()
    {
        (SessionFinalizer finalizer, _) = Make();
        List<FlFrameRecord> records = SessionFixtures.Stream(100, FlMeasured.OutputRes | FlMeasured.PresentArgs | FlMeasured.UpscalerParams);

        FrameBlobs frames = finalizer.Build(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(), Hooked = SessionFixtures.Hooked(records) }).Frames!;

        frames.RenderRes.Should().BeNull("one tuple throughout: the row's render_w/h carry it");
    }

    [Fact]
    public void SensorsBecomeOneSeriesPerFieldAlignedToTMsWithMinusOneForATickWithNoReading()
    {
        (SessionFinalizer finalizer, _) = Make();
        List<Application.Telemetry.TelemetrySample> sensors = SessionFixtures.Sensors(3);
        sensors[1] = sensors[1] with { Sample = sensors[1].Sample with { TempCoreC = null } };

        FinalizedSession built = finalizer.Build(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(tier: CaptureTier.NotHooked), Sensors = sensors });

        built.Frames.Should().BeNull("a Tier-2 session has no frames");
        built.Row.Tier.Should().Be(CaptureTier.NotHooked);
        built.Sensors.Select(s => s.Series).Should().Equal("t_ms", "gpu_temp", "gpu_load", "vram_adapter");
        RawSeriesCodec.Floats(built.Sensors[0].Data).Should().Equal(0f, 1000f, 2000f);
        RawSeriesCodec.Floats(built.Sensors[1].Data).Should().Equal(60f, SessionFinalizer.SensorMissing, 62f);
        built.Sensors.Should().AllSatisfy(s => s.Hz.Should().Be(1));
    }

    [Fact]
    public void TheMachinesReadingsBecomeTheCpuAndMemorySeriesTheSchemaAlwaysAllowed()
    {
        (SessionFinalizer finalizer, _) = Make();

        FinalizedSession built = finalizer.Build(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(tier: CaptureTier.NotHooked), Sensors = SessionFixtures.SensorsWithSystem(3) });

        built.Sensors.Select(s => s.Series).Should().Equal(["t_ms", "gpu_temp", "gpu_load", "vram_adapter", "cpu_load", "ram_mb"], "no tick carried a CPU temperature, so there is no cpu_temp series - absent, not a row of -1");
        RawSeriesCodec.Floats(built.Sensors[4].Data).Should().Equal(SessionFinalizer.SensorMissing, 21f, 22f);
        RawSeriesCodec.Floats(built.Sensors[5].Data).Should().Equal(16000f, 16000f, 16000f);

        FinalizedSession hot = finalizer.Build(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(tier: CaptureTier.NotHooked), Sensors = SessionFixtures.SensorsWithSystem(3, cpuTemp: 70) });
        hot.Sensors.Select(s => s.Series).Should().Contain("cpu_temp");
    }

    [Fact]
    public async Task TooShortIsDiscardedAndNothingIsWritten()
    {
        (SessionFinalizer finalizer, FakeSessionRepository repo) = Make();

        FinalizeOutcome outcome = await finalizer.FinalizeAsync(new FinalizeInput { Skeleton = SessionFixtures.Skeleton(seconds: 29.9) }, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(FinalizeStatus.Discarded);
        repo.Stored.Should().BeEmpty();
        repo.Sweeps.Should().BeEmpty();
    }

    [Fact]
    public async Task ASavedSessionIsInsertedOnceAndTheRetentionSweepFollows()
    {
        (SessionFinalizer finalizer, FakeSessionRepository repo) = Make();
        repo.SweptPerCall = 2;
        var guid = Guid.NewGuid();
        var input = new FinalizeInput
        {
            Skeleton = SessionFixtures.Skeleton(guid),
            Hooked = SessionFixtures.Hooked(SessionFixtures.Stream(500, FlMeasured.OutputRes | FlMeasured.PresentArgs)),
            RetentionKeep = 5,
        };

        FinalizeOutcome saved = await finalizer.FinalizeAsync(input, TestContext.Current.CancellationToken);
        FinalizeOutcome again = await finalizer.FinalizeAsync(input, TestContext.Current.CancellationToken);

        saved.Status.Should().Be(FinalizeStatus.Saved);
        saved.SessionId.Should().Be(1);
        saved.RetentionSwept.Should().Be(2);
        repo.Sweeps.Should().Equal((7L, 5));
        again.Status.Should().Be(FinalizeStatus.AlreadyStored, "recovery after a finalize that landed must not store the session twice");
        repo.Stored.Should().ContainSingle();
    }

    /// <summary>
    /// 2026-09-22 23:26 and 2026-09-23 00:25 on the owner's machine: a GIRLS' FRONTLINE 2 session's entry was removed
    /// while it ran, and the insert died on the foreign key ("FAULTED SqliteException … FOREIGN KEY constraint failed").
    /// With no row left for the session, that is an outcome with a name, and nothing is written.
    /// </summary>
    [Fact]
    public async Task ASessionWhoseGameWasRemovedWhileItRanIsNotWrittenAndSaysSo()
    {
        (SessionFinalizer finalizer, FakeSessionRepository repo) = Make();
        repo.Owner = static (_, _, _) => null;
        var input = new FinalizeInput { Skeleton = SessionFixtures.Skeleton(), ExePath = @"D:\SteamLibrary\steamapps\common\GF2\GF2_Exilium.exe" };

        FinalizeOutcome outcome = await finalizer.FinalizeAsync(input, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(FinalizeStatus.GameRemoved);
        outcome.SessionId.Should().BeNull();
        repo.Stored.Should().BeEmpty();
        repo.Sweeps.Should().BeEmpty();
        repo.OwnerLookups.Should().Equal((7L, @"D:\SteamLibrary\steamapps\common\GF2\GF2_Exilium.exe"));
    }

    [Fact]
    public async Task ASessionWhoseEntryWasReimportedIsStoredUnderTheRowThatHoldsItsExecutableNow()
    {
        (SessionFinalizer finalizer, FakeSessionRepository repo) = Make();
        repo.Owner = static (_, path, _) => path is null ? null : 42;
        var input = new FinalizeInput { Skeleton = SessionFixtures.Skeleton(), ExePath = @"D:\Games\Title\game.exe", RetentionKeep = 5 };

        FinalizeOutcome outcome = await finalizer.FinalizeAsync(input, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(FinalizeStatus.Saved);
        outcome.GameId.Should().Be(42, "the row that holds the session's executable now");
        repo.Stored.Should().ContainSingle().Which.Row.GameId.Should().Be(42);
        repo.Sweeps.Should().Equal((42L, 5));
    }

    [Fact]
    public async Task ARemovalBetweenTheLookupAndTheWriteIsTheSameAnswer()
    {
        (SessionFinalizer finalizer, FakeSessionRepository repo) = Make();
        repo.InsertFailure = new SessionOwnerMissingException("games row 7 no longer exists");

        FinalizeOutcome outcome = await finalizer.FinalizeAsync(new FinalizeInput { Skeleton = SessionFixtures.Skeleton() }, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(FinalizeStatus.GameRemoved, "the write transaction's own check, not the foreign key");
        repo.Sweeps.Should().BeEmpty();
    }
}
