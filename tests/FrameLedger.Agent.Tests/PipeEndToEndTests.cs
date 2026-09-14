using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Agent.Composition;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// The pipe end to end (P3 PR-1 read half, PR-1b command half): the Agent's own composition — the watcher, the
/// recorder, the pipe server — in this process over a scratch ledger and a pipe named for this test, our
/// harness discovered by the watcher or started by <c>LaunchGame</c>, and a client that hears the session's
/// story and drives it: pause, resume, stop; then the commands that touch no session.
/// </summary>
/// <remarks>
/// <para>
/// In-process rather than a second <c>FrameLedger.Agent.exe</c>, because <c>--serve</c> takes no <c>--data-dir</c>
/// by design (decision D6) and the product's pipe name must never be answered by a test. hook-harness is the
/// only thing this suite ever injects into; the self-constraint is asserted below as the sibling suites do.
/// </para>
/// <para>
/// <b>The target is a byte-identical copy of hook-harness under a name unique to this run.</b> The watcher falls
/// back to matching a tracked game BY FILE NAME when exactly one row carries it (<c>ProcessWatcher</c>,
/// <c>StalePath</c>), and the other test assemblies run their own <c>hook-harness.exe</c> from their own
/// directories at the same time — measured 2026-09-13: the watcher adopted a sibling suite's harness, the session
/// refused for want of consent on THAT path, and this test saw a <c>SessionCompleted</c> with no start. A unique
/// name keeps the watcher's fallback from ever seeing a stranger's process; the story reader keys on the pid
/// and the guid as well.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Collection("agent-harness")]
public sealed class PipeEndToEndTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan _ack = TimeSpan.FromSeconds(20);

    private static string Harness => Path.Combine(AppContext.BaseDirectory, "hook-harness.exe");

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "fl-pipe-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly string _pipeName = "FrameLedger.test." + Guid.NewGuid().ToString("N");

    /// <summary>The ONE executable this suite may write a consent record for: our harness, copied under a run-unique name.</summary>
    private string ConsentedExecutable => Path.Combine(_dataDir, "hook-harness-" + Path.GetFileName(_dataDir) + ".exe");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dataDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void TheTargetAndTheConsentRecordAreBothOurOwnHarness()
    {
        Path.GetFileName(Harness).Should().Be("hook-harness.exe");
        Path.GetFileName(ConsentedExecutable).Should().StartWith("hook-harness-");
        Path.GetDirectoryName(ConsentedExecutable).Should().Be(_dataDir, "the copy lives in this run's scratch directory and nowhere else");
        StageTarget();
        File.ReadAllBytes(ConsentedExecutable).Should().Equal(File.ReadAllBytes(Harness), "the target IS hook-harness, byte for byte, under another name");
        _pipeName.Should().NotBe(IpcProtocol.PipeName, "a test must never answer on the product's pipe");
    }

    [Fact]
    public async Task AWatchedHarnessSessionIsNarratedPausedResumedAndStoppedOverThePipe()
    {
        StageTarget();
        var paths = new AgentPaths(_dataDir);
        Directory.CreateDirectory(paths.Logs);
        Directory.CreateDirectory(paths.Tmp);

        LedgerDatabase db = await LedgerDatabase.OpenAsync(paths.Database, ct: Ct).ConfigureAwait(true);
        await using (db.ConfigureAwait(true))
        {
            await GrantConsentAsync(db).ConfigureAwait(true);
            var lifetime = new RecordingLifetime();
            ServiceProvider services = new ServiceCollection().AddSingleton<IAgentLifetime>(lifetime).AddFrameLedgerAgent(db, paths, _pipeName).BuildServiceProvider();
            await using (services.ConfigureAwait(true))
            {
                await RunWatchedScenarioAsync(services, lifetime).ConfigureAwait(true);
            }
        }
    }

    [Fact]
    public async Task LaunchGameStartsTheHarnessInLaunchModeAndNarratesItToTheEnd()
    {
        StageTarget();
        var paths = new AgentPaths(_dataDir);
        Directory.CreateDirectory(paths.Logs);
        Directory.CreateDirectory(paths.Tmp);

        LedgerDatabase db = await LedgerDatabase.OpenAsync(paths.Database, ct: Ct).ConfigureAwait(true);
        await using (db.ConfigureAwait(true))
        {
            await GrantConsentAsync(db).ConfigureAwait(true);
            ServiceProvider services = new ServiceCollection().AddFrameLedgerAgent(db, paths, _pipeName).BuildServiceProvider();
            await using (services.ConfigureAwait(true))
            {
                await RunLaunchScenarioAsync(services).ConfigureAwait(true);
            }
        }
    }

    private void StageTarget()
    {
        File.Exists(Harness).Should().BeTrue("hook-harness.exe must be staged beside the test binary (FrameLedger.DrainFixtures.targets)");
        Directory.CreateDirectory(_dataDir);
        File.Copy(Harness, ConsentedExecutable, overwrite: true);
        // The composition logs through Serilog's static logger; in this process nothing configures it, so the
        // orchestrator's and the pipe's lines would vanish. A file in the scratch directory is what a hang leaves behind.
        Directory.CreateDirectory(Path.Combine(_dataDir, "logs"));
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information()
            .WriteTo.File(Path.Combine(_dataDir, "logs", "e2e-.log"), formatProvider: System.Globalization.CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    private async Task GrantConsentAsync(LedgerDatabase db)
    {
        ExecutableFingerprint fingerprint = FrameLedger.Infrastructure.Io.ExecutableIdentity.Read(ConsentedExecutable)!.Value;
        ConsentWriteOutcome written = await new SqliteGameConsentStore(db).RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = fingerprint,
            DisclosureVersion = OperatorDisclosure.AgentConsoleVersion,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            Provenance = ConsentProvenance.AgentConsoleOperator,
        }, Ct).ConfigureAwait(false);
        written.Should().Be(ConsentWriteOutcome.Written);
    }

    private sealed class RecordingLifetime : IAgentLifetime
    {
        public int ShutdownRequests { get; private set; }

        public void RequestShutdown() => ShutdownRequests++;
    }

    /// <summary>The Agent's serve loop and pipe, a client, the harness, and the story the client hears and drives.</summary>
    private async Task RunWatchedScenarioAsync(ServiceProvider services, RecordingLifetime lifetime)
    {
        PipeServer server = services.GetRequiredService<PipeServer>();
        CaptureOrchestrator orchestrator = services.GetRequiredService<CaptureOrchestrator>();
        using var stop = new CancellationTokenSource();
        Task serving = server.RunAsync(stop.Token);
        Task watching = orchestrator.RunAsync(stop.Token);

        Process? harness = null;
        PipeClient? client = null;
        try
        {
            client = NewClient();
            await client.ConnectAsync(TimeSpan.FromSeconds(10), Ct).ConfigureAwait(false);
            await HelloAsync(client).ConfigureAwait(false);

            harness = Process.Start(new ProcessStartInfo(ConsentedExecutable, "--real --hold-presenting 40") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
            int pid = harness.Id;
            Narration story = await ReadStoryAsync(client, s => s.Pid == pid, s => s.Progress.Count >= 3).ConfigureAwait(false);
            AssertNarration(story, pid);

            await PauseResumeAsync(client).ConfigureAwait(false);
            await StopAndAssertAsync(client, story, harness).ConfigureAwait(false);
            await SessionlessCommandsAsync(client, services, lifetime).ConfigureAwait(false);
            server.RejectedClients.Should().Be(0);
        }
        finally
        {
            await TearDownAsync(client, harness, stop).ConfigureAwait(false);
            await JoinAsync(serving, watching).ConfigureAwait(false);
        }
    }

    private async Task RunLaunchScenarioAsync(ServiceProvider services)
    {
        PipeServer server = services.GetRequiredService<PipeServer>();
        CaptureOrchestrator orchestrator = services.GetRequiredService<CaptureOrchestrator>();
        long gameId = (await services.GetRequiredService<IGameRepository>().FindAsync(ConsentedExecutable, Ct).ConfigureAwait(false))!.Id;
        using var stop = new CancellationTokenSource();
        Task serving = server.RunAsync(stop.Token);
        Task watching = orchestrator.RunAsync(stop.Token);

        PipeClient? client = null;
        int launchedPid = 0;
        try
        {
            client = NewClient();
            await client.ConnectAsync(TimeSpan.FromSeconds(10), Ct).ConfigureAwait(false);
            await HelloAsync(client).ConfigureAwait(false);

            LaunchAck launch = await client.RequestAsync<LaunchGameRequest, LaunchAck>(IpcMessageType.LaunchGame,
                new LaunchGameRequest(gameId, "--real --hold-presenting 8"), IpcMessageType.LaunchAck, _ack, Ct).ConfigureAwait(false);
            launch.Accepted.Should().BeTrue(launch.Outcome);
            launch.SessionGuid.Should().NotBeNull();

            LaunchAck again = await client.RequestAsync<LaunchGameRequest, LaunchAck>(IpcMessageType.LaunchGame,
                new LaunchGameRequest(gameId, ""), IpcMessageType.LaunchAck, _ack, Ct).ConfigureAwait(false);
            again.Accepted.Should().BeFalse("one session per game at a time");
            again.Outcome.Should().Be(nameof(LaunchOutcome.SessionRunning));

            Narration story = await ReadStoryAsync(client, s => s.SessionGuid == launch.SessionGuid, static s => s.Completed is not null).ConfigureAwait(false);
            story.Started.Should().NotBeNull("the Agent started the harness itself and attached to its ring");
            launchedPid = story.Started!.Pid;
            story.Started.Tier.Should().Be(1);
            story.Progress.Count.Should().BeGreaterThanOrEqualTo(2, "1 Hz over an 8 s hold");
            story.Completed!.Reason.Should().Be(nameof(Application.Capture.SessionEndReason.TargetExited), "the harness ended its hold");
            story.Completed.Tier.Should().Be(1);
            (await client.GetStatusAsync(_ack, Ct).ConfigureAwait(false)).State.Should().Be("idle");
        }
        finally
        {
            if (launchedPid != 0)
            {
                KillIfRunning(launchedPid);
            }

            await TearDownAsync(client, null, stop).ConfigureAwait(false);
            await JoinAsync(serving, watching).ConfigureAwait(false);
        }
    }

    /// <summary>The serve loop and the pipe end with the token; both tasks are locals of the caller, joined here.</summary>
    private static async Task JoinAsync(Task serving, Task watching)
    {
        Task both = Task.WhenAll(serving, watching);
        try
        {
            await both.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task HelloAsync(PipeClient client)
    {
        HelloAck hello = await client.HelloAsync("test", _ack, Ct).ConfigureAwait(false);
        hello.Protocol.Should().Be(IpcProtocol.Version);
        hello.OverlayBuildId.Should().NotBeNullOrEmpty("the guard answers its build id");
        hello.TelemetrySource.Should().Contain("l1", "the Agent composes L1 + L2 + L3");
        hello.Pid.Should().Be(Environment.ProcessId);
        hello.DisclosureVersion.Should().Be(SafetyDisclosure.Version, "D14: the Agent names the FR-2.1 text it stamps against");
        (await client.GetStatusAsync(_ack, Ct).ConfigureAwait(false)).State.Should().Be("idle");
    }

    private static void AssertNarration(Narration story, int harnessPid)
    {
        story.Started.Should().NotBeNull("the watcher found the harness and the ring was attached");
        story.Started!.Tier.Should().Be(1);
        story.Started.Pid.Should().Be(harnessPid);
        story.Progress.Count.Should().BeGreaterThanOrEqualTo(3, "1 Hz while the harness presents");
        story.Progress.Should().AllSatisfy(p => p.SessionGuid.Should().Be(story.Started.SessionGuid));
        story.Progress.Skip(1).Should().Contain(static p => p.PresentedFps5s > 0, "the harness presents at ~120/s");
        story.Progress.Should().AllSatisfy(static p => p.NativeFps5s.Should().BeNull("the harness has no frame generation, so no Native number may appear"));
        story.ProgressSpacing.Min().Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(850), "rate-limited to the interval");
    }

    private static async Task PauseResumeAsync(PipeClient client)
    {
        PauseAck paused = await client.RequestAsync<PauseCaptureRequest, PauseAck>(IpcMessageType.PauseCapture, new PauseCaptureRequest(), IpcMessageType.PauseAck, _ack, Ct).ConfigureAwait(false);
        paused.Paused.Should().BeTrue();
        StatusAck status = await client.GetStatusAsync(_ack, Ct).ConfigureAwait(false);
        status.Paused.Should().BeTrue("FR-3.9's pause rides beside the session state");
        status.State.Should().Be("capturing", "a paused session is still a hooked, supervised session");

        PauseAck resumed = await client.RequestAsync<ResumeCaptureRequest, PauseAck>(IpcMessageType.ResumeCapture, new ResumeCaptureRequest(), IpcMessageType.PauseAck, _ack, Ct).ConfigureAwait(false);
        resumed.Paused.Should().BeFalse();
        (await client.GetStatusAsync(_ack, Ct).ConfigureAwait(false)).Paused.Should().BeFalse();
    }

    private static async Task StopAndAssertAsync(PipeClient client, Narration story, Process harness)
    {
        Guid guid = story.Started!.SessionGuid;
        StopAck stopAck = await client.RequestAsync<StopSessionRequest, StopAck>(IpcMessageType.StopSession, new StopSessionRequest(guid), IpcMessageType.StopAck, _ack, Ct).ConfigureAwait(false);
        stopAck.Accepted.Should().BeTrue(stopAck.Reason);

        Narration ended = await ReadStoryAsync(client, s => s.SessionGuid == guid, static s => s.Completed is not null, guid).ConfigureAwait(false);
        ended.Completed.Should().NotBeNull("the stop ends the session at its next tick");
        ended.Completed!.SessionGuid.Should().Be(guid);
        ended.Completed.Reason.Should().Be(nameof(Application.Capture.SessionEndReason.StoppedByUser));
        ended.Completed.Tier.Should().Be(1);
        ended.Completed.ExitStatus.Should().Be("normal", "a user stop is not a crash and not a safety stop");
        harness.HasExited.Should().BeFalse("the stop is the session's, never the target's");
        (await client.GetStatusAsync(_ack, Ct).ConfigureAwait(false)).State.Should().Be("idle");

        // A second stop for the same guid may still be accepted for a moment: SessionCompleted is published from
        // the recorder's Ended, a few microseconds before the session's task completes and is reaped — measured
        // 2026-09-13 — and a stop of an ending session is harmless. A guid nobody ever ran is the honest negative.
        StopAck stranger = await client.RequestAsync<StopSessionRequest, StopAck>(IpcMessageType.StopSession, new StopSessionRequest(Guid.NewGuid()), IpcMessageType.StopAck, _ack, Ct).ConfigureAwait(false);
        stranger.Accepted.Should().BeFalse("nothing runs under a guid nobody started");
    }

    private async Task SessionlessCommandsAsync(PipeClient client, ServiceProvider services, RecordingLifetime lifetime)
    {
        long gameId = (await services.GetRequiredService<IGameRepository>().FindAsync(ConsentedExecutable, Ct).ConfigureAwait(false))!.Id;

        WatchlistAck watchlist = await client.RequestAsync<SetWatchlistRequest, WatchlistAck>(IpcMessageType.SetWatchlist,
            new SetWatchlistRequest([new WatchlistEntry(null, ConsentedExecutable), new WatchlistEntry(null, Path.Combine(_dataDir, "nothing-here.exe"))]),
            IpcMessageType.WatchlistAck, _ack, Ct).ConfigureAwait(false);
        watchlist.Games.Should().ContainSingle().Which.GameId.Should().Be(gameId, "the existing row is ensured, not duplicated");
        watchlist.Unreadable.Should().ContainSingle();

        HookEnabledAck revoked = await client.RequestAsync<SetHookEnabledRequest, HookEnabledAck>(IpcMessageType.SetHookEnabled,
            new SetHookEnabledRequest(gameId, Enabled: false, null), IpcMessageType.HookEnabledAck, _ack, Ct).ConfigureAwait(false);
        revoked.Enabled.Should().BeFalse();
        revoked.Outcome.Should().Be(nameof(ConsentWriteOutcome.Written));

        // The real pre-scan of the scratch directory is clean; what decides the stamp is the disclosure version (D14).
        Func<Task> otherText = async () => await client.RequestAsync<SetHookEnabledRequest, HookEnabledAck>(IpcMessageType.SetHookEnabled,
            new SetHookEnabledRequest(gameId, Enabled: true, "client-says-so"), IpcMessageType.HookEnabledAck, _ack, Ct).ConfigureAwait(false);
        (await otherText.Should().ThrowAsync<IpcRequestException>().ConfigureAwait(false)).Which.Code.Should().Be(IpcErrorCode.DisclosureVersionMismatch,
            "a client that showed other text, or none, stamps nothing");
        var consent = new SqliteGameConsentStore(services.GetRequiredService<LedgerDatabase>());
        (await consent.FindAsync(ConsentedExecutable, Ct).ConfigureAwait(false)).HookEnabled.Should().BeFalse();

        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);
        HookEnabledAck enabled = await client.RequestAsync<SetHookEnabledRequest, HookEnabledAck>(IpcMessageType.SetHookEnabled,
            new SetHookEnabledRequest(gameId, Enabled: true, SafetyDisclosure.Version), IpcMessageType.HookEnabledAck, _ack, Ct).ConfigureAwait(false);
        enabled.Enabled.Should().BeTrue();
        enabled.Prescan.Should().Be("clean");
        GameConsentRecord stamped = await consent.FindAsync(ConsentedExecutable, Ct).ConfigureAwait(false);
        stamped.HookEnabled.Should().BeTrue();
        stamped.Provenance.Should().Be(ConsentProvenance.ConsentDialog, "FR-2.1's member, produced by the Agent's stamp and nothing else");
        stamped.DisclosureVersion.Should().Be(SafetyDisclosure.Version);
        stamped.ConsentedAt.Should().BeOnOrAfter(before, "the Agent's clock stamped it, just now");

        UpdateRulesAck rules = await client.RequestAsync<UpdateRulesRequest, UpdateRulesAck>(IpcMessageType.UpdateRules, new UpdateRulesRequest(), IpcMessageType.UpdateRulesAck, _ack, Ct).ConfigureAwait(false);
        rules.Outcome.Should().NotBeNullOrEmpty();

        await client.RequestAsync<ShutdownRequest, ShutdownAck>(IpcMessageType.Shutdown, new ShutdownRequest(), IpcMessageType.ShutdownAck, _ack, Ct).ConfigureAwait(false);
        lifetime.ShutdownRequests.Should().Be(1, "Shutdown asks the host's lifetime and nothing else");
    }

    private static async Task TearDownAsync(PipeClient? client, Process? harness, CancellationTokenSource stop)
    {
        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        if (harness is not null)
        {
            if (!harness.HasExited)
            {
                harness.Kill(entireProcessTree: true);
            }

            harness.Dispose();
        }

        await stop.CancelAsync().ConfigureAwait(false);
    }

    private static void KillIfRunning(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Born here so the caller's try/finally owns it; CA2000 tracks only what a method news itself.</summary>
    private PipeClient NewClient() => new(_pipeName);

    private sealed class Narration
    {
        public SessionStartedEvent? Started { get; set; }

        public List<SessionProgressEvent> Progress { get; } = [];

        public List<TimeSpan> ProgressSpacing { get; } = [];

        public SessionCompletedEvent? Completed { get; set; }
    }

    /// <summary>
    /// One session's story: started when <paramref name="mine"/> says so (or already known by <paramref name="known"/>),
    /// then its progress and completion by guid, until <paramref name="done"/> or the timeout. Other sessions' events are ignored.
    /// </summary>
    private static async Task<Narration> ReadStoryAsync(PipeClient client, Func<SessionStartedEvent, bool> mine, Func<Narration, bool> done, Guid? known = null)
    {
        var story = new Narration();
        if (known is { } g0)
        {
            story.Started = new SessionStartedEvent(g0, 0, null, 0, 1, DateTimeOffset.UtcNow);
        }

        var clock = Stopwatch.StartNew();
        TimeSpan? lastProgress = null;
        while (clock.Elapsed < _timeout && !done(story))
        {
            IpcEnvelope e = await client.Events.ReadAsync(Ct).AsTask().WaitAsync(_timeout - clock.Elapsed, Ct).ConfigureAwait(false);
            Guid? ours = story.Started?.SessionGuid;
            switch (e.Type)
            {
                case IpcMessageType.SessionStarted when ours is null:
                    SessionStartedEvent? started = IpcCodec.Payload<SessionStartedEvent>(e);
                    if (started is not null && mine(started))
                    {
                        story.Started = started;
                    }

                    break;
                case IpcMessageType.SessionProgress when ours is { } g:
                    SessionProgressEvent progress = IpcCodec.Payload<SessionProgressEvent>(e)!;
                    if (progress.SessionGuid != g)
                    {
                        break;
                    }

                    story.Progress.Add(progress);
                    if (lastProgress is { } last)
                    {
                        story.ProgressSpacing.Add(clock.Elapsed - last);
                    }

                    lastProgress = clock.Elapsed;
                    break;
                case IpcMessageType.SessionCompleted when ours is { } g:
                    SessionCompletedEvent completed = IpcCodec.Payload<SessionCompletedEvent>(e)!;
                    if (completed.SessionGuid == g)
                    {
                        story.Completed = completed;
                    }

                    break;
                default:
                    break;
            }
        }

        return story;
    }
}
