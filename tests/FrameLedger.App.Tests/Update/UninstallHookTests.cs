using FluentAssertions;
using FrameLedger.App.Update;

namespace FrameLedger.App.Tests.Update;

/// <summary><c>12_BUILD</c> §Publish &amp; package, the uninstall hook: every step runs, a failing one does not stop the next, and the data folder goes only on an explicit yes.</summary>
public sealed class UninstallHookTests
{
    private sealed class Probe
    {
        public int Agent { get; set; }

        public int Layer { get; set; }

        public int Task { get; set; }

        public int Asked { get; set; }

        public int Deleted { get; set; }

        public List<string> Log { get; } = [];

        public bool Answer { get; set; }

        public Func<bool>? LayerBehaviour { get; set; }

        public UninstallHook Hook() => new(
            stopAgent: () =>
            {
                Agent++;
                return true;
            },
            unregisterLayer: () =>
            {
                Layer++;
                return LayerBehaviour is { } b ? b() : true;
            },
            removeTask: () =>
            {
                Task++;
                return true;
            },
            askDeleteData: () =>
            {
                Asked++;
                return Answer;
            },
            deleteData: () => Deleted++,
            log: Log.Add);
    }

    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local\FrameLedger", @"C:\Users\u\AppData\Local\FrameLedger.App\current\", true)]
    [InlineData(@"C:\Users\u\AppData\Local\FrameLedger", @"C:\Users\u\AppData\Local\FrameLedger\current\", false)]
    [InlineData(@"C:\Users\u\AppData\Local\FrameLedger\current", @"C:\Users\u\AppData\Local\FrameLedger", false)]
    [InlineData(@"C:\Users\u\AppData\Local\FrameLedger", @"c:\users\u\appdata\local\frameledger", false)]
    [InlineData(@"C:\Users\u\AppData\Local\FrameLedger", @"C:\Users\u\AppData\Local\FrameLedgerX", true)]
    public void TheDataFolderIsDeletableOnlyWhenItAndTheInstallAreSeparate(string data, string install, bool separate) =>
        UninstallHook.AreSeparate(data, install).Should().Be(separate, "a package id equal to the data folder's name puts the install inside the ledger's folder");

    [Fact]
    public void YesRemovesEverythingIncludingTheDataFolder()
    {
        var p = new Probe { Answer = true };

        UninstallOutcome outcome = p.Hook().Run();

        outcome.Should().Be(new UninstallOutcome(true, true, true, true, true));
        (p.Agent, p.Layer, p.Task, p.Asked, p.Deleted).Should().Be((1, 1, 1, 1, 1));
        p.Log.Should().Contain(static l => l.Contains("data folder delete: done", StringComparison.Ordinal));
    }

    [Fact]
    public void NoKeepsTheDataFolderAndStillRemovesTheRegistrationAndTheTask()
    {
        var p = new Probe { Answer = false };

        UninstallOutcome outcome = p.Hook().Run();

        outcome.Should().Be(new UninstallOutcome(true, true, true, true, false));
        p.Deleted.Should().Be(0, "the folder is the user's; only a yes removes it");
        (p.Layer, p.Task).Should().Be((1, 1));
    }

    [Fact]
    public void AStepThatThrowsIsLoggedAndTheNextStillRuns()
    {
        var p = new Probe { Answer = false, LayerBehaviour = static () => throw new UnauthorizedAccessException("HKCU") };

        UninstallOutcome outcome = p.Hook().Run();

        outcome.LayerUnregistered.Should().BeFalse();
        outcome.TaskRemoved.Should().BeTrue("the task removal does not depend on the layer step");
        outcome.DataAsked.Should().BeTrue();
        p.Log.Should().Contain(static l => l.Contains("vulkan layer unregister failed: UnauthorizedAccessException", StringComparison.Ordinal));
    }
}
