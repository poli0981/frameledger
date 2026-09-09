using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Io;
using FrameLedger.Infrastructure.Watch;

namespace FrameLedger.Infrastructure.Tests.Watch;

/// <summary>The snapshot against the one process this test can vouch for: itself, with a parent and a start time.</summary>
public sealed class ToolhelpProcessSnapshotSourceTests
{
    [Fact]
    public void ThisProcessIsInTheSnapshotWithItsNormalisedImagePathAndACreationTime()
    {
        IReadOnlyList<ProcessSnapshot> snapshot = new ToolhelpProcessSnapshotSource().Take();

        snapshot.Should().NotBeEmpty();
        ProcessSnapshot me = snapshot.Should().ContainSingle(p => p.Pid == Environment.ProcessId).Subject;
        me.ImagePath.Should().Be(ExecutableIdentity.Normalise(Environment.ProcessPath!), "the watcher compares against consent records normalised the same way");
        me.ImageName.Should().Be(Path.GetFileName(Environment.ProcessPath), "the name comes from the snapshot itself");
        me.StartedAt.Should().NotBeNull();
        me.StartedAt!.Value.Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(1)).And.BeAfter(DateTimeOffset.UtcNow.AddHours(-24));
        me.ParentPid.Should().BeGreaterThan(0);
        snapshot.Should().NotContain(p => p.Pid <= 4, "Idle and System are never listed");
    }

    [Fact]
    public void AChildThisTestStartsIsADescendantOfThisProcess()
    {
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            IReadOnlyList<ProcessSnapshot> snapshot = new ToolhelpProcessSnapshotSource().Take();
            ProcessTree.IsDescendant(snapshot, Environment.ProcessId, child.Id).Should().BeTrue();
            ProcessSnapshot kid = snapshot.Single(p => p.Pid == child.Id);
            kid.ParentPid.Should().Be(Environment.ProcessId);
            kid.StartedAt.Should().NotBeNull();
        }
        finally
        {
            try
            {
                child.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
