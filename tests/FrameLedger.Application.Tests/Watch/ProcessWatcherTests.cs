using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>The 1 Hz diff: exact path, unambiguous file-name fallback, unreadable matches nothing, gone is reported once.</summary>
public sealed class ProcessWatcherTests
{
    private static GameRow Game(long id, string path) => new()
    {
        Id = id,
        Name = Path.GetFileNameWithoutExtension(path),
        Fingerprint = new ExecutableFingerprint { ExePath = path, SizeBytes = 1, MtimeUnixMs = 1 },
        HookEnabled = false,
        HookCrashCount = 0,
        AddedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    private static ProcessSnapshot Proc(int pid, string? path, int ppid = 1) =>
        new(pid, ppid, path is null ? "unknown.exe" : Path.GetFileName(path), path, DateTimeOffset.UnixEpoch);

    [Fact]
    public void AnExactPathMatchAppearsOnceAndIsGoneOnce()
    {
        var watcher = new ProcessWatcher();
        GameRow game = Game(1, @"C:\Games\Title\game.exe");
        List<GameRow> watchlist = [game];

        IReadOnlyList<WatchEvent> first = watcher.Poll([Proc(100, @"c:\games\title\GAME.EXE"), Proc(101, @"C:\Windows\explorer.exe")], watchlist);
        IReadOnlyList<WatchEvent> second = watcher.Poll([Proc(100, @"c:\games\title\GAME.EXE")], watchlist);
        IReadOnlyList<WatchEvent> third = watcher.Poll([], watchlist);
        IReadOnlyList<WatchEvent> fourth = watcher.Poll([], watchlist);

        TrackedProcessAppeared appeared = first.Should().ContainSingle().Subject.Should().BeOfType<TrackedProcessAppeared>().Subject;
        appeared.Pid.Should().Be(100);
        appeared.Game.Should().BeSameAs(game);
        appeared.StalePath.Should().BeFalse("the paths differ only in case, which NTFS does not");
        second.Should().BeEmpty("already reported");
        third.Should().ContainSingle().Which.Should().BeOfType<TrackedProcessGone>().Which.Pid.Should().Be(100);
        fourth.Should().BeEmpty();
    }

    [Fact]
    public void AFileNameMatchIsStaleOnlyWhenExactlyOneRowCarriesThatName()
    {
        var watcher = new ProcessWatcher();
        GameRow moved = Game(1, @"D:\Old\game.exe");
        IReadOnlyList<WatchEvent> single = watcher.Poll([Proc(7, @"E:\New\game.exe")], [moved]);

        TrackedProcessAppeared appeared = single.Should().ContainSingle().Subject.Should().BeOfType<TrackedProcessAppeared>().Subject;
        appeared.StalePath.Should().BeTrue();
        appeared.ImagePath.Should().Be(@"E:\New\game.exe", "the session is keyed on the path the process runs from");

        var ambiguous = new ProcessWatcher();
        IReadOnlyList<WatchEvent> none = ambiguous.Poll([Proc(8, @"E:\New\game.exe")], [moved, Game(2, @"F:\Other\game.exe")]);
        none.Should().BeEmpty("two rows share the file name and guessing which one moved lands a session on the wrong row");
    }

    [Fact]
    public void AProcessThatCouldNotBeOpenedMatchesNothingEvenByName()
    {
        var watcher = new ProcessWatcher();
        IReadOnlyList<WatchEvent> events = watcher.Poll([Proc(9, null)], [Game(1, @"C:\Games\unknown.exe")]);
        events.Should().BeEmpty("an image path we could not read is not an identity");
    }
}
