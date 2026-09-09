using FluentAssertions;
using FrameLedger.Application.Watch;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>Pid reuse is the whole difficulty of a process tree, and the election is a fold over the tree.</summary>
public sealed class ProcessTreeAndElectionTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static ProcessSnapshot P(int pid, int ppid, string? path, int startSeconds) =>
        new(pid, ppid, path is null ? "x.exe" : Path.GetFileName(path), path, _t0.AddSeconds(startSeconds));

    [Fact]
    public void DescendantsFollowParentLinksButNotAChildThatStartedBeforeItsParent()
    {
        // pid 50 says its parent is 10, but it started BEFORE 10 did: 10 is a reused number, and 50 belongs
        // to whoever had it first. Without the time rule a launcher's tree would adopt strangers.
        List<ProcessSnapshot> snapshot =
        [
            P(10, 1, @"C:\L\launcher.exe", 100),
            P(20, 10, @"C:\G\game.exe", 105),
            P(30, 20, @"C:\G\helper.exe", 106),
            P(50, 10, @"C:\Unrelated\old.exe", 40),
            P(60, 99, @"C:\Other\x.exe", 110),
        ];

        IReadOnlyList<ProcessSnapshot> d = ProcessTree.Descendants(snapshot, 10);

        d.Select(static p => p.Pid).Should().Equal(20, 30);
        ProcessTree.IsDescendant(snapshot, 10, 30).Should().BeTrue();
        ProcessTree.IsDescendant(snapshot, 10, 50).Should().BeFalse("started before its 'parent'");
        ProcessTree.IsDescendant(snapshot, 10, 60).Should().BeFalse();
    }

    [Fact]
    public void AnUnknownStartTimeTrustsTheLink()
    {
        List<ProcessSnapshot> snapshot =
        [
            new(10, 1, "l.exe", @"C:\L\launcher.exe", null),
            new(20, 10, "g.exe", @"C:\G\game.exe", _t0),
        ];
        ProcessTree.Descendants(snapshot, 10).Should().ContainSingle().Which.Pid.Should().Be(20,
            "refusing a link we could not date would hide a real child behind a right we lacked");
    }

    [Fact]
    public void TheElectionPicksTheNewestTrackedDescendantAndNeverAnUnreadableOne()
    {
        List<ProcessSnapshot> snapshot =
        [
            P(10, 1, @"C:\L\launcher.exe", 100),
            P(20, 10, @"C:\G\game.exe", 105),
            P(21, 10, @"C:\G\game.exe", 130),
            P(22, 10, @"C:\G\crashreporter.exe", 140),
            P(23, 10, null, 150),
        ];
        HashSet<string> tracked = new([@"C:\G\game.exe"], StringComparer.OrdinalIgnoreCase);

        ProcessSnapshot? elected = DescendantElection.Elect(snapshot, 10, tracked.Contains);

        elected.Should().NotBeNull();
        elected!.Value.Pid.Should().Be(21, "the newest game.exe is the one presenting after a relaunch");
        DescendantElection.Elect(snapshot, 10, static _ => false).Should().BeNull("nothing tracked, nothing elected");
        DescendantElection.Elect(snapshot, 999, tracked.Contains).Should().BeNull();
    }
}
