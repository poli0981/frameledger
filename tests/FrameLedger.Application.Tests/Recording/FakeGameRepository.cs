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

    public ValueTask<bool> ChangeExecutableAsync(long gameId, ExecutableFingerprint fingerprint, DateTimeOffset at, CancellationToken ct = default) =>
        ValueTask.FromResult(true);

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
            };
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }
}
