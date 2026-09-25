using System.Text;
using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Detection;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Detection;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Ipc;

/// <summary>
/// The command half (<c>07_IPC</c> §Messages, UI → Agent) over fakes and the real SQLite consent store: every
/// message asks, and what the Agent establishes for itself is observed on the store, the orchestrator, the pause
/// and the lifetime. The rule under test is <c>07_IPC</c> §The pipe is not a trust boundary — in particular that
/// <c>SetHookEnabled true</c> runs the pre-scan, records a block, and stamps NOTHING while the reviewed
/// disclosure does not exist.
/// </summary>
public sealed class AgentCommandHandlerTests : IAsyncDisposable
{
    private const string _exe = @"C:\Games\Title\game.exe";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-commands-" + Guid.NewGuid().ToString("N"));
    private LedgerDatabase? _db;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class FakeGuard : IAntiCheatGuard
    {
        public AntiCheatVerdict PreScan { get; set; } = AntiCheatVerdict.Allowed();

        public List<string> Scanned { get; } = [];

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, CancellationToken ct = default) => ValueTask.FromResult(AntiCheatVerdict.Allowed());

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, CancellationToken ct = default) =>
            throw new InvalidOperationException("a command never injects");

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, CancellationToken ct = default) =>
            throw new InvalidOperationException("a command never injects");

        public ValueTask<AntiCheatVerdict> PreScanGameAsync(string executablePath, CancellationToken ct = default)
        {
            Scanned.Add(executablePath);
            return ValueTask.FromResult(PreScan);
        }
    }

    private sealed class FakeIdentity : IExecutableIdentitySource
    {
        public HashSet<string> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ExecutableFingerprint? Read(string normalisedExePath) =>
            Unreadable.Contains(normalisedExePath) ? null : new ExecutableFingerprint { ExePath = normalisedExePath, SizeBytes = 90_000, MtimeUnixMs = 1_700_000_000_000 };

        public string Normalise(string exePath) => exePath.ToUpperInvariant();
    }

    private sealed class FakeRecorder : ISessionRecorder
    {
        public async Task<RecordedSession> RecordAsync(RecordRequest request, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            throw new OperationCanceledException(ct);
        }
    }

    private sealed class FakeSnapshots : IProcessSnapshotSource
    {
        public IReadOnlyList<ProcessSnapshot> Take() => [];
    }

    private sealed class FakeLifetime : IAgentLifetime
    {
        public int Requests { get; private set; }

        public void RequestShutdown() => Requests++;
    }

    /// <summary>The Agent's clock for the stamp (a client cannot attest a time).</summary>
    private sealed class FakeClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Harness
    {
        public required AgentCommandHandler Handler { get; init; }

        public required FakeGameRepository Games { get; init; }

        public required SqliteGameConsentStore Consent { get; init; }

        public required FakeGuard Guard { get; init; }

        public required FakeIdentity Identity { get; init; }

        public required CaptureOrchestrator Orchestrator { get; init; }

        public required CapturePause Pause { get; init; }

        public required FakeLifetime Lifetime { get; init; }

        public int RulesUpdates { get; set; }
    }

    /// <summary>What the executable runs as, scripted (beta.8).</summary>
    private sealed class ScriptedArchitecture(string answer) : IExecutableArchitectureSource
    {
        public List<string> Read { get; } = [];

        string IExecutableArchitectureSource.Read(string exePath)
        {
            Read.Add(exePath);
            return answer;
        }
    }

    private async Task<Harness> BuildAsync(bool consented = true, string? disclosureVersion = null, Func<CancellationToken, ValueTask<SweepRetentionAck>>? sweep = null,
        IExecutableArchitectureSource? architecture = null)
    {
        _db ??= await LedgerDatabase.OpenAsync(Path.Combine(_dir, LedgerPaths.DatabaseFileName), ct: Ct).ConfigureAwait(false);
        var consent = new SqliteGameConsentStore(_db);
        var games = new FakeGameRepository();
        var identity = new FakeIdentity();
        var guard = new FakeGuard();
        var pause = new CapturePause();
        var lifetime = new FakeLifetime();
        await games.EnsureAsync(identity.Read(_exe)!.Value, "Title", Ct).ConfigureAwait(false);
        if (consented)
        {
            await consent.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
            {
                Fingerprint = identity.Read(_exe)!.Value,
                DisclosureVersion = "test/1",
                AcknowledgedAt = DateTimeOffset.UnixEpoch,
            }, Ct).ConfigureAwait(false);
        }

        var orchestrator = new CaptureOrchestrator(new FakeRecorder(), games, new FakeSnapshots(), identity,
            new OrchestratorOptions { PayloadPath = @"C:\FL\FrameLedger.Overlay.dll" }, static _ => { });
        Harness h = null!;
        var handler = new AgentCommandHandler(games, consent, guard, identity, orchestrator, pause, lifetime,
            _ => { h.RulesUpdates++; return ValueTask.FromResult("AlreadyCurrent"); },
            disclosureVersion, new FakeClock(), sweepRetention: sweep, architecture: architecture);
        h = new Harness
        {
            Handler = handler,
            Games = games,
            Consent = consent,
            Guard = guard,
            Identity = identity,
            Orchestrator = orchestrator,
            Pause = pause,
            Lifetime = lifetime,
        };
        return h;
    }

    [Fact]
    public async Task SweepRetentionRunsTheComposedSweepAndAnswersItsCounts()
    {
        int runs = 0;
        Harness h = await BuildAsync(sweep: _ =>
        {
            runs++;
            return ValueTask.FromResult(new SweepRetentionAck(20, 3, 7));
        }).ConfigureAwait(true);

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SweepRetention, new SweepRetentionRequest()).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.SweepRetentionAck);
        IpcCodec.Payload<SweepRetentionAck>(ack).Should().Be(new SweepRetentionAck(20, 3, 7));
        runs.Should().Be(1);
    }

    [Fact]
    public async Task WithNoSweepComposedSweepRetentionIsNotACommandThisHalfAnswers()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);

        byte[]? ack = await h.Handler.HandleAsync(IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.SweepRetention, "1", new SweepRetentionRequest())), Ct).ConfigureAwait(true);

        ack.Should().BeNull("the request handler then answers UnknownType, which the App reads as an Agent that predates the sweep");
    }

    private static async Task<IpcEnvelope> AskAsync<T>(AgentCommandHandler handler, string type, T payload)
        where T : class
    {
        byte[]? ack = await handler.HandleAsync(IpcCodec.Decode(IpcCodec.Encode(type, "1", payload)), Ct).ConfigureAwait(false);
        ack.Should().NotBeNull($"{type} is a command");
        return IpcCodec.Decode(ack!);
    }

    private static async Task<IpcEnvelope> AskAsync(AgentCommandHandler handler, string type)
    {
        byte[]? ack = await handler.HandleAsync(IpcCodec.Decode(IpcCodec.Encode(type, "1")), Ct).ConfigureAwait(false);
        ack.Should().NotBeNull($"{type} is a command");
        return IpcCodec.Decode(ack!);
    }

    [Fact]
    public async Task ARequestThatIsNotACommandIsNotAnswered()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);
        (await h.Handler.HandleAsync(IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.Hello, "1")), Ct).ConfigureAwait(true)).Should().BeNull();
    }

    [Fact]
    public async Task SetWatchlistEnsuresIdentityRowsWithHookingOffAndNamesWhatItCouldNotRead()
    {
        Harness h = await BuildAsync(consented: false).ConfigureAwait(true);
        h.Identity.Unreadable.Add(@"D:\GONE\MISSING.EXE");

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetWatchlist,
            new SetWatchlistRequest([new WatchlistEntry(null, @"c:\games\other\other.exe"), new WatchlistEntry(7, @"d:\gone\missing.exe")])).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.WatchlistAck);
        ack.Id.Should().Be("1");
        WatchlistAck payload = IpcCodec.Payload<WatchlistAck>(ack)!;
        payload.Games.Should().ContainSingle().Which.ExePath.Should().Be(@"C:\GAMES\OTHER\OTHER.EXE", "the Agent normalises; the client's spelling is never the key");
        payload.Games[0].Name.Should().Be("OTHER");
        payload.Unreadable.Should().Equal(@"d:\gone\missing.exe");
        h.Games.Rows[@"C:\GAMES\OTHER\OTHER.EXE"].HookEnabled.Should().BeFalse("identity only; hooking is never enabled by a watchlist");
        h.Games.Rows.Should().HaveCount(2, "the existing row stays; nothing is removed by this message");
    }

    [Fact]
    public async Task SetHookEnabledFalseRevokesThroughTheStore()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);
        long gameId = h.Games.Rows[_exe].Id;
        (await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true)).HookEnabled.Should().BeTrue();

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(gameId, Enabled: false, null)).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.HookEnabledAck);
        HookEnabledAck payload = IpcCodec.Payload<HookEnabledAck>(ack)!;
        payload.Enabled.Should().BeFalse();
        payload.Outcome.Should().Be(nameof(ConsentWriteOutcome.Written));
        (await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true)).HookEnabled.Should().BeFalse();
        h.Guard.Scanned.Should().BeEmpty("a revoke needs no scan");
    }

    [Fact]
    public async Task SetHookEnabledTrueRunsThePreScanAndABlockIsRecordedAndAnsweredRefused()
    {
        Harness h = await BuildAsync(consented: false).ConfigureAwait(true);
        long gameId = h.Games.Rows[_exe].Id;
        h.Guard.PreScan = AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "eac", "EasyAntiCheat_EOS.dll");

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(gameId, Enabled: true, "whatever-the-client-says")).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.Refused);
        RefusedAck refused = IpcCodec.Payload<RefusedAck>(ack)!;
        refused.GameId.Should().Be(gameId);
        refused.Reason.Should().Be(nameof(AntiCheatRefusalReason.BlockedModule));
        refused.Family.Should().Be("eac");
        refused.Signal.Should().Be("EasyAntiCheat_EOS.dll");
        h.Guard.Scanned.Should().ContainSingle().Which.Should().Be(_exe, "the guard is handed the executable and resolves the install root itself (2026-09-25)");
        GameConsentRecord record = await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true);
        record.HookEnabled.Should().BeFalse();
        record.BlockedReason.Should().NotBeNullOrEmpty("the block is on the row for FR-2.2's disabled toggle");
        record.ConsentedAt.Should().BeNull("nothing was stamped");
    }

    /// <summary>
    /// beta.8: an executable that cannot run as x64 is refused before anything is scanned or stamped — the dialog would
    /// promise a measurement the hook cannot make — and the refusal names the architecture. An x64 one goes on as before.
    /// </summary>
    [Fact]
    public async Task SetHookEnabledTrueForAnExecutableThatCannotRunAsX64IsRefusedBeforeTheScan()
    {
        var x86 = new ScriptedArchitecture(ExecutableArchitecture.X86);
        Harness h = await BuildAsync(consented: false, disclosureVersion: "consent-dialog/1", architecture: x86).ConfigureAwait(true);
        long gameId = h.Games.Rows[_exe].Id;

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(gameId, Enabled: true, "consent-dialog/1")).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.Refused);
        RefusedAck refused = IpcCodec.Payload<RefusedAck>(ack)!;
        (refused.Reason, refused.Family, refused.Signal).Should().Be((AgentCommandHandler.NotX64Reason, (string?)null, ExecutableArchitecture.X86));
        x86.Read.Should().ContainSingle().Which.Should().Be(_exe);
        h.Guard.Scanned.Should().BeEmpty("nothing is scanned for a process the hook can never enter");
        GameConsentRecord record = await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true);
        (record.HookEnabled, record.ConsentedAt, record.BlockedReason).Should().Be((false, (DateTimeOffset?)null, (string?)null),
            "nothing is written: this is not a block, and a 64-bit build of the game could be hooked");

        Harness x64 = await BuildAsync(consented: false, architecture: new ScriptedArchitecture(ExecutableArchitecture.X64)).ConfigureAwait(true);
        _ = await AskAsync(x64.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(x64.Games.Rows[_exe].Id, Enabled: true, "v")).ConfigureAwait(true);
        x64.Guard.Scanned.Should().ContainSingle("an x64 executable is scanned as before");
    }

    [Fact]
    public async Task SetHookEnabledTrueWithACleanScanStampsNothingWhenTheAgentCarriesNoDisclosure()
    {
        Harness h = await BuildAsync(consented: false).ConfigureAwait(true);
        long gameId = h.Games.Rows[_exe].Id;

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(gameId, Enabled: true, "client-claims-a-version")).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.Error);
        ErrorAck error = IpcCodec.Payload<ErrorAck>(ack)!;
        error.Code.Should().Be(IpcErrorCode.DisclosureUnavailable);
        error.Message.Should().Contain("consent grant");
        h.Guard.Scanned.Should().ContainSingle("the pre-scan ran first");
        GameConsentRecord record = await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true);
        record.HookEnabled.Should().BeFalse();
        record.ConsentedAt.Should().BeNull("a client's version string is not a disclosure shown; nothing is stamped");
        record.Provenance.Should().Be(ConsentProvenance.NotRecorded);
    }

    [Fact]
    public async Task SetHookEnabledTrueWithAnotherDisclosureVersionStampsNothing()
    {
        Harness h = await BuildAsync(consented: false, disclosureVersion: "consent-dialog/9").ConfigureAwait(true);
        long gameId = h.Games.Rows[_exe].Id;

        foreach (string? claimed in new[] { "consent-dialog/8", null, "" })
        {
            IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(gameId, Enabled: true, claimed)).ConfigureAwait(true);
            ack.Type.Should().Be(IpcMessageType.Error, claimed ?? "(null)");
            ErrorAck error = IpcCodec.Payload<ErrorAck>(ack)!;
            error.Code.Should().Be(IpcErrorCode.DisclosureVersionMismatch);
            error.Message.Should().Contain("consent-dialog/9").And.Contain("restart both");
        }

        GameConsentRecord record = await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true);
        record.HookEnabled.Should().BeFalse("D14: text the Agent does not stand behind stamps nothing");
        record.Provenance.Should().Be(ConsentProvenance.NotRecorded);
    }

    [Fact]
    public async Task SetHookEnabledTrueWithTheAgentsDisclosureVersionStampsFromTheAgentsClockWithConsentDialogProvenance()
    {
        Harness h = await BuildAsync(consented: false, disclosureVersion: "consent-dialog/9").ConfigureAwait(true);
        long gameId = h.Games.Rows[_exe].Id;

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(gameId, Enabled: true, "consent-dialog/9")).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.HookEnabledAck);
        HookEnabledAck enabled = IpcCodec.Payload<HookEnabledAck>(ack)!;
        enabled.Should().Be(new HookEnabledAck(gameId, Enabled: true, nameof(ConsentWriteOutcome.Written), "clean"));
        h.Guard.Scanned.Should().ContainSingle("the pre-scan ran first, and passed");

        GameConsentRecord record = await h.Consent.FindAsync(_exe, Ct).ConfigureAwait(true);
        record.HookEnabled.Should().BeTrue();
        record.Provenance.Should().Be(ConsentProvenance.ConsentDialog, "FR-2.1's member, produced by the Agent and nobody else");
        record.DisclosureVersion.Should().Be("consent-dialog/9", "the Agent's version, not the client's echo of it");
        record.ConsentedAt.Should().Be(FakeClock.Now, "the Agent's clock: a client cannot attest a time");
    }

    [Fact]
    public async Task SetHookEnabledForAnUnknownGameOrAnUnreadableExecutableIsAnError()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);
        IpcEnvelope unknown = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(999, Enabled: true, null)).ConfigureAwait(true);
        IpcCodec.Payload<ErrorAck>(unknown)!.Code.Should().Be(IpcErrorCode.UnknownGame);

        h.Identity.Unreadable.Add(_exe);
        IpcEnvelope unreadable = await AskAsync(h.Handler, IpcMessageType.SetHookEnabled, new SetHookEnabledRequest(h.Games.Rows[_exe].Id, Enabled: true, null)).ConfigureAwait(true);
        IpcCodec.Payload<ErrorAck>(unreadable)!.Code.Should().Be(IpcErrorCode.ExecutableUnreadable);
        h.Guard.Scanned.Should().BeEmpty("nothing is scanned for a file that cannot be read");
    }

    [Fact]
    public async Task PauseAndResumeFlipTheOneGlobalFlagTheStatusReports()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);
        h.Handler.IsPaused.Should().BeFalse();

        IpcEnvelope paused = await AskAsync(h.Handler, IpcMessageType.PauseCapture).ConfigureAwait(true);
        paused.Type.Should().Be(IpcMessageType.PauseAck);
        IpcCodec.Payload<PauseAck>(paused)!.Paused.Should().BeTrue();
        h.Pause.IsPaused.Should().BeTrue();
        h.Handler.IsPaused.Should().BeTrue();

        IpcEnvelope resumed = await AskAsync(h.Handler, IpcMessageType.ResumeCapture).ConfigureAwait(true);
        IpcCodec.Payload<PauseAck>(resumed)!.Paused.Should().BeFalse();
        h.Pause.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task StopSessionForAGuidNobodyRunsIsNotAccepted()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);
        var guid = Guid.NewGuid();

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.StopSession, new StopSessionRequest(guid)).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.StopAck);
        StopAck payload = IpcCodec.Payload<StopAck>(ack)!;
        payload.SessionGuid.Should().Be(guid);
        payload.Accepted.Should().BeFalse();
        payload.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LaunchGameIsTheOrchestratorsAnswer()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);

        IpcEnvelope ack = await AskAsync(h.Handler, IpcMessageType.LaunchGame, new LaunchGameRequest(h.Games.Rows[_exe].Id, null)).ConfigureAwait(true);

        ack.Type.Should().Be(IpcMessageType.LaunchAck);
        LaunchAck payload = IpcCodec.Payload<LaunchAck>(ack)!;
        payload.Accepted.Should().BeFalse("this orchestrator was composed without launch mode");
        payload.Outcome.Should().Be(nameof(LaunchOutcome.Unavailable));
        payload.SessionGuid.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRulesIsATriggerWithNoSourceAndShutdownAsksTheLifetime()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);

        IpcEnvelope rules = await AskAsync(h.Handler, IpcMessageType.UpdateRules).ConfigureAwait(true);
        rules.Type.Should().Be(IpcMessageType.UpdateRulesAck);
        IpcCodec.Payload<UpdateRulesAck>(rules)!.Outcome.Should().Be("AlreadyCurrent");
        h.RulesUpdates.Should().Be(1);

        // A payload naming a path is ignored: the rules SOURCE is not a parameter (07_IPC §The pipe is not a trust boundary).
        byte[] withPath = IpcCodec.Encode(new IpcEnvelope { Type = IpcMessageType.UpdateRules, Id = "2", Payload = System.Text.Json.JsonDocument.Parse("{\"path\":\"C:\\\\evil\\\\rules.json\"}").RootElement });
        (await h.Handler.HandleAsync(IpcCodec.Decode(withPath), Ct).ConfigureAwait(true)).Should().NotBeNull();
        h.RulesUpdates.Should().Be(2, "answered as the same bare trigger");

        IpcEnvelope shutdown = await AskAsync(h.Handler, IpcMessageType.Shutdown).ConfigureAwait(true);
        shutdown.Type.Should().Be(IpcMessageType.ShutdownAck);
        h.Lifetime.Requests.Should().Be(1);
    }

    [Fact]
    public async Task ACommandWithoutItsPayloadIsMalformedNotAGuess()
    {
        Harness h = await BuildAsync().ConfigureAwait(true);
        foreach (string type in new[] { IpcMessageType.SetWatchlist, IpcMessageType.LaunchGame, IpcMessageType.SetHookEnabled, IpcMessageType.StopSession })
        {
            IpcEnvelope ack = await AskAsync(h.Handler, type).ConfigureAwait(true);
            ack.Type.Should().Be(IpcMessageType.Error, type);
            IpcCodec.Payload<ErrorAck>(ack)!.Code.Should().Be(IpcErrorCode.Malformed, type);
        }

        Encoding.UTF8.GetString(IpcCodec.Encode(IpcMessageType.Ping, "9")).Should().NotContain("payload");
    }
}
