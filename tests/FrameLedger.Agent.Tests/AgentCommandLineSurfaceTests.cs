using FluentAssertions;
using FrameLedger.Agent.Cli;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// What the Agent can be ASKED to do, pinned (P2 PR-F). §S27's gap was a user-named pid on a binary carrying no
/// consent record, and the Agent is now that binary by design; the surface is the place that stays closed.
/// </summary>
public sealed class AgentCommandLineSurfaceTests
{
    [Fact]
    public void TheAcceptedOptionsAreExactlyThese()
    {
        // --exe / --seconds / --args are the host's three, with the same property: none can widen WHAT is
        // injected or skip a check. --last pages `sessions`. --data-dir is HANDOFF §P2 decision D6: a scratch
        // ledger for an operator or a test, and it exists only under --console.
        // --partial-flush-seconds joined in P2 PR-G, and this assertion is what made adding it a deliberate
        // act: it bounds how often a RUNNING session writes its own crash-recovery file, so like --seconds it
        // cannot widen what is injected or skip a check.
        AgentCommandLine.AcceptedOptions.Should().BeEquivalentTo(["--exe", "--seconds", "--args", "--last", "--data-dir", "--partial-flush-seconds"]);
    }

    [Fact]
    public void NoVerbOrOptionNamesAPidAPayloadOrAnOverride()
    {
        // --pid is §S27's gap. --payload would defeat §S22. --force and --yes are the "I understand, continue
        // anyway" button CLAUDE.md rule 2 forbids. --diag is the App's (10_LOGGING) and answers "not implemented".
        string[] forbidden = ["pid", "payload", "force", "yes", "override"];
        IEnumerable<string> surface = [.. AgentCommandLine.AcceptedOptions, .. Enum.GetNames<AgentVerb>()];
        foreach (string token in surface)
        {
            foreach (string bad in forbidden)
            {
                token.Should().NotContainEquivalentOf(bad);
            }
        }

        foreach (string bad in new[] { "--pid", "--force", "--yes", "--payload" })
        {
            AgentCommandLine.Parse(["--console", "capture", "--exe", "g.exe", bad, "1"]).Error.Should().NotBeNull(bad);
        }
    }

    [Fact]
    public void ServeTakesNothingAndDataDirIsConsoleOnly()
    {
        AgentCommandLine.Parse(["--serve"]).Verb.Should().Be(AgentVerb.Serve);
        AgentCommandLine.Parse(["--serve", "--data-dir", @"C:\tmp"]).Error.Should().Contain("only under --console",
            "the product directory is not selectable (decision D6)");

        AgentCommandLine before = AgentCommandLine.Parse(["--console", "--data-dir", @"C:\tmp\x", "capture", "--exe", "g.exe", "--seconds", "8"]);
        before.Error.Should().BeNull();
        before.Verb.Should().Be(AgentVerb.Capture);
        before.DataDirectory.Should().Be(@"C:\tmp\x");
        before.ExePath.Should().Be("g.exe");
        before.Seconds.Should().Be(8);

        AgentCommandLine after = AgentCommandLine.Parse(["--console", "sessions", "--last", "3", "--data-dir", @"C:\tmp\y"]);
        after.Error.Should().BeNull();
        after.Verb.Should().Be(AgentVerb.Sessions);
        after.Last.Should().Be(3);
        after.DataDirectory.Should().Be(@"C:\tmp\y");
    }

    [Fact]
    public void TheDesignsUnbuiltFlagsAnswerNotImplementedRatherThanBeingSilentlyAccepted()
    {
        foreach (string flag in AgentCommandLine.NotImplementedFlags)
        {
            AgentCommandLine parsed = AgentCommandLine.Parse([flag]);
            parsed.Verb.Should().Be(AgentVerb.NotImplemented, flag);
            parsed.Flag.Should().Be(flag);
        }

        AgentCommandLine.NotImplementedFlags.Should().BeEquivalentTo(
            ["--diag", "--install-task", "--uninstall-task", "--register-vklayer", "--unregister-vklayer"],
            "12_BUILD §Debugging's list, minus --serve and --console which are built");
    }

    [Fact]
    public void EveryConsoleVerbParsesAndTheOnesThatNeedAnExecutableRequireIt()
    {
        AgentCommandLine.Parse(["--console", "consent", "list"]).Verb.Should().Be(AgentVerb.ConsentList);
        AgentCommandLine.Parse(["--console", "consent", "grant", "--exe", "g.exe"]).Verb.Should().Be(AgentVerb.ConsentGrant);
        AgentCommandLine.Parse(["--console", "consent", "revoke", "--exe", "g.exe"]).Verb.Should().Be(AgentVerb.ConsentRevoke);
        AgentCommandLine.Parse(["--console", "launch", "--exe", "g.exe", "--args", "--real"]).Arguments.Should().Be("--real");
        AgentCommandLine.Parse(["--console", "recover"]).Verb.Should().Be(AgentVerb.Recover);
        AgentCommandLine.Parse(["--console", "db", "path"]).Verb.Should().Be(AgentVerb.DbPath);
        AgentCommandLine.Parse(["--console", "games", "add", "--exe", "g.exe"]).Verb.Should().Be(AgentVerb.GamesAdd);
        AgentCommandLine.Parse(["--console", "killswitch", "on"]).Verb.Should().Be(AgentVerb.KillSwitchOn);
        AgentCommandLine.Parse(["--console", "killswitch", "off"]).Verb.Should().Be(AgentVerb.KillSwitchOff);
        AgentCommandLine.Parse(["--console", "killswitch", "status"]).Verb.Should().Be(AgentVerb.KillSwitchStatus);

        foreach (string[] needsExe in new[] { new[] { "consent", "grant" }, ["consent", "revoke"], ["capture"], ["launch"], ["games", "add"] })
        {
            AgentCommandLine.Parse(["--console", .. needsExe]).Error.Should().Contain("--exe", string.Join(' ', needsExe));
        }

        AgentCommandLine.Parse([]).Error.Should().Contain("usage");
        AgentCommandLine.Parse(["--console"]).Error.Should().Contain("usage");
        AgentCommandLine.Parse(["--console", "dance"]).Error.Should().Contain("usage");
    }

    [Fact]
    public void ASecondsValueThatIsNotAPositiveNumberIsAnErrorAndNeverASilentZero()
    {
        // ZERO MEANS "UNTIL THE TARGET EXITS", so leniency would produce an UNBOUNDED session — the opposite
        // of what an operator typing --seconds wants.
        foreach (string bad in new[] { "abc", "0", "-5", "", "1.5", "2s", "+9", " 9" })
        {
            AgentCommandLine.Parse(["--console", "capture", "--exe", "game.exe", "--seconds", bad]).Error
                .Should().NotBeNull($"'--seconds {bad}' must be refused rather than read as unbounded");
        }

        AgentCommandLine.Parse(["--console", "capture", "--exe", "game.exe"]).Seconds.Should().Be(0);
        AgentCommandLine.Parse(["--console", "sessions", "--last", "0"]).Error.Should().NotBeNull();

        // Same rule for the flush: 0 means "the product's 60 s", so garbage must not read as it.
        foreach (string bad in new[] { "abc", "0", "-2", "1.5" })
        {
            AgentCommandLine.Parse(["--console", "capture", "--exe", "g.exe", "--partial-flush-seconds", bad]).Error.Should().NotBeNull(bad);
        }

        AgentCommandLine.Parse(["--console", "capture", "--exe", "g.exe"]).PartialFlushSeconds.Should().Be(0, "absent is the product's interval");
        AgentCommandLine.Parse(["--console", "capture", "--exe", "g.exe", "--partial-flush-seconds", "2"]).PartialFlushSeconds.Should().Be(2);
    }
}
