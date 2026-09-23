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

    /// <summary>
    /// Points a library row at another executable (2026-09-21): the remedy for a store import that guessed the wrong one,
    /// which used to be "remove the game and add it again". Everything downstream is keyed on the executable, so the row
    /// comes out exactly as a new game would: hooking OFF, no consent, the pre-scan not run. It can only downgrade — it
    /// never enables anything and it leaves <c>hook_blocked_reason</c> alone, because nothing clears a block. False when
    /// the row is absent or removed, or when another row (in the library or kept for its sessions) already owns that
    /// path: two rows for one executable is what <c>exe_path UNIQUE</c> exists to refuse.
    /// </summary>
    ValueTask<bool> ChangeExecutableAsync(long gameId, ExecutableFingerprint fingerprint, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>
    /// Points a library row at the SAME executable under another path (2026-09-22; <c>19_SAFETY</c> §A moved drive is the
    /// same executable): the drive changed its letter, the bytes did not. Unlike <see cref="ChangeExecutableAsync"/> it
    /// keeps everything — consent, block, pre-scan state, detection — because the fingerprint's size and mtime are the
    /// executable's identity and the path is only where it lives; the write itself requires them to match
    /// (<c>WHERE exe_size_bytes = @size AND exe_mtime_ms = @mtime</c>), so a different binary cannot inherit a consent
    /// through this call. False when the row is absent or removed, when the bytes differ, or when another row already
    /// owns the new path.
    /// </summary>
    ValueTask<bool> RelocateExecutableAsync(long gameId, ExecutableFingerprint moved, DateTimeOffset at, CancellationToken ct = default);

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
    /// The user's recording switch (schema 0009, 2026-09-23): off, the Agent does not watch for the program — no session,
    /// no measurement, no injection — and a session of it that is running stops at its next tick. The UI's write; false
    /// when no row has that id.
    /// </summary>
    ValueTask<bool> SetRecordingAsync(long gameId, bool record, CancellationToken ct = default);

    /// <summary>
    /// The Agent's detection sweep (P4 PR-1): persist one static detection run under the provenance rule of
    /// <c>05_DETECTION</c> §Caching — a <c>user</c> field is never overwritten, a <c>detected</c> one is refreshed, an
    /// empty field with no provenance is filled and badged <c>detected</c>; <c>capability_flags</c> and the cache key
    /// are always written. False when the game does not exist.
    /// </summary>
    ValueTask<bool> ApplyDetectionAsync(long gameId, DetectionWrite detection, CancellationToken ct = default);

    /// <summary>
    /// The library import (FR-1.2, P4 PR-4): a store's <c>platform</c>, <c>store_id</c> and version for a game, under
    /// the same per-field provenance rule as <see cref="ApplyDetectionAsync"/> — the user's value stays, a badged one
    /// is refreshed, an empty one is filled and badged <c>detected</c>. Never a hook column. False when the game
    /// does not exist.
    /// </summary>
    ValueTask<bool> ApplyStoreMetadataAsync(long gameId, StoreMetadata store, CancellationToken ct = default);
}
