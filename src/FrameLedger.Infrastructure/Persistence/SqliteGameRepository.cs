using System.Data.Common;
using System.Text.Json;
using Dapper;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// The <c>games</c> table minus its consent columns. Nothing here can set <c>hook_enabled</c> to 1, stamp
/// <c>hook_consent_at</c>, or touch <c>hook_blocked_reason</c>: those are <see cref="SqliteGameConsentStore"/>'s,
/// and a second writer of the same columns would be the "two views of one state" the port forbids. The UI's
/// half (P3 PR-3) writes the metadata, the tri-state defaults, <c>removed_at</c> and, since 2026-09-23,
/// <c>record_sessions</c> — and nothing else.
/// </summary>
public sealed class SqliteGameRepository : IGameRepository
{
    private const string _columns =
        "id, name, exe_path, exe_size_bytes, exe_mtime_ms, hook_enabled, hook_blocked_reason, hook_autodisabled_reason, "
        + "hook_crash_count, hook_last_injected_at, added_at, updated_at, "
        + "platform, store_id, engine, engine_version, publisher, game_version, cover_path, notes, field_provenance, capability_flags, "
        + "rt_default, pt_default, rr_default, hook_consent_at, hook_prescan_state, removed_at, "
        + "detection_rules_version, detection_exe_size_bytes, detection_exe_mtime_ms, record_sessions";

    private const string _selectByPath = $"SELECT {_columns} FROM games WHERE exe_path = @path";

    private const string _selectById = $"SELECT {_columns} FROM games WHERE id = @id";

    private const string _selectLibrary = $"SELECT {_columns} FROM games WHERE removed_at IS NULL ORDER BY name, exe_path";

    private const string _insert =
        "INSERT INTO games (name, exe_path, exe_size_bytes, exe_mtime_ms, added_at, updated_at) "
        + "VALUES (@name, @path, @size, @mtime, @now, @now)";

    private const string _restore = "UPDATE games SET removed_at = NULL, updated_at = @now WHERE id = @id";

    private const string _autoDisable =
        "UPDATE games SET hook_enabled = 0, hook_autodisabled_reason = @reason, hook_autodisabled_at = @at, updated_at = @at WHERE id = @id";

    // A downgrade by construction: the consent columns go to their "nothing happened" defaults, never to a grant, and
    // hook_blocked_reason is not named (IGameRepository.ChangeExecutableAsync). NOT EXISTS is the UNIQUE index's answer
    // as a false instead of a constraint exception.
    private const string _changeExecutable =
        "UPDATE games SET exe_path = @path, exe_size_bytes = @size, exe_mtime_ms = @mtime, hook_enabled = 0, hook_consent_at = NULL, "
        + "hook_consent_provenance = 'NotRecorded', hook_consent_disclosure_version = '', hook_prescan_state = 'not_run', "
        + "updated_at = @at "
        + "WHERE id = @id AND removed_at IS NULL AND NOT EXISTS (SELECT 1 FROM games other WHERE other.exe_path = @path AND other.id <> @id)";

    // The same bytes under another path: nothing but exe_path and updated_at move, and the size/mtime predicate is what
    // makes "the same bytes" a condition of the write rather than a promise of the caller (IGameRepository.RelocateExecutableAsync).
    private const string _relocateExecutable =
        "UPDATE games SET exe_path = @path, updated_at = @at "
        + "WHERE id = @id AND removed_at IS NULL AND exe_size_bytes = @size AND exe_mtime_ms = @mtime "
        + "AND NOT EXISTS (SELECT 1 FROM games other WHERE other.exe_path = @path AND other.id <> @id)";

    private const string _crash =
        "UPDATE games SET hook_crash_count = hook_crash_count + 1, updated_at = @now WHERE id = @id RETURNING hook_crash_count";

    private const string _injected = "UPDATE games SET hook_last_injected_at = @at, updated_at = @at WHERE id = @id";

    private const string _updateMetadata =
        "UPDATE games SET name = @Name, platform = @Platform, store_id = @StoreId, engine = @Engine, engine_version = @EngineVersion, "
        + "publisher = @Publisher, game_version = @GameVersion, cover_path = @CoverPath, notes = @Notes, field_provenance = @provenance, "
        + "updated_at = @now WHERE id = @id";

    private const string _remove = "UPDATE games SET removed_at = @now, updated_at = @now WHERE id = @id AND removed_at IS NULL";

    private const string _setRecording = "UPDATE games SET record_sessions = @record, updated_at = @now WHERE id = @id";

    private const string _delete = "DELETE FROM games WHERE id = @id";

    private const string _applyStore =
        "UPDATE games SET platform = @platform, store_id = @storeId, game_version = @gameVersion, field_provenance = @provenance, "
        + "updated_at = @now WHERE id = @id";

    private const string _applyDetection =
        "UPDATE games SET engine = @engine, engine_version = @engineVersion, platform = @platform, capability_flags = @flags, "
        + "field_provenance = @provenance, detection_rules_version = @rules, detection_exe_size_bytes = @size, detection_exe_mtime_ms = @mtime, "
        + "updated_at = @now WHERE id = @id";

    private readonly LedgerDatabase _db;

    public SqliteGameRepository(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public ValueTask<GameRow> EnsureAsync(ExecutableFingerprint fingerprint, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _db.WriteAsync(async (c, tx, token) =>
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            GameRow? existing = await ReadByPathAsync(c, tx, fingerprint.ExePath, token).ConfigureAwait(false);
            if (existing is { RemovedAt: not null })
            {
                // FR-1.4 "keep its sessions" and then the game ran again: back into the library, hook state untouched.
                await c.ExecuteAsync(new CommandDefinition(_restore, new { id = existing.Id, now }, tx, cancellationToken: token)).ConfigureAwait(false);
                return (await ReadByPathAsync(c, tx, fingerprint.ExePath, token).ConfigureAwait(false))!;
            }

            if (existing is not null)
            {
                return existing;
            }

            // Hooking OFF for every newly added game (19_SAFETY, CLAUDE.md rule 1): the row exists so a
            // Tier-2 session has somewhere to land; nothing about it says the user enabled anything.
            var p = new { name, path = fingerprint.ExePath, size = fingerprint.SizeBytes, mtime = fingerprint.MtimeUnixMs, now };
            await c.ExecuteAsync(new CommandDefinition(_insert, p, tx, cancellationToken: token)).ConfigureAwait(false);
            return (await ReadByPathAsync(c, tx, fingerprint.ExePath, token).ConfigureAwait(false))!;
        }, ct);
    }

    public ValueTask<GameRow?> FindAsync(string normalisedExePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);
        return _db.ReadAsync((c, token) => ReadByPathAsync(c, null, normalisedExePath, token), ct);
    }

    public ValueTask<GameRow?> FindByIdAsync(long gameId, CancellationToken ct = default) =>
        _db.ReadAsync((c, token) => SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectById, new { id = gameId }, cancellationToken: token), Read), ct);

    public ValueTask<IReadOnlyList<GameRow>> ListAsync(CancellationToken ct = default) =>
        _db.ReadAsync((c, token) => SqliteReaders.ReadAllAsync(c, new CommandDefinition(_selectLibrary, cancellationToken: token), Read), ct);

    public ValueTask<bool> AutoDisableHookAsync(long gameId, string reason, DateTimeOffset at, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
            _autoDisable, new { id = gameId, reason, at = at.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)).ConfigureAwait(false) == 1, ct);
    }

    public ValueTask<bool> ChangeExecutableAsync(long gameId, ExecutableFingerprint fingerprint, DateTimeOffset at, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint.ExePath, nameof(fingerprint));
        var p = new { id = gameId, path = fingerprint.ExePath, size = fingerprint.SizeBytes, mtime = fingerprint.MtimeUnixMs, at = at.ToUnixTimeMilliseconds() };
        return _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
            _changeExecutable, p, tx, cancellationToken: token)).ConfigureAwait(false) == 1, ct);
    }

    public ValueTask<bool> RelocateExecutableAsync(long gameId, ExecutableFingerprint moved, DateTimeOffset at, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moved.ExePath, nameof(moved));
        var p = new { id = gameId, path = moved.ExePath, size = moved.SizeBytes, mtime = moved.MtimeUnixMs, at = at.ToUnixTimeMilliseconds() };
        return _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
            _relocateExecutable, p, tx, cancellationToken: token)).ConfigureAwait(false) == 1, ct);
    }

    public ValueTask<int> RecordCrashAsync(long gameId, CancellationToken ct = default) =>
        _db.WriteAsync((c, tx, token) => c.ExecuteScalarAsync<int>(new CommandDefinition(
            _crash, new { id = gameId, now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)), ct);

    public ValueTask<bool> RecordInjectionAsync(long gameId, DateTimeOffset at, CancellationToken ct = default) =>
        _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
            _injected, new { id = gameId, at = at.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)).ConfigureAwait(false) == 1, ct);

    public ValueTask<bool> UpdateMetadataAsync(long gameId, GameMetadata metadata, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Name, nameof(metadata));
        if (!GameMetadata.Platforms.Contains(metadata.Platform, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{metadata.Platform}' is not a platform (steam|gog|epic|itch|none)", nameof(metadata));
        }

        return _db.WriteAsync(async (c, tx, token) =>
        {
            GameRow? before = await SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectById, new { id = gameId }, tx, cancellationToken: token), Read).ConfigureAwait(false);
            if (before is null)
            {
                return false;
            }

            var p = new
            {
                id = gameId,
                metadata.Name,
                metadata.Platform,
                metadata.StoreId,
                metadata.Engine,
                metadata.EngineVersion,
                metadata.Publisher,
                metadata.GameVersion,
                metadata.CoverPath,
                metadata.Notes,
                provenance = FieldProvenance.AfterUserEdit(before.FieldProvenanceJson, GameMetadata.Of(before), metadata),
                now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            return await c.ExecuteAsync(new CommandDefinition(_updateMetadata, p, tx, cancellationToken: token)).ConfigureAwait(false) == 1;
        }, ct);
    }

    public ValueTask<bool> SetTriStateDefaultAsync(long gameId, TriStateKind kind, Tri value, CancellationToken ct = default)
    {
        string column = kind switch
        {
            TriStateKind.RayTracing => "rt_default",
            TriStateKind.PathTracing => "pt_default",
            TriStateKind.RayReconstruction => "rr_default",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a tri-state kind"),
        };
        string sql = $"UPDATE games SET {column} = @value, updated_at = @now WHERE id = @id";
        return _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
            sql, new { id = gameId, value = Vocabulary.Tri(value), now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)).ConfigureAwait(false) == 1, ct);
    }

    public ValueTask<bool> SetRecordingAsync(long gameId, bool record, CancellationToken ct = default) =>
        _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
            _setRecording, new { id = gameId, record = record ? 1 : 0, now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)).ConfigureAwait(false) == 1, ct);

    public ValueTask<bool> RemoveAsync(long gameId, bool keepSessions, CancellationToken ct = default) =>
        _db.WriteAsync(async (c, tx, token) =>
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int n = keepSessions
                ? await c.ExecuteAsync(new CommandDefinition(_remove, new { id = gameId, now }, tx, cancellationToken: token)).ConfigureAwait(false)
                : await c.ExecuteAsync(new CommandDefinition(_delete, new { id = gameId }, tx, cancellationToken: token)).ConfigureAwait(false);
            return n == 1;
        }, ct);

    public ValueTask<bool> ApplyDetectionAsync(long gameId, DetectionWrite detection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentException.ThrowIfNullOrWhiteSpace(detection.RulesVersion, nameof(detection));
        return _db.WriteAsync(async (c, tx, token) =>
        {
            GameRow? before = await SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectById, new { id = gameId }, tx, cancellationToken: token), Read).ConfigureAwait(false);
            if (before is null)
            {
                return false;
            }

            // The provenance rule (05_DETECTION §Caching), applied per field: a user's value is never overwritten, a
            // detected one is refreshed, and an empty field with no provenance is filled and badged. A null detected
            // value leaves the field as it is — detection never erases.
            Dictionary<string, string> map = FieldProvenance.Parse(before.FieldProvenanceJson);
            string? engine = FieldProvenance.Resolve(map, "engine", before.Engine, detection.EngineId);
            string? engineVersion = FieldProvenance.Resolve(map, "engine_version", before.EngineVersion, detection.EngineVersion);
            string platform = FieldProvenance.Resolve(map, "platform", string.Equals(before.Platform, "none", StringComparison.Ordinal) ? null : before.Platform, detection.PlatformId) ?? "none";
            var p = new
            {
                id = gameId,
                engine,
                engineVersion,
                platform,
                flags = JsonSerializer.Serialize(detection.CapabilityIds.ToArray(), LedgerJsonContext.Default.StringArray),
                provenance = map.Count == 0 ? before.FieldProvenanceJson : JsonSerializer.Serialize(map, LedgerJsonContext.Default.DictionaryStringString),
                rules = detection.RulesVersion,
                size = detection.ExeSizeBytes,
                mtime = detection.ExeMtimeMs,
                now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            return await c.ExecuteAsync(new CommandDefinition(_applyDetection, p, tx, cancellationToken: token)).ConfigureAwait(false) == 1;
        }, ct);
    }

    public ValueTask<bool> ApplyStoreMetadataAsync(long gameId, StoreMetadata store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!GameMetadata.Platforms.Contains(store.Platform, StringComparer.Ordinal) || string.Equals(store.Platform, "none", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{store.Platform}' is not a store (steam|gog|epic|itch)", nameof(store));
        }

        return _db.WriteAsync(async (c, tx, token) =>
        {
            GameRow? before = await SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectById, new { id = gameId }, tx, cancellationToken: token), Read).ConfigureAwait(false);
            if (before is null)
            {
                return false;
            }

            // The same rule as a detection write (05_DETECTION §Caching): the user's value stays, a badged one is
            // refreshed, an empty one is filled and badged. A store's facts are detected facts, not the user's.
            Dictionary<string, string> map = FieldProvenance.Parse(before.FieldProvenanceJson);
            string platform = FieldProvenance.Resolve(map, "platform", string.Equals(before.Platform, "none", StringComparison.Ordinal) ? null : before.Platform, store.Platform) ?? "none";
            string? storeId = FieldProvenance.Resolve(map, "store_id", before.StoreId, store.StoreId);
            string? gameVersion = FieldProvenance.Resolve(map, "game_version", before.GameVersion, store.GameVersion);
            var p = new
            {
                id = gameId,
                platform,
                storeId,
                gameVersion,
                provenance = map.Count == 0 ? before.FieldProvenanceJson : JsonSerializer.Serialize(map, LedgerJsonContext.Default.DictionaryStringString),
                now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            return await c.ExecuteAsync(new CommandDefinition(_applyStore, p, tx, cancellationToken: token)).ConfigureAwait(false) == 1;
        }, ct);
    }

    private static Task<GameRow?> ReadByPathAsync(SqliteConnection c, SqliteTransaction? tx, string path, CancellationToken ct) =>
        SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectByPath, new { path }, tx, cancellationToken: ct), Read);

    private static GameRow Read(DbDataReader r) => ReadMore(new GameRow
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        Fingerprint = new ExecutableFingerprint
        {
            ExePath = r.GetString(2),
            SizeBytes = SqliteReaders.Int64(r, 3) ?? 0,
            MtimeUnixMs = SqliteReaders.Int64(r, 4) ?? 0,
        },
        HookEnabled = r.GetInt64(5) != 0,
        HookBlockedReason = SqliteReaders.String(r, 6),
        HookAutoDisabledReason = SqliteReaders.String(r, 7),
        HookCrashCount = (int)r.GetInt64(8),
        HookLastInjectedAt = At(SqliteReaders.Int64(r, 9)),
        AddedAt = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(10)),
        UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(11)),
    }, r);

    private static GameRow ReadMore(GameRow row, DbDataReader r) => row with
    {
        Platform = SqliteReaders.String(r, 12) ?? "none",
        StoreId = SqliteReaders.String(r, 13),
        Engine = SqliteReaders.String(r, 14),
        EngineVersion = SqliteReaders.String(r, 15),
        Publisher = SqliteReaders.String(r, 16),
        GameVersion = SqliteReaders.String(r, 17),
        CoverPath = SqliteReaders.String(r, 18),
        Notes = SqliteReaders.String(r, 19),
        FieldProvenanceJson = SqliteReaders.String(r, 20),
        CapabilityFlagsJson = SqliteReaders.String(r, 21),
        RtDefault = Vocabulary.ParseTri(SqliteReaders.String(r, 22)),
        PtDefault = Vocabulary.ParseTri(SqliteReaders.String(r, 23)),
        RrDefault = Vocabulary.ParseTri(SqliteReaders.String(r, 24)),
        HookConsentAt = At(SqliteReaders.Int64(r, 25)),
        HookPrescanState = SqliteReaders.String(r, 26) ?? "not_run",
        RemovedAt = At(SqliteReaders.Int64(r, 27)),
        DetectionRulesVersion = SqliteReaders.String(r, 28),
        DetectionExeSizeBytes = SqliteReaders.Int64(r, 29),
        DetectionExeMtimeMs = SqliteReaders.Int64(r, 30),
        RecordSessions = r.GetInt64(31) != 0,
    };

    private static DateTimeOffset? At(long? unixMs) => unixMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

    /// <summary><c>field_provenance</c> (<c>06_DATA_MODEL</c>): a JSON object, field → <c>detected|user</c>; absent reads as <c>user</c>.</summary>
    internal static class FieldProvenance
    {
        public const string User = "user";

        public const string Detected = "detected";

        /// <summary>
        /// The value a detection run leaves in <paramref name="field"/>, and the map updated to match (P4 PR-1). Null
        /// <paramref name="detected"/> = not established: the current value stays and the map is untouched. A field
        /// badged <c>detected</c> is refreshed. A field with no entry and no value has nothing to protect: it is filled
        /// and badged. Anything else — a <c>user</c> entry, an unrecognised entry, or a value with no entry — reads as
        /// the user's (<c>05_DETECTION</c> §Caching, <c>06_DATA_MODEL</c> §games) and is left alone.
        /// </summary>
        public static string? Resolve(Dictionary<string, string> map, string field, string? current, string? detected)
        {
            if (detected is null)
            {
                return current;
            }

            bool writable = map.TryGetValue(field, out string? badge)
                ? string.Equals(badge, Detected, StringComparison.Ordinal)
                : string.IsNullOrEmpty(current);
            if (!writable)
            {
                return current;
            }

            map[field] = Detected;
            return detected;
        }

        /// <summary>The stored JSON after a user edit: every field whose value changed is now <c>user</c>; the rest keep what they had.</summary>
        public static string? AfterUserEdit(string? storedJson, GameMetadata before, GameMetadata after)
        {
            Dictionary<string, string> map = Parse(storedJson);
            Mark(map, "name", before.Name, after.Name);
            Mark(map, "platform", before.Platform, after.Platform);
            Mark(map, "store_id", before.StoreId, after.StoreId);
            Mark(map, "engine", before.Engine, after.Engine);
            Mark(map, "engine_version", before.EngineVersion, after.EngineVersion);
            Mark(map, "publisher", before.Publisher, after.Publisher);
            Mark(map, "game_version", before.GameVersion, after.GameVersion);
            Mark(map, "cover_path", before.CoverPath, after.CoverPath);
            return map.Count == 0 ? storedJson : JsonSerializer.Serialize(map, LedgerJsonContext.Default.DictionaryStringString);
        }

        public static Dictionary<string, string> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            try
            {
                return new Dictionary<string, string>(
                    JsonSerializer.Deserialize(json, LedgerJsonContext.Default.DictionaryStringString) ?? [], StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                // Unrecognised reads as "user" (06_DATA_MODEL), which is what an empty map means here.
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private static void Mark(Dictionary<string, string> map, string field, string? before, string? after)
        {
            if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
            {
                map[field] = User;
            }
        }
    }
}
