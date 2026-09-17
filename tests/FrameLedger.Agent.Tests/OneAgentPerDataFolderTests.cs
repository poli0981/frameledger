using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Agent.Cli;
using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// One capturing Agent per data folder, in the shipped binary (2026-09-17). While this test holds a temporary folder's
/// claim, <c>FrameLedger.Agent.exe --console --data-dir &lt;it&gt; recover</c> exits 10 with one line and creates nothing: the
/// claim comes before logging, rules and the ledger. <c>--serve</c> claims the profile folder the same way, and a test must
/// never run against that folder, so a capturing console verb stands in for it.
/// </summary>
public sealed class OneAgentPerDataFolderTests : IDisposable
{
    private static readonly AgentVerb[] _capturing = [AgentVerb.Serve, AgentVerb.Capture, AgentVerb.Launch, AgentVerb.Recover];

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "fl-oneagent-" + Guid.NewGuid().ToString("N"));

    private static string Agent => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataDir))
            {
                Directory.Delete(_dataDir, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task ACapturingVerbForAFolderAnotherProcessHoldsExitsBeforeItLogsOrOpensAnything()
    {
        File.Exists(Agent).Should().BeTrue("the Agent must be built and copied beside this test");
        using AgentInstanceLock? held = AgentInstanceLock.TryAcquire(_dataDir);
        held.Should().NotBeNull("nothing else holds a folder named for this test");

        var start = new ProcessStartInfo(Agent) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in (string[])["--console", "--data-dir", _dataDir, "recover"])
        {
            start.ArgumentList.Add(arg);
        }

        using Process agent = Process.Start(start)!;
        Task<string> stdout = agent.StandardOutput.ReadToEndAsync(Ct);
        string stderr = await agent.StandardError.ReadToEndAsync(Ct).ConfigureAwait(true);
        await agent.WaitForExitAsync(Ct).ConfigureAwait(true);
        string output = await stdout.ConfigureAwait(true);

        agent.ExitCode.Should().Be(AgentInstanceLock.ExitHeldElsewhere, "{0}{1}", output, stderr);
        stderr.Should().Contain("already capturing").And.Contain(_dataDir);
        Directory.Exists(_dataDir).Should().BeFalse("refused before logging and the ledger: no logs folder, no ledger.db");
    }

    [Fact]
    public void OnlyTheVerbsThatInjectReadARingOrFinalizeSessionsClaimTheFolder()
    {
        foreach (AgentVerb verb in Enum.GetValues<AgentVerb>())
        {
            Program.Captures(verb).Should().Be(_capturing.Contains(verb),
                "{0}: the capturing verbs claim the folder; the rest run beside a serving Agent, as the App's own --install-task does", verb);
        }
    }
}
