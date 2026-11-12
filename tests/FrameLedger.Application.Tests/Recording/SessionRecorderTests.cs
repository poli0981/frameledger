// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Application.Tests.AntiCheat;
using FrameLedger.Application.Tests.Capture;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Display;
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
    /// A clock that advances <see cref="Step"/> per drain call (the sink moves it); its timestamp is the same clock, so the
    /// flush interval and the records' QPC follow it too.
    /// </summary>
    /// <remarks>
    /// <b>How many drains a session gets is the machine's, not the test's</b> (2026-10-03, beta.12 PR-0): the loop is bounded
    /// by <c>MaxDuration</c> on the WALL clock, and a loaded runner fits fewer 1 ms drains into it — the beta.11 trial theory
    /// went red on <c>main</c> with a hooked session under the 30 s minimum. So the harness's minimum is one step: a session
    /// that drained at all is over it, and a held session whose clock never moved stays under it, on any machine.
    /// </remarks>
    private sealed class SteppingClock : TimeProvider
    {
        public static readonly TimeSpan Step = TimeSpan.FromSeconds(5);

        public DateTimeOffset Now { get; set; } = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public override long GetTimestamp() => Now.UtcTicks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }

    private sealed class FakeGuard : IAntiCheatGuard
    {
        public AntiCheatVerdict Verdict { get; set; } = AntiCheatVerdict.Allowed();

        /// <summary>What every re-scan answers (D48's tests): allowed unless a test says otherwise.</summary>
        public AntiCheatVerdict Rescan { get; set; } = AntiCheatVerdict.Allowed();

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, string? toleratedFamily, CancellationToken ct = default) => ValueTask.FromResult(Rescan);

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, string? toleratedFamily, CancellationToken ct = default) => ValueTask.FromResult(Verdict);

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, string? toleratedFamily, CancellationToken ct = default) =>
            ValueTask.FromResult(Verdict);

        public ValueTask<AntiCheatVerdict> PreScanGameAsync(string executablePath, string? toleratedFamily, CancellationToken ct = default) =>
            ValueTask.FromResult(AntiCheatVerdict.Allowed());
    }

    /// <summary>Region 5 for a recorder test (train B): what the fake Overlay publishes, and what the loop asked of it.</summary>
    private sealed class InGameScript
    {
        private uint _published;

        /// <summary>What each publication carries; null publishes nothing. The count of publications goes in.</summary>
        public Func<uint, FlAdvancedState>? Publish { get; init; }

        /// <summary>The hook families the capture side says are in: the present hook alone unless a test says more.</summary>
        public FlHookFamily Hooks { get; init; } = FlHookFamily.Present;

        public List<bool> Asked { get; } = [];

        /// <summary>How many publications were handed out.</summary>
        public uint Published => _published;

        /// <summary>A new publication on every read, as if the watchdog ticked as often as the test's loop.</summary>
        public FlAdvancedState? Next() => Publish?.Invoke(++_published);
    }

    private sealed class FakeSink(SteppingClock clock, int recordsToServe, ulong adapterLuid = 0, InGameScript? inGame = null) : ICaptureSink
    {
        private int _served;

        public FlAdvancedState? AdvancedState => inGame?.Asked is [.., true] ? inGame.Next() : null;

        public void SetAdvancedRequested(bool requested) => inGame?.Asked.Add(requested);

        public FlWriterState WriterState => new() { Status = (uint)FlStatus.Ready, HooksInstalledMask = (uint)(inGame?.Hooks ?? FlHookFamily.Present), RuntimeCensus = 0x8000_0000 };

        public FlShmHandshake Handshake => new() { AdapterLuid = adapterLuid };

        public FlDisplayState? DisplayState => null;

        public long TotalDropped => 0;

        public long TotalGaps => 0;

        public DrainResult Drain(Span<FlFrameRecord> into, IList<ulong> gapIndices)
        {
            clock.Now += SteppingClock.Step;
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

        public IReadOnlyList<int> PidsOf(string normalisedExePath) => [];
    }

    private sealed class FakePoller(SteppingClock? steps = null) : ITelemetryPoller
    {
        private int _served;

        public List<ulong> Adapters { get; } = [];

        public string Descriptor => "l1+lhm";

        public bool FollowAdapter(ulong luid)
        {
            Adapters.Add(luid);
            return true;
        }

        public long Dropped => 0;

        public void Start()
        {
        }

        public int Drain(ICollection<TelemetrySample> into)
        {
            // A held Tier-2 session's only clock is its ticks (2026-09-22): a poller told to step the clock makes each one 5 s.
            if (steps is not null)
            {
                steps.Now += SteppingClock.Step;
            }

            into.Add(new TelemetrySample(Stopwatch.GetTimestamp(), new GpuSample { TakenAt = DateTimeOffset.UtcNow, Layer = TelemetryLayer.Lhm, TempCoreC = 60 + _served++ }));
            return 1;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>D33: the Overlay's channel, always published.</summary>
    private sealed class OpenChannel : IOverlayToleranceChannel
    {
        public IDisposable? TryPublish(int pid, string family) => new MemoryStream();
    }

    /// <summary>The window reader (beta.10), answering the same window on every tick.</summary>
    private sealed class FixedWindow(WindowView? answer) : IDisplayProbe
    {
        public WindowView? Observe(IReadOnlyCollection<int> pids, ulong hwnd) => answer;
    }

    private sealed class Factory(IGameConsentStore store, FakeGuard guard, SteppingClock clock, int records, FakeLiveness liveness,
        IUserModeExceptionSwitch? exceptions = null, IDisplayProbe? display = null, ulong adapterLuid = 0, InGameScript? inGame = null) : ICaptureSessionFactory
    {
        public CaptureSession Create(ICaptureObserver observer) => new(
            store, new HookedCaptureGate(guard), guard, new FixedResolver(),
            new DelegateLivenessSource(_ => liveness),
            new DelegateRingAttacher(_ => (new FakeSink(clock, records, adapterLuid, inGame), ShmAttachRefusal.Ok)),
            new CaptureOptions
            {
                DrainInterval = TimeSpan.FromMilliseconds(1),
                ScanInterval = TimeSpan.FromMilliseconds(5),
                AttachBudget = TimeSpan.FromMilliseconds(50),
                MaxDuration = TimeSpan.FromMilliseconds(150),
                LogFlushGrace = TimeSpan.FromMilliseconds(1),
                HoldInterval = TimeSpan.FromMilliseconds(1),
            },
            observer: observer,
            exceptions: exceptions,
            tolerance: exceptions is null ? null : new OpenChannel(),
            display: display);
    }

    private sealed class NoCrashEvents : ICrashEventSource
    {
        public bool FoundCrash(CrashQuery query) => false;
    }

    /// <summary>beta.14: a game that started an engine's crash reporter <paramref name="beforeEnd"/> before the session ended, 20 min after it started.</summary>
    private sealed class OneReporter(TimeSpan beforeEnd) : ICrashReporterSightings
    {
        public Task<CrashReporterSighting?> FindAsync(CrashQuery query, CancellationToken ct = default)
        {
            DateTimeOffset endedAt = query.WindowEnd - ExitStatusMapper.CrashWitnessGrace;
            return Task.FromResult<CrashReporterSighting?>(new CrashReporterSighting("CrashReportClient.exe", query.TargetPid, query.ExePath,
                endedAt - beforeEnd - TimeSpan.FromMinutes(20), endedAt - beforeEnd));
        }
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

        public required SqliteGameConsentStore Store { get; init; }

        public FakePoller? Poller { get; init; }
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
        bool clockStepsOnDrain = false, IDriverProfileSource? profiles = null, bool excepted = false, IDisplayProbe? display = null, ulong adapterLuid = 0,
        AntiCheatVerdict? rescan = null, ICrashReporterSightings? reporters = null, IAdvancedHardware? advanced = null, bool advancedOn = false,
        InGameScript? inGame = null)
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

        if (excepted)
        {
            await ExceptAsync(store).ConfigureAwait(false);
        }

        var clock = new SteppingClock();
        var guard = new FakeGuard { Verdict = verdict ?? AntiCheatVerdict.Allowed(), Rescan = rescan ?? AntiCheatVerdict.Allowed() };
        var games = new FakeGameRepository();
        var sessions = new FakeSessionRepository();
        var partials = new FakePartialSessionStore();
        FakePoller? fakePoller = poller ? new FakePoller(clockStepsOnDrain ? clock : null) : null;
        var recorder = new SessionRecorder(
            new Factory(store, guard, clock, records, liveness ?? _alive, excepted ? new FakeExceptionSwitch(on: true) : null, display, adapterLuid, inGame),
            games, new FakeSnapshots(), new FixedHardware(), partials,
            new SessionFinalizer(sessions, new RawSeriesCodec()), new NoCrashEvents(),
            _ => fakePoller, clock,
            new RecorderOptions { PartialFlushInterval = TimeSpan.FromSeconds(10), MinimumSessionLength = SteppingClock.Step, AdvancedMetrics = advancedOn }, profiles: profiles,
            exceptionLapse: new UserModeExceptionLapsePolicy(store, sessions), reporters: reporters, advanced: advanced);
        return new Harness { Recorder = recorder, Games = games, Sessions = sessions, Partials = partials, Clock = clock, Store = store, Poller = fakePoller };
    }

    /// <summary>D33: block the consented row for NetEase Yidun, grant its exception on these bytes, and turn hooking on again.</summary>
    private static async Task ExceptAsync(SqliteGameConsentStore store)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await store.RecordGuardBlockAsync(Fingerprint, AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatFile, "NetEase Yidun", "NEP2.dll"), ct)
            .ConfigureAwait(false);
        await store.GrantAntiCheatExceptionAsync(new AntiCheatExceptionGrantRequest
        {
            Fingerprint = Fingerprint,
            Block = "AntiCheatFile|NetEase Yidun|NEP2.dll",
            Verdict = AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll"),
            DisclosureVersion = "ac-exception-dialog/2",
            GrantedAt = DateTimeOffset.UnixEpoch,
        }, ct).ConfigureAwait(false);
        await store.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = Fingerprint,
            DisclosureVersion = "operator-disclosure/test",
            AcknowledgedAt = DateTimeOffset.UnixEpoch,
        }, ct).ConfigureAwait(false);
    }

    private static RecordRequest Request(CaptureMode mode = CaptureMode.Attach) => new()
    {
        NormalisedExePath = _exe,
        Observed = Fingerprint,
        PayloadPath = "payload.dll",
        Mode = mode,
    };

    /// <summary>
    /// D33 (owner decision 2026-09-26): a hooked session under the game's user-mode exception is marked with the family —
    /// the column the App reads, the notes a bug bundle carries — and an ordinary end leaves the exception in force.
    /// </summary>
    [Fact]
    public async Task ASessionUnderTheExceptionIsMarkedAndAnOrdinaryEndKeepsTheException()
    {
        Harness h = await MakeAsync(verdict: AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll"), excepted: true);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Row.Tier.Should().Be(CaptureTier.Hooked);
        r.Row.AcExceptionFamily.Should().Be("NetEase Yidun");
        r.Row.CaptureNotes.Should().Contain("ac-exception=NetEase Yidun");
        CaptureNotes.Parse(r.Row.CaptureNotes).AcExceptionFamily.Should().Be("NetEase Yidun");
        r.ExceptionLapse.Should().BeNull();
        (await h.Store.FindAsync(_exe, TestContext.Current.CancellationToken)).Exception.Should().NotBeNull();
    }

    /// <summary>D33: a crash while hooked under the exception ends it — hooking off, the block standing, the reason recorded.</summary>
    [Fact]
    public async Task ACrashUnderTheExceptionEndsIt()
    {
        using var crashed = new FakeLiveness(exited: true, exitCode: unchecked((int)0xC0000005));
        Harness h = await MakeAsync(verdict: AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll"), excepted: true, liveness: crashed);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.ExitStatus.Should().Be(ExitStatus.Crashed);
        r.ExceptionLapse.Should().Be(UserModeExceptionLapse.SessionCrashed);
        GameConsentRecord after = await h.Store.FindAsync(_exe, TestContext.Current.CancellationToken);
        after.Exception.Should().BeNull();
        after.HookEnabled.Should().BeFalse("the block stands, so hooking goes off with the exception");
        after.BlockedReason.Should().Be("AntiCheatFile|NetEase Yidun|NEP2.dll", "nothing clears a block");
        after.ConsentedAt.Should().NotBeNull("the end of an exception keeps the consent stamp, as a block does");
        GameRow row = (await new SqliteGameRepository(_db!).FindAsync(_exe, TestContext.Current.CancellationToken))!;
        row.AcException.TrialFailed.Should().BeTrue("D38: the grant had no good session yet, so a crash ends it for good");
    }

    /// <summary>
    /// D38 (owner decision 2026-10-03): during the trial a stored hooked session that recorded no frame ends the exception for
    /// good; past the trial (two good sessions under the grant) the same session leaves it in force.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task ASessionThatRecordedNothingEndsTheExceptionOnlyDuringItsTrial(int goodSoFar, bool ends)
    {
        Harness h = await MakeAsync(verdict: AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll"), excepted: true, records: 0);
        h.Sessions.UnderGrantDefault = goodSoFar;

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Row.Tier.Should().Be(CaptureTier.Hooked);
        r.Row.FrameCount.Should().Be(0);
        r.Finalize.Status.Should().Be(FinalizeStatus.Saved, "only a stored session is judged");
        r.ExceptionLapse.Should().Be(ends ? UserModeExceptionLapse.TrialFailed : null);
        GameRow after = (await new SqliteGameRepository(_db!).FindAsync(_exe, TestContext.Current.CancellationToken))!;
        after.AcException.IsGranted.Should().Be(!ends);
        after.AcException.TrialFailed.Should().Be(ends);
    }

    /// <summary>
    /// beta.13: the adapter the Overlay names in the handshake reaches the telemetry thread once, however many ticks carry it;
    /// a session whose handshake names none (0, before the first present or through OpenGL) tells it nothing.
    /// </summary>
    [Fact]
    public async Task TheGamesAdapterIsHandedToTheTelemetryThreadOnce()
    {
        Harness named = await MakeAsync(adapterLuid: 0x0000_0001_0000_0042);
        await named.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);
        named.Poller!.Adapters.Should().Equal(0x0000_0001_0000_0042UL);

        Harness unnamed = await MakeAsync();
        await unnamed.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);
        unnamed.Poller!.Adapters.Should().BeEmpty();
    }

    [Fact]
    public async Task AHookedSessionIsRecordedEndToEnd()
    {
        Harness h = await MakeAsync();

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Outcome.Reason.Should().Be(SessionEndReason.Running, "a bounded capture ends on its own limit");
        r.ExitStatus.Should().Be(ExitStatus.Normal);
        r.Finalize.Status.Should().Be(FinalizeStatus.Saved, "one drain steps the clock to the harness's one-step minimum");
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
        entry.Notes.Select(n => n.Text).Should().ContainInOrder("started Attach", $"attached pid={_pid} layout=0 build=", "ended Running", "finalizing Normal");
        entry.Ticks.Should().BeGreaterThan(1, "a 10 s interval against a clock stepping 5 s per drain flushes several times");
        entry.Records.Should().HaveCount(3_000, "the final forced flush wrote everything");
        entry.Sensors.Should().NotBeEmpty();
        entry.Disposed.Should().BeTrue();
    }

    private static FakePartialSessionStore.Entry? FindEntry(Harness h, Guid guid) =>
        h.Partials.Created.GetValueOrDefault(guid);

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

    /// <summary>
    /// D48(b), beta.14: the re-scan could not read the game's tree as the game exited — the session is that exit, its exit
    /// code deciding normal or crashed, and the notes keep what the scan said.
    /// </summary>
    [Fact]
    public async Task ARescanThatCouldNotReadAsTheGameExitedEndsAsItsExitWithTheScanInTheNotes()
    {
        using var exited = new FakeLiveness(exited: true, exitCode: 0);
        Harness h = await MakeAsync(liveness: exited,
            rescan: AntiCheatVerdict.Refused(AntiCheatRefusalReason.ProcessUnreadable, string.Empty, "a process in the scan set could not be read"));

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Outcome.Reason.Should().Be(SessionEndReason.TargetExited);
        r.ExitStatus.Should().Be(ExitStatus.Normal, "a game that exited with 0 ended normally, whatever the last scan could read");
        r.Row.CaptureNotes.Should().Contain("scan_at_exit=ProcessUnreadable");
        CaptureNotes.Parse(r.Row.CaptureNotes).ScanAtExit.Should().Be("ProcessUnreadable");
    }

    /// <summary>
    /// beta.14: Unreal handles its own crash, starts CrashReportClient.exe and exits with code 3 — no exception code, no event.
    /// The reporter the watcher saw is the witness: crashed, and the notes name it.
    /// </summary>
    [Fact]
    public async Task AGameThatStartedItsCrashReporterAndExitedWithinAMinuteCrashed()
    {
        using var exited = new FakeLiveness(exited: true, exitCode: 3);
        Harness h = await MakeAsync(liveness: exited, reporters: new OneReporter(TimeSpan.FromSeconds(6)));

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.ExitStatus.Should().Be(ExitStatus.Crashed, "exit code 3 alone read 'normal' until beta.14");
        r.Row.CaptureNotes.Should().Contain("crash_reporter=CrashReportClient.exe");
        CaptureNotes.Parse(r.Row.CaptureNotes).CrashReporter.Should().Be("CrashReportClient.exe");
    }

    [Fact]
    public async Task AReporterWhoseGameKeptRunningIsANoteOnly()
    {
        using var exited = new FakeLiveness(exited: true, exitCode: 0);
        Harness h = await MakeAsync(liveness: exited, reporters: new OneReporter(TimeSpan.FromMinutes(10)));

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.ExitStatus.Should().Be(ExitStatus.Normal, "the game ran ten more minutes after it reported");
        r.Row.CaptureNotes.Should().Contain("crash_reporter_seen=CrashReportClient.exe");
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

    private static readonly ScreenRect _screen = new(0, 0, 1920, 1080);

    /// <summary>
    /// beta.10 (<c>03_METRICS</c> §Display mode): a Tier-2 row carries the window it was shown in. The columns are on the
    /// recorder's skeleton, not the aggregator's — a Tier-2 session never reaches the aggregator, and the window is exactly
    /// what it still has to say.
    /// </summary>
    [Fact]
    public async Task ATierTwoRowCarriesTheWindowItWasShownIn()
    {
        Harness h = await MakeAsync(verdict: AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedDriver, "Riot Vanguard", "vgk.sys"), clockStepsOnDrain: true,
            display: new FixedWindow(new WindowView(false, _screen, _screen, 144)));

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Finalize.Status.Should().Be(FinalizeStatus.Saved);
        SessionRow row = h.Sessions.Stored.Single().Row;
        row.Tier.Should().Be(CaptureTier.NotHooked);
        row.DisplayCoversMs.Should().BeGreaterThan(0, "a window covering its monitor that nothing could ask about");
        row.DisplayExclusiveMs.Should().Be(0);
        row.DisplayBorderlessMs.Should().Be(0);
        row.DisplaySource.Should().Be(DisplaySource.Window);
        (row.DisplayWindowW, row.DisplayWindowH, row.DisplayMonitorHz).Should().Be((1920, 1080, 144));
        row.DisplayBufferW.Should().BeNull("nothing was injected to describe a swap chain");
        SessionDisplayColumns.DisplayOf(row)!.Dominant.Should().Be(DisplayMode.CoversScreen);
    }

    [Fact]
    public async Task AHookedRowKeepsItsDisplayThroughTheAggregatorAndARowWithoutAReaderHasNone()
    {
        Harness withReader = await MakeAsync(display: new FixedWindow(new WindowView(false, new ScreenRect(10, 10, 1290, 730), _screen, 60)));
        Harness without = await MakeAsync();

        await withReader.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);
        await without.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        SessionRow hooked = withReader.Sessions.Stored.Single().Row;
        hooked.Tier.Should().Be(CaptureTier.Hooked);
        hooked.DisplayWindowedMs.Should().BeGreaterThan(0);
        (hooked.DisplayWindowW, hooked.DisplayWindowH).Should().Be((1280, 720));
        SessionRow plain = without.Sessions.Stored.Single().Row;
        plain.DisplayWindowedMs.Should().BeNull("no reader, no display facts — N/A, never a zero");
        SessionDisplayColumns.DisplayOf(plain).Should().BeNull();
    }

    /// <summary>The Agent's shared advanced reader (owner decision D101), scripted: what it last read, and what it answers about a session.</summary>
    private sealed class FakeAdvanced : IAdvancedHardware
    {
        private int _acquired;
        private int _released;

        public int Acquired => Volatile.Read(ref _acquired);

        public int Released => Volatile.Read(ref _released);

        public AdvancedSnapshot? Latest { get; set; }

        public List<AdvancedSessionTarget> Asked { get; } = [];

        public bool Throws { get; init; }

        public IDisposable Acquire()
        {
            Interlocked.Increment(ref _acquired);
            return new Hold(this);
        }

        public IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target)
        {
            lock (Asked)
            {
                Asked.Add(target);
            }

            return Throws
                ? throw new InvalidOperationException("the driver did not answer")
                : [new AdvancedFact("nvapi.session.vrr_enabled", Instance: null, AdvancedValue.Of(true))];
        }

        public static AdvancedSnapshot Snapshot() => new(
            DateTimeOffset.UnixEpoch,
            [],
            [new AdvancedFact("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84))],
            [],
            new AdvancedSources(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated));

        private sealed class Hold(FakeAdvanced hub) : IDisposable
        {
            public void Dispose() => Interlocked.Increment(ref hub._released);
        }
    }

    [Fact]
    public async Task WithAdvancedMetricsOffTheSharedReaderIsNeverHeldAndTheSessionStoresNothingOfThem()
    {
        // D101: off means nothing more is read or stored - not held, not asked, no chunk, no row.
        var hub = new FakeAdvanced { Latest = FakeAdvanced.Snapshot() };
        Harness h = await MakeAsync(advanced: hub, advancedOn: false);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        hub.Acquired.Should().Be(0);
        hub.Asked.Should().BeEmpty();
        h.Sessions.Stored.Single().Advanced.Should().BeNull();
        h.Partials.Created[r.Row.SessionGuid].AdvancedContexts.Should().BeEmpty();
    }

    [Fact]
    public async Task WithAdvancedMetricsOnTheSessionHoldsTheReaderAsksItsFactsOnceAndStoresItsRow()
    {
        var hub = new FakeAdvanced { Latest = FakeAdvanced.Snapshot() };
        Harness h = await MakeAsync(advanced: hub, advancedOn: true);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        hub.Acquired.Should().Be(1);
        hub.Released.Should().Be(1, "the session lets go when it is over, and the shared reader closes some seconds after the last holder");
        AdvancedSessionTarget asked = hub.Asked.Should().ContainSingle("a session's facts are read once").Subject;
        asked.ExecutablePath.Should().Be(_exe);
        asked.ProcessId.Should().Be(_pid);
        AdvancedSessionRow row = h.Sessions.Stored.Single().Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        row.MachineFactsJson.Should().Contain("nvapi.gpu.rt_cores");
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        facts.Should().ContainSingle().Which.Id.Should().Be("nvapi.session.vrr_enabled");
        sources.ElevatedSensors.Should().Be(AdvancedSourceStatus.NotElevated);
        h.Partials.Created[r.Row.SessionGuid].AdvancedContexts.Should().NotBeEmpty("the crash file carries the context, so a recovered session keeps it");
        r.Row.Tier.Should().Be(CaptureTier.Hooked, "and the session itself is what it always was");
    }

    [Fact]
    public async Task WithAdvancedMetricsOffTheOverlayIsToldSoAndNothingTheGameSaidIsStored()
    {
        // D111, the Agent's half: off is a word that is written, and a region that holds something is not read.
        var script = new InGameScript { Publish = static n => InGameStates.Ffx(n) };
        Harness h = await MakeAsync(advanced: new FakeAdvanced { Latest = FakeAdvanced.Snapshot() }, advancedOn: false, inGame: script);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        script.Asked.Should().Equal(false);
        FinalizedSession stored = h.Sessions.Stored.Single();
        stored.Advanced.Should().BeNull();
        stored.Sensors.Should().NotContain(static b => b.Series.StartsWith(AdvancedMetric.SeriesPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithAdvancedMetricsOnWhatTheGameToldItsUpscalerIsStoredWithTheSession()
    {
        var hub = new FakeAdvanced { Latest = FakeAdvanced.Snapshot() };
        var script = new InGameScript { Publish = static n => InGameStates.Ffx(n) };
        Harness h = await MakeAsync(advanced: hub, advancedOn: true, inGame: script);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        script.Asked.Should().Equal(true);
        FinalizedSession stored = h.Sessions.Stored.Single();
        AdvancedSessionRow row = stored.Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        facts.Select(static f => f.Id).Should().Equal(
            ["nvapi.session.vrr_enabled", "ffx.upscale.sharpening", "ffx.upscale.jitter", "ffx.upscale.exposure", "ffx.upscale.depth", "ffx.upscale.flags"],
            "the driver's facts of the session, then what the game said");
        facts.Single(static f => string.Equals(f.Id, "ffx.upscale.depth", StringComparison.Ordinal)).Value.Rows!.Select(static x => x.Value).Should().Contain("0.9 – 1.1 rad (51.6° – 63°)");
        sources.InGame.Should().Be(AdvancedInGameStatus.Reading);
        sources.Nvapi.Should().Be(AdvancedSourceStatus.Reading, "the Agent's own sources are said as before");
        // The two calls hooked only for Advanced metrics: this game made neither, and the capture side said neither
        // hook was in - "not hooked", which is not "the game never called".
        sources.StreamlineConstants.Should().Be(AdvancedInGameCall.NotHooked);
        sources.FsrFrameGeneration.Should().Be(AdvancedInGameCall.NotHooked);

        // The two series, beside whatever the Agent's own reader gave the same tick.
        stored.Sensors.Select(static b => b.Series).Should().Contain([InGameAdvancedSampler.FrameTimeSeries, InGameAdvancedSampler.MemorySeries]);
        row.StatsJson.Should().Contain(InGameAdvancedSampler.FrameTimeSeries).And.Contain(InGameAdvancedSampler.MemorySeries);
        IReadOnlyDictionary<string, SensorSeriesStats> stats = AdvancedJson.ParseStats(row.StatsJson);
        stats[InGameAdvancedSampler.FrameTimeSeries].Mean.Should().BeApproximately(16.0, 1e-3, "sixty dispatches of 16 ms a publication");
        stats[InGameAdvancedSampler.MemorySeries].Max.Should().BeApproximately(256.0, 1e-3);
        h.Partials.Created[r.Row.SessionGuid].AdvancedContexts.Should().NotBeEmpty("a recovered session keeps what the game said too");
        r.Row.Tier.Should().Be(CaptureTier.Hooked);
    }

    [Fact]
    public async Task TheTwoCallsHookedOnlyForAdvancedMetricsAreEachReadOrHookedAndNeverMade()
    {
        // 0.1.0-beta.43. A game that configures FSR frame generation and does not use Streamline's constants, with
        // both hooks in: the configuration is read and stored; of slSetConstants the row says the hook was in and
        // the game never made the call - which says something about the game, where "not hooked" would not.
        var script = new InGameScript
        {
            Publish = static n => InGameStates.FrameGeneration(n),
            Hooks = FlHookFamily.Present | FlHookFamily.SlConstants | FlHookFamily.FfxConfigure,
        };
        Harness h = await MakeAsync(advanced: new FakeAdvanced { Latest = FakeAdvanced.Snapshot() }, advancedOn: true, inGame: script);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        AdvancedSessionRow row = h.Sessions.Stored.Single().Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        sources.InGame.Should().Be(AdvancedInGameStatus.Reading);
        sources.FsrFrameGeneration.Should().Be(AdvancedInGameCall.Read);
        sources.StreamlineConstants.Should().Be(AdvancedInGameCall.NeverCalled);
        facts.Single(static f => string.Equals(f.Id, "ffx.framegen.config", StringComparison.Ordinal)).Value.Rows!
            .Select(static x => x.Name + "=" + x.Value)
            .Should().Contain(["frameGenerationEnabled=on", "generationRect=1920 × 1080 at 0, 0"]);
        facts.Should().NotContain(static f => f.Id.StartsWith("sl.constants.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhatTheGameToldStreamlineIsStoredWithItsCountAsOfTheEndOfTheSession()
    {
        // The resets are a count, and a count does not rewrite the session's context while it runs: the stored fact is
        // the count of the LAST publication, brought up to date when the session concludes.
        var script = new InGameScript
        {
            Publish = static n => InGameStates.Streamline(n, resets: n),
            Hooks = FlHookFamily.Present | FlHookFamily.SlConstants,
        };
        Harness h = await MakeAsync(advanced: new FakeAdvanced { Latest = FakeAdvanced.Snapshot() }, advancedOn: true, inGame: script);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        AdvancedSessionRow row = h.Sessions.Stored.Single().Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        sources.StreamlineConstants.Should().Be(AdvancedInGameCall.Read);
        sources.FsrFrameGeneration.Should().Be(AdvancedInGameCall.NotHooked);
        facts.Select(static f => f.Id).Should().Contain(["sl.constants.jitter", "sl.constants.motion_vectors", "sl.constants.depth", "sl.constants.projection", "sl.constants.resets"]);
        double resets = facts.Single(static f => string.Equals(f.Id, "sl.constants.resets", StringComparison.Ordinal)).Value.Number!.Value;
        resets.Should().BeGreaterThan(1, "more than one publication was taken, each with a higher count");
        resets.Should().Be(script.Published, "the count stored is the last publication's, not the first's");
    }

    [Fact]
    public async Task AHookedSessionWhoseGameMadeNoneOfTheCallsSaysSoRatherThanNothing()
    {
        // Most titles: no FSR through ffx-api, and DLSS options set through a call that has no export. The row must
        // not read as "the game uses no upscaler".
        var script = new InGameScript();
        Harness h = await MakeAsync(advanced: new FakeAdvanced { Latest = FakeAdvanced.Snapshot() }, advancedOn: true, inGame: script);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        script.Asked.Should().Equal(true);
        AdvancedSessionRow row = h.Sessions.Stored.Single().Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        facts.Select(static f => f.Id).Should().Equal("nvapi.session.vrr_enabled");
        sources.InGame.Should().Be(AdvancedInGameStatus.NothingSeen);
    }

    [Fact]
    public async Task ASessionThatWasNotMeasuredInsideTheGameSaysThatOfItsInGameEntries()
    {
        var script = new InGameScript { Publish = static n => InGameStates.Ffx(n) };
        Harness h = await MakeAsync(
            verdict: AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll"),
            advanced: new FakeAdvanced { Latest = FakeAdvanced.Snapshot() }, advancedOn: true, inGame: script, clockStepsOnDrain: true);

        RecordedSession r = await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        r.Row.Tier.Should().Be(CaptureTier.NotHooked);
        script.Asked.Should().BeEmpty("there was no ring to write the word to");
        AdvancedSessionRow row = h.Sessions.Stored.Single().Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        facts.Should().NotContain(static f => f.Id.StartsWith("ffx.", StringComparison.Ordinal) || f.Id.StartsWith("sl.", StringComparison.Ordinal));
        sources.InGame.Should().Be(AdvancedInGameStatus.NotHooked);
        sources.StreamlineConstants.Should().BeNull("a session not measured inside the game has no hook to speak of");
        sources.FsrFrameGeneration.Should().BeNull();
    }

    [Fact]
    public async Task ASessionWithTheOptionOnWhoseReaderNeverAnsweredStillStoresARowThatSaysSo()
    {
        // Nothing was read: no snapshot, and the facts read threw. The row is there with every source unknown, which the
        // page shows as N/A with its reason - where no row at all would read as "the option was off".
        var hub = new FakeAdvanced { Latest = null, Throws = true };
        Harness h = await MakeAsync(advanced: hub, advancedOn: true);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        AdvancedSessionRow row = h.Sessions.Stored.Single().Advanced.Should().BeOfType<AdvancedSessionRow>().Subject;
        row.MachineFactsJson.Should().BeNull();
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        facts.Should().BeEmpty("a read that threw costs the session its facts, not itself");
        sources.Should().Be(
            AdvancedSources.Unknown with
            {
                InGame = AdvancedInGameStatus.NothingSeen,
                StreamlineConstants = AdvancedInGameCall.NotHooked,
                FsrFrameGeneration = AdvancedInGameCall.NotHooked,
                StreamlineFeatureVersion = AdvancedInGameCall.NotHooked,
                StreamlineFeatureRequirements = AdvancedInGameCall.NotHooked,
                StreamlineFeatureSupported = AdvancedInGameCall.NotHooked,
                StreamlineFeatureLoaded = AdvancedInGameCall.NotHooked,
                StreamlineSetFeatureLoaded = AdvancedInGameCall.NotHooked,
                StreamlineAllocateResources = AdvancedInGameCall.NotHooked,
                StreamlineFreeResources = AdvancedInGameCall.NotHooked,
                StreamlineShutdown = AdvancedInGameCall.NotHooked,
            },
            "the Agent's sources never answered; the game was measured from inside, made none of the calls, and the capture side published none of the ten hooks that are installed only for Advanced metrics");
        hub.Released.Should().Be(1);
    }

    [Fact]
    public async Task ARecorderWithNoSharedReaderRecordsAsBeforeWhateverTheOptionSays()
    {
        Harness h = await MakeAsync(advanced: null, advancedOn: true);

        await h.Recorder.RecordAsync(Request(), TestContext.Current.CancellationToken);

        h.Sessions.Stored.Single().Advanced.Should().BeNull("the unshipped host and the tests have no reader to hold");
    }
}
