using Dapper;
using FrameLedger.Application.Consent;
using FrameLedger.Domain.Consent;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// <see cref="IGuardBypassStore"/> over <c>games.guard_bypass_at</c> / <c>guard_bypass_disclosure_version</c> (schema
/// 0007). Two statements, two columns each, and a WHERE clause that is the rule: the row must exist, must be in the
/// library, and must be about the executable the acknowledgement names.
/// </summary>
public sealed class SqliteGuardBypassStore : IGuardBypassStore
{
    // The fingerprint is IN the predicate: an acknowledgement for another binary updates zero rows.
    private const string _record =
        "UPDATE games SET guard_bypass_at = @at, guard_bypass_disclosure_version = @disclosure, updated_at = @at "
        + "WHERE exe_path = @path AND removed_at IS NULL AND exe_size_bytes = @size AND exe_mtime_ms = @mtime";

    private const string _exists = "SELECT COUNT(*) FROM games WHERE exe_path = @path AND removed_at IS NULL";

    private const string _revoke =
        "UPDATE games SET guard_bypass_at = NULL, guard_bypass_disclosure_version = '', updated_at = @at WHERE exe_path = @path";

    private readonly LedgerDatabase _db;

    public SqliteGuardBypassStore(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async ValueTask<ConsentWriteOutcome> RecordAsync(GuardBypassAcknowledgement acknowledgement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (string.IsNullOrWhiteSpace(acknowledgement.DisclosureVersion))
        {
            throw new ArgumentException("a bypass acknowledgement must name the disclosure it answered", nameof(acknowledgement));
        }

        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                ExecutableFingerprint fp = acknowledgement.Fingerprint;
                var p = new
                {
                    path = fp.ExePath,
                    size = fp.SizeBytes,
                    mtime = fp.MtimeUnixMs,
                    at = acknowledgement.AcknowledgedAt.ToUnixTimeMilliseconds(),
                    disclosure = acknowledgement.DisclosureVersion,
                };
                int written = await c.ExecuteAsync(new CommandDefinition(_record, p, tx, cancellationToken: token)).ConfigureAwait(false);
                if (written == 1)
                {
                    return ConsentWriteOutcome.Written;
                }

                long rows = await c.ExecuteScalarAsync<long>(new CommandDefinition(_exists, new { path = fp.ExePath }, tx, cancellationToken: token)).ConfigureAwait(false);
                return rows == 0 ? ConsentWriteOutcome.Failed : ConsentWriteOutcome.StaleFingerprint;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    public async ValueTask<ConsentWriteOutcome> RevokeAsync(string normalisedExePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);
        try
        {
            await _db.WriteAsync(async (c, tx, token) => await c.ExecuteAsync(new CommandDefinition(
                _revoke, new { path = normalisedExePath, at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, tx, cancellationToken: token)).ConfigureAwait(false), ct).ConfigureAwait(false);
            return ConsentWriteOutcome.Written;
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }
}
