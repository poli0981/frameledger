using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Agent.Composition;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;
using Microsoft.Extensions.DependencyInjection;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// The pipe's read half end to end (P3 PR-1): the Agent's own composition — the watcher, the recorder, the pipe
/// server — in this process over a scratch ledger and a pipe named for this test, our harness discovered by the
/// watcher, and a client that hears <c>SessionStarted</c>, at least three <c>SessionProgress</c> a second apart,
/// and <c>SessionCompleted</c> for the same guid. Then <c>GetStatus</c> is idle again.
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

    private void StageTarget()
    {
        File.Exists(Harness).Should().BeTrue("hook-harness.exe must be staged beside the test binary (FrameLedger.DrainFixtures.targets)");
        Directory.CreateDirectory(_dataDir);
        File.Copy(Harness, ConsentedExecutable, overwrite: true);
    }

    [Fact]
    public async Task AWatchedHarnessSessionIsNarratedOverThePipeFromStartToCompletion()
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
                await RunAgentAndNarrateAsync(services).ConfigureAwait(true);
            }
        }
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

    /// <summary>The Agent's serve loop and pipe, a client, the harness, and the story the client hears.</summary>
    private async Task RunAgentAndNarrateAsync(ServiceProvider services)
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
            HelloAck hello = await client.HelloAsync("test", _timeout, Ct).ConfigureAwait(false);
            hello.Protocol.Should().Be(IpcProtocol.Version);
            hello.OverlayBuildId.Should().NotBeNullOrEmpty("the guard answers its build id");
            hello.TelemetrySource.Should().Contain("l1", "the Agent composes L1 + L2 + L3");
            hello.Pid.Should().Be(Environment.ProcessId);
            (await client.GetStatusAsync(_timeout, Ct).ConfigureAwait(false)).State.Should().Be("idle");

            harness = Process.Start(new ProcessStartInfo(ConsentedExecutable, "--real --hold-presenting 12") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;

            Narration story = await ReadStoryAsync(client, harness.Id).ConfigureAwait(false);
            AssertStory(story, harness.Id);

            (await client.GetStatusAsync(_timeout, Ct).ConfigureAwait(false)).State.Should().Be("idle");
            server.RejectedClients.Should().Be(0);
        }
        finally
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
            try
            {
                await Task.WhenAll(serving, watching).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    /// <summary>Born here so the caller's try/finally owns it; CA2000 tracks only what a method news itself.</summary>
    private PipeClient NewClient() => new(_pipeName);

    private static void AssertStory(Narration story, int harnessPid)
    {
        story.Started.Should().NotBeNull("the watcher found the harness and the ring was attached");
        story.Started!.Tier.Should().Be(1);
        story.Started.Pid.Should().Be(harnessPid);
        story.Progress.Count.Should().BeGreaterThanOrEqualTo(3, "1 Hz over a 12 s hold");
        story.Progress.Should().AllSatisfy(p => p.SessionGuid.Should().Be(story.Started.SessionGuid));
        story.Progress.Skip(1).Should().Contain(static p => p.PresentedFps5s > 0, "the harness presents at ~120/s");
        story.Progress.Should().AllSatisfy(static p => p.NativeFps5s.Should().BeNull("the harness has no frame generation, so no Native number may appear"));
        story.ProgressSpacing.Min().Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(850), "rate-limited to the interval");
        story.Completed.Should().NotBeNull();
        story.Completed!.SessionGuid.Should().Be(story.Started.SessionGuid);
        story.Completed.Tier.Should().Be(1);
        story.Completed.Finalize.Should().Be("discarded", "12 s is under the product's 30 s minimum; the pipe, not the row, is under test");
    }

    private sealed class Narration
    {
        public SessionStartedEvent? Started { get; set; }

        public List<SessionProgressEvent> Progress { get; } = [];

        public List<TimeSpan> ProgressSpacing { get; } = [];

        public SessionCompletedEvent? Completed { get; set; }
    }

    /// <summary>Our harness's story only: keyed on its pid at the start, on the guid from then on; other sessions are ignored.</summary>
    private static async Task<Narration> ReadStoryAsync(PipeClient client, int harnessPid)
    {
        var story = new Narration();
        var clock = Stopwatch.StartNew();
        TimeSpan? lastProgress = null;
        while (clock.Elapsed < _timeout)
        {
            IpcEnvelope e = await client.Events.ReadAsync(Ct).AsTask().WaitAsync(_timeout - clock.Elapsed, Ct).ConfigureAwait(false);
            Guid? ours = story.Started?.SessionGuid;
            switch (e.Type)
            {
                case IpcMessageType.SessionStarted when ours is null:
                    SessionStartedEvent? started = IpcCodec.Payload<SessionStartedEvent>(e);
                    if (started?.Pid == harnessPid)
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
                        return story;
                    }

                    break;
                default:
                    break;
            }
        }

        return story;
    }
}
