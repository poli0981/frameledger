using Dapper;
using FrameLedger.Application.Persistence;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// <c>legal_acceptance</c>. ~~READ-ONLY. There is deliberately no write here (FR-11 is the UI's, P3).~~ The write
/// exists since P3 PR-3 (2026-09-13) for the UI's Legal Gate (PR-9); the Agent still only reads.
/// </summary>
public sealed class SqliteLegalAcceptanceStore : ILegalAcceptanceStore
{
    private const string _selectOne = "SELECT doc, version, accepted_at FROM legal_acceptance WHERE doc = @document";

    private const string _selectAll = "SELECT doc, version, accepted_at FROM legal_acceptance ORDER BY doc";

    private const string _upsert =
        "INSERT INTO legal_acceptance (doc, version, accepted_at) VALUES (@document, @version, @at) "
        + "ON CONFLICT(doc) DO UPDATE SET version = excluded.version, accepted_at = excluded.accepted_at";

    private readonly LedgerDatabase _db;

    public SqliteLegalAcceptanceStore(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public ValueTask<LegalAcceptance?> FindAsync(string document, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        return _db.ReadAsync((c, token) => SqliteReaders.ReadOneAsync(
            c, new CommandDefinition(_selectOne, new { document }, cancellationToken: token),
            static r => new LegalAcceptance(r.GetString(0), r.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2)))), ct);
    }

    public ValueTask<IReadOnlyList<LegalAcceptance>> ListAsync(CancellationToken ct = default) =>
        _db.ReadAsync((c, token) => SqliteReaders.ReadAllAsync(
            c, new CommandDefinition(_selectAll, cancellationToken: token),
            static r => new LegalAcceptance(r.GetString(0), r.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2)))), ct);

    public async ValueTask RecordAsync(LegalAcceptance acceptance, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptance.Document, nameof(acceptance));
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptance.Version, nameof(acceptance));
        await _db.WriteAsync(
            (c, tx, token) => c.ExecuteAsync(new CommandDefinition(
                _upsert, new { document = acceptance.Document, version = acceptance.Version, at = acceptance.AcceptedAt.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)),
            ct).ConfigureAwait(false);
    }
}
