using Dapper;
using FluentAssertions;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// The per-game guard bypass on disk (schema 0007, owner decision 2026-09-21): it is a record of one human act about one
/// executable, it touches two columns and no others, it cannot exist for a game nobody added or for a binary other
/// than the stored one, and withdrawing consent — or pointing the row at another executable — takes it away.
/// </summary>
public sealed class SqliteGuardBypassStoreTests
{
    private const string _version = "guard-bypass-dialog/1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ExecutableFingerprint Fingerprint(string path = @"C:\Games\Title\game.exe", long size = 90_000) =>
        new() { ExePath = path, SizeBytes = size, MtimeUnixMs = 1_700_000_000_000 };

    private static GuardBypassAcknowledgement Ack(ExecutableFingerprint? fp = null, string version = _version) => new()
    {
        Fingerprint = fp ?? Fingerprint(),
        DisclosureVersion = version,
        AcknowledgedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000),
    };

    [Fact]
    public async Task EveryRowStartsWithTheBypassOffAndARecordedOneReadsBackThroughTheConsentRecord()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var games = new SqliteGameRepository(f.Db);
        var consent = new SqliteGameConsentStore(f.Db);
        var bypass = new SqliteGuardBypassStore(f.Db);
        GameRow row = await games.EnsureAsync(Fingerprint(), "Title", Ct);
        row.GuardBypassAt.Should().BeNull();
        (await consent.FindAsync(Fingerprint().ExePath, Ct)).GuardBypassAcknowledged.Should().BeFalse("off is the default, and the only state before schema 0007");

        (await bypass.RecordAsync(Ack(), Ct)).Should().Be(ConsentWriteOutcome.Written);

        GameConsentRecord record = await consent.FindAsync(Fingerprint().ExePath, Ct);
        record.GuardBypassAcknowledged.Should().BeTrue();
        record.GuardBypassAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000));
        record.GuardBypassDisclosureVersion.Should().Be(_version);
        record.HookEnabled.Should().BeFalse("the bypass enables nothing: hooking and consent are their own acts");
        record.ConsentedAt.Should().BeNull();
        (await games.FindByIdAsync(row.Id, Ct))!.GuardBypassAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ItNeverTouchesTheBlockTheConsentOrTheEnablement()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var games = new SqliteGameRepository(f.Db);
        GameRow row = await games.EnsureAsync(Fingerprint(), "Title", Ct);
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET hook_blocked_reason = 'BlockedModule: Easy Anti-Cheat x', hook_prescan_state = 'blocked' WHERE id = @id",
            new { id = row.Id }, tx, cancellationToken: ct)), Ct);

        (await new SqliteGuardBypassStore(f.Db).RecordAsync(Ack(), Ct)).Should().Be(ConsentWriteOutcome.Written);

        GameRow after = (await games.FindByIdAsync(row.Id, Ct))!;
        after.HookBlockedReason.Should().Be("BlockedModule: Easy Anti-Cheat x", "the finding stays on the row and stays true; the gate reads the bypass beside it");
        after.HookPrescanState.Should().Be("blocked");
        after.HookEnabled.Should().BeFalse();
        after.HookConsentAt.Should().BeNull();
    }

    [Fact]
    public async Task ItCannotBeRecordedForAGameNobodyAddedOrForAnotherBinaryOrWithoutNamingItsDisclosure()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var games = new SqliteGameRepository(f.Db);
        var bypass = new SqliteGuardBypassStore(f.Db);

        (await bypass.RecordAsync(Ack(), Ct)).Should().Be(ConsentWriteOutcome.Failed, "there is no row: a game nobody added has nothing to bypass, and none is created for it");
        (await games.FindAsync(Fingerprint().ExePath, Ct)).Should().BeNull();

        await games.EnsureAsync(Fingerprint(), "Title", Ct);
        (await bypass.RecordAsync(Ack(Fingerprint(size: 91_000)), Ct)).Should().Be(ConsentWriteOutcome.StaleFingerprint, "the acknowledgement is about one executable; a patched binary is another");
        (await new SqliteGameConsentStore(f.Db).FindAsync(Fingerprint().ExePath, Ct)).GuardBypassAcknowledged.Should().BeFalse();

        Func<Task> unnamed = async () => await bypass.RecordAsync(Ack(version: " "), Ct).ConfigureAwait(false);
        await unnamed.Should().ThrowAsync<ArgumentException>("an acknowledgement of nothing is not one");
    }

    [Fact]
    public async Task RevokingItWithdrawingConsentAndChangingTheExecutableEachTurnItOff()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var games = new SqliteGameRepository(f.Db);
        var consent = new SqliteGameConsentStore(f.Db);
        var bypass = new SqliteGuardBypassStore(f.Db);
        GameRow row = await games.EnsureAsync(Fingerprint(), "Title", Ct);

        await bypass.RecordAsync(Ack(), Ct);
        (await bypass.RevokeAsync(Fingerprint().ExePath, Ct)).Should().Be(ConsentWriteOutcome.Written);
        (await consent.FindAsync(Fingerprint().ExePath, Ct)).GuardBypassAcknowledged.Should().BeFalse();
        (await bypass.RevokeAsync(Fingerprint().ExePath, Ct)).Should().Be(ConsentWriteOutcome.Written, "idempotent");

        await bypass.RecordAsync(Ack(), Ct);
        await consent.RevokeAsync(Fingerprint().ExePath, Ct);
        (await consent.FindAsync(Fingerprint().ExePath, Ct)).GuardBypassAcknowledged.Should().BeFalse("turning hooking off returns the row to its safest state");

        await bypass.RecordAsync(Ack(), Ct);
        ExecutableFingerprint other = Fingerprint(@"C:\Games\Title\other.exe");
        (await games.ChangeExecutableAsync(row.Id, other, DateTimeOffset.UtcNow, Ct)).Should().BeTrue();
        (await consent.FindAsync(other.ExePath, Ct)).GuardBypassAcknowledged.Should().BeFalse("the user overruled a judgement about THAT file, not this one");
    }

    [Fact]
    public async Task ATimestampWithoutItsDisclosureVersionIsNotAnAcknowledgement()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var games = new SqliteGameRepository(f.Db);
        GameRow row = await games.EnsureAsync(Fingerprint(), "Title", Ct);
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET guard_bypass_at = 5 WHERE id = @id", new { id = row.Id }, tx, cancellationToken: ct)), Ct);

        GameConsentRecord record = await new SqliteGameConsentStore(f.Db).FindAsync(Fingerprint().ExePath, Ct);

        record.GuardBypassAt.Should().NotBeNull();
        record.GuardBypassAcknowledged.Should().BeFalse("a bare timestamp is a column somebody set, not a disclosure somebody accepted");
    }
}
