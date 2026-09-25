using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Detection;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Ipc;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Detection;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.AntiCheat;

/// <summary>
/// The Agent's pre-scan of the library (beta.8): every entry is scanned once and again when the rules or the executable
/// change; a block is final and never scanned again; a finding on an entry the user had turned on tells the App; nothing
/// is scanned while its session runs, for an unreadable executable, or under unusable rules.
/// </summary>
public sealed class AntiCheatPreScanSweepTests
{
    private const string _exe = @"C:\Games\Title\game.exe";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ScriptedRules(string version) : IDetectionRulesSource
    {
        public string Version { get; set; } = version;

        public bool Unusable { get; set; }

        public ValueTask<DetectionRuleSet> LoadAsync(CancellationToken ct = default) => Unusable
            ? throw new InvalidOperationException("rules unusable")
            : ValueTask.FromResult(new DetectionRuleSet { SchemaVersion = 2, RulesVersion = Version, Engines = [], Platforms = [], Capabilities = [] });
    }

    private sealed class FakeIdentity : IExecutableIdentitySource
    {
        public ExecutableFingerprint? OnDisk { get; set; } = new() { ExePath = _exe, SizeBytes = 5, MtimeUnixMs = 5 };

        public ExecutableFingerprint? Read(string normalisedExePath) => OnDisk;

        public string Normalise(string exePath) => exePath;
    }

    private sealed class ScriptedGuard : IAntiCheatGuard
    {
        public AntiCheatVerdict Answer { get; set; } = AntiCheatVerdict.Allowed();

        public List<string> Scanned { get; } = [];

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, CancellationToken ct = default) =>
            throw new InvalidOperationException("the pre-scan never evaluates a process");

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, CancellationToken ct = default) =>
            throw new InvalidOperationException("the pre-scan never injects");

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, CancellationToken ct = default) =>
            throw new InvalidOperationException("the pre-scan never injects");

        public ValueTask<AntiCheatVerdict> PreScanGameAsync(string executablePath, CancellationToken ct = default)
        {
            Scanned.Add(executablePath);
            return ValueTask.FromResult(Answer);
        }
    }

    /// <summary>The adapter's rule, over the fake library: the key always, the block on a finding, never over a block.</summary>
    private sealed class LibraryConsent(FakeGameRepository games) : IGameConsentStore
    {
        public List<(ExecutableFingerprint Scanned, AntiCheatVerdict Verdict, string Rules)> Writes { get; } = [];

        public ValueTask<ConsentWriteOutcome> RecordPreScanAsync(ExecutableFingerprint scanned, AntiCheatVerdict verdict, string rulesVersion, CancellationToken ct = default)
        {
            Writes.Add((scanned, verdict, rulesVersion));
            GameRow row = games.Rows[scanned.ExePath];
            if (row.BlockedByGuard)
            {
                return ValueTask.FromResult(ConsentWriteOutcome.NotFound);
            }

            games.Rows[scanned.ExePath] = row with
            {
                HookPrescanRulesVersion = rulesVersion,
                HookPrescanExeSizeBytes = scanned.SizeBytes,
                HookPrescanExeMtimeMs = scanned.MtimeUnixMs,
                HookPrescanState = verdict.IsFindingAboutTheGame ? "blocked" : verdict.IsAllowed ? "clean" : "unverified",
                HookBlockedReason = verdict.IsFindingAboutTheGame ? $"{verdict.Reason}|{verdict.Family}|{verdict.Signal}" : null,
                HookEnabled = !verdict.IsFindingAboutTheGame && row.HookEnabled,
            };
            return ValueTask.FromResult(ConsentWriteOutcome.Written);
        }

        public ValueTask<GameConsentRecord> FindAsync(string normalisedExePath, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<GameConsentRecord>> ListEnabledAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ConsentWriteOutcome> RecordOperatorAcknowledgementAsync(OperatorAcknowledgement acknowledgement, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<ConsentWriteOutcome> RevokeAsync(string normalisedExePath, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ConsentWriteOutcome> RecordGuardBlockAsync(ExecutableFingerprint fingerprint, AntiCheatVerdict refusal, CancellationToken ct = default) =>
            throw new NotSupportedException("the pre-scan writes through RecordPreScanAsync");
    }

    private sealed class Rig : IDisposable
    {
        public Rig(FakeGameRepository games)
        {
            Games = games;
            Consent = new LibraryConsent(games);
            Sweep = new AntiCheatPreScanSweep(games, Consent, Guard, Rules, Identity, static _ => { }, Pipe, Recording.Contains);
        }

        public AntiCheatPreScanSweep Sweep { get; }

        public FakeGameRepository Games { get; }

        public ScriptedRules Rules { get; } = new("2026.09.4");

        public ScriptedGuard Guard { get; } = new();

        public FakeIdentity Identity { get; } = new();

        public LibraryConsent Consent { get; }

        public RecordingPublisher Pipe { get; } = new();

        public HashSet<long> Recording { get; } = [];

        public void Dispose() => Sweep.Dispose();
    }

    private static async Task<Rig> BuildAsync(bool hookEnabled = false)
    {
        var games = new FakeGameRepository();
        GameRow row = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _exe, SizeBytes = 1, MtimeUnixMs = 1 }, "Title", Ct).ConfigureAwait(false);
        games.Rows[_exe] = row with { HookEnabled = hookEnabled };
        return new Rig(games);
    }

    [Fact]
    public async Task AnEntryNeverScannedIsScannedOnceUnderItsKeyAndThenLeftAlone()
    {
        using Rig r = await BuildAsync();

        AntiCheatPreScanReport first = await r.Sweep.SweepOnceAsync(Ct);
        first.Clean.Should().Be(1);
        r.Guard.Scanned.Should().Equal(_exe);
        r.Consent.Writes.Should().ContainSingle().Which.Rules.Should().Be("2026.09.4");
        r.Consent.Writes[0].Scanned.SizeBytes.Should().Be(5, "the key is the executable ON DISK now, not the fingerprint the row was added with");
        r.Games.Rows[_exe].HookPrescanState.Should().Be("clean");

        AntiCheatPreScanReport second = await r.Sweep.SweepOnceAsync(Ct);
        second.Scanned.Should().Be(0);
        second.Current.Should().Be(1);
        r.Guard.Scanned.Should().ContainSingle();
    }

    [Fact]
    public async Task NewRulesOrANewExecutableIsAReScan()
    {
        using Rig r = await BuildAsync();
        _ = await r.Sweep.SweepOnceAsync(Ct);

        r.Rules.Version = "2026.09.5";
        (await r.Sweep.SweepOnceAsync(Ct)).Scanned.Should().Be(1, "a rules update may name a game the library already holds");

        r.Identity.OnDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 6, MtimeUnixMs = 6 };
        (await r.Sweep.SweepOnceAsync(Ct)).Scanned.Should().Be(1, "a game update may have added anti-cheat");
        r.Guard.Scanned.Should().HaveCount(3);
    }

    [Fact]
    public async Task AFindingIsTheBlockAndABlockIsNeverScannedAgain()
    {
        using Rig r = await BuildAsync();
        r.Guard.Answer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatDirectory, "Easy Anti-Cheat", "EasyAntiCheat");

        AntiCheatPreScanReport first = await r.Sweep.SweepOnceAsync(Ct);
        first.Found.Should().Be(1);
        r.Games.Rows[_exe].BlockedByGuard.Should().BeTrue();

        // Nothing clears a block: new rules that no longer name it, and a scan that would now pass, change nothing.
        r.Rules.Version = "2026.09.9";
        r.Guard.Answer = AntiCheatVerdict.Allowed();
        AntiCheatPreScanReport second = await r.Sweep.SweepOnceAsync(Ct);
        second.Blocked.Should().Be(1);
        second.Scanned.Should().Be(0);
        r.Guard.Scanned.Should().ContainSingle("a blocked row is not asked again");
    }

    [Fact]
    public async Task TurningOffHookingTheUserTurnedOnTellsTheApp()
    {
        using Rig r = await BuildAsync(hookEnabled: true);
        r.Guard.Answer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedStoreId, "Valve VAC", "steam:730");

        _ = await r.Sweep.SweepOnceAsync(Ct);

        HookingTurnedOffEvent e = r.Pipe.Of<HookingTurnedOffEvent>().Should().ContainSingle().Subject;
        e.GameName.Should().Be("Title");
        e.Reason.Should().Be(nameof(AntiCheatRefusalReason.BlockedStoreId));
        e.Family.Should().Be("Valve VAC");
        e.Signal.Should().Be("steam:730");
        r.Pipe.Published.Should().ContainSingle(static p => p.Type == IpcMessageType.HookingTurnedOff);
        r.Games.Rows[_exe].HookEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task AFindingOnAnEntryWhoseHookingWasAlreadyOffTellsNobody()
    {
        using Rig r = await BuildAsync(hookEnabled: false);
        r.Guard.Answer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatFile, "Kernel driver in the game folder", "x.sys");

        (await r.Sweep.SweepOnceAsync(Ct)).Found.Should().Be(1);
        r.Pipe.Published.Should().BeEmpty("nothing the user can see changed but the game's page");
    }

    [Fact]
    public async Task AScanThatCouldNotAnswerIsUnverifiedNotABlock()
    {
        using Rig r = await BuildAsync(hookEnabled: true);
        r.Guard.Answer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.PreScanFailed, string.Empty, "the game directory could not be listed");

        AntiCheatPreScanReport report = await r.Sweep.SweepOnceAsync(Ct);
        report.Unverified.Should().Be(1);
        r.Games.Rows[_exe].HookPrescanState.Should().Be("unverified");
        r.Games.Rows[_exe].HookEnabled.Should().BeTrue("a scan that could not look does not turn hooking off");
        r.Pipe.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task NothingIsScannedWhileItsSessionRunsForAnUnreadableFileOrUnderUnusableRules()
    {
        using Rig r = await BuildAsync();

        r.Recording.Add(r.Games.Rows[_exe].Id);
        (await r.Sweep.SweepOnceAsync(Ct)).Scanned.Should().Be(0, "the session's own checks are running");
        r.Recording.Clear();

        r.Identity.OnDisk = null;
        (await r.Sweep.SweepOnceAsync(Ct)).Unreadable.Should().Be(1);
        r.Identity.OnDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 5, MtimeUnixMs = 5 };

        r.Rules.Unusable = true;
        (await r.Sweep.SweepOnceAsync(Ct)).RulesUnusable.Should().BeTrue();

        r.Guard.Scanned.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestNowWakesTheLoopBeforeItsInterval()
    {
        using Rig r = await BuildAsync();
        r.Sweep.RequestNow();
        r.Sweep.RequestNow();    // idempotent: one wake is one wake
        (await r.Sweep.WaitAsync(TimeSpan.FromMinutes(5), Ct)).Should().BeTrue();
        r.Sweep.Dispose();
    }
}
