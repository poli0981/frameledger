using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// One session per executable at a time, started for a tracked process and for nothing else; the recorder is a fake
/// that records what it was asked, so the orchestrator's one decision — WHEN — is what is observed.
/// </summary>
public sealed class CaptureOrchestratorTests
{
    private sealed class FakeRecorder : ISessionRecorder
    {
        public List<RecordRequest> Requests { get; } = [];

        public List<TaskCompletionSource<RecordedSession>> Pending { get; } = [];

        public Task<RecordedSession> RecordAsync(RecordRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            var tcs = new TaskCompletionSource<RecordedSession>(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending.Add(tcs);
            return tcs.Task;
        }
    }

    private sealed class FakeSnapshots : IProcessSnapshotSource
    {
        public List<ProcessSnapshot> Processes { get; } = [];

        public IReadOnlyList<ProcessSnapshot> Take() => [.. Processes];
    }

    private sealed class FakeIdentity : IExecutableIdentitySource
    {
        public HashSet<string> Missing { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ExecutableFingerprint? Read(string normalisedExePath) =>
            Missing.Contains(normalisedExePath) ? null : new() { ExePath = normalisedExePath, SizeBytes = 5, MtimeUnixMs = 5 };

        public string Normalise(string exePath) => exePath;
    }

    private const string _game = @"C:\Games\Title\game.exe";

    private static async Task<(CaptureOrchestrator Orchestrator, FakeRecorder Recorder, FakeSnapshots Processes, List<string> Log)> BuildAsync()
    {
        var games = new FakeGameRepository();
        await games.EnsureAsync(new ExecutableFingerprint { ExePath = _game, SizeBytes = 1, MtimeUnixMs = 1 }, "Title", TestContext.Current.CancellationToken).ConfigureAwait(false);
        var recorder = new FakeRecorder();
        var processes = new FakeSnapshots();
        List<string> log = [];
        var orchestrator = new CaptureOrchestrator(recorder, games, processes, new FakeIdentity(),
            new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add);
        return (orchestrator, recorder, processes, log);
    }

    [Fact]
    public async Task ATrackedProcessStartsExactlyOneAttachSessionKeyedOnTheRealPath()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, FakeSnapshots processes, List<string> log) = await BuildAsync().ConfigureAwait(true);
        CancellationToken ct = TestContext.Current.CancellationToken;

        processes.Processes.Add(new ProcessSnapshot(4242, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        IReadOnlyList<WatchEvent> first = await o.PollOnceAsync(ct).ConfigureAwait(true);
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await o.PollOnceAsync(ct).ConfigureAwait(true);

        first.Should().ContainSingle().Which.Should().BeOfType<TrackedProcessAppeared>();
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);
        RecordRequest request = recorder.Requests.Should().ContainSingle().Subject;
        request.Mode.Should().Be(CaptureMode.Attach);
        request.NormalisedExePath.Should().Be(_game);
        request.PayloadPath.Should().Be(@"C:\FL\FrameLedger.Overlay.dll");
        request.Observed.Should().NotBeNull();
        request.GameName.Should().Be("Title");
        o.RunningSessions.Should().Be(1);
        log.Should().Contain(l => l.Contains("starting a session", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASecondProcessOfTheSameGameWhileASessionRunsStartsNothingAndAfterItEndsStartsAgain()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, FakeSnapshots processes, List<string> log) = await BuildAsync().ConfigureAwait(true);
        CancellationToken ct = TestContext.Current.CancellationToken;

        processes.Processes.Add(new ProcessSnapshot(1, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);
        processes.Processes.Add(new ProcessSnapshot(2, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);

        recorder.Requests.Should().HaveCount(1, "one session per game at a time");
        log.Should().Contain(l => l.Contains("one at a time", StringComparison.Ordinal));

        // The first session ends (cancelled: the fake never builds a row) and both processes go away; a fresh
        // appearance starts a fresh session.
        recorder.Pending[0].SetException(new OperationCanceledException());
        processes.Processes.Clear();
        // The session task's own report runs as a continuation; give it its turn before the reap.
        await WaitUntilAsync(() => log.Exists(l => l.Contains("cancelled", StringComparison.Ordinal))).ConfigureAwait(true);
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        o.RunningSessions.Should().Be(0);
        log.Should().Contain(l => l.Contains("cancelled", StringComparison.Ordinal));

        processes.Processes.Add(new ProcessSnapshot(3, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 2).ConfigureAwait(true);
        recorder.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnUntrackedProcessStartsNothing()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, FakeSnapshots processes, _) = await BuildAsync().ConfigureAwait(true);
        processes.Processes.Add(new ProcessSnapshot(5, 1, "explorer.exe", @"C:\Windows\explorer.exe", DateTimeOffset.UnixEpoch));

        IReadOnlyList<WatchEvent> events = await o.PollOnceAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        events.Should().BeEmpty();
        recorder.Requests.Should().BeEmpty();
        o.RunningSessions.Should().Be(0);
    }

    /// <summary>
    /// The owner's log on 2026-09-22 23:26 and 2026-09-23 00:25 read "session: FAULTED SqliteException: …" and nothing
    /// about which session or which game; it took the UI log's import line to find out it was GIRLS' FRONTLINE 2.
    /// </summary>
    [Fact]
    public async Task AFaultedSessionIsLoggedWithItsGuidItsGameAndItsPath()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, FakeSnapshots processes, List<string> log) = await BuildAsync().ConfigureAwait(true);
        processes.Processes.Add(new ProcessSnapshot(7, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);

        recorder.Pending[0].SetException(new InvalidOperationException("the ledger said no"));
        await WaitUntilAsync(() => log.Exists(l => l.Contains("FAULTED", StringComparison.Ordinal))).ConfigureAwait(true);

        string line = log.Single(l => l.Contains("FAULTED", StringComparison.Ordinal));
        line.Should().Contain(recorder.Requests[0].SessionGuid!.Value.ToString("N")).And.Contain("Title").And.Contain(_game).And.Contain("the ledger said no");
    }

    [Fact]
    public async Task StopSessionCancelsTheRequestsOwnStopTokenAndNothingElse()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, FakeSnapshots processes, List<string> log) = await BuildAsync().ConfigureAwait(true);
        CancellationToken ct = TestContext.Current.CancellationToken;
        processes.Processes.Add(new ProcessSnapshot(4242, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);
        RecordRequest request = recorder.Requests.Single();
        request.SessionGuid.Should().NotBeNull("the orchestrator names the session so a stop can address it");
        request.StopToken.IsCancellationRequested.Should().BeFalse();

        o.StopSession(Guid.NewGuid()).Should().BeFalse("no session carries a guid nobody started");
        o.StopSession(request.SessionGuid!.Value).Should().BeTrue();

        request.StopToken.IsCancellationRequested.Should().BeTrue("the stop is the session's own token");
        ct.IsCancellationRequested.Should().BeFalse("the host's token is untouched");
        log.Should().Contain(l => l.Contains("stop requested", StringComparison.Ordinal));
    }

    private sealed class FakeLaunches(FakeRecorder recorder) : ILaunchRecorderFactory, IDisposable
    {
        private Launch? _current;

        public void Dispose() => _current?.Dispose();

        public int Prepared { get; private set; }

        public int Disposed { get; private set; }

        public ValueTask<ILaunchRecording> PrepareAsync(string normalisedExePath, CancellationToken ct = default)
        {
            Prepared++;
            _current = new Launch(recorder, this);
            return ValueTask.FromResult<ILaunchRecording>(_current);
        }

        private sealed class Launch(FakeRecorder recorder, FakeLaunches owner) : ILaunchRecording
        {
            public ISessionRecorder Recorder => recorder;

            public string Description => "vulkan layer: test";

            public void Dispose() => owner.Disposed++;
        }
    }

    [Fact]
    public async Task LaunchStartsALaunchModeSessionOncePerGameAndRefusesWhatItCannotName()
    {
        var games = new FakeGameRepository();
        GameRow game = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _game, SizeBytes = 1, MtimeUnixMs = 1 }, "Title", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var attach = new FakeRecorder();
        var launchRecorder = new FakeRecorder();
        using var launches = new FakeLaunches(launchRecorder);
        List<string> log = [];
        var o = new CaptureOrchestrator(attach, games, new FakeSnapshots(), new FakeIdentity(),
            new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add, launches);
        CancellationToken ct = TestContext.Current.CancellationToken;

        (await o.LaunchAsync(999, "", ct).ConfigureAwait(true)).Outcome.Should().Be(LaunchOutcome.UnknownGame);

        LaunchResult first = await o.LaunchAsync(game.Id, "-benchmark", ct).ConfigureAwait(true);
        first.Outcome.Should().Be(LaunchOutcome.Accepted);
        first.SessionGuid.Should().NotBeNull();
        await WaitForRequestsAsync(launchRecorder, 1).ConfigureAwait(true);
        RecordRequest request = launchRecorder.Requests.Single();
        request.Mode.Should().Be(CaptureMode.Launch);
        request.Arguments.Should().Be("-benchmark");
        request.SessionGuid.Should().Be(first.SessionGuid);
        request.GameName.Should().Be("Title");
        attach.Requests.Should().BeEmpty("launch mode uses the launch recorder, not the watcher's");

        (await o.LaunchAsync(game.Id, "", ct).ConfigureAwait(true)).Outcome.Should().Be(LaunchOutcome.SessionRunning, "one session per game at a time");
        o.StopSession(first.SessionGuid!.Value).Should().BeTrue("a launched session is addressable like a watched one");

        launchRecorder.Pending[0].SetException(new OperationCanceledException());
        await WaitUntilAsync(() => launches.Disposed == 1).ConfigureAwait(true);
        launches.Disposed.Should().Be(1, "the launch environment is released with the session");

        var noLaunch = new CaptureOrchestrator(attach, games, new FakeSnapshots(), new FakeIdentity(),
            new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add);
        (await noLaunch.LaunchAsync(game.Id, "", ct).ConfigureAwait(true)).Outcome.Should().Be(LaunchOutcome.Unavailable);
    }

    [Fact]
    public async Task RunAsyncPollsUntilCancelledAndThenReportsWhatWasRunning()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, FakeSnapshots processes, List<string> log) = await BuildAsync().ConfigureAwait(true);
        processes.Processes.Add(new ProcessSnapshot(9, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Task run = o.RunAsync(cts.Token);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);
        recorder.Pending[0].SetException(new OperationCanceledException());
        await cts.CancelAsync().ConfigureAwait(true);
        await run.ConfigureAwait(true);
        await WaitUntilAsync(() => log.Exists(l => l.Contains("cancelled", StringComparison.Ordinal))).ConfigureAwait(true);

        log.Should().Contain(l => l.Contains("cancelled", StringComparison.Ordinal), "a running session is reported when the loop stops");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WaitForRequestsAsync(FakeRecorder recorder, int count)
    {
        for (int i = 0; i < 200 && recorder.Requests.Count < count; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A game launched from a drive that changed its letter (2026-09-22): the row's file is gone, the running one has the
    /// row's bytes, so the row follows it and the session is the row's — keyed on the new path WITH its consent, rather
    /// than "BY FILE NAME ONLY" as a stranger.
    /// </summary>
    [Fact]
    public async Task AProcessFromAMovedDriveMovesTheRowAndTheSessionIsTheRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var games = new FakeGameRepository();
        GameRow row = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _game, SizeBytes = 5, MtimeUnixMs = 5 }, "Title", ct);
        var identity = new FakeIdentity();
        identity.Missing.Add(_game);
        var recorder = new FakeRecorder();
        var processes = new FakeSnapshots();
        List<string> log = [];
        var o = new CaptureOrchestrator(recorder, games, processes, identity, new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add,
            relocator: new ExecutableRelocator(games, identity, () => [@"C:\", @"H:\"], log.Add));
        const string moved = @"H:\Games\Title\game.exe";

        processes.Processes.Add(new ProcessSnapshot(4242, 1, "game.exe", moved, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);

        recorder.Requests.Should().ContainSingle().Which.NormalisedExePath.Should().Be(moved);
        games.Rows.Should().ContainKey(moved).And.NotContainKey(_game);
        games.Rows[moved].Id.Should().Be(row.Id);
        log.Should().Contain(l => l.Contains("executable moved", StringComparison.Ordinal));
        log.Should().NotContain(l => l.Contains("BY FILE NAME ONLY", StringComparison.Ordinal), "the row was moved before the session started, so the path is the row's");
    }

    private const string _flower = @"D:\SteamLibrary\steamapps\common\Flower in Us\Game.exe";
    private const string _hello = @"D:\another\it\hello-hello-world\HELLO, HELLO WORLD!\swiftshader\Game.exe";

    /// <summary>
    /// The owner's log, 2026-09-23 00:16: <c>watch: pid 2580 matched Flower in Us BY FILE NAME ONLY — it runs from
    /// …\HELLO, HELLO WORLD!\swiftshader\Game.exe</c>, a new "Flower in Us" entry at that path, and one second later a
    /// second session when an NW.js child matched the new entry exactly. Another game's <c>Game.exe</c> is not in the
    /// library — even with the entry's own file gone and the bytes identical, as every RPG Maker MV stub is — and nothing
    /// is recorded or created for it.
    /// </summary>
    [Fact]
    public async Task AnotherGamesProcessWithTheSameFileNameStartsNothingAndCreatesNoEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var games = new FakeGameRepository();
        await games.EnsureAsync(new ExecutableFingerprint { ExePath = _flower, SizeBytes = 5, MtimeUnixMs = 5 }, "Flower in Us", ct);
        var identity = new FakeIdentity();
        identity.Missing.Add(_flower);    // the hardest case: the entry's file gone, and FakeIdentity gives every other path its bytes
        var recorder = new FakeRecorder();
        var processes = new FakeSnapshots();
        List<string> log = [];
        var o = new CaptureOrchestrator(recorder, games, processes, identity, new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add,
            relocator: new ExecutableRelocator(games, identity, () => [@"C:\", @"D:\"], log.Add));

        processes.Processes.Add(new ProcessSnapshot(2580, 1, "Game.exe", _hello, DateTimeOffset.UnixEpoch));
        processes.Processes.Add(new ProcessSnapshot(3536, 2580, "Game.exe", _hello, DateTimeOffset.UnixEpoch.AddSeconds(1)));
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        processes.Processes.Add(new ProcessSnapshot(28488, 2580, "Game.exe", _hello, DateTimeOffset.UnixEpoch.AddSeconds(2)));
        await o.PollOnceAsync(ct).ConfigureAwait(true);

        recorder.Requests.Should().BeEmpty("a process that is not in the library records nothing");
        games.Rows.Keys.Should().Equal([_flower], "the entry did not move to another game's executable, and no entry was created");
        log.Where(l => l.Contains("not in the library", StringComparison.Ordinal)).Should().ContainSingle("one line per executable, not one per NW.js process")
            .Which.Should().Contain(_hello).And.Contain("Flower in Us");

        processes.Processes.Clear();
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        log.Should().NotContain(l => l.Contains("exited", StringComparison.Ordinal), "a declined process's exit is not the entry's");
    }

    /// <summary>An entry removed and re-imported while its game runs has a new id and the same executable: its next process finds the running session.</summary>
    [Fact]
    public async Task AnEntryReimportedMidSessionDoesNotStartASecondSessionForTheSameExecutable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var games = new FakeGameRepository();
        GameRow original = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _game, SizeBytes = 1, MtimeUnixMs = 1 }, "Title", ct);
        var recorder = new FakeRecorder();
        var processes = new FakeSnapshots();
        List<string> log = [];
        var o = new CaptureOrchestrator(recorder, games, processes, new FakeIdentity(), new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add);

        processes.Processes.Add(new ProcessSnapshot(1, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);
        games.Rows[_game] = original with { Id = original.Id + 100 };
        processes.Processes.Add(new ProcessSnapshot(2, 1, "game.exe", _game, DateTimeOffset.UnixEpoch));
        await o.PollOnceAsync(ct).ConfigureAwait(true);

        recorder.Requests.Should().ContainSingle("one session per executable, whatever id its entry has now");
        o.IsRecording(original.Id).Should().BeTrue();
        log.Should().Contain(l => l.Contains("one at a time", StringComparison.Ordinal));
    }

    private static RecordedSession LauncherExited(int launcherPid) => new()
    {
        SessionGuid = Guid.NewGuid(),
        Outcome = new CaptureOutcome { Reason = SessionEndReason.LaunchTargetExited, TargetPid = launcherPid },
        Row = SessionFixtures.Skeleton(),
        ExitStatus = ExitStatus.Normal,
        Finalize = new FinalizeOutcome(FinalizeStatus.Discarded, null, 0),
        CrashPolicy = CrashPolicyOutcome.NotAnEarlyCrash,
        CrashEventFound = false,
    };

    private static async Task<(CaptureOrchestrator Orchestrator, FakeRecorder Recorder, FakeSnapshots Processes, List<string> Log)> BuildLauncherAsync()
    {
        var games = new FakeGameRepository();
        await games.EnsureAsync(new ExecutableFingerprint { ExePath = _game, SizeBytes = 1, MtimeUnixMs = 1 }, "Title", TestContext.Current.CancellationToken).ConfigureAwait(false);
        var recorder = new FakeRecorder();
        var processes = new FakeSnapshots();
        processes.Processes.Add(new ProcessSnapshot(10, 1, "launcher.exe", @"C:\Games\Title\launcher.exe", DateTimeOffset.UnixEpoch));
        processes.Processes.Add(new ProcessSnapshot(20, 10, "game.exe", _game, DateTimeOffset.UnixEpoch.AddSeconds(5)));
        List<string> log = [];
        var o = new CaptureOrchestrator(recorder, games, processes, new FakeIdentity(), new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, log.Add);
        return (o, recorder, processes, log);
    }

    /// <summary>The child appeared while its launcher ran and the watcher records it: the election after the launcher exits is not a second session.</summary>
    [Fact]
    public async Task AnElectedChildTheWatcherAlreadyRecordsIsNotASecondSession()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, _, List<string> log) = await BuildLauncherAsync().ConfigureAwait(true);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await o.PollOnceAsync(ct).ConfigureAwait(true);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);

        RecordedSession? elected = await o.ElectAfterLaunchAsync(LauncherExited(10), ct).ConfigureAwait(true);

        elected.Should().BeNull();
        recorder.Requests.Should().ContainSingle();
        log.Should().Contain(l => l.Contains("already recorded", StringComparison.Ordinal));
    }

    /// <summary>The election got there first: its session is registered, so the watcher does not start another, and it can be stopped.</summary>
    [Fact]
    public async Task AnElectedSessionIsRegisteredSoTheWatcherStartsNoSecondOne()
    {
        (CaptureOrchestrator o, FakeRecorder recorder, _, _) = await BuildLauncherAsync().ConfigureAwait(true);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Task<RecordedSession?> election = o.ElectAfterLaunchAsync(LauncherExited(10), ct);
        await WaitForRequestsAsync(recorder, 1).ConfigureAwait(true);
        await o.PollOnceAsync(ct).ConfigureAwait(true);

        recorder.Requests.Should().ContainSingle("the watcher saw the elected child while its session ran");
        RecordRequest request = recorder.Requests[0];
        request.GameName.Should().Be("Title");
        o.StopSession(request.SessionGuid!.Value).Should().BeTrue("an elected session is addressable now");
        RecordedSession done = LauncherExited(20) with { SessionGuid = request.SessionGuid.Value };
        recorder.Pending[0].SetResult(done);
        (await election.ConfigureAwait(true)).Should().BeSameAs(done);
    }
}
