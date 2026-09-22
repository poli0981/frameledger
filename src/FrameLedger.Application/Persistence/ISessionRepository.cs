namespace FrameLedger.Application.Persistence;

/// <summary>
/// <c>sessions</c> and the tables under it. The Agent writes (finalize, retention sweep); the UI reads — by
/// game for the Sessions tab (NFR-4: a game with 100 sessions opens in ≤ 500 ms, which is why the list is one
/// query over the aggregate columns and the blobs are fetched per session on demand).
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Writes the session under <c>session.Row.GameId</c>, in one transaction. Throws <see cref="SessionOwnerMissingException"/>
    /// — never the foreign key's <c>SqliteException</c> — when that row no longer exists at the moment of the write.
    /// </summary>
    ValueTask<long> InsertFinalizedAsync(FinalizedSession session, CancellationToken ct = default);

    /// <summary>
    /// The <c>games</c> row a finishing session belongs to (2026-09-23), or null when there is none any more:
    /// <paramref name="gameId"/> when that row existed before the session started or holds its executable (an id SQLite
    /// reused for a later game is neither), else the row that holds <paramref name="exePath"/> — the game re-imported
    /// while the session ran. Two GIRLS' FRONTLINE 2 sessions on 2026-09-22/23 died on the foreign key because their
    /// entry was removed and re-imported mid-session.
    /// </summary>
    ValueTask<long?> ResolveOwnerAsync(long gameId, string? exePath, DateTimeOffset startedAt, CancellationToken ct = default);

    ValueTask<bool> ExistsAsync(Guid sessionGuid, CancellationToken ct = default);

    ValueTask<SessionRow?> FindAsync(Guid sessionGuid, CancellationToken ct = default);

    ValueTask<SessionRow?> FindByIdAsync(long sessionId, CancellationToken ct = default);

    ValueTask<IReadOnlyList<SessionRow>> ListRecentAsync(int count, CancellationToken ct = default);

    /// <summary>A game's sessions, newest first, at most <paramref name="limit"/>.</summary>
    ValueTask<IReadOnlyList<SessionRow>> ListByGameAsync(long gameId, int limit, CancellationToken ct = default);

    /// <summary>Every game's sessions in aggregate (count, hooked count, playtime, last played), for the library grid and the Dashboard totals.</summary>
    ValueTask<IReadOnlyList<GameSessionSummary>> SummariseByGameAsync(CancellationToken ct = default);

    /// <summary>Sessions that started at or after <paramref name="since"/> (the Dashboard's "this week").</summary>
    ValueTask<long> CountSinceAsync(DateTimeOffset since, CancellationToken ct = default);

    ValueTask<int> SweepRetentionAsync(long gameId, int keep, CancellationToken ct = default);

    /// <summary>
    /// <c>06_DATA_MODEL</c> §Retention on demand (P4 PR-7): the per-game rule of <see cref="SweepRetentionAsync"/> for
    /// every game at once, a removed game whose sessions were kept included, in one transaction. <paramref name="keep"/>
    /// must be at least 1 — "unlimited" is the caller's to honour by not sweeping, never a keep of zero that deletes
    /// every raw series.
    /// </summary>
    ValueTask<RetentionSweepResult> SweepRetentionAllAsync(int keep, CancellationToken ct = default);

    ValueTask<FrameBlobs?> FindFramesAsync(long sessionId, CancellationToken ct = default);

    /// <summary>The session's segments in frame order (the ribbon, <c>08_UI</c> §Session summary).</summary>
    ValueTask<IReadOnlyList<SegmentRow>> FindSegmentsAsync(long sessionId, CancellationToken ct = default);

    /// <summary>The session's sensor series, still encoded (the sensor strip decodes what it draws).</summary>
    ValueTask<IReadOnlyList<SensorBlob>> FindSensorsAsync(long sessionId, CancellationToken ct = default);
}
