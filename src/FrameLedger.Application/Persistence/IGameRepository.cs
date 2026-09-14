using FrameLedger.Application.TriState;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Persistence;

/// <summary>
/// The <c>games</c> table minus its consent columns (those are <c>IGameConsentStore</c>'s). The Agent uses the
/// first half (ensure, crash count, injection stamp, auto-disable); the UI uses the second (FR-1.3 metadata,
/// FR-1.4 removal, FR-8.3 defaults) — <c>06_DATA_MODEL</c> §Writer ownership, one table with two writers of
/// DISJOINT columns.
/// </summary>
public interface IGameRepository
{
    /// <summary>The row for this executable, created hooking-off when absent; a row removed with its sessions kept (FR-1.4) is restored to the library.</summary>
    ValueTask<GameRow> EnsureAsync(ExecutableFingerprint fingerprint, string name, CancellationToken ct = default);

    ValueTask<GameRow?> FindAsync(string normalisedExePath, CancellationToken ct = default);

    ValueTask<GameRow?> FindByIdAsync(long gameId, CancellationToken ct = default);

    /// <summary>The library: every row not removed, by name.</summary>
    ValueTask<IReadOnlyList<GameRow>> ListAsync(CancellationToken ct = default);

    ValueTask<bool> AutoDisableHookAsync(long gameId, string reason, DateTimeOffset at, CancellationToken ct = default);

    ValueTask<int> RecordCrashAsync(long gameId, CancellationToken ct = default);

    ValueTask<bool> RecordInjectionAsync(long gameId, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>FR-1.3: replaces the user-editable fields; each field that changed is marked <c>user</c> in <c>field_provenance</c>. False when the game does not exist.</summary>
    ValueTask<bool> UpdateMetadataAsync(long gameId, GameMetadata metadata, CancellationToken ct = default);

    /// <summary>FR-8.3: the game default one feature's sessions inherit when they measured nothing.</summary>
    ValueTask<bool> SetTriStateDefaultAsync(long gameId, TriStateKind kind, Tri value, CancellationToken ct = default);

    /// <summary>
    /// FR-1.4. <paramref name="keepSessions"/> true marks the row removed (its sessions stay readable and a later
    /// launch restores it); false deletes the row and, by cascade, every session, blob and annotation under it.
    /// Touches no hook-state column: revoking consent is the pipe's <c>SetHookEnabled false</c>, sent first.
    /// </summary>
    ValueTask<bool> RemoveAsync(long gameId, bool keepSessions, CancellationToken ct = default);

    /// <summary>
    /// The Agent's detection sweep (P4 PR-1): persist one static detection run under the provenance rule of
    /// <c>05_DETECTION</c> §Caching — a <c>user</c> field is never overwritten, a <c>detected</c> one is refreshed, an
    /// empty field with no provenance is filled and badged <c>detected</c>; <c>capability_flags</c> and the cache key
    /// are always written. False when the game does not exist.
    /// </summary>
    ValueTask<bool> ApplyDetectionAsync(long gameId, DetectionWrite detection, CancellationToken ct = default);
}
