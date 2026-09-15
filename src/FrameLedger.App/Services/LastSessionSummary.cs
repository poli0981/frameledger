using System.IO;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Services;

/// <summary>
/// Step 2's other optional item, "last session metadata + aggregates JSON (never raw blobs by default)"
/// (<c>10_LOGGING</c> §Bug report flow): the newest session in the ledger, written as the same <c>session.json</c> File ▸
/// Export writes (<see cref="SessionExporter.Document"/>: metadata, aggregates, segments, hardware) but without the
/// user's notes and tags, which are theirs and are not a diagnosis. No frame or sensor series.
/// </summary>
public sealed class LastSessionSummary
{
    private readonly ISessionRepository _sessions;
    private readonly IGameRepository _games;
    private readonly IHardwareSnapshotRepository _hardware;

    public LastSessionSummary(ISessionRepository sessions, IGameRepository games, IHardwareSnapshotRepository hardware)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
    }

    /// <summary>The newest session and its game, or null when the ledger has none.</summary>
    public async Task<LastSessionInfo?> FindAsync(CancellationToken ct = default)
    {
        IReadOnlyList<SessionRow> recent = await _sessions.ListRecentAsync(1, ct).ConfigureAwait(false);
        if (recent.Count == 0)
        {
            return null;
        }

        SessionRow row = recent[0];
        GameRow? game = await _games.FindByIdAsync(row.GameId, ct).ConfigureAwait(false);
        return game is null ? null : new LastSessionInfo(row.Id, game.Name, row.StartedAt);
    }

    /// <summary>The session's <c>session.json</c> bytes without notes or tags, or null when it is gone.</summary>
    public async Task<byte[]?> JsonAsync(long sessionId, CancellationToken ct = default)
    {
        SessionRow? row = await _sessions.FindByIdAsync(sessionId, ct).ConfigureAwait(false);
        GameRow? game = row is null ? null : await _games.FindByIdAsync(row.GameId, ct).ConfigureAwait(false);
        if (row is null || game is null)
        {
            return null;
        }

        IReadOnlyList<SegmentRow> segments = await _sessions.FindSegmentsAsync(sessionId, ct).ConfigureAwait(false);
        HardwareSnapshot? snapshot = await _hardware.FindAsync(row.SnapshotId, ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        SessionExporter.WriteJson(buffer, SessionExporter.Document(row, game, snapshot, segments, annotation: null));
        return buffer.ToArray();
    }
}
