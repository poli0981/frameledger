using FluentAssertions;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.Infrastructure.Tests.Startup;

/// <summary>
/// One capturing Agent per data folder (2026-09-17): the first process to claim a folder holds it, every other claim is
/// refused until its handle goes, however the folder is spelled, and another folder is another claim. The folders here are
/// named for the test, so an installed Agent's claim on the profile folder is never touched.
/// </summary>
public sealed class AgentInstanceLockTests
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-claim-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheFirstClaimHoldsTheFolderAndEveryOtherIsRefusedUntilItIsReleased()
    {
        AgentInstanceLock.IsHeld(_dir).Should().BeFalse();
        using (AgentInstanceLock? first = AgentInstanceLock.TryAcquire(_dir))
        {
            first.Should().NotBeNull();
            AgentInstanceLock.IsHeld(_dir).Should().BeTrue();
            AgentInstanceLock.TryAcquire(_dir).Should().BeNull("a second Agent for the same folder");
            AgentInstanceLock.TryAcquire(_dir.ToUpperInvariant() + Path.DirectorySeparatorChar).Should().BeNull("the same folder, spelled differently");
            AgentInstanceLock.IsHeld(_dir + "-elsewhere").Should().BeFalse("another folder is another Agent's");
        }

        AgentInstanceLock.IsHeld(_dir).Should().BeFalse("the claim goes with its handle, so a crashed Agent leaves nothing behind");
        using AgentInstanceLock? again = AgentInstanceLock.TryAcquire(_dir);
        again.Should().NotBeNull();
    }

    [Fact]
    public void TheNameLivesInThisSessionsNamespaceAndCarriesAHashNotThePath()
    {
        const string prefix = @"Local\FrameLedger.Agent.";
        string name = AgentInstanceLock.NameFor(_dir);

        name.Should().StartWith(prefix).And.HaveLength(prefix.Length + 32);
        name.Should().NotContainEquivalentOf("fl-claim");
        AgentInstanceLock.NameFor(_dir + Path.DirectorySeparatorChar).Should().Be(name);
        AgentInstanceLock.NameFor(LedgerPaths.DefaultDirectory).Should().NotBe(name, "a test's folder is never the profile's");
    }
}
