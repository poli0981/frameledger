// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Detection;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Tests;

/// <summary>
/// The use case that composes the rules source, the probe and the evaluator.
/// </summary>
public sealed class StaticGameDetectorTests
{
    private sealed class ScriptedRules(DetectionRuleSet rules) : IDetectionRulesSource
    {
        public int Calls { get; private set; }

        public ValueTask<DetectionRuleSet> LoadAsync(CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(rules);
        }
    }

    private sealed class ScriptedProbe(GameFileSnapshot snapshot, UnrealBuildFacts? unreal = null) : IGameFileProbe
    {
        public DetectionRuleSet? SawRules { get; private set; }

        public int UnrealReads { get; private set; }

        public ValueTask<GameFileSnapshot> SnapshotAsync(string exePath, DetectionRuleSet rules,
            CancellationToken ct = default)
        {
            SawRules = rules;
            return ValueTask.FromResult(snapshot);
        }

        public ValueTask<UnrealBuildFacts?> ReadUnrealBuildAsync(GameFileSnapshot s, CancellationToken ct = default)
        {
            UnrealReads++;
            return ValueTask.FromResult(unreal);
        }

        public UnityBuildFacts? Unity { get; init; }

        public GodotBuildFacts? Godot { get; init; }

        public RpgMakerBuildFacts? RpgMaker { get; init; }

        public RenPyBuildFacts? RenPy { get; init; }

        /// <summary>Which of the beta.19 second reads was made, in order.</summary>
        public List<string> BuildReads { get; } = [];

        public ValueTask<UnityBuildFacts?> ReadUnityBuildAsync(GameFileSnapshot s, CancellationToken ct = default)
        {
            BuildReads.Add("unity");
            return ValueTask.FromResult(Unity);
        }

        public ValueTask<GodotBuildFacts?> ReadGodotBuildAsync(GameFileSnapshot s, CancellationToken ct = default)
        {
            BuildReads.Add("godot");
            return ValueTask.FromResult(Godot);
        }

        public ValueTask<RpgMakerBuildFacts?> ReadRpgMakerBuildAsync(GameFileSnapshot s, CancellationToken ct = default)
        {
            BuildReads.Add("rpgmaker");
            return ValueTask.FromResult(RpgMaker);
        }

        public ValueTask<RenPyBuildFacts?> ReadRenPyBuildAsync(GameFileSnapshot s, CancellationToken ct = default)
        {
            BuildReads.Add("renpy");
            return ValueTask.FromResult(RenPy);
        }
    }

    private static SignalGroup Any(params string[] globs) => new()
    {
        Combinator = SignalCombinator.Any,
        Signals = [.. globs.Select(g => new DetectionSignal
        {
            Type = DetectionSignalType.SiblingGlob,
            Value = g,
        })],
    };

    private static DetectionRuleSet Rules() => new()
    {
        SchemaVersion = 2,
        RulesVersion = "2026.08.1",
        Engines =
        [
            new EngineRule { Id = "unity", Name = "Unity", Signals = Any("UnityPlayer.dll"), Version = null },
            new EngineRule { Id = "godot", Name = "Godot", Signals = Any("*.pck"), Version = null },
        ],
        Platforms = [new PlatformRule { Id = "steam", Name = "Steam", Signals = Any("steam_api64.dll") }],
        Capabilities =
        [
            new CapabilityRule { Id = "dlss", Name = "DLSS", Signals = Any("nvngx_dlss.dll") },
            new CapabilityRule { Id = "fsr", Name = "FSR", Signals = Any("ffx_fsr2_*.dll") },
        ],
    };

    /// <summary>The Vulkan fact passes through untouched (P4 PR-2): true, false, or null — the detector claims nothing the probe did not see.</summary>
    [Fact]
    public async Task TheVulkanFactIsTheProbesThreeValuedAnswer()
    {
        foreach (bool? seen in new bool?[] { true, false, null })
        {
            var detector = new StaticGameDetector(new ScriptedRules(Rules()), new ScriptedProbe(Snapshot("UnityPlayer.dll") with { VulkanLoaderReferenced = seen }));
            StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);
            r.UsesVulkan.Should().Be(seen);
        }
    }

    private static GameFileSnapshot Snapshot(params string[] files) => new()
    {
        ExePath = "C:/Games/Example/Game.exe",
        ExeNameWithoutExtension = "Game",
        GameDirectory = "C:/Games/Example",
        RelativeFiles = files,
        RelativeDirectories = [],
        FileListingComplete = true,
        SiblingFileVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        MatchedStringNeedles = new HashSet<string>(StringComparer.Ordinal),
        StringsRegexCaptures = new Dictionary<string, string>(StringComparer.Ordinal),
        ManifestFields = new Dictionary<string, string>(StringComparer.Ordinal),
        UncollectedFacts = new HashSet<DetectionSignalType> { DetectionSignalType.ManifestField },
    };

    [Fact]
    public async Task ItReportsEnginePlatformAndEveryCapability()
    {
        var detector = new StaticGameDetector(
            new ScriptedRules(Rules()),
            new ScriptedProbe(Snapshot("UnityPlayer.dll", "steam_api64.dll", "nvngx_dlss.dll", "ffx_fsr2_x64.dll")));

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineId.Should().Be("unity");
        r.PlatformId.Should().Be("steam");
        r.CapabilityIds.Should().BeEquivalentTo(["dlss", "fsr"]);
        r.RulesVersion.Should().Be("2026.08.1");
        r.EngineUndetermined.Should().BeFalse();
    }

    [Fact]
    public async Task ACleanDirectoryYieldsNothing_WhichIsAnAnswer()
    {
        var detector = new StaticGameDetector(new ScriptedRules(Rules()), new ScriptedProbe(Snapshot("Game.exe")));

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineId.Should().BeNull();
        r.PlatformId.Should().BeNull();
        r.CapabilityIds.Should().BeEmpty();
        r.EngineUndetermined.Should().BeFalse("every rule cleanly missed; that is knowledge, not a gap");
    }

    [Fact]
    public async Task TheProbeIsGivenTheRules_BecauseTheSnapshotDependsOnThem()
    {
        // The strings pass has to know its needles before it reads. If this ever
        // stops being true the evaluator could become genuinely pure — but until
        // then, pretending otherwise is how a needle silently never gets looked
        // for.
        var probe = new ScriptedProbe(Snapshot("Game.exe"));
        var detector = new StaticGameDetector(new ScriptedRules(Rules()), probe);

        await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        probe.SawRules.Should().NotBeNull();
        probe.SawRules!.RulesVersion.Should().Be("2026.08.1");
    }

    [Fact]
    public async Task AnUndeterminedEngineWalkIsReportedAsSuch_NotAsNoEngine()
    {
        DetectionRuleSet rules = Rules() with
        {
            Engines =
            [
                new EngineRule
                {
                    Id = "needs_pe",
                    Name = "Needs PE",
                    Signals = new SignalGroup
                    {
                        Combinator = SignalCombinator.Any,
                        Signals = [new DetectionSignal
                        {
                            Type = DetectionSignalType.PeCompanyContains,
                            Value = "Whoever",
                        }],
                    },
                    Version = null,
                },
                new EngineRule { Id = "unity", Name = "Unity", Signals = Any("UnityPlayer.dll"), Version = null },
            ],
        };
        var detector = new StaticGameDetector(
            new ScriptedRules(rules), new ScriptedProbe(Snapshot("UnityPlayer.dll")));

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineUndetermined.Should().BeTrue();
        r.EngineId.Should().BeNull("a later rule is not the first match when an earlier one could not be decided");
    }

    private static DetectionRuleSet UnrealRules() => Rules() with
    {
        Engines =
        [
            new EngineRule
            {
                Id = "unity",
                Name = "Unity",
                Signals = Any("UnityPlayer.dll"),
                Version = new VersionExtractor { Type = VersionExtractorType.PeFileVersion, From = "UnityPlayer.dll" },
            },
            new EngineRule
            {
                Id = "unreal",
                Name = "Unreal Engine",
                Signals = Any("*-Win64-Shipping.exe"),
                Version = new VersionExtractor { Type = VersionExtractorType.UnrealBuild },
            },
        ],
    };

    /// <summary>
    /// beta.10: an Unreal match asks the probe for the engine's build facts — once — and reports the ladder's answer with
    /// the witness it rests on (Lies of P's shape: 4.27.2.0 and no version strings).
    /// </summary>
    [Fact]
    public async Task AnUnrealMatchReadsTheBuildFactsOnceAndReportsTheirVersionAndWitness()
    {
        var facts = new UnrealBuildFacts { FixedFileVersion = [4, 27, 2, 0] };
        var probe = new ScriptedProbe(Snapshot("LiesofP/Binaries/Win64/LOP-Win64-Shipping.exe"), facts);
        var detector = new StaticGameDetector(new ScriptedRules(UnrealRules()), probe);

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineId.Should().Be("unreal");
        r.EngineVersion.Should().Be("4.27.2");
        r.EngineVersionSource.Should().Be(EngineVersionSource.UnrealFileVersion);
        probe.UnrealReads.Should().Be(1);
    }

    /// <summary>No facts (no shipping executable, or one that could not be read) is no version — never the old digit, never a guess.</summary>
    [Fact]
    public async Task AnUnrealMatchWithNoFactsHasNoVersionAndNoWitness()
    {
        var probe = new ScriptedProbe(Snapshot("Game/Binaries/Win64/Game-Win64-Shipping.exe"));
        var detector = new StaticGameDetector(new ScriptedRules(UnrealRules()), probe);

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineId.Should().Be("unreal");
        r.EngineVersion.Should().BeNull();
        r.EngineVersionSource.Should().BeNull();
    }

    /// <summary>Every other engine answers from the snapshot, never with the second read, and names its extractor as the witness.</summary>
    [Fact]
    public async Task AnyOtherEngineNeverTriggersTheSecondReadAndNamesItsExtractor()
    {
        GameFileSnapshot unity = Snapshot("UnityPlayer.dll") with
        {
            SiblingFileVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["UnityPlayer.dll"] = "2022.3.32.1" },
        };
        var probe = new ScriptedProbe(unity, new UnrealBuildFacts { FixedFileVersion = [5, 4, 4, 0] });
        var detector = new StaticGameDetector(new ScriptedRules(UnrealRules()), probe);

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineId.Should().Be("unity");
        r.EngineVersion.Should().Be("2022.3.32.1");
        r.EngineVersionSource.Should().Be("pe_file_version");
        probe.UnrealReads.Should().Be(0);
    }

    private static DetectionRuleSet BuildRules() => Rules() with
    {
        Engines =
        [
            new EngineRule { Id = "unity", Name = "Unity", Signals = Any("UnityPlayer.dll"), Version = new VersionExtractor { Type = VersionExtractorType.UnityBuild } },
            new EngineRule { Id = "rpgmaker_mv", Name = "RPG Maker MV/MZ", Signals = Any("nw.dll"), Version = new VersionExtractor { Type = VersionExtractorType.RpgMakerBuild } },
            new EngineRule { Id = "godot", Name = "Godot", Signals = Any("*.pck"), Version = new VersionExtractor { Type = VersionExtractorType.GodotBuild } },
            new EngineRule { Id = "renpy", Name = "Ren'Py", Signals = Any("*.rpa"), Version = new VersionExtractor { Type = VersionExtractorType.RenPyBuild } },
        ],
    };

    /// <summary>
    /// beta.19: Unity, Godot and RPG Maker decide their versions in code as Unreal does — one second read, for the engine that
    /// matched only, and the decider's witness reported (the owner's Dialoop, 衔蝶 and A Doll's Locket - Remake as measured);
    /// Ren'Py since beta.20 (Bound Between Desks).
    /// </summary>
    [Theory]
    [InlineData("UnityPlayer.dll", "unity", "6000.3.3f1", EngineVersionSource.UnityPlayer)]
    [InlineData("xiandie.pck", "godot", "4.4.1", EngineVersionSource.GodotPack)]
    [InlineData("css/nw.dll", "rpgmaker", "1.10.0", EngineVersionSource.RpgMakerMzCore)]
    [InlineData("game/archive.rpa", "renpy", "8.5.0", EngineVersionSource.RenPyVcVersion)]
    public async Task EnginesDecidedInCodeReadTheirBuildFactsOnceForTheEngineThatMatched(string marker, string read, string version, string source)
    {
        var probe = new ScriptedProbe(Snapshot(marker))
        {
            Unity = new UnityBuildFacts { PlayerProductVersion = "6000.3.3f1 (ef04196de0d6)", PlayerFileVersion = [6000, 3, 3, 15664153] },
            Godot = new GodotBuildFacts { PackVersion = [4, 4, 1], ExeProductName = "xiandie", ExeProductVersion = "0.1.0.3" },
            RpgMaker = new RpgMakerBuildFacts { CoreScript = "rmmz_core.js", CoreScriptVersion = "1.10.0" },
            RenPy = new RenPyBuildFacts { VcVersion = "8.5.0.25111603" },
        };
        var detector = new StaticGameDetector(new ScriptedRules(BuildRules()), probe);

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineVersion.Should().Be(version);
        r.EngineVersionSource.Should().Be(source);
        probe.BuildReads.Should().Equal([read], "one read, for the matched engine only");
        probe.UnrealReads.Should().Be(0);
    }

    /// <summary>A build read that found nothing is no version and no witness — never the old rules' raw file version.</summary>
    [Fact]
    public async Task ABuildReadWithNoFactsHasNoVersion()
    {
        var detector = new StaticGameDetector(new ScriptedRules(BuildRules()), new ScriptedProbe(Snapshot("UnityPlayer.dll")));

        StaticDetectionResult r = await detector.DetectAsync(@"C:\Games\Example\Game.exe", TestContext.Current.CancellationToken);

        r.EngineId.Should().Be("unity");
        r.EngineVersion.Should().BeNull();
        r.EngineVersionSource.Should().BeNull();
    }

    [Fact]
    public async Task ItRejectsAnEmptyExePathRatherThanProbingNothing()
    {
        var detector = new StaticGameDetector(new ScriptedRules(Rules()), new ScriptedProbe(Snapshot()));

        await ((Func<Task>)(() => detector.DetectAsync("  ", TestContext.Current.CancellationToken).AsTask()))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ItRejectsNullCollaborators()
    {
        ((Action)(() => _ = new StaticGameDetector(null!, new ScriptedProbe(Snapshot()))))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new StaticGameDetector(new ScriptedRules(Rules()), null!)))
            .Should().Throw<ArgumentNullException>();
    }
}
