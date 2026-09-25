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

        /// <summary>D33: the family each scan named, in order (null for none).</summary>
        public List<string?> Tolerated { get; } = [];

        /// <summary>D33: the answer to a scan that names a family; <see cref="Answer"/> when unset.</summary>
        public AntiCheatVerdict? TolerantAnswer { get; set; }

        public ValueTask<AntiCheatVerdict> EvaluateAsync(int targetPid, string? toleratedFamily, CancellationToken ct = default) =>
            throw new InvalidOperationException("the pre-scan never evaluates a process");

        public ValueTask<AntiCheatVerdict> GuardedInjectAsync(int targetPid, string payloadPath, string? toleratedFamily, CancellationToken ct = default) =>
            throw new InvalidOperationException("the pre-scan never injects");

        public ValueTask<AntiCheatVerdict> GuardedInjectWhenReadyAsync(int targetPid, string payloadPath, int timeoutMs, string? toleratedFamily, CancellationToken ct = default) =>
            throw new InvalidOperationException("the pre-scan never injects");

        public ValueTask<AntiCheatVerdict> PreScanGameAsync(string executablePath, string? toleratedFamily, CancellationToken ct = default)
        {
            Scanned.Add(executablePath);
            Tolerated.Add(toleratedFamily);
            return ValueTask.FromResult(toleratedFamily is not null && TolerantAnswer is { } tolerant ? tolerant : Answer);
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

        /// <summary>D33: every eligibility write, in order.</summary>
        public List<(bool Eligible, string? Verdict, int Sessions)> Eligibility { get; } = [];

        /// <summary>D33: every exception ended, in order.</summary>
        public List<string> Revoked { get; } = [];

        public ValueTask<ConsentWriteOutcome> RecordExceptionEligibilityAsync(ExecutableFingerprint scanned, string block, AntiCheatVerdict? verdict, int sessions,
            string rulesVersion, CancellationToken ct = default)
        {
            GameRow row = games.Rows[scanned.ExePath];
            if (!string.Equals(row.HookBlockedReason, block, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(ConsentWriteOutcome.NotFound);
            }

            bool eligible = verdict is { } v && UserModeExceptionRules.IsEligible(StoredBlock.Parse(block), v, sessions);
            string? text = verdict is { } said ? $"{said.Reason}|{said.Family}|{said.Signal}" : null;
            Eligibility.Add((eligible, text, sessions));
            games.Rows[scanned.ExePath] = row with
            {
                AcException = row.AcException with
                {
                    Eligible = eligible,
                    Verdict = text,
                    Sessions = sessions,
                    CheckedRulesVersion = rulesVersion,
                    CheckedExeSizeBytes = scanned.SizeBytes,
                    CheckedExeMtimeMs = scanned.MtimeUnixMs,
                    CheckedBlock = block,
                },
            };
            return ValueTask.FromResult(ConsentWriteOutcome.Written);
        }

        public ValueTask<ConsentWriteOutcome> GrantAntiCheatExceptionAsync(AntiCheatExceptionGrantRequest grant, CancellationToken ct = default) =>
            throw new NotSupportedException("the sweep never grants");

        public ValueTask<ConsentWriteOutcome> RevokeAntiCheatExceptionAsync(string normalisedExePath, string reason, CancellationToken ct = default)
        {
            GameRow row = games.Rows[normalisedExePath];
            if (!row.AcException.IsGranted)
            {
                return ValueTask.FromResult(ConsentWriteOutcome.NotFound);
            }

            Revoked.Add(reason);
            games.Rows[normalisedExePath] = row with
            {
                HookEnabled = row.HookBlockedReason is null && row.HookEnabled,
                AcException = row.AcException with { GrantedAt = null, LapsedReason = reason, LapsedAt = DateTimeOffset.UnixEpoch },
            };
            return ValueTask.FromResult(ConsentWriteOutcome.Written);
        }
    }

    private sealed class Rig : IDisposable
    {
        public Rig(FakeGameRepository games, bool exceptions = false)
        {
            Games = games;
            Consent = new LibraryConsent(games);
            // D33: the exception's evidence and option are composed only where a test is about them.
            Sweep = exceptions
                ? new AntiCheatPreScanSweep(games, Consent, Guard, Rules, Identity, static _ => { }, Pipe, Recording.Contains, Sessions, Option)
                : new AntiCheatPreScanSweep(games, Consent, Guard, Rules, Identity, static _ => { }, Pipe, Recording.Contains);
        }

        public FakeSessionRepository Sessions { get; } = new();

        public FakeExceptionSwitch Option { get; } = new();

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

    private const string _yidun = "AntiCheatFile|NetEase Yidun|NEP2.dll";

    private static readonly AntiCheatVerdict _letThrough = AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll");

    /// <summary>D33: a blocked entry, with the exception's evidence and option composed; optionally granted on the bytes on disk.</summary>
    private static async Task<Rig> BlockedAsync(string block = _yidun, int sessions = 3, bool granted = false, bool optionOn = true)
    {
        var games = new FakeGameRepository();
        GameRow row = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _exe, SizeBytes = 5, MtimeUnixMs = 5 }, "Title", Ct).ConfigureAwait(false);
        games.Rows[_exe] = row with
        {
            HookBlockedReason = block,
            HookPrescanState = "blocked",
            HookEnabled = granted,
            AcException = granted
                ? new AntiCheatExceptionState { GrantedAt = DateTimeOffset.UnixEpoch, Family = "NetEase Yidun", ExeSizeBytes = 5, ExeMtimeMs = 5 }
                : AntiCheatExceptionState.None,
        };
        var rig = new Rig(games, exceptions: true);
        rig.Sessions.SuccessfulHooked[row.Id] = sessions;
        rig.Option.On = optionOn;
        rig.Guard.TolerantAnswer = _letThrough;
        return rig;
    }

    /// <summary>
    /// D33 (owner decision 2026-09-26): with the option on, a blocked entry is asked one question — a pre-scan NAMING its
    /// block's family — and eligible when the guard let exactly that family through and the evidence reaches two sessions.
    /// It is never scanned for a new block, and nothing is granted.
    /// </summary>
    [Fact]
    public async Task WithTheOptionOnABlockedEntryIsAskedAboutItsExceptionAndNothingElse()
    {
        using Rig r = await BlockedAsync();

        AntiCheatPreScanReport report = await r.Sweep.SweepOnceAsync(Ct);

        report.Blocked.Should().Be(1);
        report.Scanned.Should().Be(0, "a block is never scanned for a new one");
        report.ExceptionsChecked.Should().Be(1);
        r.Guard.Tolerated.Should().Equal("NetEase Yidun");
        r.Consent.Writes.Should().BeEmpty("the pre-scan's own columns are not written over a block");
        r.Consent.Eligibility.Should().ContainSingle().Which.Should().Be((true, "AllowedUnderUserModeException|NetEase Yidun|NEP2.dll", 3));
        r.Games.Rows[_exe].AcException.IsGranted.Should().BeFalse("eligibility grants nothing; the user does, through the disclosure");
        r.Games.Rows[_exe].HookBlockedReason.Should().Be(_yidun, "nothing clears a block");
    }

    [Fact]
    public async Task WithTheOptionOffNothingIsAsked()
    {
        using Rig r = await BlockedAsync(optionOn: false);

        (await r.Sweep.SweepOnceAsync(Ct)).ExceptionsChecked.Should().Be(0);
        r.Guard.Scanned.Should().BeEmpty("off by default means no exception question is asked of any game");
        r.Consent.Eligibility.Should().BeEmpty();
    }

    /// <summary>The answer is keyed like the pre-scan: rules, executable and the block itself; the count is re-read every pass.</summary>
    [Fact]
    public async Task TheExceptionQuestionIsKeyedAndTheCountIsLive()
    {
        using Rig r = await BlockedAsync(sessions: 1);
        _ = await r.Sweep.SweepOnceAsync(Ct);
        r.Consent.Eligibility.Should().ContainSingle().Which.Eligible.Should().BeFalse("one session is not the owner's two");

        _ = await r.Sweep.SweepOnceAsync(Ct);
        r.Guard.Scanned.Should().ContainSingle("nothing in the key changed");
        r.Consent.Eligibility.Should().ContainSingle("and neither did the answer");

        r.Sessions.SuccessfulHooked[r.Games.Rows[_exe].Id] = 2;
        _ = await r.Sweep.SweepOnceAsync(Ct);
        r.Guard.Scanned.Should().ContainSingle("a new session is counted, not scanned for");
        r.Consent.Eligibility[^1].Should().Be((true, "AllowedUnderUserModeException|NetEase Yidun|NEP2.dll", 2));

        r.Rules.Version = "2026.09.6";
        _ = await r.Sweep.SweepOnceAsync(Ct);
        r.Guard.Scanned.Should().HaveCount(2, "new rules may have turned the family kernel-level");
    }

    [Fact]
    public async Task ATitleListBlockIsNeverAskedAndNeverEligible()
    {
        using Rig r = await BlockedAsync(block: "BlockedStoreId|Valve VAC|steam:730");

        _ = await r.Sweep.SweepOnceAsync(Ct);

        r.Guard.Scanned.Should().BeEmpty("the notorious titles are on the lists by design");
        r.Consent.Eligibility.Should().ContainSingle().Which.Should().Be((false, (string?)null, 3));
    }

    [Fact]
    public async Task AKernelDriverInTheTreeIsNotEligible()
    {
        using Rig r = await BlockedAsync();
        r.Guard.TolerantAnswer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatFile, "Kernel driver in the game folder", "NEPKernel.sys");

        _ = await r.Sweep.SweepOnceAsync(Ct);

        r.Consent.Eligibility.Should().ContainSingle().Which.Eligible.Should().BeFalse("the Aniimo shape: D30 is never excepted");
    }

    /// <summary>A grant made on bytes no longer on disk ends — with the option off too: a game update is not the file the user accepted the risk for.</summary>
    [Fact]
    public async Task AGrantOnOtherBytesEndsWhateverTheOption()
    {
        using Rig r = await BlockedAsync(granted: true, optionOn: false);
        r.Identity.OnDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 6, MtimeUnixMs = 6 };

        AntiCheatPreScanReport report = await r.Sweep.SweepOnceAsync(Ct);

        report.ExceptionsEnded.Should().Be(1);
        r.Consent.Revoked.Should().Equal(UserModeExceptionLapse.ExecutableChanged);
        r.Games.Rows[_exe].HookEnabled.Should().BeFalse("the block stands, so hooking goes off with the exception");
        r.Guard.Scanned.Should().BeEmpty("with the option off nothing is asked");
    }

    [Fact]
    public async Task AGrantTheFactsNoLongerSupportEnds()
    {
        using Rig r = await BlockedAsync(granted: true);
        r.Guard.TolerantAnswer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll");

        (await r.Sweep.SweepOnceAsync(Ct)).ExceptionsEnded.Should().Be(1);

        r.Consent.Revoked.Should().Equal(UserModeExceptionLapse.NoLongerEligible);
        r.Games.Rows[_exe].AcException.LapsedReason.Should().Be(UserModeExceptionLapse.NoLongerEligible);
    }

    /// <summary>A scan that could not look ends nothing: the session start runs the full check anyway, and a folder busy for one pass is not a finding.</summary>
    [Fact]
    public async Task AScanThatCouldNotAnswerEndsNoGrant()
    {
        using Rig r = await BlockedAsync(granted: true);
        r.Guard.TolerantAnswer = AntiCheatVerdict.Refused(AntiCheatRefusalReason.PreScanFailed, string.Empty, "the game directory could not be listed");

        (await r.Sweep.SweepOnceAsync(Ct)).ExceptionsEnded.Should().Be(0);

        r.Consent.Revoked.Should().BeEmpty();
        r.Consent.Eligibility.Should().ContainSingle().Which.Eligible.Should().BeFalse();
        r.Games.Rows[_exe].AcException.IsGranted.Should().BeTrue();
    }

    [Fact]
    public async Task NoExceptionQuestionWhileItsSessionRuns()
    {
        using Rig r = await BlockedAsync(granted: true);
        r.Recording.Add(r.Games.Rows[_exe].Id);
        r.Identity.OnDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 6, MtimeUnixMs = 6 };

        _ = await r.Sweep.SweepOnceAsync(Ct);

        r.Guard.Scanned.Should().BeEmpty();
        r.Consent.Revoked.Should().BeEmpty("the session's own checks are running; the next pass after it ends looks");
    }
}
