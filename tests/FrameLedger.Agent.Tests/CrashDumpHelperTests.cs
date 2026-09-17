using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Agent.Cli;
using FrameLedger.Infrastructure.Diagnostics;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// The product's crash-dump path end to end (2026-09-17): <see cref="CrashDumpWriter.TryWrite"/> starts the shipped
/// <c>FrameLedger.Agent.exe --write-crash-dump</c>, which dumps its parent — here, this test process, whose binary sits in
/// the same directory as the Agent's copy, as the App's does in an install. The dump is taken from OUTSIDE, so nothing in
/// this process is suspended by the process that is writing it; the in-process version froze a parallel test run one
/// time in eight.
/// </summary>
public sealed class CrashDumpHelperTests : IDisposable
{
    private static string Agent => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-crashdump-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void TheWriterHasTheAgentDumpThisProcessFromOutside()
    {
        File.Exists(Agent).Should().BeTrue("the Agent must be built and copied beside this test");
        var lines = new List<string>();
        var now = new DateTimeOffset(2026, 9, 15, 1, 2, 3, TimeSpan.FromHours(7));

        string? path = CrashDumpWriter.TryWrite(_dir, "test", now, Agent, lines.Add);

        path.Should().NotBeNull("the Agent dumps its parent, this test process: {0}", string.Join(" | ", lines));
        Path.GetFileName(path).Should().Be($"test-20260914-180203-{Environment.ProcessId}.dmp", "the name carries the UTC time and the crashing pid");
        byte[] head = new byte[4];
        using (var file = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            file.ReadExactly(head);
        }

        head.Should().Equal("MDMP"u8.ToArray());
        lines.Should().BeEmpty();
    }

    [Fact]
    public void TheHelperRefusesAParentThatIsNotAFrameLedgerBinaryFromItsDirectory()
    {
        // cmd.exe from System32 is the parent here: the helper must read nothing and write nothing.
        Directory.CreateDirectory(_dir);
        string file = Path.Combine(_dir, "refused.dmp");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add(Agent);
        start.ArgumentList.Add(CrashDumpWriter.DumperFlag);
        start.ArgumentList.Add(file);
        using Process cmd = Process.Start(start)!;
        string stderr = cmd.StandardError.ReadToEnd();
        cmd.WaitForExit(30_000).Should().BeTrue();

        cmd.ExitCode.Should().Be(ParentDump.ExitRefused, stderr);
        stderr.Should().Contain("refused");
        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void TheFlagTakesAFileAndNothingElse()
    {
        AgentCommandLine parsed = AgentCommandLine.Parse(["--write-crash-dump", @"C:\x\a.dmp"]);
        parsed.Verb.Should().Be(AgentVerb.WriteCrashDump);
        parsed.CrashDumpFile.Should().Be(@"C:\x\a.dmp");
        AgentCommandLine.Parse(["--write-crash-dump"]).Error.Should().Contain("exactly one argument");
        AgentCommandLine.Parse(["--write-crash-dump", "a.dmp", "--data-dir", "x"]).Error.Should().Contain("exactly one argument");
        AgentCommandLine.Parse(["--console", "--write-crash-dump", "a.dmp"]).Error.Should().Contain("usage", "a flag, not a console verb");
    }
}
