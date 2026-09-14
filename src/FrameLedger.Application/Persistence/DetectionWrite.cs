namespace FrameLedger.Application.Persistence;

/// <summary>
/// What one static detection run wants to persist on a <c>games</c> row (P4 PR-1): the engine, its version, the
/// platform, the capability ids, and the cache key it ran under (<c>05_DETECTION</c> §Caching). The repository — not
/// the caller — applies the provenance rule: a field the user supplied is never overwritten, a field detection
/// wrote before is refreshed, and an empty field with no provenance is filled and badged <c>detected</c>.
/// </summary>
/// <remarks>
/// A null <see cref="EngineId"/> / <see cref="EngineVersion"/> / <see cref="PlatformId"/> means "not established"
/// (<c>StaticDetectionResult</c>): the existing value is left as it is, whatever its provenance — detection never
/// erases. <see cref="CapabilityIds"/> is always written in full (an empty list clears the row's flags), because the
/// Supports row has no user-edit surface and a stale "Supports DLSS-G" after the DLL was removed is the confusion
/// <c>05_DETECTION</c> §Capability hints exists to prevent.
/// </remarks>
public sealed record DetectionWrite
{
    /// <summary>The engine rule id, or null when no engine rule matched or the walk stopped on <c>Unknown</c>.</summary>
    public string? EngineId { get; init; }

    public string? EngineVersion { get; init; }

    /// <summary>The platform rule id (<c>steam</c>, <c>gog</c>, <c>epic</c>, <c>itch</c>), or null.</summary>
    public string? PlatformId { get; init; }

    /// <summary>The capability rule ids that matched, in rule order; stored verbatim as a JSON array.</summary>
    public required IReadOnlyList<string> CapabilityIds { get; init; }

    /// <summary>The <c>rulesVersion</c> the run evaluated under.</summary>
    public required string RulesVersion { get; init; }

    /// <summary>The exe size the run saw (the cache key's exe half — never the consent fingerprint).</summary>
    public required long ExeSizeBytes { get; init; }

    /// <summary>The exe mtime, unix ms, the run saw.</summary>
    public required long ExeMtimeMs { get; init; }
}
