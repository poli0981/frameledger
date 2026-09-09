using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Recording;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// Crash safety end to end (P2 PR-G, `14_TESTING` line 52): an Agent killed mid-session leaves a
/// <c>.partial</c>, the next start's <c>recover</c> turns its valid prefix into an <c>interrupted</c> row and
/// removes the file, and a second <c>recover</c> finds nothing to do.
/// </summary>
/// <remarks>
/// <para>
/// <b>The kill is a real <c>Process.Kill</c> of a real Agent</b>, which is the only arrangement that produces
/// the thing under test: a file whose last chunk may be torn because nobody got to finish it. An in-process
/// test can write a truncated file, and <c>PartialSessionFileTests</c> already kills at every byte offset —
/// what it cannot show is that the Agent, on its own, wrote a prefix worth recovering before it died.
/// </para>
/// <para>
/// <b>Nothing here waits on a clock it does not own.</b> The guard tick is polled for through the ring, the
/// <c>.partial</c> is polled for on disk, and the kill happens once a flush has actually landed —
/// <c>--partial-flush-seconds 2</c> is what makes that a couple of seconds rather than a minute.
/// </para>
/// <para>
/// Same self-constraint as the sibling class: hook-harness is the only thing this suite injects into, and the
/// scratch <c>--data-dir</c> is deleted with the test.
/// </para>
/// </remarks>
// ONE COLLECTION FOR EVERY CLASS THAT STARTS hook-harness, so they never run at the same time. xUnit
// parallelises across classes but never within a collection, and two harnesses of the same image are exactly
// the ambiguity TargetResolver refuses on ("2 processes share the image and none can be singled out") —
// measured 2026-09-10, when this class and its sibling first ran concurrently. Refusing was correct; the
// suites were wrong to be concurrent.
[Trait("Category", "Integration")]
[Collection("agent-harness")]
public sealed class PartialRecoveryEndToEndTests : IDisposable
{
    private static string Harness => Path.Combine(AppContext.BaseDirectory, "hook-harness.exe");

    private static string Agent => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    /// <summary>The ONE executable this suite may write a consent record for.</summary>
    private static string ConsentedExecutable => Harness;

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "fl-agent-recover-" + Guid.NewGuid().ToString("N"));

    private string Ledger => Path.Combine(_dataDir, LedgerPaths.DatabaseFileName);

    private string PartialDirectory => Path.Combine(_dataDir, "tmp");

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

    private async Task GrantConsentAsync()
    {
        Path.GetFileName(ConsentedExecutable).Should().Be("hook-harness.exe", "this suite injects into nothing else");
        ExecutableFingerprint fingerprint = FrameLedger.Infrastructure.Io.ExecutableIdentity.Read(ConsentedExecutable)!.Value;
        Directory.CreateDirectory(_dataDir);
        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: TestContext.Current.CancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            ConsentWriteOutcome outcome = await new SqliteGameConsentStore(db).RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
            {
                Fingerprint = fingerprint,
                DisclosureVersion = OperatorDisclosure.AgentConsoleVersion,
                AcknowledgedAt = DateTimeOffset.UtcNow,
                Provenance = ConsentProvenance.AgentConsoleOperator,
            }, TestContext.Current.CancellationToken).ConfigureAwait(false);
            outcome.Should().Be(ConsentWriteOutcome.Written);
        }
    }

    private Process StartAgent(params string[] consoleArgs)
    {
        File.Exists(Agent).Should().BeTrue("the Agent must be built and copied beside this test");
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

    private static Process StartHarness(string arguments)
    {
        File.Exists(Harness).Should().BeTrue(
            "hook-harness.exe must be staged beside the test binary (FrameLedger.DrainFixtures.targets). "
            + "This FAILS rather than skipping: an integration test that quietly does nothing when its fixture "
            + "is absent is a gate that cannot fail.");
        var p = Process.Start(new ProcessStartInfo(Harness, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        Thread.Sleep(800);
        p.HasExited.Should().BeFalse("the harness must be running before the Agent looks for it");
        return p;
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

    /// <summary>
    /// Reads a <c>.partial</c> that a LIVE Agent still holds open. <c>PartialSessionFile.Read</c> is the
    /// product's convenience overload and opens the file the way recovery does — at startup, when nobody is
    /// writing — so it cannot open one mid-session. The writer holds <c>FileShare.Read</c>, which permits a
    /// reader that asks for read access and tolerates the existing writer, so this asks for exactly that and
    /// hands the bytes to the same parser.
    /// </summary>
    private static PartialSession? ReadLive(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return PartialSessionFile.Parse(memory.GetBuffer().AsSpan(0, (int)memory.Length));
    }

    /// <summary>
    /// Polls for a <c>.partial</c> whose prefix is worth recovering: at least one flushed tick, records, and a
    /// session already longer than the Agent's 30 s discard threshold. The state is polled for, never slept on.
    /// </summary>
    /// <remarks>
    /// <b>The threshold is the product's and the test waits it out rather than lowering it.</b> A first version
    /// killed the Agent after one 2 s flush; recovery answered <c>Discarded — 2.5 s is under the 30 s minimum</c>,
    /// which is the correct behaviour (<c>04_CAPTURE</c> §Discard rule) and made the case prove nothing about
    /// recovery. Adding a knob to shrink the minimum would have made the test pass by changing what the product
    /// stores, so the run takes ~35 s instead. <c>--partial-flush-seconds 2</c> is still what makes the prefix
    /// land promptly once that time has passed; the alternative is a 60 s wait for the first flush.
    /// </remarks>
    private async Task<string> WaitForRecoverablePartialAsync(TimeSpan minimumSession, TimeSpan budget)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + budget;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (Directory.Exists(PartialDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(PartialDirectory, "*.partial"))
                {
                    PartialSession? partial = ReadLive(file);
                    if (partial?.LastTick is not null && partial.Records.Count > 0
                        && DateTimeOffset.UtcNow - partial.Header.StartedAt >= minimumSession)
                    {
                        return file;
                    }
                }
            }

            await Task.Delay(500, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"no .partial with a flushed tick and {minimumSession.TotalSeconds:0} s of session appeared under {PartialDirectory} within {budget}");
    }

    private static async Task<(string Output, int Exit)> FinishAsync(Process p)
    {
        Task<string> err = p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        string stdout = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        await p.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (stdout + "\n---- stderr ----\n" + await err.ConfigureAwait(false) + $"\n---- exit {p.ExitCode} ----\n", p.ExitCode);
    }

    [Fact]
    public async Task AKilledAgentLeavesAPartialThatRecoverTurnsIntoAnInterruptedSessionExactlyOnce()
    {
        await GrantConsentAsync().ConfigureAwait(true);

        // Long enough to outlive the 30 s threshold, the flush after it and the kill.
        Process harness = StartHarness("--real --hold-presenting 90");
        try
        {
            // Disposed by the using even after Kill disposes it: Process.Dispose is idempotent, and the
            // alternative — a nullable local nulled at the kill — is dead code the analyzers reject.
            using Process agent = StartAgent("capture", "--exe", ConsentedExecutable, "--partial-flush-seconds", "2");

            // Past the Agent's 30 s minimum, plus one flush interval so the prefix on disk is past it too.
            string file = await WaitForRecoverablePartialAsync(TimeSpan.FromSeconds(34), TimeSpan.FromSeconds(90)).ConfigureAwait(true);
            PartialSession before = ReadLive(file)!;
            before.Records.Should().NotBeEmpty("the flush wrote the records drained so far");
            before.Notes.Should().Contain(note => note.Text.StartsWith("attached ", StringComparison.Ordinal),
                "the header is written before the ring is ours, so Tier 1 is the attached NOTE's evidence and not the header's field");

            // THE KILL. No finalize runs, nothing deletes the file, and the last chunk may be torn.
            Kill(agent);

            PartialSession after = ReadLive(file)!;
            int recordsOnDisk = after.Records.Count;
            recordsOnDisk.Should().BeGreaterThan(0, "the valid prefix survives the kill");

            using (Process recover = StartAgent("recover"))
            {
                (string output, int exit) = await FinishAsync(recover).ConfigureAwait(true);
                exit.Should().Be(0, output);
                string guid = Path.GetFileNameWithoutExtension(file);
                output.Should().Contain(guid + ": Recovered as session", output);
                File.Exists(file).Should().BeFalse("recovery removes what it stored");

                LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: TestContext.Current.CancellationToken).ConfigureAwait(true);
                await using (db.ConfigureAwait(true))
                {
                    SessionRow? row = await new SqliteSessionRepository(db)
                        .FindAsync(Guid.ParseExact(guid, "N"), TestContext.Current.CancellationToken).ConfigureAwait(true);
                    row.Should().NotBeNull();
                    row!.ExitStatus.Should().Be(ExitStatus.Interrupted, "nobody finalized it; the Agent died");
                    row.DurationSeconds.Should().BeGreaterThanOrEqualTo(30, "shorter than that and recovery would DISCARD it, correctly");
                    row.Tier.Should().Be(CaptureTier.Hooked);
                    row.FrameCount.Should().Be(recordsOnDisk, "the prefix IS the session");
                    row.CaptureNotes.Should().Contain("recovered from .partial");
                }
            }

            // AND ONCE ONLY. A second recover has nothing pending, so it cannot produce a duplicate row.
            using Process again = StartAgent("recover");
            (string secondOutput, int secondExit) = await FinishAsync(again).ConfigureAwait(true);
            secondExit.Should().Be(0, secondOutput);
            secondOutput.Should().Contain("recover: 0 pending .partial file(s)", secondOutput);
        }
        finally
        {
            Kill(harness);
        }
    }
}
