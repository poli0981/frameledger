using FluentAssertions;
using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.Infrastructure.Tests.Startup;

/// <summary>The logon task through the real Task Scheduler (COM, unelevated), under a test's own name, removed whatever happens.</summary>
[Trait("Category", "Integration")]
public sealed class LogonTaskTests
{
    [Fact]
    public void InstallsQueriesRepairsAndRemovesThisUsersTask()
    {
        var task = new LogonTask("FrameLedger.Test-" + Guid.NewGuid().ToString("N"));
        string exe = Path.Combine(Path.GetTempPath(), "FrameLedger.Agent.exe");
        string moved = Path.Combine(Path.GetTempPath(), "moved", "FrameLedger.Agent.exe");
        try
        {
            task.Query(exe).Should().Be(LogonTaskState.NotInstalled);

            task.Install(exe).Should().BeNull();

            task.Query(exe).Should().Be(LogonTaskState.Installed);
            task.Query(moved).Should().Be(LogonTaskState.Stale, "the action names the other path");

            task.Install(moved).Should().BeNull("Repair is a create-or-update");
            task.Query(moved).Should().Be(LogonTaskState.Installed);

            task.Remove().Should().BeNull();
            task.Query(exe).Should().Be(LogonTaskState.NotInstalled);
            task.Remove().Should().BeNull("removing a task that is not there is not an error");
        }
        finally
        {
            task.Remove();
        }
    }

    [Fact]
    public void RejectsAnEmptyName()
    {
        FluentActions.Invoking(static () => new LogonTask(" ")).Should().Throw<ArgumentException>();
        LogonTask.DefaultTaskName.Should().Be("FrameLedger.Agent");
    }
}
