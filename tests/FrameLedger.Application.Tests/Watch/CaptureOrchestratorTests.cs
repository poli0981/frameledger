using FluentAssertions;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// One session per game at a time, started for a tracked process and for nothing else; the recorder is a fake
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
        public ExecutableFingerprint? Read(string normalisedExePath) => new() { ExePath = normalisedExePath, SizeBytes = 5, MtimeUnixMs = 5 };
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
}
