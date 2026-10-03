// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Detection;

/// <summary>
/// Runs the static-hint rules over one game and reports what they established.
/// </summary>
/// <remarks>
/// <para>
/// Constructed by its caller, like <c>HookedCaptureGate</c>. There is no DI
/// container in this repository yet and this is the wrong consumer to introduce
/// one from; composition lands with the Generic Host.
/// </para>
/// <para>
/// It writes nothing. The result is a value the caller decides what to do with,
/// and <see cref="StaticDetectionResult.ShouldWrite"/> is the rule it must apply
/// before persisting any of it.
/// </para>
/// </remarks>
public sealed class StaticGameDetector(IDetectionRulesSource rulesSource, IGameFileProbe probe)
{
    private readonly IDetectionRulesSource _rulesSource =
        rulesSource ?? throw new ArgumentNullException(nameof(rulesSource));

    private readonly IGameFileProbe _probe = probe ?? throw new ArgumentNullException(nameof(probe));

    /// <summary>Detects engine, platform and shipped capabilities for one executable.</summary>
    public async ValueTask<StaticDetectionResult> DetectAsync(string exePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        DetectionRuleSet rules = await _rulesSource.LoadAsync(ct).ConfigureAwait(false);
        GameFileSnapshot snapshot = await _probe.SnapshotAsync(exePath, rules, ct).ConfigureAwait(false);

        var evaluator = new RuleEvaluator(rules);
        EngineMatch engine = evaluator.MatchEngine(snapshot);
        PlatformRule? platform = evaluator.MatchPlatform(snapshot, out bool platformUndetermined);
        EngineVersionReading version = await VersionAsync(engine, snapshot, ct).ConfigureAwait(false);

        return new StaticDetectionResult
        {
            EngineId = engine.Rule?.Id,
            EngineVersion = version.Version,
            EngineVersionSource = version.Source,
            EngineUndetermined = engine.IsUndetermined,
            PlatformId = platform?.Id,
            PlatformUndetermined = platformUndetermined,
            CapabilityIds = [.. evaluator.MatchCapabilities(snapshot).Select(c => c.Id)],
            RulesVersion = rules.RulesVersion,
            UsesVulkan = snapshot.VulkanLoaderReferenced,
            ExeArchitecture = snapshot.ExeArchitecture,
            ExeFileVersion = snapshot.PeFileVersion,
            ExeProductVersion = snapshot.PeProductVersion,
            Libraries = snapshot.Libraries,
        };
    }

    /// <summary>
    /// The matched engine's version and its witness. Every extractor but one answers from the snapshot; Unreal's is the
    /// second read (beta.10), made here for the one engine that matched rather than by the probe for every game.
    /// </summary>
    private async ValueTask<EngineVersionReading> VersionAsync(EngineMatch engine, GameFileSnapshot snapshot, CancellationToken ct)
    {
        if (engine.Rule?.Version is not { } extractor)
        {
            return EngineVersionReading.None;
        }

        if (extractor.Type != VersionExtractorType.UnrealBuild)
        {
            return engine.Version is null ? EngineVersionReading.None : new EngineVersionReading(engine.Version, EngineVersionSource.Of(extractor.Type));
        }

        UnrealBuildFacts? facts = await _probe.ReadUnrealBuildAsync(snapshot, ct).ConfigureAwait(false);
        return facts is null ? EngineVersionReading.None : UnrealVersion.Decide(facts);
    }
}
