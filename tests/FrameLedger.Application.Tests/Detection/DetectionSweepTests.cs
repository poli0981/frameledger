using FluentAssertions;
using FrameLedger.Application.Detection;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Tests.Detection;

/// <summary>
/// The sweep (P4 PR-1): a game is scanned when its cache key is stale — never scanned, the exe changed, the rules
/// moved on — and left alone when it is current (<c>05_DETECTION</c> §Caching). An unreadable exe is skipped, an
/// unusable rules file scans nothing, and <c>RequestNow</c> wakes the loop before its interval.
/// </summary>
public sealed class DetectionSweepTests
{
    private const string _exe = @"C:\Games\Title\game.exe";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ScriptedRules(string version) : IDetectionRulesSource
    {
        public string Version { get; set; } = version;

        public bool Unusable { get; set; }

        public ValueTask<DetectionRuleSet> LoadAsync(CancellationToken ct = default) => Unusable
            ? throw new InvalidOperationException("rules unusable")
            : ValueTask.FromResult(new DetectionRuleSet
            {
                SchemaVersion = 2,
                RulesVersion = Version,
                Engines = [new EngineRule { Id = "unity", Name = "Unity", Signals = new SignalGroup { Combinator = SignalCombinator.All, Signals = [new DetectionSignal { Type = DetectionSignalType.SiblingGlob, Value = "UnityPlayer.dll" }] } }],
                Platforms = [new PlatformRule { Id = "steam", Name = "Steam", Signals = new SignalGroup { Combinator = SignalCombinator.Any, Signals = [new DetectionSignal { Type = DetectionSignalType.SiblingGlob, Value = "steam_api64.dll" }] } }],
                Capabilities = [new CapabilityRule { Id = "dlss", Name = "DLSS", Signals = new SignalGroup { Combinator = SignalCombinator.Any, Signals = [new DetectionSignal { Type = DetectionSignalType.SiblingGlob, Value = "nvngx_dlss.dll" }] } }],
            });
    }

    private sealed class ScriptedProbe : IGameFileProbe
    {
        public int Calls { get; private set; }

        public ValueTask<GameFileSnapshot> SnapshotAsync(string exePath, DetectionRuleSet rules, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(new GameFileSnapshot
            {
                ExePath = exePath,
                ExeNameWithoutExtension = "game",
                GameDirectory = "C:/Games/Title",
                RelativeFiles = ["game.exe", "UnityPlayer.dll", "steam_api64.dll", "nvngx_dlss.dll"],
                RelativeDirectories = [],
                FileListingComplete = true,
                SiblingFileVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                MatchedStringNeedles = new HashSet<string>(StringComparer.Ordinal),
                StringsRegexCaptures = new Dictionary<string, string>(StringComparer.Ordinal),
                ManifestFields = new Dictionary<string, string>(StringComparer.Ordinal),
                UncollectedFacts = new HashSet<DetectionSignalType>(),
            });
        }
    }

    private sealed class FakeIdentity : IExecutableIdentitySource
    {
        public ExecutableFingerprint? OnDisk { get; set; } = new() { ExePath = _exe, SizeBytes = 5, MtimeUnixMs = 5 };

        public ExecutableFingerprint? Read(string normalisedExePath) => OnDisk;

        public string Normalise(string exePath) => exePath;
    }

    private static async Task<(DetectionSweep Sweep, FakeGameRepository Games, ScriptedRules Rules, ScriptedProbe Probe, FakeIdentity Identity, List<string> Log)> BuildAsync()
    {
        var games = new FakeGameRepository();
        await games.EnsureAsync(new ExecutableFingerprint { ExePath = _exe, SizeBytes = 1, MtimeUnixMs = 1 }, "Title", Ct).ConfigureAwait(false);
        var rules = new ScriptedRules("2026.09.1");
        var probe = new ScriptedProbe();
        var identity = new FakeIdentity();
        List<string> log = [];
        return (new DetectionSweep(games, rules, probe, identity, log.Add), games, rules, probe, identity, log);
    }

    [Fact]
    public async Task AGameNeverScannedIsScannedOnceAndThenLeftAloneWhileItsKeyHolds()
    {
        (DetectionSweep sweep, FakeGameRepository games, _, ScriptedProbe probe, _, List<string> log) = await BuildAsync();

        DetectionSweepReport first = await sweep.SweepOnceAsync(Ct);
        first.Scanned.Should().Be(1);
        first.Current.Should().Be(0);
        probe.Calls.Should().Be(1);
        (long GameId, DetectionWrite Write) w = games.Detections.Single();
        w.Write.EngineId.Should().Be("unity");
        w.Write.PlatformId.Should().Be("steam");
        w.Write.CapabilityIds.Should().Equal("dlss");
        w.Write.RulesVersion.Should().Be("2026.09.1");
        w.Write.ExeSizeBytes.Should().Be(5, "the key's exe half is what is on disk NOW, not the consent fingerprint the row was added with");
        w.Write.ExeMtimeMs.Should().Be(5);
        log.Should().ContainSingle(static l => l.Contains("engine=unity", StringComparison.Ordinal) && l.Contains("capabilities=[dlss]", StringComparison.Ordinal));

        DetectionSweepReport second = await sweep.SweepOnceAsync(Ct);
        second.Scanned.Should().Be(0);
        second.Current.Should().Be(1, "the stored key matches the file and the rules");
        probe.Calls.Should().Be(1, "a current game is not walked again");
    }

    [Fact]
    public async Task AChangedExeOrANewRulesVersionRescans()
    {
        (DetectionSweep sweep, FakeGameRepository games, ScriptedRules rules, ScriptedProbe probe, FakeIdentity identity, _) = await BuildAsync();
        await sweep.SweepOnceAsync(Ct);

        identity.OnDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 6, MtimeUnixMs = 9 };
        (await sweep.SweepOnceAsync(Ct)).Scanned.Should().Be(1, "the exe on disk changed (a patch)");
        games.Detections[^1].Write.ExeSizeBytes.Should().Be(6);

        rules.Version = "2026.10.1";
        (await sweep.SweepOnceAsync(Ct)).Scanned.Should().Be(1, "the rules moved on — the trigger 05_DETECTION §Caching names");
        games.Detections[^1].Write.RulesVersion.Should().Be("2026.10.1");
        probe.Calls.Should().Be(3);

        (await sweep.SweepOnceAsync(Ct)).Scanned.Should().Be(0);
    }

    [Fact]
    public async Task AnUnreadableExeIsSkippedAndNamedAndAnUnusableRulesFileScansNothing()
    {
        (DetectionSweep sweep, FakeGameRepository games, ScriptedRules rules, ScriptedProbe probe, FakeIdentity identity, List<string> log) = await BuildAsync();

        identity.OnDisk = null;
        DetectionSweepReport r = await sweep.SweepOnceAsync(Ct);
        r.Unreadable.Should().Be(1);
        r.Scanned.Should().Be(0);
        probe.Calls.Should().Be(0, "nothing is walked for a file that cannot be read");
        log.Should().ContainSingle(static l => l.Contains("unreadable", StringComparison.Ordinal));

        identity.OnDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 5, MtimeUnixMs = 5 };
        rules.Unusable = true;
        DetectionSweepReport bad = await sweep.SweepOnceAsync(Ct);
        bad.RulesUnusable.Should().BeTrue();
        bad.Scanned.Should().Be(0);
        games.Detections.Should().BeEmpty("an unusable rules file writes nothing rather than a wrong answer");
    }

    [Fact]
    public async Task RequestNowWakesTheWaitBeforeTheIntervalAndIsIdempotent()
    {
        (DetectionSweep sweep, _, _, _, _, _) = await BuildAsync();

        sweep.RequestNow();
        sweep.RequestNow();
        (await sweep.WaitAsync(TimeSpan.FromSeconds(30), Ct)).Should().BeTrue("woken at once — the request was queued before the wait");
        (await sweep.WaitAsync(TimeSpan.FromMilliseconds(50), Ct)).Should().BeFalse("two requests while nobody waited are one wake; the second wait times out");

        Task<bool> waiting = sweep.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        sweep.RequestNow();
        (await waiting).Should().BeTrue("a waiter is woken early");
    }

    [Fact]
    public void StalenessIsTheCacheKeyFieldByField()
    {
        var onDisk = new ExecutableFingerprint { ExePath = _exe, SizeBytes = 5, MtimeUnixMs = 7 };
        GameRow current = new() { Id = 1, Name = "T", Fingerprint = onDisk, HookEnabled = false, HookCrashCount = 0, AddedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch, DetectionRulesVersion = "v", DetectionExeSizeBytes = 5, DetectionExeMtimeMs = 7 };
        DetectionSweep.IsStale(current, onDisk, "v").Should().BeFalse();
        DetectionSweep.IsStale(current with { DetectionRulesVersion = null }, onDisk, "v").Should().BeTrue("never scanned");
        DetectionSweep.IsStale(current, onDisk, "w").Should().BeTrue("rules moved");
        DetectionSweep.IsStale(current with { DetectionExeSizeBytes = 4 }, onDisk, "v").Should().BeTrue("size changed");
        DetectionSweep.IsStale(current with { DetectionExeMtimeMs = 8 }, onDisk, "v").Should().BeTrue("mtime changed");
    }
}
