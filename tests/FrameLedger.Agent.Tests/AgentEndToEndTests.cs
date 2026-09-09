using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Settings;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// The shipped Agent as a separate process (P2 PR-F): <c>--console capture</c> against our own harness, with a
/// scratch ledger under <c>--data-dir</c>, ends as ONE <c>sessions</c> row — the milestone's technical half,
/// from outside the process.
/// </summary>
/// <remarks>
/// <para>
/// <b>hook-harness is the only thing this suite ever injects into</b>, and
/// <c>TheTargetAndTheConsentRecordAreBothOurOwnHarness</c> asserts that on the suite itself, exactly as the
/// capture host's suite does. The consent record is written through the real store rather than through
/// <c>consent grant</c>, which refuses redirected stdin by design; the test is standing in for a human, so the
/// self-constraint pins WHICH executable it may stand in for.
/// </para>
/// <para>
/// <b>The scratch ledger is this test's, never the profile's.</b> <c>--data-dir</c> is the whole reason decision
/// D6 exists; every Agent this class starts is given one, and the directory is deleted with the test.
/// </para>
/// </remarks>
// ONE COLLECTION FOR EVERY CLASS THAT STARTS hook-harness, so they never run at the same time. xUnit
// parallelises across classes but never within a collection, and two harnesses of the same image are exactly
// the ambiguity TargetResolver refuses on ("2 processes share the image and none can be singled out") —
// measured 2026-09-10, when this class and its sibling first ran concurrently. Refusing was correct; the
// suites were wrong to be concurrent.
[Trait("Category", "Integration")]
[Collection("agent-harness")]
public sealed class AgentEndToEndTests : IDisposable
{
    private static string Harness => Path.Combine(AppContext.BaseDirectory, "hook-harness.exe");

    private static string Agent => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    /// <summary>The ONE executable this suite may write a consent record for.</summary>
    private static string ConsentedExecutable => Harness;

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "fl-agent-e2e-" + Guid.NewGuid().ToString("N"));

    private string Ledger => Path.Combine(_dataDir, LedgerPaths.DatabaseFileName);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dataDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void TheTargetAndTheConsentRecordAreBothOurOwnHarness()
    {
        Path.GetFileName(ConsentedExecutable).Should().Be("hook-harness.exe");
        Path.GetDirectoryName(ConsentedExecutable).Should().Be(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        Path.GetFileName(Agent).Should().Be("FrameLedger.Agent.exe");
    }

    private async Task<ConsentWriteOutcome> GrantConsentAsync()
    {
        ExecutableFingerprint fingerprint = FrameLedger.Infrastructure.Io.ExecutableIdentity.Read(ConsentedExecutable)!.Value;
        Directory.CreateDirectory(_dataDir);
        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: TestContext.Current.CancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await new SqliteGameConsentStore(db).RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
            {
                Fingerprint = fingerprint,
                DisclosureVersion = OperatorDisclosure.AgentConsoleVersion,
                AcknowledgedAt = DateTimeOffset.UtcNow,
                Provenance = ConsentProvenance.AgentConsoleOperator,
            }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SetKillSwitchAsync(bool engaged)
    {
        Directory.CreateDirectory(_dataDir);
        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: TestContext.Current.CancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            await new SettingsKillSwitch(new SqliteSettingsStore(db)).SetAsync(engaged, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
    }

    private static Process StartHarness(string arguments)
    {
        File.Exists(Harness).Should().BeTrue("hook-harness.exe must be staged beside the test binary (FrameLedger.DrainFixtures.targets); this FAILS rather than skipping");
        File.Exists(Agent).Should().BeTrue("the Agent must be built and copied beside this test");
        var p = Process.Start(new ProcessStartInfo(Harness, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        Thread.Sleep(800);
        p.HasExited.Should().BeFalse("the harness must be running before the Agent looks for it");
        return p;
    }

    private Process StartAgent(params string[] consoleArgs)
    {
        var psi = new ProcessStartInfo(Agent) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("--console");
        psi.ArgumentList.Add("--data-dir");
        psi.ArgumentList.Add(_dataDir);
        foreach (string a in consoleArgs)
        {
            psi.ArgumentList.Add(a);
        }

        return Process.Start(psi)!;
    }

    private static async Task<(string Stdout, int Exit)> FinishAsync(Process p)
    {
        Task<string> err = p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        string stdout = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        await p.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (stdout + "\n---- stderr ----\n" + await err.ConfigureAwait(false) + $"\n---- exit {p.ExitCode} ----\n", p.ExitCode);
    }

    private static void Kill(Process p)
    {
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        p.Dispose();
    }

    [Fact]
    public async Task AConsentedBoundedCaptureLandsAsOneHookedRowInTheScratchLedger()
    {
        // THE MILESTONE'S TECHNICAL HALF, from outside the process: the shipped Agent, our harness, one row.
        (await GrantConsentAsync()).Should().Be(ConsentWriteOutcome.Written);

        Process harness = StartHarness("--real --hold-presenting 40");
        try
        {
            using Process agent = StartAgent("capture", "--exe", ConsentedExecutable, "--seconds", "8");
            (string output, int exit) = await FinishAsync(agent);

            exit.Should().Be(0, output);
            output.Should().Contain("ledger: session ").And.Contain("SAVED as sessions.id=", output);
            output.Should().Contain("telemetry=l1", output);

            LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using (db.ConfigureAwait(true))
            {
                var sessions = new SqliteSessionRepository(db);
                SessionRow row = (await sessions.ListRecentAsync(1, TestContext.Current.CancellationToken).ConfigureAwait(true)).Should().ContainSingle().Subject;
                row.Tier.Should().Be(CaptureTier.Hooked);
                row.Mode.Should().Be(CaptureMode.Attach);
                row.ExitStatus.Should().Be(ExitStatus.Normal, "the harness was still presenting when the bounded capture ended");
                row.FrameCount.Should().BeGreaterThan(100, "8 s at ~120/s");
                row.Api.Should().Be("d3d11");
                row.GuardTicksPublished.Should().BeGreaterThanOrEqualTo(1);
                row.OverlayBuildId.Should().NotBeNullOrEmpty();
                row.TelemetrySource.Should().Contain("l1", "the Agent composes L1 + L2 + L3 for every session");
                FrameBlobs? frames = await sessions.FindFramesAsync(row.Id, TestContext.Current.CancellationToken).ConfigureAwait(true);
                frames.Should().NotBeNull();
                frames!.SampleCount.Should().Be(row.FrameCount);
            }

            Directory.EnumerateFiles(Path.Combine(_dataDir, "tmp"), "*.partial").Should().BeEmpty("finalize deletes the .partial");
        }
        finally
        {
            Kill(harness);
        }
    }

    [Fact]
    public async Task WithTheKillSwitchOnAConsentedGameIsRefusedAndNothingIsEverInjected()
    {
        // FR-2.4 from outside the process (decision D7): consent is present and enabled, the switch is on, the
        // gate refuses with its own reason, and no ring ever exists in the target.
        (await GrantConsentAsync()).Should().Be(ConsentWriteOutcome.Written);
        await SetKillSwitchAsync(engaged: true);

        Process harness = StartHarness("--real --hold-presenting 20");
        try
        {
            using Process agent = StartAgent("capture", "--exe", ConsentedExecutable, "--seconds", "3");
            (string output, int exit) = await FinishAsync(agent);

            output.Should().Contain(nameof(SessionEndReason.RefusedKillSwitch), output);
            exit.Should().NotBe(0, output);

            harness.HasExited.Should().BeFalse();
            Action open = () => MemoryMappedFile.OpenExisting($@"Local\FrameLedger.Ring.{harness.Id}", MemoryMappedFileRights.Read).Dispose();
            open.Should().Throw<FileNotFoundException>("no ring means no Overlay means no injection");
        }
        finally
        {
            Kill(harness);
        }
    }

    [Fact]
    public async Task ConsentGrantRefusesRedirectedStdinAndWritesNothing()
    {
        using Process agent = StartAgent("consent", "grant", "--exe", ConsentedExecutable);
        (string output, int exit) = await FinishAsync(agent);

        exit.Should().Be(2, output);
        output.Should().Contain("stdin is redirected", output);
        output.Should().Contain("OPERATOR SURFACE", "the Agent's disclosure names its own surface first");

        using Process list = StartAgent("consent", "list");
        (string listed, int listExit) = await FinishAsync(list);
        listExit.Should().Be(0, listed);
        listed.Should().Contain("nothing is enabled", listed);
    }

    [Fact]
    public async Task TheUnbuiltFlagsAnswerNotImplementedWithExitTwo()
    {
        var psi = new ProcessStartInfo(Agent) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("--register-vklayer");
        using Process agent = Process.Start(psi)!;
        (string output, int exit) = await FinishAsync(agent);

        exit.Should().Be(2, output);
        output.Should().Contain("not implemented in P2", output);
    }
}
