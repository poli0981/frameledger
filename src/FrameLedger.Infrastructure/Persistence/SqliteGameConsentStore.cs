using System.Data.Common;
using Dapper;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Consent;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// The <c>games</c> table's consent columns behind <see cref="IGameConsentStore"/> — the adapter
/// <c>04_CAPTURE</c> §The guard said P2 would write.
/// </summary>
/// <remarks>
/// <para>
/// <b>It ships, and that changes what keeps CLAUDE.md rule 1 true.</b> The file-backed store it replaces
/// lived in the unshipped capture host so that no published binary could mint consent. This one is in
/// <c>FrameLedger.Infrastructure</c>, inside both publish closures, and <c>GameConsentRecord.Stored</c>'s
/// <c>InternalsVisibleTo</c> list now names this assembly. What holds rule 1 is therefore no longer
/// packaging: it is that the only producers of an acknowledgement are a disclosure shown to a human
/// (the capture host's verb today; the Agent's console verb in PR-F, decision D4; the UI's dialog in P3),
/// that the provenance is recorded by NAME, and that every anti-cheat check still runs afterwards.
/// <c>20_OPEN_QUESTIONS</c> §S27 carries the restatement.
/// </para>
/// <para>
/// The semantics are the file store's, unchanged: a grant merges the block state forward and cannot clear
/// it; a block forces the toggle off and preserves the stamp; a revoke withdraws the stamp; a default
/// verdict records "could not verify" (<c>hook_prescan_state = 'unverified'</c>) and never a block; a
/// re-grant against a different binary cannot inherit an existing block. Every failure returns
/// <see cref="ConsentWriteOutcome.Failed"/> rather than throwing past the port.
/// </para>
/// </remarks>
public sealed class SqliteGameConsentStore : IGameConsentStore
{
    private const string _columns =
        "exe_path, exe_size_bytes, exe_mtime_ms, hook_enabled, hook_consent_at, hook_consent_provenance, "
        + "hook_consent_disclosure_version, hook_blocked_reason, hook_prescan_state, updated_at, "
        + "ac_exception_at, ac_exception_family, ac_exception_disclosure_version, ac_exception_exe_size_bytes, ac_exception_exe_mtime_ms";

    private const string _selectOne = $"SELECT {_columns} FROM games WHERE exe_path = @path";

    private const string _selectEnabled = $"SELECT {_columns} FROM games WHERE hook_enabled = 1 ORDER BY exe_path";

    private const string _insertGrant =
        "INSERT INTO games (name, exe_path, exe_size_bytes, exe_mtime_ms, hook_enabled, hook_consent_at, "
        + "hook_consent_provenance, hook_consent_disclosure_version, added_at, updated_at) "
        + "VALUES (@name, @path, @size, @mtime, 1, @at, @provenance, @disclosure, @at, @at)";

    // The block columns are NOT in the SET list: that is the merge-forward, expressed as an omission.
    private const string _updateGrant =
        "UPDATE games SET exe_size_bytes = @size, exe_mtime_ms = @mtime, hook_enabled = 1, hook_consent_at = @at, "
        + "hook_consent_provenance = @provenance, hook_consent_disclosure_version = @disclosure, updated_at = @at "
        + "WHERE exe_path = @path";

    private const string _revoke =
        "UPDATE games SET hook_enabled = 0, hook_consent_at = NULL, hook_consent_provenance = @provenance, "
        + "hook_consent_disclosure_version = '', updated_at = @at WHERE exe_path = @path";

    private const string _insertBlock =
        "INSERT INTO games (name, exe_path, exe_size_bytes, exe_mtime_ms, hook_enabled, hook_blocked_reason, "
        + "hook_prescan_state, added_at, updated_at) "
        + "VALUES (@name, @path, @size, @mtime, @enabled, @reason, @state, @at, @at)";

    // The consent columns are NOT in the SET list: the stamp and its provenance are PRESERVED across a block.
    private const string _updateBlock =
        "UPDATE games SET exe_size_bytes = @size, exe_mtime_ms = @mtime, hook_enabled = @enabled, "
        + "hook_blocked_reason = @reason, hook_prescan_state = @state, updated_at = @at WHERE exe_path = @path";

    // The pre-scan of the library (schema 0010). Both forms leave a block alone in SQL, not only in the sweep that skips
    // blocked rows: the WHERE is the guarantee, so a block written between the sweep's read and this write still stands.
    private const string _preScanFinding =
        "UPDATE games SET hook_enabled = 0, hook_blocked_reason = @reason, hook_prescan_state = 'blocked', "
        + "hook_prescan_rules_version = @rules, hook_prescan_exe_size_bytes = @size, hook_prescan_exe_mtime_ms = @mtime, "
        + "updated_at = @at WHERE exe_path = @path AND hook_blocked_reason IS NULL AND hook_prescan_state <> 'blocked'";

    private const string _preScanOther =
        "UPDATE games SET hook_prescan_state = @state, hook_prescan_rules_version = @rules, "
        + "hook_prescan_exe_size_bytes = @size, hook_prescan_exe_mtime_ms = @mtime, updated_at = @at "
        + "WHERE exe_path = @path AND hook_blocked_reason IS NULL AND hook_prescan_state <> 'blocked'";

    // D33 (schema 0014). The eligibility and the grant are each bound to the block they were reached about — the WHERE is
    // the guarantee, so a block rewritten between the caller's read and this write leaves the row alone. Neither names
    // hook_blocked_reason, hook_enabled or the consent columns in its SET list.
    private const string _exceptionEligibility =
        "UPDATE games SET ac_exception_eligible = @eligible, ac_exception_verdict = @verdict, ac_exception_sessions = @sessions, "
        + "ac_exception_checked_rules_version = @rules, ac_exception_checked_exe_size_bytes = @size, ac_exception_checked_exe_mtime_ms = @mtime, "
        + "ac_exception_checked_block = @block, updated_at = @at WHERE exe_path = @path AND hook_blocked_reason = @block";

    private const string _exceptionGrant =
        "UPDATE games SET ac_exception_at = @at, ac_exception_family = @family, ac_exception_disclosure_version = @disclosure, "
        + "ac_exception_exe_size_bytes = @size, ac_exception_exe_mtime_ms = @mtime, ac_exception_lapsed_at = NULL, ac_exception_lapsed_reason = NULL, "
        + "ac_exception_eligible = 1, ac_exception_verdict = @verdict, ac_exception_sessions = @sessions, updated_at = @at "
        + "WHERE exe_path = @path AND hook_blocked_reason = @block";

    // The end of an exception keeps the consent stamp, as a block does, and turns hooking off while the block stands. The
    // grant's family, disclosure and bytes stay as history; ac_exception_at going NULL is what ends it.
    private const string _exceptionRevoke =
        "UPDATE games SET ac_exception_at = NULL, ac_exception_lapsed_at = @at, ac_exception_lapsed_reason = @reason, "
        + "hook_enabled = CASE WHEN hook_blocked_reason IS NOT NULL THEN 0 ELSE hook_enabled END, updated_at = @at "
        + "WHERE exe_path = @path AND ac_exception_at IS NOT NULL";

    private readonly LedgerDatabase _db;

    public SqliteGameConsentStore(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// The words a block is stored in (<c>hook_blocked_reason</c>): <c>Reason|Family|Signal</c> since 2026-09-25, so the
    /// App can say what was found in the user's language. It was <c>"Reason: Family Signal"</c>, which cannot be split
    /// back apart — family names and file names both carry spaces — and rows written that way keep it. A family name
    /// cannot contain '|' (the rules schema's pattern) and a Windows file name cannot either.
    /// </summary>
    public static string BlockText(AntiCheatVerdict verdict) => $"{verdict.Reason}|{verdict.Family}|{verdict.Signal}";

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> RecordPreScanAsync(
        ExecutableFingerprint scanned, AntiCheatVerdict verdict, string rulesVersion, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesVersion);
        bool finding = verdict.IsFindingAboutTheGame;
        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                var p = new
                {
                    path = scanned.ExePath,
                    reason = BlockText(verdict),
                    state = verdict.IsAllowed ? "clean" : "unverified",
                    rules = rulesVersion,
                    size = scanned.SizeBytes,
                    mtime = scanned.MtimeUnixMs,
                    at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                int rows = await c.ExecuteAsync(new CommandDefinition(finding ? _preScanFinding : _preScanOther, p, tx, cancellationToken: token))
                    .ConfigureAwait(false);
                return rows == 0 ? ConsentWriteOutcome.NotFound : ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async ValueTask<GameConsentRecord> FindAsync(string normalisedExePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);

        try
        {
            Row? row = await _db.ReadAsync((c, token) => ReadRowAsync(c, null, normalisedExePath, token), ct).ConfigureAwait(false);
            return row?.ToRecord() ?? default;
        }
        catch (SqliteException)
        {
            // An unreadable store consents to nothing. Same answer as "no record", and for the same reason.
            return default;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<GameConsentRecord>> ListEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<Row> rows = await _db.ReadAsync(
                (c, token) => SqliteReaders.ReadAllAsync(c, new CommandDefinition(_selectEnabled, cancellationToken: token), Row.From),
                ct).ConfigureAwait(false);
            return [.. rows.Select(r => r.ToRecord())];
        }
        catch (SqliteException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> RecordOperatorAcknowledgementAsync(
        OperatorAcknowledgement acknowledgement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (acknowledgement.Provenance == ConsentProvenance.NotRecorded)
        {
            // An acknowledgement that names no disclosure is not one: writing it would mint a consent stamp
            // whose provenance says nothing was shown, the exact record HookRequest.FromConsent refuses.
            throw new ArgumentException("an acknowledgement must name the disclosure surface that produced it", nameof(acknowledgement));
        }

        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                ExecutableFingerprint fp = acknowledgement.Fingerprint;
                Row? existing = await ReadRowAsync(c, tx, fp.ExePath, token).ConfigureAwait(false);

                // A FINGERPRINT THAT DOES NOT MATCH THE STORED ONE IS REFUSED when a BLOCK is at stake — never
                // silently re-pointed at a different binary. An ordinary re-grant after a patch keeps working, and
                // so does one under a user-mode exception granted on exactly these bytes (D33): the user accepted the
                // exception's disclosure for this file, and is now accepting FR-2.1's for it.
                if (existing is not null && existing.BlockedReason is not null && !existing.Fingerprint.Matches(fp)
                    && existing.Grant?.Covers(fp) != true)
                {
                    return ConsentWriteOutcome.StaleFingerprint;
                }

                // A GRANT MAY NOT CLEAR A BLOCK: the acknowledgement carries neither field, so the row's own
                // values are the only source and there is nothing for a caller to override.
                long at = acknowledgement.AcknowledgedAt.ToUnixTimeMilliseconds();
                var p = new
                {
                    path = fp.ExePath,
                    name = System.IO.Path.GetFileNameWithoutExtension(fp.ExePath),
                    size = fp.SizeBytes,
                    mtime = fp.MtimeUnixMs,
                    at,
                    // The acknowledgement names its own surface (P2 PR-F); NotRecorded is refused above.
                    provenance = acknowledgement.Provenance.ToString(),
                    disclosure = acknowledgement.DisclosureVersion,
                };
                await c.ExecuteAsync(new CommandDefinition(existing is null ? _insertGrant : _updateGrant, p, tx, cancellationToken: token))
                    .ConfigureAwait(false);
                return ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> RevokeAsync(string normalisedExePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);

        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                // The stamp goes with the toggle here, unlike a block: a revoke IS the withdrawal, so being
                // shown the disclosure again is the correct consequence. hook_blocked_reason is untouched.
                var p = new
                {
                    path = normalisedExePath,
                    provenance = nameof(ConsentProvenance.NotRecorded),
                    at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                int rows = await c.ExecuteAsync(new CommandDefinition(_revoke, p, tx, cancellationToken: token)).ConfigureAwait(false);
                return rows == 0 ? ConsentWriteOutcome.NotFound : ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> RecordGuardBlockAsync(
        ExecutableFingerprint fingerprint, AntiCheatVerdict refusal, CancellationToken ct = default)
    {
        // NOTHING MANAGED AUTHORS AN ANTI-CHEAT FACT: an allowing verdict cannot become a block.
        if (refusal.IsAllowed)
        {
            return ConsentWriteOutcome.Failed;
        }

        // A DEFAULT-CONSTRUCTED VERDICT HAS SCANNED NOTHING: a refusal nobody produced is "could not
        // verify", not a block. 05_DETECTION forbids both collapses.
        bool evaluated = refusal.Reason != AntiCheatRefusalReason.Allow && !string.IsNullOrEmpty(refusal.Family + refusal.Signal);
        bool unverified = !evaluated || refusal.Reason == AntiCheatRefusalReason.PreScanFailed;

        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                Row? existing = await ReadRowAsync(c, tx, fingerprint.ExePath, token).ConfigureAwait(false);
                var p = new
                {
                    path = fingerprint.ExePath,
                    name = System.IO.Path.GetFileNameWithoutExtension(fingerprint.ExePath),
                    size = fingerprint.SizeBytes,
                    mtime = fingerprint.MtimeUnixMs,
                    // Forced to 0 on a real block (19_SAFETY); an unverified pre-scan does NOT disable the toggle.
                    enabled = unverified && existing?.HookEnabled == true ? 1 : 0,
                    reason = unverified ? existing?.BlockedReason : BlockText(refusal),
                    state = unverified ? "unverified" : "blocked",
                    at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                await c.ExecuteAsync(new CommandDefinition(existing is null ? _insertBlock : _updateBlock, p, tx, cancellationToken: token))
                    .ConfigureAwait(false);
                return ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> RecordExceptionEligibilityAsync(
        ExecutableFingerprint scanned, string block, AntiCheatVerdict? verdict, int sessions, string rulesVersion, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(block);
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesVersion);
        bool eligible = verdict is { } v && UserModeExceptionRules.IsEligible(StoredBlock.Parse(block), v, sessions);
        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                var p = new
                {
                    path = scanned.ExePath,
                    eligible = eligible ? 1 : 0,
                    verdict = verdict is { } answered ? BlockText(answered) : null,
                    sessions,
                    rules = rulesVersion,
                    size = scanned.SizeBytes,
                    mtime = scanned.MtimeUnixMs,
                    block,
                    at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                int rows = await c.ExecuteAsync(new CommandDefinition(_exceptionEligibility, p, tx, cancellationToken: token)).ConfigureAwait(false);
                return rows == 0 ? ConsentWriteOutcome.NotFound : ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> GrantAntiCheatExceptionAsync(AntiCheatExceptionGrantRequest grant, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(grant.DisclosureVersion, nameof(grant));

        // THE STORE APPLIES THE RULE ITSELF: a grant over facts that do not make the game eligible is refused here, not
        // only by the caller that gathered them.
        StoredBlock? block = StoredBlock.Parse(grant.Block);
        if (!UserModeExceptionRules.IsEligible(block, grant.Verdict, grant.Sessions))
        {
            return ConsentWriteOutcome.NotEligible;
        }

        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                var p = new
                {
                    path = grant.Fingerprint.ExePath,
                    block = grant.Block,
                    family = block!.Value.Family,
                    disclosure = grant.DisclosureVersion,
                    size = grant.Fingerprint.SizeBytes,
                    mtime = grant.Fingerprint.MtimeUnixMs,
                    verdict = BlockText(grant.Verdict),
                    sessions = grant.Sessions,
                    at = grant.GrantedAt.ToUnixTimeMilliseconds(),
                };
                int rows = await c.ExecuteAsync(new CommandDefinition(_exceptionGrant, p, tx, cancellationToken: token)).ConfigureAwait(false);
                return rows == 0 ? ConsentWriteOutcome.NotFound : ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ConsentWriteOutcome> RevokeAntiCheatExceptionAsync(string normalisedExePath, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        try
        {
            return await _db.WriteAsync(async (c, tx, token) =>
            {
                var p = new { path = normalisedExePath, reason, at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                int rows = await c.ExecuteAsync(new CommandDefinition(_exceptionRevoke, p, tx, cancellationToken: token)).ConfigureAwait(false);
                return rows == 0 ? ConsentWriteOutcome.NotFound : ConsentWriteOutcome.Written;
            }, ct).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            return ConsentWriteOutcome.Failed;
        }
    }

    private static Task<Row?> ReadRowAsync(SqliteConnection c, SqliteTransaction? tx, string path, CancellationToken ct) =>
        SqliteReaders.ReadOneAsync(c, new CommandDefinition(_selectOne, new { path }, tx, cancellationToken: ct), Row.From);

    /// <summary>The consent columns of one row, read by ordinal in <see cref="_columns"/>' order.</summary>
    private sealed record Row(
        ExecutableFingerprint Fingerprint,
        bool HookEnabled,
        long? ConsentedAtMs,
        string ProvenanceName,
        string DisclosureVersion,
        string? BlockedReason,
        string PreScanState,
        long UpdatedAtMs,
        AntiCheatExceptionGrant? Grant)
    {
        public static Row From(DbDataReader r) => new(
            new ExecutableFingerprint
            {
                ExePath = r.GetString(0),
                SizeBytes = SqliteReaders.Int64(r, 1) ?? 0,
                MtimeUnixMs = SqliteReaders.Int64(r, 2) ?? 0,
            },
            r.GetInt64(3) != 0,
            SqliteReaders.Int64(r, 4),
            r.GetString(5),
            r.GetString(6),
            SqliteReaders.String(r, 7),
            r.GetString(8),
            r.GetInt64(9),
            ReadGrant(r));

        /// <summary>D33: the grant in force (schema 0014, ordinals 10–14), or null when <c>ac_exception_at</c> is.</summary>
        private static AntiCheatExceptionGrant? ReadGrant(DbDataReader r) =>
            SqliteReaders.Int64(r, 10) is { } at && SqliteReaders.String(r, 11) is { Length: > 0 } family
                ? new AntiCheatExceptionGrant
                {
                    GrantedAt = DateTimeOffset.FromUnixTimeMilliseconds(at),
                    Family = family,
                    DisclosureVersion = SqliteReaders.String(r, 12) ?? string.Empty,
                    ExeSizeBytes = SqliteReaders.Int64(r, 13) ?? -1,
                    ExeMtimeUnixMs = SqliteReaders.Int64(r, 14) ?? -1,
                }
                : null;

        public GameConsentRecord ToRecord() => GameConsentRecord.Stored(
            Fingerprint,
            HookEnabled,
            ConsentedAtMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null,
            ParseProvenance(ProvenanceName),
            DisclosureVersion,
            BlockedReason,
            string.Equals(PreScanState, "unverified", StringComparison.Ordinal),
            DateTimeOffset.FromUnixTimeMilliseconds(UpdatedAtMs),
            Grant);
    }

    /// <summary>
    /// Only <see cref="ConsentProvenance"/>'s declared NAMES are accepted, exactly cased. <c>Enum.TryParse</c>
    /// alone parses numeric strings too and does not check the result against the declared members; this is
    /// the field that decides whether a timestamp counts as consent.
    /// </summary>
    private static ConsentProvenance ParseProvenance(string name) =>
        Enum.TryParse(name, ignoreCase: false, out ConsentProvenance p)
        && Enum.IsDefined(p)
        && string.Equals(p.ToString(), name, StringComparison.Ordinal)
            ? p
            : ConsentProvenance.NotRecorded;
}
