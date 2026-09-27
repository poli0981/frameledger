using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Startup;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Tests.Startup;

/// <summary>
/// The Agent's admin mode (beta.10, owner decision D34), below the prompt: the setting read before the Agent exists, the
/// marker that keeps two prompts from racing, the account check, the lock whose DACL survives an elevated creator, and the
/// start that gives a game the shell's token. The UAC prompt itself is the owner's to check on real hardware (HANDOFF).
/// </summary>
public sealed class AgentElevationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-elev-" + Guid.NewGuid().ToString("N"));

    public AgentElevationTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A pooled handle can outlive the test; a scratch folder left under %TEMP% is not worth a red build.
        }
    }

    private string Ledger => Path.Combine(_dir, LedgerPaths.DatabaseFileName);

    [Fact]
    public async Task TheSettingIsOnOnlyWhenTheLedgerSaysExactlyOne()
    {
        RunElevatedSetting.Read(Ledger).Should().BeFalse("no ledger yet is off — and nothing is created");
        File.Exists(Ledger).Should().BeFalse();

        await using (LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: TestContext.Current.CancellationToken))
        {
            RunElevatedSetting.Read(Ledger).Should().BeFalse("a ledger without the row is off (the registry's default)");
            await new SqliteSettingsStore(db).SetAsync(RunElevatedSetting.Key, "1", TestContext.Current.CancellationToken);
            RunElevatedSetting.Read(Ledger).Should().BeTrue("read while the writer's connection is open, as the App reads it beside the Agent");
            await new SqliteSettingsStore(db).SetAsync(RunElevatedSetting.Key, "0", TestContext.Current.CancellationToken);
            RunElevatedSetting.Read(Ledger).Should().BeFalse();
        }
    }

    [Fact]
    public void ALedgerWithoutASettingsTableIsOffAndIsNotMigrated()
    {
        // The first start after an update meets a ledger at an older schema; this read must neither fail nor migrate it.
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Ledger, Pooling = false }.ToString()))
        {
            connection.Open();
            using SqliteCommand create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE unrelated (x INTEGER)";
            create.ExecuteNonQuery();
        }

        RunElevatedSetting.Read(Ledger).Should().BeFalse();
        using (var check = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Ledger, Pooling = false }.ToString()))
        {
            check.Open();
            using SqliteCommand tables = check.CreateCommand();
            tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'";
            tables.ExecuteScalar().Should().Be(1L, "nothing was written to the file");
        }
    }

    [Fact]
    public void OneStartAtATimeMayAskForElevation()
    {
        ElevationMarker.IsHeld(_dir).Should().BeFalse();
        using (ElevationMarker? first = ElevationMarker.TryAcquire(_dir))
        {
            first.Should().NotBeNull();
            ElevationMarker.IsHeld(_dir).Should().BeTrue();
            ElevationMarker.TryAcquire(_dir).Should().BeNull("a second prompt beside the first");
            AgentInstanceLock.IsHeld(_dir).Should().BeFalse("the marker is its own object, not the Agent's claim");
        }

        ElevationMarker.IsHeld(_dir).Should().BeFalse("the marker goes with its handle");
        ElevationMarker.NameFor(_dir).Should().StartWith(@"Local\FrameLedger.Agent.Elevating.")
            .And.EndWith(AgentInstanceLock.NameFor(_dir)[@"Local\FrameLedger.Agent.".Length..], "the folder's key, its own name");
    }

    [Fact]
    public void TheElevatedAgentKnowsWhetherItIsTheUserWhoAsked()
    {
        string me = AgentElevation.CurrentUserSid();
        AgentElevation.IsCurrentUser(me).Should().BeTrue();
        AgentElevation.IsCurrentUser("s" + me[1..]).Should().BeTrue("a SID's letter compares without case");
        AgentElevation.IsCurrentUser("S-1-5-18").Should().BeFalse("LocalSystem is another account");
        AgentElevation.Refusals.Should().Equal(AgentElevation.Declined, AgentElevation.OtherAccount, AgentElevation.Failed);
    }

    [Fact]
    public void TheMigrationLockIsTheUsersWhoeverCreatedIt()
    {
        SecurityIdentifier user = PipeAccessControl.CurrentUser();
        var descriptor = new RawSecurityDescriptor(SharedMutex.Sddl(user));
        descriptor.Owner.Should().Be(user, "an elevated creator's default owner would be Administrators");
        descriptor.DiscretionaryAcl!.Cast<CommonAce>().Select(static a => a.SecurityIdentifier)
            .Should().Contain(user, "the same user's unelevated process opens it — an elevated default DACL does not list the user");

        string name = @"Local\FrameLedger.Test.Mutex." + Guid.NewGuid().ToString("N");
        using SharedMutex first = SharedMutex.CreateOrOpen(name);
        using SharedMutex second = SharedMutex.CreateOrOpen(name);
        first.Wait(TimeSpan.FromSeconds(1), out bool abandoned).Should().BeTrue();
        abandoned.Should().BeFalse();
        first.Release();
        second.Wait(TimeSpan.FromSeconds(1), out _).Should().BeTrue("released, the other handle takes it");
        second.Release();
    }

    [Fact]
    public void TheEnvironmentBlockIsThisProcessesWithTheCallersOnTopSortedAndDoublyTerminated()
    {
        string block = UnelevatedProcess.EnvironmentBlock(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FL_TEST_VARIABLE"] = "yes",
            ["PATH"] = "C:\\only",
        });

        block.Should().EndWith("\0\0");
        string[] entries = block.TrimEnd('\0').Split('\0');
        entries.Should().Contain("FL_TEST_VARIABLE=yes").And.Contain("PATH=C:\\only").And.NotContain(static e => e.StartsWith("PATH=", StringComparison.OrdinalIgnoreCase) && e != "PATH=C:\\only");
        entries.Select(static e => e[..e.IndexOf('=', StringComparison.Ordinal)]).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What an elevated Agent gives a game (D34): the shell's token. On an unelevated run there is nothing to test — the call
    /// needs the impersonation privilege an elevated token holds — and a machine without a shell has no token to give; both
    /// skip. Where it runs, the child's elevation is the SHELL's: a runner with UAC off has an elevated shell, and a game
    /// started from it would be elevated by a double-click too.
    /// </summary>
    [Fact]
    public void AGameStartedByAnElevatedAgentGetsTheShellsElevation()
    {
        Assert.SkipUnless(Environment.IsPrivilegedProcess, "CreateProcessWithTokenW needs SeImpersonatePrivilege, which only an elevated token holds");
        int? shell = UnelevatedProcess.ShellProcessId();
        Assert.SkipWhen(shell is null, "no desktop shell in this session");
        string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        (int Pid, IDisposable Process)? started = UnelevatedProcess.Start(cmd, "/c ping -n 3 127.0.0.1 >nul", _dir,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), out int error);

        started.Should().NotBeNull($"the start failed with Win32 error {error}");
        using (started!.Value.Process)
        {
            TokenPrivileges.IsElevated(started.Value.Pid).Should().Be(TokenPrivileges.IsElevated(shell!.Value),
                "the game runs as a double-click would run it, never with the Agent's token");
            using Process child = Process.GetProcessById(started.Value.Pid);
            child.WaitForExit(10_000).Should().BeTrue();
        }
    }
}
