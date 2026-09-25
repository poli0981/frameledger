using Dapper;
using FluentAssertions;
using FrameLedger.Application.Consent;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// Every case the file-backed store carried, re-targeted at the SQLite adapter: the semantics moved
/// unchanged, and these are what say so.
/// </summary>
public sealed class SqliteGameConsentStoreTests
{
    private const string _disclosure = "unshipped-host-disclosure/2";

    private static ExecutableFingerprint Fingerprint(string path = @"C:\Games\Title\game.exe") =>
        new() { ExePath = path, SizeBytes = 90_000, MtimeUnixMs = 1_700_000_000_000 };

    private static OperatorAcknowledgement Ack(ExecutableFingerprint? fp = null) => new()
    {
        Fingerprint = fp ?? Fingerprint(),
        DisclosureVersion = _disclosure,
        AcknowledgedAt = DateTimeOffset.UnixEpoch,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnAbsentRecordIsTheRefusingDefault()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);

        GameConsentRecord r = await store.FindAsync(@"C:\Games\Nothing\here.exe", Ct);

        r.IsFromStore.Should().BeFalse();
        r.HookEnabled.Should().BeFalse();
        r.ConsentedAt.Should().BeNull();
    }

    [Fact]
    public async Task AnAcknowledgementRoundTripsWithItsProvenanceAndDisclosureVersion()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);

        (await store.RecordOperatorAcknowledgementAsync(Ack(), Ct)).Should().Be(ConsentWriteOutcome.Written);
        GameConsentRecord r = await store.FindAsync(Fingerprint().ExePath, Ct);

        r.IsFromStore.Should().BeTrue();
        r.HookEnabled.Should().BeTrue();
        r.ConsentedAt.Should().Be(DateTimeOffset.UnixEpoch);
        r.Provenance.Should().Be(ConsentProvenance.UnshippedHostOperator);
        r.DisclosureVersion.Should().Be(_disclosure);
        r.Fingerprint.SizeBytes.Should().Be(90_000);
        r.PreScanUnverified.Should().BeFalse();

        // unix-ms, stamped from the acknowledgement, on the row itself — not a local time, not "now".
        long stored = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT hook_consent_at FROM games WHERE exe_path = @p", new { p = Fingerprint().ExePath }, cancellationToken: ct)), Ct);
        stored.Should().Be(0);
    }

    [Fact]
    public async Task AFilenameMatchIsNotAConsentMatchButCaseIs()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(Fingerprint(@"C:\a\game.exe")), Ct);

        (await store.FindAsync(@"C:\b\game.exe", Ct)).IsFromStore.Should().BeFalse("a different binary with the same filename must not inherit consent");
        (await store.FindAsync(@"C:\a\game.exe", Ct)).IsFromStore.Should().BeTrue();
        (await store.FindAsync(@"c:\A\GAME.EXE", Ct)).IsFromStore.Should().BeTrue("the UNIQUE index is NOCASE, like the lookup");
        (await store.RecordOperatorAcknowledgementAsync(Ack(Fingerprint(@"c:\A\GAME.EXE")), Ct)).Should().Be(ConsentWriteOutcome.Written);
        (await store.ListEnabledAsync(Ct)).Should().ContainSingle("a re-grant under a different casing updates the one row rather than adding a second");
    }

    [Fact]
    public async Task AGrantCannotClearAnExistingBlock()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordGuardBlockAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll"), Ct);

        (await store.RecordOperatorAcknowledgementAsync(Ack(), Ct)).Should().Be(ConsentWriteOutcome.Written);

        GameConsentRecord r = await store.FindAsync(Fingerprint().ExePath, Ct);
        r.BlockedReason.Should().NotBeNull().And.Contain("BattlEye");
    }

    [Fact]
    public async Task ABlockPreservesTheConsentStampAndForcesTheToggleOff()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        await store.RecordGuardBlockAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatDirectory, "Easy Anti-Cheat", "EasyAntiCheat/"), Ct);

        GameConsentRecord r = await store.FindAsync(Fingerprint().ExePath, Ct);
        r.HookEnabled.Should().BeFalse();
        r.ConsentedAt.Should().Be(DateTimeOffset.UnixEpoch);
        r.Provenance.Should().Be(ConsentProvenance.UnshippedHostOperator);
        r.BlockedReason.Should().Contain("Easy Anti-Cheat");
        string? state = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT hook_prescan_state FROM games WHERE exe_path = @p", new { p = Fingerprint().ExePath }, cancellationToken: ct)), Ct);
        state.Should().Be("blocked");
    }

    [Fact]
    public async Task AnAllowingVerdictCannotRecordABlock()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);

        (await store.RecordGuardBlockAsync(Fingerprint(), AntiCheatVerdict.Allowed(), Ct)).Should().Be(ConsentWriteOutcome.Failed);
        (await store.FindAsync(Fingerprint().ExePath, Ct)).IsFromStore.Should().BeFalse();
    }

    [Fact]
    public async Task ADefaultVerdictRecordsCouldNotVerifyAndNeverABlock()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        await store.RecordGuardBlockAsync(Fingerprint(), default, Ct);

        GameConsentRecord r = await store.FindAsync(Fingerprint().ExePath, Ct);
        r.PreScanUnverified.Should().BeTrue();
        r.BlockedReason.Should().BeNull("a verdict nobody produced is not evidence of anything");
        r.HookEnabled.Should().BeTrue("could-not-verify does not disable the toggle — that would be a refusal with no appeal");
    }

    [Fact]
    public async Task RevokingWithdrawsTheStampSoTheDisclosureIsShownAgain()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        (await store.RevokeAsync(Fingerprint().ExePath, Ct)).Should().Be(ConsentWriteOutcome.Written);

        GameConsentRecord r = await store.FindAsync(Fingerprint().ExePath, Ct);
        r.HookEnabled.Should().BeFalse();
        r.ConsentedAt.Should().BeNull();
        r.Provenance.Should().Be(ConsentProvenance.NotRecorded);
        r.DisclosureVersion.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokingSomethingThatWasNeverGrantedIsNotFound()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (await new SqliteGameConsentStore(f.Db).RevokeAsync(@"C:\Games\Nothing\here.exe", Ct)).Should().Be(ConsentWriteOutcome.NotFound);
    }

    [Fact]
    public async Task ListEnabledReturnsOnlyEnabledGames()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(Fingerprint(@"C:\a\one.exe")), Ct);
        await store.RecordOperatorAcknowledgementAsync(Ack(Fingerprint(@"C:\a\two.exe")), Ct);
        await store.RevokeAsync(@"C:\a\two.exe", Ct);

        IReadOnlyList<GameConsentRecord> enabled = await store.ListEnabledAsync(Ct);

        enabled.Should().ContainSingle().Which.Fingerprint.ExePath.Should().Be(@"C:\a\one.exe");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("42")]
    [InlineData("unshippedhostoperator")]
    [InlineData("consentdialog")]
    [InlineData("ConsentDialogV2")]
    public async Task AProvenanceThatIsNotADeclaredNameReadsAsNotRecorded(string planted)
    {
        // Enum.TryParse parses numbers and ignores case unless told not to; this is the field that decides
        // whether a timestamp counts as consent, so only the declared NAMES, exactly cased, are accepted.
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET hook_consent_provenance = @planted", new { planted }, tx, cancellationToken: ct)), Ct);

        (await store.FindAsync(Fingerprint().ExePath, Ct)).Provenance.Should().Be(ConsentProvenance.NotRecorded);
    }

    [Fact]
    public async Task ARegrantAgainstADifferentBinaryCannotInheritAnExistingBlock()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);
        await store.RecordGuardBlockAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll"), Ct);

        ExecutableFingerprint patched = Fingerprint() with { SizeBytes = 12345 };

        (await store.RecordOperatorAcknowledgementAsync(Ack(patched), Ct)).Should().Be(ConsentWriteOutcome.StaleFingerprint);
        GameConsentRecord r = await store.FindAsync(Fingerprint().ExePath, Ct);
        r.BlockedReason.Should().Contain("BattlEye");
        r.HookEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ARegrantAfterAPatchStillWorksWhenNothingIsBlocked()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        ExecutableFingerprint patched = Fingerprint() with { SizeBytes = 12345 };

        (await store.RecordOperatorAcknowledgementAsync(Ack(patched), Ct)).Should().Be(ConsentWriteOutcome.Written);
        (await store.FindAsync(Fingerprint().ExePath, Ct)).Fingerprint.SizeBytes.Should().Be(12345);
    }

    [Fact]
    public async Task AStoreWhoseTableIsGoneRefusesEveryWriteAndConsentsToNothing()
    {
        // The file store's "unreadable store" cases, in SQLite's terms: a ledger this build cannot read
        // answers Failed on writes and the refusing default on reads — never an exception past the port,
        // never a fresh store silently put in its place.
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition("DROP TABLE games", transaction: tx, cancellationToken: ct)), Ct);

        (await store.FindAsync(Fingerprint().ExePath, Ct)).IsFromStore.Should().BeFalse();
        (await store.ListEnabledAsync(Ct)).Should().BeEmpty();
        (await store.RecordOperatorAcknowledgementAsync(Ack(), Ct)).Should().Be(ConsentWriteOutcome.Failed);
        (await store.RecordGuardBlockAsync(Fingerprint(), default, Ct)).Should().Be(ConsentWriteOutcome.Failed);
        (await store.RevokeAsync(Fingerprint().ExePath, Ct)).Should().Be(ConsentWriteOutcome.Failed);
    }

    [Fact]
    public async Task TheConsentStoreAndTheGameRepositoryReadTheSameRow()
    {
        // One table, two ports: the repository's non-consent face sees the row the store wrote, and cannot
        // change what the store decided.
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        var games = new SqliteGameRepository(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        Application.Persistence.GameRow row = (await games.FindAsync(Fingerprint().ExePath, Ct))!;

        row.HookEnabled.Should().BeTrue();
        row.Name.Should().Be("game");
        row.Fingerprint.Should().Be(Fingerprint());
        (await games.ListAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task TheAgentConsolesAcknowledgementStampsItsOwnProvenanceAndANamelessOneIsRefused()
    {
        // P2 PR-F (decision D4): the acknowledgement names the surface that produced it, and the store
        // writes that NAME. NotRecorded is not a surface, and a stamp with it would be the timestamp-without-
        // disclosure record HookRequest.FromConsent exists to refuse — so the store refuses to mint it.
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        ExecutableFingerprint fp = Fingerprint();

        (await store.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = fp,
            DisclosureVersion = "agent-console-operator/1",
            AcknowledgedAt = DateTimeOffset.UnixEpoch,
            Provenance = ConsentProvenance.AgentConsoleOperator,
        }, Ct)).Should().Be(ConsentWriteOutcome.Written);

        GameConsentRecord record = await store.FindAsync(fp.ExePath, Ct);
        record.Provenance.Should().Be(ConsentProvenance.AgentConsoleOperator);
        record.DisclosureVersion.Should().Be("agent-console-operator/1");
        record.HookEnabled.Should().BeTrue();

        Func<Task> nameless = async () => await store.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = fp,
            DisclosureVersion = "x",
            AcknowledgedAt = DateTimeOffset.UnixEpoch,
            Provenance = ConsentProvenance.NotRecorded,
        }, Ct).ConfigureAwait(false);
        await nameless.Should().ThrowAsync<ArgumentException>().ConfigureAwait(true);
    }

    // --- The Agent's pre-scan of the library (beta.8, schema 0010) ------------------------------------------------------

    private static async Task<(string State, string? Reason, long Enabled, string? Rules, long? Size, long? Mtime, long? ConsentedAt)> RowAsync(
        LedgerFixture f, string path)
    {
        return await f.Db.ReadAsync((c, ct) => c.QuerySingleAsync<(string, string?, long, string?, long?, long?, long?)>(new CommandDefinition(
            "SELECT hook_prescan_state, hook_blocked_reason, hook_enabled, hook_prescan_rules_version, hook_prescan_exe_size_bytes, "
            + "hook_prescan_exe_mtime_ms, hook_consent_at FROM games WHERE exe_path = @p", new { p = path }, cancellationToken: ct)), Ct).ConfigureAwait(false);
    }

    [Fact]
    public async Task APreScanPassIsCleanUnderItsKeyAndLeavesTheConsentAlone()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);
        ExecutableFingerprint scanned = Fingerprint() with { SizeBytes = 91_000, MtimeUnixMs = 1_800_000_000_000 };

        (await store.RecordPreScanAsync(scanned, AntiCheatVerdict.Allowed(), "2026.09.4", Ct)).Should().Be(ConsentWriteOutcome.Written);

        var row = await RowAsync(f, scanned.ExePath);
        row.State.Should().Be("clean", "a game the user never enabled can be known clean now, not only one they did");
        row.Reason.Should().BeNull();
        row.Enabled.Should().Be(1, "a pass changes nothing the user switched on");
        (row.Rules, row.Size, row.Mtime).Should().Be(("2026.09.4", 91_000L, 1_800_000_000_000L));
        row.ConsentedAt.Should().NotBeNull();
        GameConsentRecord r = await store.FindAsync(scanned.ExePath, Ct);
        r.Fingerprint.SizeBytes.Should().Be(90_000, "the consent fingerprint is not the pre-scan's key");
    }

    [Fact]
    public async Task APreScanFindingIsTheBlockInTheStructuredFormAndTheConsentStampSurvives()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        (await store.RecordPreScanAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatDirectory, "Easy Anti-Cheat", "EasyAntiCheat"),
            "2026.09.4", Ct)).Should().Be(ConsentWriteOutcome.Written);

        var row = await RowAsync(f, Fingerprint().ExePath);
        row.State.Should().Be("blocked");
        row.Reason.Should().Be("AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat", "Reason|Family|Signal, so the App can say it in the user's language");
        row.Enabled.Should().Be(0);
        row.ConsentedAt.Should().NotBeNull("a block is not a withdrawal of consent");
        row.Rules.Should().Be("2026.09.4");
    }

    [Fact]
    public async Task APreScanThatCouldNotAnswerIsUnverifiedAndDoesNotDisableTheToggle()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordOperatorAcknowledgementAsync(Ack(), Ct);

        await store.RecordPreScanAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.PreScanFailed, string.Empty, "could not be listed"), "2026.09.4", Ct);

        var row = await RowAsync(f, Fingerprint().ExePath);
        (row.State, row.Reason, row.Enabled).Should().Be(("unverified", (string?)null, 1L));
    }

    [Fact]
    public async Task NothingThePreScanWritesClearsABlockOrAddsARow()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var store = new SqliteGameConsentStore(f.Db);
        await store.RecordGuardBlockAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "BattlEye", "BEClient_x64.dll"), Ct);

        (await store.RecordPreScanAsync(Fingerprint(), AntiCheatVerdict.Allowed(), "2026.09.9", Ct)).Should().Be(ConsentWriteOutcome.NotFound);
        (await store.RecordPreScanAsync(Fingerprint(), AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatFile, "Other", "o.sys"), "2026.09.9", Ct))
            .Should().Be(ConsentWriteOutcome.NotFound, "the first finding stands");
        var row = await RowAsync(f, Fingerprint().ExePath);
        row.State.Should().Be("blocked");
        row.Reason.Should().Be("BlockedModule|BattlEye|BEClient_x64.dll");
        row.Rules.Should().BeNull("a blocked row is not re-keyed either: nothing about it changes");

        (await store.RecordPreScanAsync(Fingerprint(@"C:\Games\Other\other.exe"), AntiCheatVerdict.Allowed(), "2026.09.9", Ct))
            .Should().Be(ConsentWriteOutcome.NotFound, "the library is the App's and the watcher's to add to");
        (await store.FindAsync(@"C:\Games\Other\other.exe", Ct)).IsFromStore.Should().BeFalse();
    }
}
