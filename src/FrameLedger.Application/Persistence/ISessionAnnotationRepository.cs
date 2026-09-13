namespace FrameLedger.Application.Persistence;

/// <summary>The UI's writes beside a session: tags, notes and the FR-8.3 overrides, one row per session.</summary>
public interface ISessionAnnotationRepository
{
    ValueTask<SessionAnnotation?> FindAsync(long sessionId, CancellationToken ct = default);

    /// <summary>Every annotation of a game's sessions, for a list view — one query, not one per row.</summary>
    ValueTask<IReadOnlyList<SessionAnnotation>> ListByGameAsync(long gameId, CancellationToken ct = default);

    /// <summary>Replaces the row; an <see cref="SessionAnnotation.IsEmpty"/> annotation deletes it. False when the session does not exist.</summary>
    ValueTask<bool> UpsertAsync(SessionAnnotation annotation, CancellationToken ct = default);
}
