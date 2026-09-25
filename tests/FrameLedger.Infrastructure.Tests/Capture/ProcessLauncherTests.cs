using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Infrastructure.Capture;

namespace FrameLedger.Infrastructure.Tests.Capture;

/// <summary>A start that fails keeps Windows' own reason (beta.8): the launcher swallowed it, and the user read only "could not start".</summary>
public sealed class ProcessLauncherTests
{
    [Fact]
    public void AStartThatFailsSaysWhy()
    {
        IProcessLauncher launcher = new ProcessLauncher(enableVulkanLayer: false);
        string missing = Path.Combine(Path.GetTempPath(), "fl-no-such-" + Guid.NewGuid().ToString("N"), "game.exe");

        (int Pid, ITargetLiveness Alive)? started = launcher.Start(missing, string.Empty, out int? error);

        started.Should().BeNull();
        error.Should().BeOneOf(2, 3, 267);
    }
}
