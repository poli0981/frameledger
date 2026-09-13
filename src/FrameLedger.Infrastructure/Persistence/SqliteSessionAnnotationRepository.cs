using System.Data.Common;
using System.Text.Json;
using Dapper;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// <c>session_annotations</c>: tags as a JSON string array, notes, and the three FR-8.3 overrides (schema 0002).
/// One row per session, replaced wholesale; an empty annotation deletes the row rather than leaving a row of
/// NULLs that reads as "annotated with nothing".
/// </summary>
public sealed class SqliteSessionAnnotationRepository : ISessionAnnotationRepository
{
    private const string _columns = "session_id, tags, notes, rt_override, pt_override, rr_override";

    private const string _selectOne = $"SELECT {_columns} FROM session_annotations WHERE session_id = @sessionId";

    private const string _selectByGame =
        $"SELECT a.session_id, a.tags, a.notes, a.rt_override, a.pt_override, a.rr_override FROM session_annotations a "
        + "JOIN sessions s ON s.id = a.session_id WHERE s.game_id = @gameId ORDER BY s.started_at DESC, s.id DESC";

    private const string _upsert =
        "INSERT INTO session_annotations (session_id, tags, notes, rt_override, pt_override, rr_override) "
        + "VALUES (@sessionId, @tags, @notes, @rt, @pt, @rr) ON CONFLICT(session_id) DO UPDATE SET "
        + "tags = excluded.tags, notes = excluded.notes, rt_override = excluded.rt_override, "
        + "pt_override = excluded.pt_override, rr_override = excluded.rr_override";

    private const string _delete = "DELETE FROM session_annotations WHERE session_id = @sessionId";

    private const string _sessionExists = "SELECT COUNT(*) FROM sessions WHERE id = @sessionId";

    private readonly LedgerDatabase _db;

    public SqliteSessionAnnotationRepository(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public ValueTask<SessionAnnotation?> FindAsync(long sessionId, CancellationToken ct = default) =>
        _db.ReadAsync((c, token) => SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectOne, new { sessionId }, cancellationToken: token), Read), ct);

    public ValueTask<IReadOnlyList<SessionAnnotation>> ListByGameAsync(long gameId, CancellationToken ct = default) =>
        _db.ReadAsync((c, token) => SqliteReaders.ReadAllAsync(c, new CommandDefinition(_selectByGame, new { gameId }, cancellationToken: token), Read), ct);

    public ValueTask<bool> UpsertAsync(SessionAnnotation annotation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return _db.WriteAsync(async (c, tx, token) =>
        {
            long exists = await c.ExecuteScalarAsync<long>(new CommandDefinition(_sessionExists, new { sessionId = annotation.SessionId }, tx, cancellationToken: token)).ConfigureAwait(false);
            if (exists == 0)
            {
                return false;
            }

            if (annotation.IsEmpty)
            {
                await c.ExecuteAsync(new CommandDefinition(_delete, new { sessionId = annotation.SessionId }, tx, cancellationToken: token)).ConfigureAwait(false);
                return true;
            }

            var p = new
            {
                sessionId = annotation.SessionId,
                tags = annotation.Tags.Count == 0 ? null : JsonSerializer.Serialize(annotation.Tags.ToArray(), LedgerJsonContext.Default.StringArray),
                notes = string.IsNullOrWhiteSpace(annotation.Notes) ? null : annotation.Notes,
                rt = TriText(annotation.RtOverride),
                pt = TriText(annotation.PtOverride),
                rr = TriText(annotation.RrOverride),
            };
            await c.ExecuteAsync(new CommandDefinition(_upsert, p, tx, cancellationToken: token)).ConfigureAwait(false);
            return true;
        }, ct);
    }

    private static string? TriText(Tri? value) => value is Tri t ? Vocabulary.Tri(t) : null;

    private static Tri? TriValue(string? text) => text is null ? null : Vocabulary.ParseTri(text);

    private static SessionAnnotation Read(DbDataReader r) => new()
    {
        SessionId = r.GetInt64(0),
        Tags = ReadTags(SqliteReaders.String(r, 1)),
        Notes = SqliteReaders.String(r, 2),
        RtOverride = TriValue(SqliteReaders.String(r, 3)),
        PtOverride = TriValue(SqliteReaders.String(r, 4)),
        RrOverride = TriValue(SqliteReaders.String(r, 5)),
    };

    private static string[] ReadTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, LedgerJsonContext.Default.StringArray) ?? [];
        }
        catch (JsonException)
        {
            // A hand-edited column is one tag rather than a row nobody can open.
            return [json];
        }
    }
}
