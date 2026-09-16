using System.Diagnostics;
using Dapper;
using FluentAssertions;
using FrameLedger.Agent.Cli;
using FrameLedger.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// 2026-09-16: <c>FrameLedger.Agent --console sessions</c>, run against the owner's own ledger while its on-disk file
/// was at schema 2, applied scripts 0003 and 0004 to it — a verb whose name says it prints was a writer, because every
/// verb opened the ledger through the one migrating path. The print verbs now open read-only and refuse a schema they
/// would have had to migrate; the file is byte-identical afterwards, and the refusal is a console line with its own exit
/// code, never a crash dump.
/// </summary>
public sealed class AgentReadOnlyVerbsTests : IDisposable
{
    private const int _exitLedgerRefused = 7;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Agent => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "fl-agent-ro-" + Guid.NewGuid().ToString("N"));

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
    public void ThePrintVerbsAreExactlyThese()
    {
        // Each of these prints and changes nothing; every other verb writes (consent, capture, recovery, the kill switch,
        // the maintenance flags) and keeps the migrating open. A verb added here must not write.
        AgentVerb[] printsOnly = [.. Enum.GetValues<AgentVerb>().Where(Program.PrintsOnly)];
        printsOnly.Should().BeEquivalentTo([AgentVerb.Sessions, AgentVerb.ConsentList, AgentVerb.KillSwitchStatus, AgentVerb.DbPath]);
    }

    [Fact]
    public async Task SessionsAgainstAnOlderLedgerRefusesAndLeavesTheFileAlone()
    {
        await MakeOlderLedgerAsync().ConfigureAwait(true);
        byte[] before = await File.ReadAllBytesAsync(Ledger, Ct).ConfigureAwait(true);

        (string output, int exit) = await RunAsync("sessions", "--last", "1").ConfigureAwait(true);

        exit.Should().Be(_exitLedgerRefused, output);
        output.Should().Contain("older than this build").And.NotContain("crash dump", output);
        byte[] after = await File.ReadAllBytesAsync(Ledger, Ct).ConfigureAwait(true);
        after.Should().Equal(before, "the print verb migrated nothing");
        Directory.Exists(Path.Combine(_dataDir, "crashdumps")).Should().BeFalse("a refusal is not a crash");
    }

    [Fact]
    public async Task SessionsAgainstNoLedgerSaysSoAndCreatesNone()
    {
        Directory.CreateDirectory(_dataDir);

        (string output, int exit) = await RunAsync("sessions").ConfigureAwait(true);

        exit.Should().Be(_exitLedgerRefused, output);
        output.Should().Contain("does not exist");
        File.Exists(Ledger).Should().BeFalse("a read-only open creates nothing");
    }

    [Fact]
    public async Task DbPathNeedsNoLedgerAtAll()
    {
        Directory.CreateDirectory(_dataDir);

        (string output, int exit) = await RunAsync("db", "path").ConfigureAwait(true);

        exit.Should().Be(0, output);
        output.Should().Contain(Ledger);
        File.Exists(Ledger).Should().BeFalse();
    }

    private async Task MakeOlderLedgerAsync()
    {
        Directory.CreateDirectory(_dataDir);
        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: Ct).ConfigureAwait(false);
        await db.DisposeAsync().ConfigureAwait(false);
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Ledger, Pooling = false }.ToString());
        await using (c.ConfigureAwait(false))
        {
            await c.OpenAsync(Ct).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition("DELETE FROM schema_migrations WHERE version > 2", cancellationToken: Ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition("PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken: Ct)).ConfigureAwait(false);
        }
    }

    private async Task<(string Output, int Exit)> RunAsync(params string[] consoleArgs)
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

        using Process p = Process.Start(psi)!;
        Task<string> err = p.StandardError.ReadToEndAsync(Ct);
        string stdout = await p.StandardOutput.ReadToEndAsync(Ct).ConfigureAwait(false);
        await p.WaitForExitAsync(Ct).ConfigureAwait(false);
        return (stdout + "\n---- stderr ----\n" + await err.ConfigureAwait(false) + $"\n---- exit {p.ExitCode} ----\n", p.ExitCode);
    }
}
