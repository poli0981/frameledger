using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// A drive that changed its letter is the same executable (2026-09-22; <c>19_SAFETY</c> §A moved drive is the same
/// executable): the row follows the file when — and only when — the file is gone from the row's path and exactly one
/// other root holds it with the same size and mtime.
/// </summary>
public sealed class ExecutableRelocatorTests
{
    private const string _stored = @"D:\SteamLibrary\steamapps\common\Title\game.exe";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class DiskByPath : IExecutableIdentitySource
    {
        public Dictionary<string, ExecutableFingerprint> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ExecutableFingerprint? Read(string normalisedExePath) => Files.TryGetValue(normalisedExePath, out ExecutableFingerprint f) ? f : null;

        public string Normalise(string exePath) => exePath;

        public void Put(string path, long size = 500, long mtime = 1_700_000_000_000) =>
            Files[path] = new ExecutableFingerprint { ExePath = path, SizeBytes = size, MtimeUnixMs = mtime };
    }

    private static async Task<(ExecutableRelocator Relocator, FakeGameRepository Games, DiskByPath Disk, List<string> Log, GameRow Row)> BuildAsync(params string[] roots)
    {
        var games = new FakeGameRepository();
        GameRow row = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _stored, SizeBytes = 500, MtimeUnixMs = 1_700_000_000_000 }, "Title", Ct).ConfigureAwait(false);
        var disk = new DiskByPath();
        List<string> log = [];
        return (new ExecutableRelocator(games, disk, () => roots, log.Add), games, disk, log, row);
    }

    /// <summary>The relocator with a merge (2026-09-23): the stale entry at <c>D:</c>, added first, and its twin at <c>H:</c>, added later.</summary>
    private sealed record Twins(ExecutableRelocator Relocator, FakeGameRepository Games, FakeGameMerge Merge, DiskByPath Disk, List<string> Log, GameRow Stale, GameRow Owner);

    private const string _twin = @"H:\SteamLibrary\steamapps\common\Title\game.exe";

    private static async Task<Twins> TwinsAsync(bool staleFirst = true, Func<long, bool>? busy = null, long ownerSize = 500)
    {
        var games = new FakeGameRepository();
        GameRow stale = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _stored, SizeBytes = 500, MtimeUnixMs = 1_700_000_000_000 }, "Title", Ct).ConfigureAwait(false);
        GameRow owner = await games.EnsureAsync(new ExecutableFingerprint { ExePath = _twin, SizeBytes = ownerSize, MtimeUnixMs = 1_700_000_000_000 }, "Title (H:)", Ct).ConfigureAwait(false);
        DateTimeOffset early = DateTimeOffset.Parse("2026-09-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset late = DateTimeOffset.Parse("2026-09-22T09:39:00Z", System.Globalization.CultureInfo.InvariantCulture);
        games.Rows[_stored] = stale = stale with { AddedAt = staleFirst ? early : late, HookEnabled = true };
        games.Rows[_twin] = owner = owner with { AddedAt = staleFirst ? late : early };
        var disk = new DiskByPath();
        disk.Put(_twin, size: ownerSize);
        var merge = new FakeGameMerge(games);
        List<string> log = [];
        return new Twins(new ExecutableRelocator(games, disk, static () => [@"C:\", @"D:\", @"H:\"], log.Add, merge: merge, busy: busy), games, merge, disk, log, stale, owner);
    }

    [Fact]
    public void TheCandidatesAreTheSamePathUnderEveryOtherDriveLetter()
    {
        ExecutableRelocator.Candidates(_stored, [@"C:\", @"D:\", @"H:\"]).Should().Equal(
            @"C:\SteamLibrary\steamapps\common\Title\game.exe", @"H:\SteamLibrary\steamapps\common\Title\game.exe");
        ExecutableRelocator.Candidates(@"\\nas\games\Title\game.exe", [@"C:\", @"H:\"]).Should().BeEmpty("a UNC path has no drive letter to change");
        ExecutableRelocator.Candidates(_stored, []).Should().BeEmpty();
    }

    [Fact]
    public async Task TheRowFollowsTheFileToTheOneRootThatHoldsTheSameBytes()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, List<string> log, GameRow row) = await BuildAsync(@"C:\", @"D:\", @"H:\");
        disk.Put(@"H:\SteamLibrary\steamapps\common\Title\game.exe");

        ExecutableFingerprint? moved = await relocator.TryRelocateAsync(row, Ct);

        moved.Should().NotBeNull();
        moved!.Value.ExePath.Should().Be(@"H:\SteamLibrary\steamapps\common\Title\game.exe");
        games.Rows.Should().ContainKey(moved.Value.ExePath).And.NotContainKey(_stored, "the row moved; it was not copied");
        games.Rows[moved.Value.ExePath].Id.Should().Be(row.Id, "the same row: its consent, its block, its sessions");
        log.Should().ContainSingle().Which.Should().Contain("executable moved").And.Contain("consent and block kept");
    }

    [Fact]
    public async Task AFileStillAtTheRowsPathOrTwoCandidatesMoveNothing()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, List<string> log, GameRow row) = await BuildAsync(@"C:\", @"D:\", @"H:\");

        // Still where the row says: nothing to do, whatever the other drives hold.
        disk.Put(_stored);
        disk.Put(@"H:\SteamLibrary\steamapps\common\Title\game.exe");
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();

        // Two drives hold the same bytes: an ambiguity, and ambiguity refuses.
        disk.Files.Remove(_stored);
        disk.Put(@"C:\SteamLibrary\steamapps\common\Title\game.exe");
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();

        // Two drives hold other bytes: the same ambiguity — D27 follows one changed file, never a guess between two.
        disk.Put(@"H:\SteamLibrary\steamapps\common\Title\game.exe", size: 501);
        disk.Put(@"C:\SteamLibrary\steamapps\common\Title\game.exe", size: 502);
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();

        log.Should().HaveCount(2).And.AllSatisfy(static l => l.Should().Contain("2 drives hold"));
        games.Rows.Should().ContainKey(_stored, "nothing moved");
        games.Relocations.Should().BeEmpty();
        games.Changes.Should().BeEmpty();
    }

    /// <summary>
    /// D27 (owner, 2026-09-23): the row's file is gone and the only file at its path under another letter has other bytes —
    /// the game was updated while the drive had another letter. The row follows it as <i>Change executable</i> does:
    /// hooking off, consent cleared. Until this date it stayed "unreadable" for good.
    /// </summary>
    [Fact]
    public async Task AnUpdatedExecutableOnTheOtherLetterIsFollowedWithHookingOff()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, List<string> log, GameRow row) = await BuildAsync(@"C:\", @"D:\", @"H:\");
        games.Rows[_stored] = row = row with { HookEnabled = true, HookConsentAt = DateTimeOffset.UnixEpoch, HookPrescanState = "clean", HookBlockedReason = "a block" };
        disk.Put(_twin, size: 501);

        ExecutableFingerprint? followed = await relocator.TryRelocateAsync(row, Ct);

        followed!.Value.ExePath.Should().Be(_twin);
        GameRow after = games.Rows[_twin];
        after.Id.Should().Be(row.Id, "the same entry, pointed at the file");
        after.Fingerprint.SizeBytes.Should().Be(501);
        after.HookEnabled.Should().BeFalse("other bytes are not the binary the consent was given for");
        after.HookConsentAt.Should().BeNull();
        after.HookBlockedReason.Should().Be("a block", "nothing clears a block");
        games.Relocations.Should().BeEmpty("a move keeps consent; this is a change, which does not");
        log.Should().ContainSingle().Which.Should().Contain("hooking OFF");
    }

    [Fact]
    public async Task TheWatcherFollowsAnUpdatedExecutableWithHookingOffToo()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, _, GameRow row) = await BuildAsync(@"C:\", @"D:\");
        games.Rows[_stored] = row = row with { HookEnabled = true };
        disk.Put(_twin, size: 501);

        (await relocator.TryAdoptAsync(row, _twin, Ct)).Should().BeTrue("the only file at the row's path, on the letter it runs from");

        games.Rows[_twin].HookEnabled.Should().BeFalse();
        games.Changes.Should().ContainSingle();
    }

    /// <summary>
    /// D26 (owner, 2026-09-23): the owner's library showed an <c>H:</c> twin beside each game on the re-lettered drive, and
    /// once the letter was back neither could move onto the other's path — "could not be moved", every 15 s. Two entries
    /// for the same bytes under two letters become one: the entry made first stays, at the file.
    /// </summary>
    [Fact]
    public async Task TwinsWithTheSameBytesAreMergedAndTheEntryMadeFirstStays()
    {
        Twins t = await TwinsAsync(staleFirst: true);

        ExecutableFingerprint? now = await t.Relocator.TryRelocateAsync(t.Stale, Ct);

        GameMergePlan plan = t.Merge.Plans.Should().ContainSingle().Subject;
        plan.SurvivorId.Should().Be(t.Stale.Id, "added first");
        plan.DroppedId.Should().Be(t.Owner.Id);
        plan.SurvivorMoves.Should().BeTrue();
        now!.Value.ExePath.Should().Be(_twin, "the surviving entry lives at the file now");
        t.Games.Rows.Should().ContainSingle().Which.Value.Id.Should().Be(t.Stale.Id);
        t.Log.Should().ContainSingle().Which.Should().Contain("merged").And.Contain("'Title' stays");
    }

    [Fact]
    public async Task WhenTheTwinWasMadeFirstItStaysAndTheStaleEntryIsGone()
    {
        Twins t = await TwinsAsync(staleFirst: false);

        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull("the stale entry is not an entry any more");

        t.Merge.Plans.Should().ContainSingle().Which.SurvivorId.Should().Be(t.Owner.Id);
        t.Games.Rows.Should().ContainSingle().Which.Value.Id.Should().Be(t.Owner.Id);
    }

    [Fact]
    public async Task AMergeWaitsWhileASessionOfEitherEntryRunsOrWaitsForRecoveryAndSaysSoOnce()
    {
        long busyId = 0;
        Twins t = await TwinsAsync(busy: id => id == busyId);
        busyId = t.Owner.Id;

        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();
        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();

        t.Merge.Plans.Should().BeEmpty("a session re-keyed under a deleted entry would be dropped");
        t.Log.Should().ContainSingle().Which.Should().Contain("merged once no session");

        busyId = 0;
        await t.Relocator.TryRelocateAsync(t.Stale, Ct);
        t.Merge.Plans.Should().ContainSingle("the next pass merges once nothing runs");
    }

    [Fact]
    public async Task TwinsWithDifferentBytesAreNotMergedAndTheLogSaysWhatToDo()
    {
        Twins t = await TwinsAsync(ownerSize: 501);

        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();
        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();

        t.Merge.Plans.Should().BeEmpty("D26 merges the same bytes: other bytes do not prove the other entry is this game");
        t.Games.Rows.Should().HaveCount(2);
        t.Log.Should().ContainSingle().Which.Should().Contain("not merged").And.Contain("remove the entry you do not want");
    }

    [Fact]
    public async Task AMergeThatFoundARowChangedIsSaidOnceAndTriedAgainNextPass()
    {
        Twins t = await TwinsAsync();
        t.Merge.Refuse = true;

        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();
        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();

        t.Merge.Plans.Should().HaveCount(2, "asked again on every pass");
        t.Log.Should().ContainSingle().Which.Should().Contain("changed underneath");
    }

    [Fact]
    public async Task AnEntryRemovedFromTheLibraryIsNotMergedWith()
    {
        Twins t = await TwinsAsync();
        t.Games.Rows[_twin] = t.Owner with { RemovedAt = DateTimeOffset.UnixEpoch };

        (await t.Relocator.TryRelocateAsync(t.Stale, Ct)).Should().BeNull();

        t.Merge.Plans.Should().BeEmpty("the user removed it; a merge must not bring its sessions back");
        t.Log.Should().ContainSingle().Which.Should().Contain("could not be moved");
    }

    [Fact]
    public async Task TheWatchersFileNameMatchAdoptsTheMovedFileAndNotACopy()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, _, GameRow row) = await BuildAsync(@"C:\", @"D:\");
        const string running = @"H:\SteamLibrary\steamapps\common\Title\game.exe";

        // Both files present: a second copy, and the running one is not the row's.
        disk.Put(_stored);
        disk.Put(running);
        (await relocator.TryAdoptAsync(row, running, Ct)).Should().BeFalse();

        // The row's file is gone and the running one has its bytes under the same path on another letter: the row follows
        // it, even though the drive list has not caught up with H: — the running file's own drive is mounted by definition.
        disk.Files.Remove(_stored);
        (await relocator.TryAdoptAsync(row, running, Ct)).Should().BeTrue();
        games.Rows.Should().ContainKey(running).And.NotContainKey(_stored);

        // A different binary at another folder's path is not adopted; the same path with other bytes is D27's (its own test).
        GameRow other = await games.EnsureAsync(new ExecutableFingerprint { ExePath = @"D:\\Other\\game.exe", SizeBytes = 7, MtimeUnixMs = 7 }, "Other", Ct);
        disk.Put(@"H:\Elsewhere\game.exe", size: 8);
        (await relocator.TryAdoptAsync(other, @"H:\Elsewhere\game.exe", Ct)).Should().BeFalse();
    }

    /// <summary>
    /// 2026-09-23: every RPG Maker MV game ships the same NW.js <c>Game.exe</c>, byte for byte, and an unzip keeps its
    /// mtime. The adopt used to accept ANY running path with the row's size and mtime, so an entry whose drive was
    /// unplugged could have followed — consent and all — another game's <c>Game.exe</c>. Only the drive letter may differ.
    /// </summary>
    [Fact]
    public async Task IdenticalBytesInAnotherFolderAreAnotherGameAndAreNotAdopted()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, _, GameRow row) = await BuildAsync(@"C:\", @"D:\", @"H:\");
        const string anotherGame = @"D:\another\it\hello-hello-world\HELLO, HELLO WORLD!\swiftshader\game.exe";
        disk.Put(anotherGame);

        (await relocator.TryAdoptAsync(row, anotherGame, Ct)).Should().BeFalse("the same bytes in another folder are another game");
        games.Rows.Should().ContainKey(_stored, "the row did not move");
        games.Relocations.Should().BeEmpty();
    }

    [Fact]
    public async Task TwoDrivesHoldingTheFileAreAnAmbiguityForTheWatcherTooAndItIsSaidOnce()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, List<string> log, GameRow row) = await BuildAsync(@"C:\", @"D:\", @"G:\", @"H:\");
        const string running = @"H:\SteamLibrary\steamapps\common\Title\game.exe";
        disk.Put(running);
        disk.Put(@"G:\SteamLibrary\steamapps\common\Title\game.exe");

        (await relocator.TryAdoptAsync(row, running, Ct)).Should().BeFalse("two drives hold it; ambiguity refuses, as the sweep's does");
        (await relocator.TryAdoptAsync(row, running, Ct)).Should().BeFalse();
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();

        games.Relocations.Should().BeEmpty();
        log.Should().ContainSingle("the same refusal about the same row is said once, until it changes").Which.Should().Contain("2 drives hold");
    }

    [Theory]
    [InlineData(@"D:\Games\T\game.exe", @"H:\Games\T\game.exe", true)]
    [InlineData(@"D:\Games\T\game.exe", @"h:\games\t\GAME.EXE", true)]
    [InlineData(@"D:\Games\T\game.exe", @"D:\Games\T\game.exe", false)]
    [InlineData(@"D:\Games\T\game.exe", @"H:\Games\Other\game.exe", false)]
    [InlineData(@"D:\Games\T\game.exe", @"\\nas\Games\T\game.exe", false)]
    public void ADriveLetterTwinIsTheSamePathOnAnotherLetter(string a, string b, bool twin)
    {
        ExecutableRelocator.IsDriveLetterTwin(a, b).Should().Be(twin);
        ExecutableRelocator.IsDriveLetterTwin(b, a).Should().Be(twin);
    }
}
