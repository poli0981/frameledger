using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>
/// FR-11: required on an empty ledger and after a version moves, one Accept writes four rows at the documents'
/// versions, a decline writes nothing and ends the flow, the steps advance, and read-only mode records nothing.
/// </summary>
public sealed class FirstRunViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IReadOnlyList<LegalDocument> Docs { get; } =
    [
        new("eula", "EULA", "1.0", "text", new Uri("https://github.com/x/eula")),
        new("gpl", "GPL", "GPL-3.0-only", "text", new Uri("https://github.com/x/gpl")),
        new("disclaimer", "Disclaimer", "2.0", "text", new Uri("https://github.com/x/d")),
        new("privacy", "Privacy", "2.0", "text", new Uri("https://github.com/x/p")),
    ];

    [Fact]
    public async Task RequiredOnAnEmptyLedgerAndSatisfiedByOneAccept()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new SqliteLegalAcceptanceStore(s.Db);
        var gate = new LegalGate(store, Docs);
        (await gate.OutstandingAsync(Ct)).Should().HaveCount(4);

        using var vm = new FirstRunViewModel(gate, new FakeAgentLink(), readOnly: false);
        vm.IsLegalStep.Should().BeTrue();
        vm.SelectedDocument!.Key.Should().Be("eula");

        await vm.AcceptCommand.ExecuteAsync(null);

        vm.IsAgentStep.Should().BeTrue("Accept moves to the Agent step");
        (await gate.IsRequiredAsync(Ct)).Should().BeFalse();
        IReadOnlyList<LegalAcceptance> rows = await store.ListAsync(Ct);
        rows.Select(static r => (r.Document, r.Version)).Should().BeEquivalentTo([("eula", "1.0"), ("gpl", "GPL-3.0-only"), ("disclaimer", "2.0"), ("privacy", "2.0")]);

        vm.NextCommand.Execute(null);
        vm.IsExplainerStep.Should().BeTrue();
        vm.NextCommand.Execute(null);
        vm.IsImportStep.Should().BeTrue();
        vm.NextCommand.Execute(null);
        vm.IsImportStep.Should().BeTrue("the last step stays");
        vm.BackCommand.Execute(null);
        vm.IsExplainerStep.Should().BeTrue();
        vm.FinishCommand.Execute(null);
        Task<bool> outcome = vm.Outcome;
        (await outcome).Should().BeTrue();
    }

    [Fact]
    public async Task AMovedVersionMakesOnlyThatDocumentOutstanding()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new SqliteLegalAcceptanceStore(s.Db);
        await new LegalGate(store, Docs).AcceptAllAsync(Ct);
        IReadOnlyList<LegalDocument> bumped = [.. Docs.Select(static d => string.Equals(d.Key, "disclaimer", StringComparison.Ordinal) ? d with { Version = "2.1" } : d)];
        var gate = new LegalGate(store, bumped);

        (await gate.OutstandingAsync(Ct)).Should().ContainSingle().Which.Key.Should().Be("disclaimer");
        (await gate.IsRequiredAsync(Ct)).Should().BeTrue("FR-11: re-shown when a document version increments");

        await gate.AcceptAllAsync(Ct);
        (await store.FindAsync("disclaimer", Ct))!.Version.Should().Be("2.1");
        (await gate.IsRequiredAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task DeclineWritesNothingAndEndsTheFlow()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new SqliteLegalAcceptanceStore(s.Db);
        using var vm = new FirstRunViewModel(new LegalGate(store, Docs), new FakeAgentLink(), readOnly: false);

        vm.DeclineCommand.Execute(null);

        Task<bool> outcome = vm.Outcome;
        (await outcome).Should().BeFalse();
        (await store.ListAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task ClosingTheWindowWithoutADecisionIsADecline()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using var vm = new FirstRunViewModel(new LegalGate(new SqliteLegalAcceptanceStore(s.Db), Docs), new FakeAgentLink(), readOnly: false);

        vm.Abandon();

        Task<bool> outcome = vm.Outcome;
        (await outcome).Should().BeFalse();
    }

    [Fact]
    public async Task ReadOnlyModeRecordsNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new SqliteLegalAcceptanceStore(s.Db);
        using var vm = new FirstRunViewModel(new LegalGate(store, Docs), new FakeAgentLink(), readOnly: true);

        await vm.AcceptCommand.ExecuteAsync(null);
        vm.CloseCommand.Execute(null);

        Task<bool> outcome = vm.Outcome;
        (await outcome).Should().BeTrue();
        (await store.ListAsync(Ct)).Should().BeEmpty("Settings ▸ Reopen shows, it does not re-record");
        vm.IsLegalStep.Should().BeTrue();
    }

    [Fact]
    public async Task TheAgentStepShowsTheHelloAcksFactsAndNAWithoutOne()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var link = new FakeAgentLink { Hello = null, IsConnected = false, State = AgentConnectionState.Starting };
        using var vm = new FirstRunViewModel(new LegalGate(new SqliteLegalAcceptanceStore(s.Db), Docs), link, readOnly: false);
        vm.AgentVersion.Should().Be(Strings.Common_NotAvailable);
        vm.CpuTemperature.Should().Be(Strings.Common_NotAvailable);

        link.Hello = new Shared.Ipc.HelloAck("1.2.3", Shared.Ipc.IpcProtocol.Version, 4, false, "b", false, "l2", true, "consent-dialog/1");
        link.State = AgentConnectionState.Connected;
        link.RaiseChanged();

        vm.AgentVersion.Should().Be("1.2.3");
        vm.TelemetrySource.Should().Be("l2");
        vm.CpuTemperature.Should().Be(Strings.Common_Yes);
        vm.Elevated.Should().Be(Strings.Common_No);
        vm.AgentState.Should().Be(AgentStatusPresentation.Pill(AgentConnectionState.Connected).Text);
    }
}
