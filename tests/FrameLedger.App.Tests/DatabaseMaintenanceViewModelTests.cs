using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// Tools ▸ Database maintenance (P4 PR-7) over a scratch ledger and a fake Agent: the check and the backup read, a
/// backup never lands on the ledger itself, the sweep is the Agent's (asked, confirmed, never with an unlimited
/// setting), compaction waits for sessions to end, and every failure is a status line.
/// </summary>
public sealed class DatabaseMaintenanceViewModelTests
{
    private sealed class Saver(string? path) : IFileSaver
    {
        public int Asked { get; private set; }

        public string? PickSavePath(string filter, string suggestedName)
        {
            Asked++;
            return path;
        }
    }

    private sealed class Prompts(bool confirm) : IDatabaseMaintenancePrompts
    {
        public List<int> Confirmations { get; } = [];

        public Task ShowAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> ConfirmSweepAsync(int keep, CancellationToken ct = default)
        {
            Confirmations.Add(keep);
            return Task.FromResult(confirm);
        }
    }

    private static DatabaseMaintenanceViewModel Vm(ScratchLedger s, FakeAgentLink agent, IFileSaver? saver = null, Prompts? prompts = null, MemorySettings? store = null) =>
        new(new LedgerMaintenance(s.Db), s.Db.Path, agent, saver ?? new Saver(null), prompts ?? new Prompts(true), new RegisteredSettings(store ?? new MemorySettings()));

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "fl-maint-" + Guid.NewGuid().ToString("N") + ".db");

    /// <summary>True when <paramref name="actual"/> is <paramref name="format"/> with any text in its placeholders.</summary>
    private static bool MatchesFormat(string format, string actual)
    {
        string pattern = "^" + Regex.Replace(Regex.Escape(format), @"\\\{\d+\}", ".+", RegexOptions.None, TimeSpan.FromSeconds(1)) + "$";
        return Regex.IsMatch(actual, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TheIntegrityCheckOfAHealthyLedgerSaysSo()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        DatabaseMaintenanceViewModel vm = Vm(s, new FakeAgentLink());

        await vm.CheckIntegrityCommand.ExecuteAsync(null);

        vm.Status.Should().Be(Strings.Maintenance_Integrity_Ok);
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ABackupGoesToTheChosenFile()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string target = TempFile();
        try
        {
            DatabaseMaintenanceViewModel vm = Vm(s, new FakeAgentLink(), new Saver(target));

            await vm.BackUpCommand.ExecuteAsync(null);

            File.Exists(target).Should().BeTrue();
            vm.Status.Should().Contain(target);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(target);
        }
    }

    [Fact]
    public async Task ABackupOntoTheLedgerItselfIsRefusedAndTheLedgerIsUntouched()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        long before = new FileInfo(s.Db.Path).Length;
        DatabaseMaintenanceViewModel vm = Vm(s, new FakeAgentLink(), new Saver(s.Db.Path.ToUpperInvariant()));

        await vm.BackUpCommand.ExecuteAsync(null);

        vm.Status.Should().Be(Strings.Maintenance_Backup_IsLedger);
        File.Exists(s.Db.Path).Should().BeTrue("the save dialog's overwrite answer must never delete the live database");
        new FileInfo(s.Db.Path).Length.Should().Be(before);
        await vm.CheckIntegrityCommand.ExecuteAsync(null);
        vm.Status.Should().Be(Strings.Maintenance_Integrity_Ok);
    }

    [Fact]
    public async Task ACancelledSaveDialogDoesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var saver = new Saver(null);
        DatabaseMaintenanceViewModel vm = Vm(s, new FakeAgentLink(), saver);

        await vm.BackUpCommand.ExecuteAsync(null);

        saver.Asked.Should().Be(1);
        vm.Status.Should().Be(Strings.Maintenance_Status_Ready);
    }

    [Fact]
    public async Task AnUnlimitedRetentionSweepsNothingAndAsksNobody()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink();
        var prompts = new Prompts(true);
        var store = new MemorySettings();
        store.Rows[SettingsRegistry.RetentionRawSessionsPerGame.Key] = "0";
        DatabaseMaintenanceViewModel vm = Vm(s, agent, prompts: prompts, store: store);

        await vm.SweepCommand.ExecuteAsync(null);

        vm.Status.Should().Be(Strings.Maintenance_Sweep_Unlimited);
        prompts.Confirmations.Should().BeEmpty();
        agent.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WithNoAgentTheSweepSaysWhoseItIs()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink { IsConnected = false };
        var prompts = new Prompts(true);
        DatabaseMaintenanceViewModel vm = Vm(s, agent, prompts: prompts);

        await vm.SweepCommand.ExecuteAsync(null);

        vm.Status.Should().Be(Strings.Maintenance_Sweep_NoAgent);
        prompts.Confirmations.Should().BeEmpty("nothing is asked that cannot be done");
    }

    [Fact]
    public async Task ADeclinedSweepSendsNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink();
        var prompts = new Prompts(false);
        DatabaseMaintenanceViewModel vm = Vm(s, agent, prompts: prompts);

        await vm.SweepCommand.ExecuteAsync(null);

        prompts.Confirmations.Should().Equal(20);
        agent.Sent.Should().BeEmpty();
        vm.Status.Should().Be(Strings.Maintenance_Status_Ready);
    }

    [Fact]
    public async Task AConfirmedSweepIsTheAgentsAndItsCountIsShown()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink
        {
            Answer = static (type, _) => FakeAgentLink.Envelope(IpcMessageType.SweepRetentionAck, new SweepRetentionAck(20, 4, 9)),
        };
        DatabaseMaintenanceViewModel vm = Vm(s, agent);

        await vm.SweepCommand.ExecuteAsync(null);

        agent.Sent.Should().ContainSingle().Which.Type.Should().Be(IpcMessageType.SweepRetention);
        vm.Status.Should().Be(string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Strings.Maintenance_Sweep_Done_Format), 9, 4, 20));
    }

    [Fact]
    public async Task AnAgentThatPredatesTheSweepIsNamedAsSuch()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink
        {
            Answer = static (_, _) => throw new IpcRequestException(IpcErrorCode.UnknownType, "unknown message type SweepRetention"),
        };
        DatabaseMaintenanceViewModel vm = Vm(s, agent);

        await vm.SweepCommand.ExecuteAsync(null);

        vm.Status.Should().Be(Strings.Maintenance_Sweep_AgentTooOld);
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CompactingWaitsWhileASessionRuns()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink
        {
            Answer = static (type, _) => FakeAgentLink.Envelope(IpcMessageType.StatusAck, new StatusAck("capturing", null, 1, [])),
        };
        DatabaseMaintenanceViewModel vm = Vm(s, agent);

        await vm.CompactCommand.ExecuteAsync(null);

        agent.Sent.Should().ContainSingle().Which.Type.Should().Be(IpcMessageType.GetStatus, "a fresh status, not the connection's cached one");
        vm.Status.Should().Be(Strings.Maintenance_Compact_SessionRunning);
    }

    [Fact]
    public async Task CompactingRunsWhenTheAgentIsIdleOrAbsent()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var idle = new FakeAgentLink
        {
            Answer = static (_, _) => FakeAgentLink.Envelope(IpcMessageType.StatusAck, new StatusAck("idle", null, null, [])),
        };
        DatabaseMaintenanceViewModel withIdleAgent = Vm(s, idle);
        await withIdleAgent.CompactCommand.ExecuteAsync(null);
        MatchesFormat(Strings.Maintenance_Compact_Done_Format, withIdleAgent.Status).Should().BeTrue(withIdleAgent.Status);

        DatabaseMaintenanceViewModel withoutAgent = Vm(s, new FakeAgentLink { IsConnected = false });
        await withoutAgent.CompactCommand.ExecuteAsync(null);
        MatchesFormat(Strings.Maintenance_Compact_Done_Format, withoutAgent.Status).Should().BeTrue(withoutAgent.Status);
    }
}
