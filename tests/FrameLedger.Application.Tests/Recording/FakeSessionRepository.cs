using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Tests.Recording;

internal sealed class FakeSessionRepository : ISessionRepository
{
    public List<FinalizedSession> Stored { get; } = [];

    public List<(long GameId, int Keep)> Sweeps { get; } = [];

    public HashSet<Guid> PreExisting { get; } = [];

    public int SweptPerCall { get; set; }

    /// <summary>When set, <see cref="InsertFinalizedAsync"/> throws it instead of storing (a write that fails).</summary>
    public Exception? InsertFailure { get; set; }

    public ValueTask<long> InsertFinalizedAsync(FinalizedSession session, CancellationToken ct = default)
    {
        if (InsertFailure is { } failure)
        {
            throw failure;
        }

        if (PreExisting.Contains(session.Row.SessionGuid) || Stored.Any(s => s.Row.SessionGuid == session.Row.SessionGuid))
        {
            throw new InvalidOperationException("session_guid already stored");
        }

        Stored.Add(session);
        return ValueTask.FromResult((long)Stored.Count);
    }

    public ValueTask<bool> ExistsAsync(Guid sessionGuid, CancellationToken ct = default) =>
        ValueTask.FromResult(PreExisting.Contains(sessionGuid) || Stored.Any(s => s.Row.SessionGuid == sessionGuid));

    /// <summary>Who owns a finishing session: by default the id it started under, as if nothing had been removed.</summary>
    public Func<long, string?, DateTimeOffset, long?> Owner { get; set; } = static (gameId, _, _) => gameId;

    /// <summary>Every owner lookup, in order.</summary>
    public List<(long GameId, string? ExePath)> OwnerLookups { get; } = [];

    public ValueTask<long?> ResolveOwnerAsync(long gameId, string? exePath, DateTimeOffset startedAt, CancellationToken ct = default)
    {
        OwnerLookups.Add((gameId, exePath));
        return ValueTask.FromResult(Owner(gameId, exePath, startedAt));
    }

    public ValueTask<SessionRow?> FindAsync(Guid sessionGuid, CancellationToken ct = default) =>
        ValueTask.FromResult(Stored.Select(s => s.Row).FirstOrDefault(r => r.SessionGuid == sessionGuid));

    public ValueTask<IReadOnlyList<SessionRow>> ListRecentAsync(int count, CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<SessionRow>>([.. Stored.Select(s => s.Row).TakeLast(count).Reverse()]);

    public ValueTask<int> SweepRetentionAsync(long gameId, int keep, CancellationToken ct = default)
    {
        Sweeps.Add((gameId, keep));
        return ValueTask.FromResult(SweptPerCall);
    }

    /// <summary>Every keep an all-games sweep was asked for (P4 PR-7), and the result it answers.</summary>
    public List<int> SweepAlls { get; } = [];

    public RetentionSweepResult SweepAllResult { get; set; } = new(0, 0);

    public ValueTask<RetentionSweepResult> SweepRetentionAllAsync(int keep, CancellationToken ct = default)
    {
        SweepAlls.Add(keep);
        return ValueTask.FromResult(SweepAllResult);
    }

    public ValueTask<FrameBlobs?> FindFramesAsync(long sessionId, CancellationToken ct = default) =>
        ValueTask.FromResult(sessionId >= 1 && sessionId <= Stored.Count ? Stored[(int)sessionId - 1].Frames : null);

    public ValueTask<SessionRow?> FindByIdAsync(long sessionId, CancellationToken ct = default) =>
        ValueTask.FromResult(sessionId >= 1 && sessionId <= Stored.Count ? Stored[(int)sessionId - 1].Row with { Id = sessionId } : null);

    public ValueTask<IReadOnlyList<SessionRow>> ListByGameAsync(long gameId, int limit, CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<SessionRow>>([.. Stored.Select(s => s.Row).Where(r => r.GameId == gameId).OrderByDescending(r => r.StartedAt).Take(limit)]);

    public ValueTask<IReadOnlyList<GameSessionSummary>> SummariseByGameAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<GameSessionSummary>>([.. Stored.GroupBy(s => s.Row.GameId).Select(g => new GameSessionSummary
        {
            GameId = g.Key,
            SessionCount = g.Count(),
            HookedCount = g.Count(s => s.Row.Tier == Domain.Sessions.CaptureTier.Hooked),
            TotalSeconds = g.Sum(s => s.Row.DurationSeconds),
            LastPlayedAt = g.Max(s => s.Row.EndedAt),
        })]);

    public ValueTask<long> CountSinceAsync(DateTimeOffset since, CancellationToken ct = default) =>
        ValueTask.FromResult((long)Stored.Count(s => s.Row.StartedAt >= since));

    public ValueTask<IReadOnlyList<SegmentRow>> FindSegmentsAsync(long sessionId, CancellationToken ct = default) =>
        ValueTask.FromResult(sessionId >= 1 && sessionId <= Stored.Count ? Stored[(int)sessionId - 1].Segments : []);

    public ValueTask<IReadOnlyList<SensorBlob>> FindSensorsAsync(long sessionId, CancellationToken ct = default) =>
        ValueTask.FromResult(sessionId >= 1 && sessionId <= Stored.Count ? Stored[(int)sessionId - 1].Sensors : []);
}
