using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Tests.Recording;

internal sealed class FakeGameRepository : IGameRepository
{
    private long _nextId = 1;

    public Dictionary<string, GameRow> Rows { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<(long GameId, string Reason, DateTimeOffset At)> Disabled { get; } = [];

    public List<(long GameId, DateTimeOffset At)> Injections { get; } = [];

    public int CrashCount { get; set; }

    public ValueTask<GameRow> EnsureAsync(ExecutableFingerprint fingerprint, string name, CancellationToken ct = default)
    {
        if (!Rows.TryGetValue(fingerprint.ExePath, out GameRow? row))
        {
            row = new GameRow
            {
                Id = _nextId++,
                Name = name,
                Fingerprint = fingerprint,
                HookEnabled = false,
                HookCrashCount = 0,
                AddedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch,
            };
            Rows[fingerprint.ExePath] = row;
        }

        return ValueTask.FromResult(row);
    }

    public ValueTask<GameRow?> FindAsync(string normalisedExePath, CancellationToken ct = default) =>
        ValueTask.FromResult(Rows.GetValueOrDefault(normalisedExePath));

    public ValueTask<IReadOnlyList<GameRow>> ListAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<GameRow>>([.. Rows.Values]);

    public ValueTask<bool> AutoDisableHookAsync(long gameId, string reason, DateTimeOffset at, CancellationToken ct = default)
    {
        Disabled.Add((gameId, reason, at));
        return ValueTask.FromResult(true);
    }

    public List<(long GameId, ExecutableFingerprint Changed)> Changes { get; } = [];

    /// <summary>
    /// The adapter's rule (2026-09-23, kept here so the relocator's D27 tests see it): a downgrade by construction — the new
    /// bytes, hooking off, consent cleared, the pre-scan not run, the block kept — refused for a removed row and for a
    /// path another row holds.
    /// </summary>
    public ValueTask<bool> ChangeExecutableAsync(long gameId, ExecutableFingerprint fingerprint, DateTimeOffset at, CancellationToken ct = default)
    {
        foreach ((string key, GameRow row) in Rows)
        {
            if (row.Id != gameId)
            {
                continue;
            }

            if (!row.InLibrary || (Rows.TryGetValue(fingerprint.ExePath, out GameRow? holder) && holder.Id != gameId))
            {
                return ValueTask.FromResult(false);
            }

            Rows.Remove(key);
            Rows[fingerprint.ExePath] = row with { Fingerprint = fingerprint, HookEnabled = false, HookConsentAt = null, HookPrescanState = "not_run", UpdatedAt = at };
            Changes.Add((gameId, fingerprint));
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    public List<(long GameId, ExecutableFingerprint Moved)> Relocations { get; } = [];

    /// <summary>The adapter's rule, kept here so the sweep and the watcher tests see it: same size and mtime, or nothing moves.</summary>
    public ValueTask<bool> RelocateExecutableAsync(long gameId, ExecutableFingerprint moved, DateTimeOffset at, CancellationToken ct = default)
    {
        foreach ((string key, GameRow row) in Rows)
        {
            if (row.Id != gameId)
            {
                continue;
            }

            if (row.Fingerprint.SizeBytes != moved.SizeBytes || row.Fingerprint.MtimeUnixMs != moved.MtimeUnixMs || Rows.ContainsKey(moved.ExePath))
            {
                return ValueTask.FromResult(false);
            }

            Rows.Remove(key);
            Rows[moved.ExePath] = row with { Fingerprint = moved, UpdatedAt = at };
            Relocations.Add((gameId, moved));
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    public ValueTask<int> RecordCrashAsync(long gameId, CancellationToken ct = default) => ValueTask.FromResult(++CrashCount);

    public ValueTask<bool> RecordInjectionAsync(long gameId, DateTimeOffset at, CancellationToken ct = default)
    {
        Injections.Add((gameId, at));
        return ValueTask.FromResult(true);
    }

    public ValueTask<GameRow?> FindByIdAsync(long gameId, CancellationToken ct = default) =>
        ValueTask.FromResult(Rows.Values.FirstOrDefault(r => r.Id == gameId));

    // The UI's half (P3 PR-3) — no recorder test reaches these; the SQLite adapter has its own tests.
    public ValueTask<bool> UpdateMetadataAsync(long gameId, GameMetadata metadata, CancellationToken ct = default) => throw new NotSupportedException();

    public ValueTask<bool> SetTriStateDefaultAsync(long gameId, TriStateKind kind, Tri value, CancellationToken ct = default) => throw new NotSupportedException();

    public ValueTask<bool> RemoveAsync(long gameId, bool keepSessions, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>The UI's recording switch (2026-09-23), applied to the row so the watcher tests see it.</summary>
    public ValueTask<bool> SetRecordingAsync(long gameId, bool record, CancellationToken ct = default)
    {
        foreach ((string key, GameRow row) in Rows)
        {
            if (row.Id == gameId)
            {
                Rows[key] = row with { RecordSessions = record };
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }

    /// <summary>Every store write, in order (P4 PR-4); the row takes the store's platform, id and version whole (the SQLite adapter's provenance rule has its own tests).</summary>
    public List<(long GameId, StoreMetadata Store)> Stores { get; } = [];

    public ValueTask<bool> ApplyStoreMetadataAsync(long gameId, StoreMetadata store, CancellationToken ct = default)
    {
        Stores.Add((gameId, store));
        foreach ((string key, GameRow row) in Rows)
        {
            if (row.Id != gameId)
            {
                continue;
            }

            Rows[key] = row with { Platform = store.Platform, StoreId = store.StoreId ?? row.StoreId, GameVersion = store.GameVersion ?? row.GameVersion };
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    /// <summary>Every detection write, in order (P4 PR-1); the row gains the cache key and the flags so a second sweep sees it as current.</summary>
    public List<(long GameId, DetectionWrite Write)> Detections { get; } = [];

    public ValueTask<bool> ApplyDetectionAsync(long gameId, DetectionWrite detection, CancellationToken ct = default)
    {
        Detections.Add((gameId, detection));
        foreach ((string key, GameRow row) in Rows)
        {
            if (row.Id != gameId)
            {
                continue;
            }

            Rows[key] = row with
            {
                Engine = detection.EngineId ?? row.Engine,
                EngineVersion = detection.EngineVersion ?? row.EngineVersion,
                Platform = detection.PlatformId ?? row.Platform,
                CapabilityFlagsJson = "[" + string.Join(",", detection.CapabilityIds.Select(static c => "\"" + c + "\"")) + "]",
                DetectionRulesVersion = detection.RulesVersion,
                DetectionExeSizeBytes = detection.ExeSizeBytes,
                DetectionExeMtimeMs = detection.ExeMtimeMs,
                ExeMachine = detection.ExeArchitecture,
                ExeFileVersion = detection.ExeFileVersion,
                ExeProductVersion = detection.ExeProductVersion,
                Libraries = detection.Libraries,
            };
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }
}
