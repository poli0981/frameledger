using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Application.Tests.Capture;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// One session end to end through fakes: the loop is the real <see cref="CaptureSession"/> over a fake
/// ring, the consent store is the real SQLite adapter, and everything the recorder is supposed to do
/// around the loop — the <c>.partial</c>, the telemetry drain, the exit classification, the finalize, the
/// crash policy — is observed on the fakes.
/// </summary>
public sealed class SessionRecorderTests : IAsyncDisposable
{
    private const string _exe = @"C:\Games\Title\game.exe";
    private const int _pid = 4242;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-recorder-" + Guid.NewGuid().ToString("N"));
    private LedgerDatabase? _db;

    private static ExecutableFingerprint Fingerprint => new() { ExePath = _exe, SizeBytes = 90_000, MtimeUnixMs = 1_700_000_000_000 };

    private static readonly FakeLiveness _alive = new(exited: false, exitCode: null);
    private static readonly FakeLiveness _crashed = new(exited: true, exitCode: -1073741819);

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync().ConfigureAwait(false);
        }

        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// A clock that advances five seconds per drain call (the sink moves it), so a 150 ms test session lasts
    /// a minute; its timestamp is the same clock, so the flush interval and the records' QPC follow it too.
    /// </summary>
    private sealed class SteppingClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public override long GetTimestamp() => Now.UtcTicks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }

    private sealed class FakeGuard : IAntiCheatGuard
    {
        public AntiCheatVerdict Verdict { get; set; } = AntiCheatVerdict.Allowed();

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, CancellationToken ct = default) => ValueTask.FromResult(AntiCheatVerdict.Allowed());

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, CancellationToken ct = default) => ValueTask.FromResult(Verdict);

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, CancellationToken ct = default) =>
            ValueTask.FromResult(Verdict);

        public ValueTask<AntiCheatVerdict> PreScanGameAsync(string executablePath, CancellationToken ct = default) =>
            ValueTask.FromResult(AntiCheatVerdict.Allowed());
    }

    private sealed class FakeSink(SteppingClock clock, int recordsToServe) : ICaptureSink
    {
        private int _served;

        public FlWriterState WriterState => new() { Status = (uint)FlStatus.Ready, HooksInstalledMask = 1, RuntimeCensus = 0x8000_0000 };

        public FlShmHandshake Handshake => default;

        public long TotalDropped => 0;

        public long TotalGaps => 0;

        public DrainResult Drain(Span<FlFrameRecord> into, IList<ulong> gapIndices)
        {
            clock.Now = clock.Now.AddSeconds(5);
            int n = Math.Min(into.Length, recordsToServe - _served);
            for (int i = 0; i < n; i++)
            {
                into[i] = new FlFrameRecord
                {
                    FrameIndex = (uint)(_served + i),
                    Qpc = (ulong)(clock.Now.UtcTicks + (_served + i) * 100_000L),    // 10 ms apart on the clock's own ticks
                    SwapchainId = 1,
                    Api = (byte)FlApi.D3D12,
                    MeasuredMask = (ushort)(FlMeasured.OutputRes | FlMeasured.PresentArgs),
                };
            }

            _served += n;
            return new DrainResult(n, 0, 0);
        }

        public void PublishGuardResult(uint completedEvaluations, bool unhookRequested)
        {
        }

        public void SetPaused(bool paused)
        {
        }

        public void RequestLogFlush()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeLiveness(bool exited, int? exitCode) : ITargetLiveness
    {
        public bool HasExited => exited;

        public int? ExitCode => exitCode;

        public bool IsForeground => true;

        public void Dispose()
        {
        }
    }

    private sealed class FixedResolver : ITargetResolver
    {
        public int? Resolve(string normalisedExePath, out SessionEndReason reason)
        {
            reason = SessionEndReason.Running;
            return _pid;
        }

        public bool IsRunning(string normalisedExePath) => false;
    }

    private sealed class FakePoller(SteppingClock? steps = null) : ITelemetryPoller
    {
        private int _served;

        public string Descriptor => "l1+lhm";

        public long Dropped => 0;

        public void Start()
        {
        }

        public int Drain(ICollection<TelemetrySample> into)
        {
            // A held Tier-2 session's only clock is its ticks (2026-09-22): a poller told to step the clock makes each one 5 s.
            if (steps is not null)
            {
                steps.Now += TimeSpan.FromSeconds(5);
            }

            into.Add(new TelemetrySample(Stopwatch.GetTimestamp(), new GpuSample { TakenAt = DateTimeOffset.UtcNow, Layer = TelemetryLayer.Lhm, TempCoreC = 60 + _served++ }));
            return 1;
        }

        public void Dispose()
        {
        }
    }

    private sealed class Factory(IGameConsentStore store, FakeGuard guard, SteppingClock clock, int records, FakeLiveness liveness) : ICaptureSessionFactory
    {
        public CaptureSession Create(ICaptureObserver observer) => new(
            store, new HookedCaptureGate(guard), guard, new FixedResolver(),
            new DelegateLivenessSource(_ => liveness),
            new DelegateRingAttacher(_ => (new FakeSink(clock, records), ShmAttachRefusal.Ok)),
            new CaptureOptions
            {
                DrainInterval = TimeSpan.FromMilliseconds(1),
                ScanInterval = TimeSpan.FromMilliseconds(5),
                AttachBudget = TimeSpan.FromMilliseconds(50),
                MaxDuration = TimeSpan.FromMilliseconds(150),
                LogFlushGrace = TimeSpan.FromMilliseconds(1),
                HoldInterval = TimeSpan.FromMilliseconds(1),
            },
            observer: observer);
    }

    private sealed class NoCrashEvents : ICrashEventSource
    {
        public bool FoundCrash(string exeFileName, DateTimeOffset windowStart, DateTimeOffset windowEnd) => false;
    }

    private sealed class FixedHardware : IHardwareSnapshotSource
    {
        public HardwareSnapshot Take() => new() { CpuName = "cpu", GpuName = "gpu", OsBuild = "10.0" };
    }

    private sealed class FakeSnapshots : IHardwareSnapshotRepository
    {
        public ValueTask<long> EnsureAsync(HardwareSnapshot snapshot, DateTimeOffset capturedAt, CancellationToken ct = default) => ValueTask.FromResult(3L);

        public ValueTask<HardwareSnapshot?> FindAsync(long id, CancellationToken ct = default) => ValueTask.FromResult<HardwareSnapshot?>(null);
    }

    private sealed class Harness
    {
        public required SessionRecorder Recorder { get; init; }

        public required FakeGameRepository Games { get; init; }

        public required FakeSessionRepository Sessions { get; init; }

        public required FakePartialSessionStore Partials { get; init; }

        public required SteppingClock Clock { get; init; }
    }

    /// <summary>What the driver-profile source answers (beta.8); a throwing one proves a failed read costs the column, not the session.</summary>
    private sealed class ScriptedProfiles(Func<string, DriverProfileReading> answer) : IDriverProfileSource
    {
        public List<string> Read { get; } = [];

        DriverProfileReading IDriverProfileSource.Read(string exePath)
        {
            Read.Add(exePath);
            return answer(exePath);
        }
    }

    private async Task<Harness> MakeAsync(bool consented = true, AntiCheatVerdict? verdict = null, int records = 3_000, FakeLiveness? liveness = null, bool poller = true,
        bool clockStepsOnDrain = false, IDriverProfileSource? profiles = null)
    {
        _db ??= await LedgerDatabase.OpenAsync(Path.Combine(_dir, LedgerPaths.DatabaseFileName), ct: TestContext.Current.CancellationToken).ConfigureAwait(false);
        var store = new SqliteGameConsentStore(_db);
        if (consented)
        {
            await store.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
            {
                Fingerprint = Fingerprint,
                DisclosureVersion = "operator-disclosure/test",
                AcknowledgedAt = DateTimeOffset.UnixEpoch,
            }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        var clock = new SteppingClock();
        var guard = new FakeGuard { Verdict = verdict ?? AntiCheatVerdict.Allowed() };
        var games = new FakeGameRepository();
        var sessions = new FakeSessionRepository();
        var partials = new FakePartialSessionStore();
        var recorder = new SessionRecorder(
            new Factory(store, guard, clock, records, liveness ?? _alive),
            games, new FakeSnapshots(), new FixedHardware(), partials,
            new SessionFinalizer(sessions, new RawSeriesCodec()), new NoCrashEvents(),
            _ => poller ? new FakePoller(clockStepsOnDrain ? clock : null) : null, clock,
            new RecorderOptions { PartialFlushInterval = TimeSpan.FromSeconds(10) }, profiles: profiles);
        return new Harness { Recorder = recorder, Games = games, Sessions = sessions, Partials = partials, Clock = clock };
    }

    private static RecordRequest Request(CaptureMode mode = CaptureMode.Attach) => new()
    {
        NormalisedExePath = _exe,
        Observed = Fingerprint,
        PayloadPath = "payload.dll",
        Mode = mode,
    };

    [Fact]
    public async Task AHookedSessionIsRecordedEndToEnd()
    {
        Harness h = await MakeAsync();

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Outcome.Reason.Should().Be(SessionEndReason.Running, "a bounded capture ends on its own limit");
        r.ExitStatus.Should().Be(ExitStatus.Normal);
        r.Finalize.Status.Should().Be(FinalizeStatus.Saved, "the stepping clock made it a minute long");
        FinalizedSession stored = h.Sessions.Stored.Single();
        stored.Row.SessionGuid.Should().Be(r.SessionGuid);
        stored.Row.Tier.Should().Be(CaptureTier.Hooked);
        stored.Row.Mode.Should().Be(CaptureMode.Attach);
        stored.Row.LateAttach.Should().BeTrue();
        stored.Row.FrameCount.Should().Be(3_000);
        stored.Row.GameId.Should().Be(1);
        stored.Row.SnapshotId.Should().Be(3);
        stored.Row.TelemetrySource.Should().Be("l1+lhm");
        stored.Row.AvgGpuTemp.Should().NotBeNull("the poller was drained on the loop's ticks");
        stored.Row.CaptureNotes.Should().Be("end=Running");
        stored.Row.QpcFrequency.Should().Be(TimeSpan.TicksPerSecond, "the recorder stores its clock's frequency");
        stored.Frames.Should().NotBeNull();
        stored.Sensors.Should().NotBeEmpty();
        h.Games.Injections.Should().ContainSingle().Which.GameId.Should().Be(1);
        h.Games.CrashCount.Should().Be(0);
        h.Partials.Files.Should().BeEmpty("the .partial is deleted once the row is in");
        h.Partials.Deleted.Should().Equal(r.SessionGuid);
        r.Row.FrameCount.Should().Be(3_000, "the row comes back to the caller as finalized");
    }

    /// <summary>
    /// beta.8: the NVIDIA driver profile the executable runs under is read beside the session and stored on its row; a
    /// source that throws costs the column and nothing else.
    /// </summary>
    [Fact]
    public async Task TheDriverProfileIsStoredOnTheRowAndAFailedReadCostsOnlyTheColumn()
    {
        var profiles = new ScriptedProfiles(static _ => new DriverProfileReading
        {
            Outcome = DriverProfileOutcome.Global,
            ProfileName = "Base Profile",
            Settings = [new DriverSettingReading(NvidiaDriverSettings.DlssSrOverride, null, DriverSettingLocation.NotSet, false)],
        });
        Harness h = await MakeAsync(profiles: profiles);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        profiles.Read.Should().Equal(_exe);
        DriverProfileRecord stored = DriverProfileRecord.Parse(h.Sessions.Stored.Single().Row.DriverProfileJson)!;
        (stored.Outcome, stored.Profile).Should().Be(("Global", "Base Profile"));

        Harness failing = await MakeAsync(profiles: new ScriptedProfiles(static _ => throw new InvalidOperationException("the driver refused")));
        RecordedSession r = await failing.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);
        r.Row.DriverProfileJson.Should().BeNull();
        r.Finalize.Status.Should().Be(FinalizeStatus.Saved, "the session is recorded as it would have been");
    }

    [Fact]
    public async Task ThePartialCarriesTheBreadcrumbsAndIsFlushedOnTheClock()
    {
        Harness h = await MakeAsync();
        FakePartialSessionStore.Entry? entry = null;
        h.Partials.Files.Should().BeEmpty();

        // Capture the entry before it is deleted: the store keeps the writer object.
        RecordedSession r = await h.Recorder.RecordAsync(Request(CaptureMode.Attach), TestContext.Current.CancellationToken);
        entry = FindEntry(h, r.SessionGuid);

        entry.Should().NotBeNull();
        entry!.Notes.Select(n => n.Text).Should().ContainInOrder("started Attach", $"attached pid={_pid} layout=0 build=", "ended Running", "finalizing Normal");
        entry.Ticks.Should().BeGreaterThan(1, "a 10 s interval against a clock stepping 5 s per drain flushes several times");
        entry.Records.Should().HaveCount(3_000, "the final forced flush wrote everything");
        entry.Sensors.Should().NotBeEmpty();
        entry.Disposed.Should().BeTrue();
    }

    private static FakePartialSessionStore.Entry? FindEntry(Harness h, Guid guid) =>
        h.Partials.Created.TryGetValue(guid, out FakePartialSessionStore.Entry? e) ? e : null;

    [Fact]
    public async Task ARefusedSessionIsATierTwoRowWithTheReasonAndNothingInjected()
    {
        Harness h = await MakeAsync(verdict: AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll"));

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Outcome.Reason.Should().Be(SessionEndReason.RefusedByGuard);
        r.Row.Tier.Should().Be(CaptureTier.NotHooked);
        r.Row.CaptureNotes.Should().Contain("end=RefusedByGuard").And.Contain("tier2").And.Contain("guard=BlockedModule|BattlEye|BEClient_x64.dll");
        r.Row.FrameCount.Should().Be(0);
        r.Outcome.HeldUnhooked.Should().BeTrue("a refused session is held, unhooked, until the target exits (2026-09-22)");
        r.Finalize.Status.Should().Be(FinalizeStatus.Discarded, "the fake clock does not move on the hold's ticks unless told to, so this one is under the minimum");
        h.Games.Injections.Should().BeEmpty();
        h.Partials.Files.Should().BeEmpty();
    }

    /// <summary>
    /// Tier 2 as the product specifies it (2026-09-22; <c>04_CAPTURE</c> §Tier selection): a refused session that lasts is SAVED —
    /// duration, the machine's telemetry and the reason — with every frame-derived value absent. Until this date every
    /// refusal returned within a second and was discarded under the minimum length.
    /// </summary>
    [Fact]
    public async Task ARefusedSessionThatLastsIsSavedAsTierTwoWithItsSensorsAndItsReason()
    {
        Harness h = await MakeAsync(verdict: AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedDriver, "Riot Vanguard", "vgk.sys"), clockStepsOnDrain: true);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Outcome.Reason.Should().Be(SessionEndReason.RefusedByGuard);
        r.Finalize.Status.Should().Be(FinalizeStatus.Saved, "the hold's ticks stepped the clock past the minimum");
        FinalizedSession stored = h.Sessions.Stored.Single();
        stored.Row.Tier.Should().Be(CaptureTier.NotHooked);
        stored.Row.FrameCount.Should().Be(0);
        stored.Frames.Should().BeNull("nothing was measured");
        stored.Sensors.Should().NotBeEmpty("the poller was drained on the hold's ticks");
        stored.Row.AvgGpuTemp.Should().NotBeNull();
        stored.Row.CaptureNotes.Should().Contain("end=RefusedByGuard").And.Contain("guard=BlockedDriver|Riot Vanguard|vgk.sys");
        h.Games.Injections.Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnconsentedGameIsRefusedBeforeTheGuardAndStillGetsAGamesRow()
    {
        Harness h = await MakeAsync(consented: false);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Outcome.Reason.Should().Be(SessionEndReason.RefusedHookNotEnabled);
        h.Games.Rows.Should().ContainKey(_exe, "a Tier-2 session has somewhere to land");
        h.Games.Rows[_exe].HookEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task AnEarlyCrashIsCountedAndTheSecondOneDisablesHooking()
    {
        Harness h = await MakeAsync(liveness: _crashed, records: 10);

        RecordedSession first = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);
        RecordedSession second = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        first.Outcome.Reason.Should().Be(SessionEndReason.TargetExited);
        first.ExitStatus.Should().Be(ExitStatus.Crashed);
        first.CrashPolicy.Should().Be(CrashPolicyOutcome.Counted);
        second.CrashPolicy.Should().Be(CrashPolicyOutcome.HookingDisabled);
        h.Games.Disabled.Should().ContainSingle();
        first.Row.CaptureNotes.Should().Contain("exit_code=-1073741819");
    }

    /// <summary>
    /// 2026-09-23 00:24–00:25 on the owner's machine: GIRLS' FRONTLINE 2's entry was removed and re-imported while its
    /// session ran, and the finalize died on the foreign key ("session: FAULTED … FOREIGN KEY constraint failed"), leaving
    /// a <c>.partial</c> that would have stopped the next start. The session lands on the row that holds its executable now,
    /// and the injection stamp follows it there.
    /// </summary>
    [Fact]
    public async Task AnEntryRemovedAndReimportedMidSessionGetsTheSessionAndTheInjectionStamp()
    {
        Harness h = await MakeAsync();
        h.Sessions.Owner = static (_, path, _) => string.Equals(path, _exe, StringComparison.OrdinalIgnoreCase) ? 42 : null;

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Finalize.Status.Should().Be(FinalizeStatus.Saved);
        r.Finalize.GameId.Should().Be(42);
        r.Row.GameId.Should().Be(42);
        h.Sessions.Stored.Single().Row.GameId.Should().Be(42);
        h.Games.Injections.Should().ContainSingle().Which.GameId.Should().Be(42, "the stamp goes to the row that owns the session now");
        h.Partials.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEntryRemovedWithItsSessionsMidSessionEndsAsGameRemovedAndLeavesNoPartial()
    {
        Harness h = await MakeAsync();
        h.Sessions.Owner = static (_, _, _) => null;

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Finalize.Status.Should().Be(FinalizeStatus.GameRemoved, "the user removed the game and its sessions; this was one of them");
        h.Sessions.Stored.Should().BeEmpty();
        h.Games.Injections.Should().BeEmpty("there is no row to stamp");
        h.Partials.Files.Should().BeEmpty("nothing is left for a recovery that would only reach the same answer");
        h.Partials.Deleted.Should().Equal(r.SessionGuid);
    }

    [Fact]
    public async Task WithoutAPollerTheSessionStillRecordsAndTheDescriptorIsNull()
    {
        Harness h = await MakeAsync(poller: false);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Finalize.Status.Should().Be(FinalizeStatus.Saved);
        h.Sessions.Stored.Single().Row.TelemetrySource.Should().BeNull();
        h.Sessions.Stored.Single().Sensors.Should().BeEmpty();
    }
}
