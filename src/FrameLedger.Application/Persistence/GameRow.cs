using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Persistence;

/// <summary>
/// A <c>games</c> row as read. The hook-state columns are the Agent's (written through
/// <c>IGameConsentStore</c> and the crash policy); the metadata and the tri-state defaults are the user's
/// (<see cref="IGameRepository.UpdateMetadataAsync"/>, <see cref="IGameRepository.SetTriStateDefaultAsync"/>).
/// </summary>
public sealed record GameRow
{
    public required long Id { get; init; }

    public required string Name { get; init; }

    public required ExecutableFingerprint Fingerprint { get; init; }

    public required bool HookEnabled { get; init; }

    public string? HookBlockedReason { get; init; }

    public string? HookAutoDisabledReason { get; init; }

    public required int HookCrashCount { get; init; }

    public DateTimeOffset? HookLastInjectedAt { get; init; }

    public required DateTimeOffset AddedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    // FR-1.3 metadata (P3 PR-3): the user's fields, plus what detection (P4) fills in and badges.
    public string Platform { get; init; } = "none";

    public string? StoreId { get; init; }

    public string? Engine { get; init; }

    public string? EngineVersion { get; init; }

    public string? Publisher { get; init; }

    public string? GameVersion { get; init; }

    public string? CoverPath { get; init; }

    public string? Notes { get; init; }

    /// <summary><c>field_provenance</c> as stored (JSON object field → <c>detected|user</c>); null or absent reads as <c>user</c> for every field.</summary>
    public string? FieldProvenanceJson { get; init; }

    /// <summary><c>capability_flags</c> as stored — what the game SHIPS, never a measurement (FR-1.5). Since P4 PR-1 a JSON array of the rule ids of <c>rules/detection-rules.json</c>.</summary>
    public string? CapabilityFlagsJson { get; init; }

    /// <summary>The <c>rulesVersion</c> the last static detection ran under (<c>DetectionCacheKey</c>'s rules half); null = never scanned.</summary>
    public string? DetectionRulesVersion { get; init; }

    /// <summary>The exe size the last static detection saw (schema 0004) — the key's exe half, NOT the consent fingerprint.</summary>
    public long? DetectionExeSizeBytes { get; init; }

    /// <summary>The exe mtime (unix ms) the last static detection saw (schema 0004).</summary>
    public long? DetectionExeMtimeMs { get; init; }

    // FR-8.3 game defaults, inherited by sessions that measured nothing.
    public Tri RtDefault { get; init; } = Tri.NotApplicable;

    public Tri PtDefault { get; init; } = Tri.NotApplicable;

    public Tri RrDefault { get; init; } = Tri.NotApplicable;

    // the rest of the hook state, read-only here
    public DateTimeOffset? HookConsentAt { get; init; }

    /// <summary><c>not_run|clean|blocked|unverified</c> (<c>05_DETECTION</c>'s tri-state pre-scan plus "not run").</summary>
    public string HookPrescanState { get; init; } = "not_run";

    /// <summary>FR-1.4: set when the user removed the game but kept its sessions; such a row is not in the library.</summary>
    public DateTimeOffset? RemovedAt { get; init; }

    /// <summary>
    /// Whether FrameLedger watches for this program at all (schema 0009, 2026-09-23): off, it records no session, measures
    /// nothing and injects nothing when the program runs — a utility that starts with Windows, say. The user's switch, on
    /// by default; entries an old import made for Steam's own tools start with it off.
    /// </summary>
    public bool RecordSessions { get; init; } = true;

    public bool InLibrary => RemovedAt is null;
}
