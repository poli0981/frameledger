using FluentAssertions;
using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.Infrastructure.Tests.Startup;

/// <summary>FR-10 "start with Windows": a per-user Run value under a test's own name, cleaned up whatever happens.</summary>
public sealed class RunAtLogonTests
{
    [Fact]
    public void SetsReadsBackAndClearsThisUsersRunValue()
    {
        var run = new RunAtLogon("FrameLedger.Test-" + Guid.NewGuid().ToString("N"));
        const string exe = @"C:\Program Files\FrameLedger\FrameLedger.exe";
        try
        {
            run.IsSet(exe).Should().BeFalse();

            run.Set(exe);

            run.IsSet(exe).Should().BeTrue();
            run.IsSet(@"C:\elsewhere\FrameLedger.exe").Should().BeFalse("a moved install is a stale entry, not a match");
            run.IsSet(exe.ToUpperInvariant()).Should().BeTrue("paths compare case-insensitively");

            run.Clear();

            run.IsSet(exe).Should().BeFalse();
            run.Clear();
        }
        finally
        {
            run.Clear();
        }
    }

    [Fact]
    public void RejectsAnEmptyNameOrPath()
    {
        FluentActions.Invoking(static () => new RunAtLogon(" ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(static () => new RunAtLogon("x").Set("")).Should().Throw<ArgumentException>();
    }
}
