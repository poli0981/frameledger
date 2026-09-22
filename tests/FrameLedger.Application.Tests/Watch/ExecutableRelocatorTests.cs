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
    public async Task AFileStillAtTheRowsPathADifferentSizeOrTwoCandidatesMoveNothing()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, List<string> log, GameRow row) = await BuildAsync(@"C:\", @"D:\", @"H:\");

        // Still where the row says: nothing to do, whatever the other drives hold.
        disk.Put(_stored);
        disk.Put(@"H:\SteamLibrary\steamapps\common\Title\game.exe");
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();

        // Gone from the row's path, and the candidate is a different binary: the size is the consent's, not the path's.
        disk.Files.Remove(_stored);
        disk.Put(@"H:\SteamLibrary\steamapps\common\Title\game.exe", size: 501);
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();

        // Two drives hold the same bytes: an ambiguity, and ambiguity refuses.
        disk.Put(@"H:\SteamLibrary\steamapps\common\Title\game.exe");
        disk.Put(@"C:\SteamLibrary\steamapps\common\Title\game.exe");
        (await relocator.TryRelocateAsync(row, Ct)).Should().BeNull();
        log.Should().ContainSingle().Which.Should().Contain("2 drives hold");
        games.Rows.Should().ContainKey(_stored, "nothing moved");
        games.Relocations.Should().BeEmpty();
    }

    [Fact]
    public async Task TheWatchersFileNameMatchAdoptsTheMovedFileAndNotACopy()
    {
        (ExecutableRelocator relocator, FakeGameRepository games, DiskByPath disk, _, GameRow row) = await BuildAsync(@"C:\", @"D:\");
        const string running = @"H:\SteamLibrary\steamapps\common\Title\game.exe";

        // Both files present: a second copy, and the running one stays a stranger.
        disk.Put(_stored);
        disk.Put(running);
        (await relocator.TryAdoptAsync(row, running, Ct)).Should().BeFalse();

        // The row's file is gone and the running one has its bytes: the row follows it, whatever the drive list says.
        disk.Files.Remove(_stored);
        (await relocator.TryAdoptAsync(row, running, Ct)).Should().BeTrue();
        games.Rows.Should().ContainKey(running).And.NotContainKey(_stored);

        // A different binary under the row's name is not adopted.
        GameRow other = await games.EnsureAsync(new ExecutableFingerprint { ExePath = @"D:\Other\game.exe", SizeBytes = 7, MtimeUnixMs = 7 }, "Other", Ct);
        disk.Put(@"H:\Other\game.exe", size: 8);
        (await relocator.TryAdoptAsync(other, @"H:\Other\game.exe", Ct)).Should().BeFalse();
    }
}
