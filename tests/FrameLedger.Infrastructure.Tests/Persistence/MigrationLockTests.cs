using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// The migration mutex is the ledger file's, not the session's (2026-09-17). With one name for every ledger, a test process
/// that froze mid-migration — <c>Infrastructure.Tests</c> on #199's run — held every other process's scratch ledger, and
/// seven App and Agent tests failed after 30 s with "another process has been migrating the ledger".
/// </summary>
public sealed class MigrationLockTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void EachLedgerFileHasItsOwnNameHoweverItIsSpelled()
    {
        string a = Path.Combine(Path.GetTempPath(), "fl-migrate-a", LedgerPaths.DatabaseFileName);
        string b = Path.Combine(Path.GetTempPath(), "fl-migrate-b", LedgerPaths.DatabaseFileName);

        MigrationRunner.LockNameFor(a).Should().StartWith(@"Local\FrameLedger.Ledger.Migrate.").And.NotBe(MigrationRunner.LockNameFor(b));
        MigrationRunner.LockNameFor(a.ToUpperInvariant()).Should().Be(MigrationRunner.LockNameFor(a));
    }

    [Fact]
    public async Task AMigrationHeldOnOneLedgerDoesNotHoldAnother()
    {
        // A thread that takes another ledger's lock and keeps it stands in for the frozen process.
        string other = Path.Combine(Path.GetTempPath(), "fl-migrate-held-" + Guid.NewGuid().ToString("N"), LedgerPaths.DatabaseFileName);
        using var taken = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            using var mutex = new Mutex(initiallyOwned: false, MigrationRunner.LockNameFor(other));
            mutex.WaitOne();
            taken.Set();
            release.Wait();
            mutex.ReleaseMutex();
        })
        { IsBackground = true };
        holder.Start();
        taken.Wait(Ct);
        try
        {
            var clock = Stopwatch.StartNew();
            await using LedgerFixture f = await LedgerFixture.OpenAsync().ConfigureAwait(true);

            clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "another ledger's migration is not this one's; one shared name waited 30 s and threw");
            f.Db.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
        }
        finally
        {
            release.Set();
            holder.Join();
        }
    }
}
