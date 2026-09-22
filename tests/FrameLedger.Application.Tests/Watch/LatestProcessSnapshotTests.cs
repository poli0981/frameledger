using FluentAssertions;
using FrameLedger.Application.Watch;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// The Tier-2 hold's clock (2026-09-23): the watcher's own snapshot, by image path. The owner's two "Flower in Us"
/// sessions were held by every <c>Game.exe</c> on the machine, because the hold asked for a file name.
/// </summary>
public sealed class LatestProcessSnapshotTests
{
    private const string _game = @"D:\SteamLibrary\steamapps\common\Flower in Us\Game.exe";

    private static ProcessSnapshot Proc(int pid, string? path, string name = "Game.exe") => new(pid, 1, name, path, DateTimeOffset.UnixEpoch);

    [Fact]
    public void BeforeTheFirstPollThereIsNoAnswer()
    {
        new LatestProcessSnapshot().Contains(_game).Should().BeNull("the console verbs never poll, and keep the name check");
    }

    [Fact]
    public void AnotherGamesGameExeDoesNotKeepThisOneRunning()
    {
        var latest = new LatestProcessSnapshot();
        latest.Publish([Proc(1, @"D:\another\it\hello-hello-world\HELLO, HELLO WORLD!\swiftshader\Game.exe")]);

        latest.Contains(_game).Should().BeFalse("same file name, another executable");

        latest.Publish([Proc(1, @"D:\another\it\hello-hello-world\HELLO, HELLO WORLD!\swiftshader\Game.exe"), Proc(2, _game.ToUpperInvariant())]);
        latest.Contains(_game).Should().BeTrue("the path, compared as NTFS compares it");
    }

    [Fact]
    public void AProcessWhosePathCouldNotBeReadStillCountsByItsName()
    {
        var latest = new LatestProcessSnapshot();
        latest.Publish([Proc(1, null)]);

        latest.Contains(_game).Should().BeTrue("a process we cannot identify must not end a hold early");
        latest.Contains(@"C:\Games\Other\other.exe").Should().BeFalse();
    }
}
