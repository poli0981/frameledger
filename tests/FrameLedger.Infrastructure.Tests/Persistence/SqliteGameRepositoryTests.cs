using System.Text.Json;
using Dapper;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// The <c>games</c> adapter (untested since P2 PR-B; P3 PR-3): hooking off on creation, the UI's metadata
/// write with its provenance rule, the FR-8.3 defaults, and FR-1.4's two removals.
/// </summary>
public sealed class SqliteGameRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ExecutableFingerprint _exe = new() { ExePath = @"C:\Games\T\t.exe", SizeBytes = 10, MtimeUnixMs = 20 };

    /// <summary>
    /// The Agent's detection write (P4 PR-1) under the provenance rule of <c>05_DETECTION</c> §Caching: an empty field
    /// is filled and badged <c>detected</c>; a <c>detected</c> field is refreshed; a <c>user</c> field is never touched;
    /// a null detected value erases nothing; the flags and the cache key are always written — and the consent
    /// fingerprint (<c>exe_size_bytes</c> / <c>exe_mtime_ms</c>) is not, because the gate reads it.
    /// </summary>
    [Fact]
    public async Task ADetectionWriteFillsEmptyFieldsRefreshesDetectedOnesAndNeverTouchesTheUsers()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "Title", Ct);

        (await repo.ApplyDetectionAsync(row.Id, new DetectionWrite
        {
            EngineId = "unity",
            EngineVersion = "2022.3.1",
            PlatformId = "steam",
            CapabilityIds = ["dlss", "dlss_g", "streamline"],
            RulesVersion = "2026.09.1",
            ExeSizeBytes = 777,
            ExeMtimeMs = 888,
        }, Ct)).Should().BeTrue();

        GameRow first = (await repo.FindByIdAsync(row.Id, Ct))!;
        first.Engine.Should().Be("unity");
        first.EngineVersion.Should().Be("2022.3.1");
        first.Platform.Should().Be("steam", "the default 'none' is an empty field with nothing to protect");
        first.CapabilityFlagsJson.Should().Be("[\"dlss\",\"dlss_g\",\"streamline\"]");
        first.DetectionRulesVersion.Should().Be("2026.09.1");
        first.DetectionExeSizeBytes.Should().Be(777);
        first.DetectionExeMtimeMs.Should().Be(888);
        first.Fingerprint.SizeBytes.Should().Be(10, "the consent fingerprint is the gate's, never the sweep's");
        first.Fingerprint.MtimeUnixMs.Should().Be(20);
        JsonSerializer.Deserialize<Dictionary<string, string>>(first.FieldProvenanceJson!).Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["engine"] = "detected",
            ["engine_version"] = "detected",
            ["platform"] = "detected",
        });

        // The user corrects the engine; the platform stays detected.
        (await repo.UpdateMetadataAsync(row.Id, new GameMetadata { Name = "Title", Platform = "steam", Engine = "Unreal Engine", EngineVersion = "2022.3.1" }, Ct)).Should().BeTrue();

        // A re-run with a newer answer refreshes what detection owns and leaves the user's correction alone;
        // a null engine version erases nothing; an empty capability list clears the flags.
        (await repo.ApplyDetectionAsync(row.Id, new DetectionWrite
        {
            EngineId = "godot",
            EngineVersion = null,
            PlatformId = "gog",
            CapabilityIds = [],
            RulesVersion = "2026.10.1",
            ExeSizeBytes = 777,
            ExeMtimeMs = 888,
        }, Ct)).Should().BeTrue();

        GameRow second = (await repo.FindByIdAsync(row.Id, Ct))!;
        second.Engine.Should().Be("Unreal Engine", "a user field is never overwritten — the rule 05_DETECTION §Caching exists for");
        second.EngineVersion.Should().Be("2022.3.1", "null detected = not established = left alone");
        second.Platform.Should().Be("gog", "a detected field is refreshed");
        second.CapabilityFlagsJson.Should().Be("[]");
        second.DetectionRulesVersion.Should().Be("2026.10.1");
        JsonSerializer.Deserialize<Dictionary<string, string>>(second.FieldProvenanceJson!)!["engine"].Should().Be("user");

        (await repo.ApplyDetectionAsync(row.Id + 99, new DetectionWrite { CapabilityIds = [], RulesVersion = "v", ExeSizeBytes = 0, ExeMtimeMs = 0 }, Ct)).Should().BeFalse();
    }

    /// <summary>The import's write (P4 PR-4) under the same provenance rule: a store fills empty fields and badges them; a user's platform stays; a null store value erases nothing; the hook columns are untouched.</summary>
    [Fact]
    public async Task AStoreWriteFillsAndBadgesButNeverOverwritesTheUser()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);

        (await repo.ApplyStoreMetadataAsync(row.Id, new StoreMetadata { Platform = "steam", StoreId = "1091500", GameVersion = "15877371" }, Ct)).Should().BeTrue();
        GameRow first = (await repo.FindByIdAsync(row.Id, Ct))!;
        first.Platform.Should().Be("steam");
        first.StoreId.Should().Be("1091500");
        first.GameVersion.Should().Be("15877371");
        first.HookEnabled.Should().BeFalse("import never enables hooking");
        JsonSerializer.Deserialize<Dictionary<string, string>>(first.FieldProvenanceJson!).Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["platform"] = "detected",
            ["store_id"] = "detected",
            ["game_version"] = "detected",
        });

        (await repo.UpdateMetadataAsync(row.Id, new GameMetadata { Name = "T", Platform = "gog", StoreId = "1091500", GameVersion = "15877371" }, Ct)).Should().BeTrue();
        (await repo.ApplyStoreMetadataAsync(row.Id, new StoreMetadata { Platform = "epic", StoreId = "e1", GameVersion = null }, Ct)).Should().BeTrue();
        GameRow second = (await repo.FindByIdAsync(row.Id, Ct))!;
        second.Platform.Should().Be("gog", "the user chose it");
        second.StoreId.Should().Be("e1", "still badged detected, so refreshed");
        second.GameVersion.Should().Be("15877371", "null erases nothing");

        Func<Task> notAStore = async () => await repo.ApplyStoreMetadataAsync(row.Id, new StoreMetadata { Platform = "none" }, Ct).ConfigureAwait(true);
        await notAStore.Should().ThrowAsync<ArgumentException>();
        (await repo.ApplyStoreMetadataAsync(row.Id + 99, new StoreMetadata { Platform = "steam" }, Ct)).Should().BeFalse();
    }

    private static async Task<long> AddSessionAsync(LedgerFixture f, long gameId)
    {
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct).ConfigureAwait(false);
        return await new SqliteSessionRepository(f.Db).InsertFinalizedAsync(new FinalizedSession
        {
            Row = new SessionRow
            {
                SessionGuid = Guid.NewGuid(),
                GameId = gameId,
                SnapshotId = snapshotId,
                StartedAt = DateTimeOffset.UnixEpoch,
                EndedAt = DateTimeOffset.UnixEpoch.AddSeconds(60),
                QpcEpoch = 0,
                QpcFrequency = 1,
                Tier = CaptureTier.NotHooked,
                Mode = CaptureMode.Attach,
                ExitStatus = ExitStatus.Normal,
            },
        }, Ct).ConfigureAwait(false);
    }

    /// <summary>The recording switch (schema 0009, 2026-09-23): on for a new entry; the UI's write turns it; the entry stays in the library.</summary>
    [Fact]
    public async Task TheRecordingSwitchIsOnForANewEntryAndTheUiTurnsIt()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        row.RecordSessions.Should().BeTrue();

        (await repo.SetRecordingAsync(row.Id, record: false, Ct)).Should().BeTrue();

        (await repo.FindByIdAsync(row.Id, Ct))!.RecordSessions.Should().BeFalse();
        (await repo.ListAsync(Ct)).Should().ContainSingle("still in the library, just not watched").Which.RecordSessions.Should().BeFalse();
        (await repo.SetRecordingAsync(row.Id, record: true, Ct)).Should().BeTrue();
        (await repo.FindByIdAsync(row.Id, Ct))!.RecordSessions.Should().BeTrue();
        (await repo.SetRecordingAsync(row.Id + 99, record: false, Ct)).Should().BeFalse("no such row");
    }

    [Fact]
    public async Task ANewGameIsHookingOffWithEveryDefaultAndEnsureIsIdempotent()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);

        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        row.HookEnabled.Should().BeFalse("CLAUDE.md rule 1");
        row.HookPrescanState.Should().Be("not_run");
        row.Platform.Should().Be("none");
        row.RtDefault.Should().Be(Tri.NotApplicable);
        row.InLibrary.Should().BeTrue();
        row.FieldProvenanceJson.Should().BeNull();

        (await repo.EnsureAsync(_exe, "Other name", Ct)).Id.Should().Be(row.Id, "the path is the identity");
        (await repo.FindByIdAsync(row.Id, Ct))!.Name.Should().Be("T");
        (await repo.FindByIdAsync(row.Id + 99, Ct)).Should().BeNull();
        (await repo.ListAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task ChangingTheExecutableLeavesARowAsANewGameWouldBeAndNeverClearsABlock()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        // The state a wrong import guess ends in once the user enabled hooking on it, plus a block to prove it survives.
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET hook_enabled = 1, hook_consent_at = 5, hook_consent_provenance = 'ConsentDialog', hook_consent_disclosure_version = 'v', "
            + "hook_prescan_state = 'clean', hook_blocked_reason = 'a block' WHERE id = @id",
            new { id = row.Id }, tx, cancellationToken: ct)), Ct);
        var real = new ExecutableFingerprint { ExePath = @"C:\Games\T\GF2_Exilium.exe", SizeBytes = 675_392, MtimeUnixMs = 30 };

        (await repo.ChangeExecutableAsync(row.Id, real, DateTimeOffset.FromUnixTimeMilliseconds(99), Ct)).Should().BeTrue();

        GameRow after = (await repo.FindByIdAsync(row.Id, Ct))!;
        after.Fingerprint.Should().Be(real);
        after.HookEnabled.Should().BeFalse("everything downstream is keyed on the executable; a consent is about the one it was given for");
        after.HookConsentAt.Should().BeNull();
        after.HookPrescanState.Should().Be("not_run");
        after.HookBlockedReason.Should().Be("a block", "nothing clears a block, and pointing the row elsewhere is not a way round that");
        (await repo.FindAsync(_exe.ExePath, Ct)).Should().BeNull("the row moved; it was not copied");
    }

    /// <summary>
    /// A moved drive (2026-09-22): the row follows the same bytes with everything kept, and the write itself refuses a
    /// different size or mtime, a removed row, and a path another row owns.
    /// </summary>
    [Fact]
    public async Task RelocatingTheExecutableKeepsEverythingAndRequiresTheSameBytes()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET hook_enabled = 1, hook_consent_at = 5, hook_consent_provenance = 'ConsentDialog', hook_consent_disclosure_version = 'v', "
            + "hook_prescan_state = 'clean', hook_blocked_reason = 'a block', detection_rules_version = '2026.09.1' WHERE id = @id",
            new { id = row.Id }, tx, cancellationToken: ct)), Ct);
        var moved = _exe with { ExePath = @"H:\Games\T\t.exe" };

        (await repo.RelocateExecutableAsync(row.Id, _exe with { ExePath = @"H:\Games\T\t.exe", SizeBytes = 11 }, DateTimeOffset.UtcNow, Ct))
            .Should().BeFalse("a different size is a different binary, and the write says so itself");
        (await repo.RelocateExecutableAsync(row.Id, moved, DateTimeOffset.FromUnixTimeMilliseconds(99), Ct)).Should().BeTrue();

        GameRow after = (await repo.FindByIdAsync(row.Id, Ct))!;
        after.Fingerprint.Should().Be(moved);
        after.HookEnabled.Should().BeTrue("the same executable: the consent is about it, wherever it lives");
        after.HookConsentAt.Should().NotBeNull();
        after.HookPrescanState.Should().Be("clean");
        after.HookBlockedReason.Should().Be("a block");
        after.DetectionRulesVersion.Should().Be("2026.09.1");
        (await repo.FindAsync(_exe.ExePath, Ct)).Should().BeNull("the row moved; it was not copied");
        GameConsentRecord consent = await new SqliteGameConsentStore(f.Db).FindAsync(moved.ExePath, Ct);
        consent.IsFromStore.Should().BeTrue("the consent store is keyed on the same row, so it follows");
        consent.Fingerprint.Matches(moved).Should().BeTrue();

        GameRow other = await repo.EnsureAsync(new ExecutableFingerprint { ExePath = @"C:\Games\O\o.exe", SizeBytes = 10, MtimeUnixMs = 20 }, "O", Ct);
        (await repo.RelocateExecutableAsync(other.Id, moved, DateTimeOffset.UtcNow, Ct)).Should().BeFalse("exe_path is UNIQUE: one executable, one row");
        await repo.RemoveAsync(other.Id, keepSessions: true, Ct);
        (await repo.RelocateExecutableAsync(other.Id, new ExecutableFingerprint { ExePath = @"H:\Games\O\o.exe", SizeBytes = 10, MtimeUnixMs = 20 }, DateTimeOffset.UtcNow, Ct))
            .Should().BeFalse("a removed row cannot be moved");
    }

    [Fact]
    public async Task ChangingTheExecutableToOneAnotherRowOwnsIsRefused()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow wrong = await repo.EnsureAsync(_exe, "T", Ct);
        var real = new ExecutableFingerprint { ExePath = @"C:\Games\T\real.exe", SizeBytes = 1, MtimeUnixMs = 2 };
        GameRow other = await repo.EnsureAsync(real, "T (added by hand)", Ct);

        (await repo.ChangeExecutableAsync(wrong.Id, real, DateTimeOffset.UtcNow, Ct)).Should().BeFalse("exe_path is UNIQUE: one executable, one row");
        (await repo.FindByIdAsync(wrong.Id, Ct))!.Fingerprint.Should().Be(_exe);

        // A row removed with its sessions kept still owns its path, and a removed row cannot be re-pointed.
        await repo.RemoveAsync(other.Id, keepSessions: true, Ct);
        (await repo.ChangeExecutableAsync(wrong.Id, real, DateTimeOffset.UtcNow, Ct)).Should().BeFalse();
        (await repo.ChangeExecutableAsync(other.Id, new ExecutableFingerprint { ExePath = @"C:\x.exe", SizeBytes = 1, MtimeUnixMs = 1 }, DateTimeOffset.UtcNow, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task AUserEditMarksOnlyTheChangedFieldsAsUserAndTouchesNoHookColumn()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        // A detection pass (P4) had badged two fields; the row's hook state is the consent store's.
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET engine = 'Unreal', publisher = 'Pub', field_provenance = '{\"engine\":\"detected\",\"publisher\":\"detected\"}', hook_prescan_state = 'clean' WHERE id = @id",
            new { id = row.Id }, tx, cancellationToken: ct)), Ct);

        bool ok = await repo.UpdateMetadataAsync(row.Id, new GameMetadata { Name = "Title", Platform = "steam", StoreId = "123", Engine = "Unreal", Publisher = "Real Pub", Notes = "n" }, Ct);
        ok.Should().BeTrue();

        GameRow after = (await repo.FindByIdAsync(row.Id, Ct))!;
        after.Name.Should().Be("Title");
        after.Platform.Should().Be("steam");
        after.StoreId.Should().Be("123");
        after.Publisher.Should().Be("Real Pub");
        after.Notes.Should().Be("n");
        after.HookPrescanState.Should().Be("clean", "the UI's write never touches a hook-state column");
        after.HookEnabled.Should().BeFalse();
        JsonSerializer.Deserialize<Dictionary<string, string>>(after.FieldProvenanceJson!).Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["engine"] = "detected",   // unchanged: keeps its badge
            ["publisher"] = "user",    // changed: the user's now, and detection must not overwrite it
            ["name"] = "user",
            ["platform"] = "user",
            ["store_id"] = "user",
        });

        (await repo.UpdateMetadataAsync(row.Id + 99, new GameMetadata { Name = "x" }, Ct)).Should().BeFalse();
        Func<Task> badPlatform = async () => await repo.UpdateMetadataAsync(row.Id, new GameMetadata { Name = "x", Platform = "origin" }, Ct).ConfigureAwait(true);
        await badPlatform.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TheTriStateDefaultsAreWrittenPerKind()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);

        (await repo.SetTriStateDefaultAsync(row.Id, TriStateKind.PathTracing, Tri.Yes, Ct)).Should().BeTrue();
        (await repo.SetTriStateDefaultAsync(row.Id, TriStateKind.RayReconstruction, Tri.No, Ct)).Should().BeTrue();
        GameRow after = (await repo.FindByIdAsync(row.Id, Ct))!;
        (after.RtDefault, after.PtDefault, after.RrDefault).Should().Be((Tri.NotApplicable, Tri.Yes, Tri.No));
        (await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string>(new CommandDefinition("SELECT pt_default FROM games", cancellationToken: ct)), Ct)).Should().Be("yes");
    }

    [Fact]
    public async Task RemovingWithSessionsKeptHidesTheGameKeepsItsRowsAndALaunchRestoresIt()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        long sessionId = await AddSessionAsync(f, row.Id);

        (await repo.RemoveAsync(row.Id, keepSessions: true, Ct)).Should().BeTrue();
        (await repo.ListAsync(Ct)).Should().BeEmpty("removed from the library");
        (await repo.FindByIdAsync(row.Id, Ct))!.InLibrary.Should().BeFalse();
        (await new SqliteSessionRepository(f.Db).FindByIdAsync(sessionId, Ct)).Should().NotBeNull("the sessions were kept");
        (await repo.RemoveAsync(row.Id, keepSessions: true, Ct)).Should().BeFalse("already removed");

        GameRow back = await repo.EnsureAsync(_exe, "T", Ct);
        back.Id.Should().Be(row.Id);
        back.InLibrary.Should().BeTrue("the watcher saw it run again");
        (await repo.ListAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task RemovingWithSessionsDeletedCascadesEverythingUnderTheGame()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        long sessionId = await AddSessionAsync(f, row.Id);
        await new SqliteSessionAnnotationRepository(f.Db).UpsertAsync(new SessionAnnotation { SessionId = sessionId, Notes = "bye" }, Ct);

        (await repo.RemoveAsync(row.Id, keepSessions: false, Ct)).Should().BeTrue();
        (await repo.FindByIdAsync(row.Id, Ct)).Should().BeNull();
        (await new SqliteSessionRepository(f.Db).FindByIdAsync(sessionId, Ct)).Should().BeNull();
        (await new SqliteSessionAnnotationRepository(f.Db).FindAsync(sessionId, Ct)).Should().BeNull();
        (await repo.RemoveAsync(row.Id, keepSessions: false, Ct)).Should().BeFalse();
    }
}
