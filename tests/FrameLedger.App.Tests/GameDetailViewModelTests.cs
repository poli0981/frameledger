using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;

namespace FrameLedger.App.Tests;

/// <summary>
/// The game page over a scratch ledger with a scripted Agent: the header and the two rows, the Sessions tab's
/// negatives, the hooking toggle through <see cref="HookingConsent"/> (a refusal is a persistent notice on the
/// page, a stamp reloads the row), FR-2.2's disabled toggle with the reason inline, FR-1.4's removal revoking
/// consent over the pipe first.
/// </summary>
public sealed class GameDetailViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeAgent : IAgentRequests
    {
        public HelloAck? Hello { get; set; } = new("agent", IpcProtocol.Version, 1, false, "b", false, "l1", false, SafetyDisclosure.Version);

        public bool IsConnected { get; set; } = true;

        public List<SetHookEnabledRequest> Sent { get; } = [];

        public Func<SetHookEnabledRequest, IpcEnvelope> Answer { get; set; } = static r => IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.HookEnabledAck, "1", new HookEnabledAck(r.GameId, r.Enabled, "Written", "clean")));

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class
        {
            var request = (SetHookEnabledRequest)(object)payload;
            Sent.Add(request);
            return Task.FromResult(Answer(request));
        }
    }

    private sealed class FakePrompt : IConsentPrompt
    {
        public bool Accepts { get; set; } = true;

        public int Shown { get; private set; }

        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default)
        {
            Shown++;
            return Task.FromResult(Accepts);
        }
    }

    private sealed class FakeNavigator : IPageNavigator
    {
        public List<string> Pages { get; } = [];

        public void Navigate<TPage>()
            where TPage : class => Pages.Add(typeof(TPage).Name);

        public void GoBack() => Pages.Add("back");
    }

    private sealed class FakeConfirmations(RemoveGameChoice choice) : IConfirmations
    {
        public Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default) => Task.FromResult(choice);
    }

    private sealed class FakeEdit(GameMetadata? answer) : IEditGamePrompt
    {
        public Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default) => Task.FromResult(answer);
    }

    private sealed class NoSummaries : ISessionSummaryOpener
    {
        public List<long> Opened { get; } = [];

        public void Open(long sessionId) => Opened.Add(sessionId);
    }

    private sealed class FakeStrip : IMessageStrip
    {
        public List<string> Lines { get; } = [];

        public void Info(string title, string body) => Lines.Add("info:" + body);

        public void Success(string title, string body) => Lines.Add("success:" + body);

        public void Warn(string title, string body) => Lines.Add("warn:" + body);
    }

    private static async Task<(GameDetailViewModel Vm, FakeAgent Agent, FakePrompt Prompt, FakeNavigator Nav, FakeStrip Strip)> BuildAsync(
        ScratchLedger s, long gameId, RemoveGameChoice remove = RemoveGameChoice.Cancel, GameMetadata? edit = null)
    {
        var agent = new FakeAgent();
        var prompt = new FakePrompt();
        var nav = new FakeNavigator();
        var strip = new FakeStrip();
        var vm = new GameDetailViewModel(s.Library, new GameSelection { GameId = gameId }, new HookingConsent(agent, prompt), nav,
            new FakeConfirmations(remove), new FakeEdit(edit), strip, new NoSummaries(),
            new Charts.SessionSeriesLoader(s.Sessions), new Infrastructure.Persistence.SqliteHardwareSnapshotRepository(s.Db));
        Task pending = vm.Pending;
        await pending.ConfigureAwait(false);
        return (vm, agent, prompt, nav, strip);
    }

    /// <summary>
    /// The owner's rows 8 and 10 (2026-09-14): DLSS-G identified, the count refused, no factor. The measured row must
    /// name the technology and say the factor was not counted — not "N/A", and not "DLSS-G " with a trailing space —
    /// and the grid's FG× stays N/A because no factor exists.
    /// </summary>
    [Fact]
    public async Task AnIdentifiedButUncountedSessionNamesTheTechnologyInsteadOfNa()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Wukong");
            DateTimeOffset t0 = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
            await s.SessionAsync(game.Id, t0, hooked: true, fgMode: "dlssg", native: null, displayed: null, factor: null, fgRefusal: "no_evaluations", presented: 304.35, qualifier: "fg_runtime_loaded");

            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

            vm.Lifetime.Kind.Should().Be(FpsReadoutKind.IdentifiedUncounted);
            vm.Lifetime.Line.Should().Be("304 FPS");
            vm.Measured.Should().Contain("DLSS-G · factor not counted").And.NotContain(static m => m.EndsWith(' '));
            SessionItemViewModel row = vm.Sessions.Single();
            row.NativeText.Should().Be("304", "the Presented figure carries the Native column");
            row.DisplayedText.Should().Be("N/A");
            row.FgText.Should().Be("N/A", "no factor was counted, so none is shown");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public async Task TheHeaderTheTwoRowsAndTheSessionsTabReadFromTheLedger()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            await s.Games.UpdateMetadataAsync(game.Id, new GameMetadata { Name = "Alpha", Platform = "steam", Publisher = "Pub", GameVersion = "1.2" }, Ct);
            DateTimeOffset t0 = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
            await s.SessionAsync(game.Id, t0, hooked: true, fgMode: "dlssg", native: 62, displayed: 118, factor: 1.9);
            await s.SessionAsync(game.Id, t0.AddDays(1), hooked: false);

            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

            vm.NotFound.Should().BeFalse();
            vm.Name.Should().Be("Alpha");
            vm.Subtitle.Should().Be("Pub · 1.2 · Steam");
            vm.Lifetime.Kind.Should().Be(FpsReadoutKind.Generated, "the last HOOKED session, not the last session");
            vm.SupportsEmpty.Should().BeTrue("nothing wrote capability_flags before P4");
            vm.Measured.Should().Contain(static m => m.StartsWith("DLSS", StringComparison.Ordinal)).And.Contain(static m => m.StartsWith("DLSS-G", StringComparison.Ordinal));
            vm.Chips.Should().HaveCount(3);
            vm.Chips[0].IsYes.Should().BeTrue("rt_flag = yes on the last hooked session");

            vm.Sessions.Should().HaveCount(2);
            SessionItemViewModel tier2 = vm.Sessions[0];
            tier2.TierText.Should().Be("T2");
            tier2.NativeText.Should().Be("N/A");
            tier2.DisplayedText.Should().Be("N/A");
            tier2.ResolutionText.Should().Be("N/A");
            SessionItemViewModel tier1 = vm.Sessions[1];
            tier1.TierText.Should().Be("T1");
            tier1.NativeText.Should().Be("62");
            tier1.DisplayedText.Should().Be("118");
            tier1.FgText.Should().Be("×1.9");
            tier1.ResolutionText.Should().Be("1485×835 → 2560×1440 · DLSS");
            tier1.Rt.IsYes.Should().BeTrue();
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public async Task TogglingOnAsksTheAgentAndReloadsTogglingOffRevokes()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        (GameDetailViewModel vm, FakeAgent agent, FakePrompt prompt, _, _) = await BuildAsync(s, game.Id);
        vm.HookEnabled.Should().BeFalse();
        vm.HookToggleEnabled.Should().BeTrue();

        // The fake Agent answers Written; the real one would have stamped the row — mirror that here so the reload shows it.
        agent.Answer = r =>
        {
            _ = new Infrastructure.Persistence.SqliteGameConsentStore(s.Db).RecordOperatorAcknowledgementAsync(new Application.Consent.OperatorAcknowledgement
            {
                Fingerprint = game.Fingerprint,
                DisclosureVersion = SafetyDisclosure.Version,
                AcknowledgedAt = DateTimeOffset.UtcNow,
                Provenance = Domain.Consent.ConsentProvenance.ConsentDialog,
            }, Ct).AsTask().GetAwaiter().GetResult();
            return IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.HookEnabledAck, "1", new HookEnabledAck(r.GameId, true, "Written", "clean")));
        };
        vm.ToggleHookingCommand.Execute(null);
        Task pending1 = vm.Pending;
        await pending1;

        prompt.Shown.Should().Be(1);
        agent.Sent.Should().ContainSingle().Which.Should().Be(new SetHookEnabledRequest(game.Id, true, SafetyDisclosure.Version));
        vm.HookEnabled.Should().BeTrue("reloaded from the row the Agent stamped");
        vm.NoticeVisible.Should().BeFalse();

        agent.Answer = r => IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.HookEnabledAck, "1", new HookEnabledAck(r.GameId, false, "Written", null)));
        vm.ToggleHookingCommand.Execute(null);
        Task pending2 = vm.Pending;
        await pending2;
        agent.Sent.Last().Enabled.Should().BeFalse("on → off is a revoke, no dialog");
        prompt.Shown.Should().Be(1);
    }

    [Fact]
    public async Task ARefusalIsAPersistentNoticeAndABlockedRowDisablesTheToggleWithTheReason()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        (GameDetailViewModel vm, FakeAgent agent, _, _, _) = await BuildAsync(s, game.Id);
        agent.Answer = static r => IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.Refused, "1", new RefusedAck(r.GameId, "BlockedModule", "eac", "EasyAntiCheat_EOS.dll")));

        vm.ToggleHookingCommand.Execute(null);
        Task pending3 = vm.Pending;
        await pending3;

        vm.NoticeVisible.Should().BeTrue("a refusal is never a toast (08_UI §Notifications policy)");
        vm.NoticeText.Should().Contain("eac");
        vm.HookEnabled.Should().BeFalse();

        // The real Agent would also have written the block; plant it as the store does and reload.
        await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
            "UPDATE games SET hook_blocked_reason = 'eac: EasyAntiCheat_EOS.dll', hook_prescan_state = 'blocked' WHERE id = @id", new { id = game.Id }, tx, cancellationToken: ct)), Ct);
        await vm.LoadAsync(Ct);
        vm.HookToggleEnabled.Should().BeFalse("FR-2.2: the switch is disabled, not clickable");
        vm.BlockedText.Should().Contain("EasyAntiCheat_EOS.dll");
    }

    [Fact]
    public async Task RemovingRevokesOverThePipeFirstAndGoesBackToTheGrid()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await new Infrastructure.Persistence.SqliteGameConsentStore(s.Db).RecordOperatorAcknowledgementAsync(new Application.Consent.OperatorAcknowledgement
        {
            Fingerprint = game.Fingerprint,
            DisclosureVersion = SafetyDisclosure.Version,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            Provenance = Domain.Consent.ConsentProvenance.ConsentDialog,
        }, Ct);
        (GameDetailViewModel vm, FakeAgent agent, _, FakeNavigator nav, FakeStrip strip) = await BuildAsync(s, game.Id, remove: RemoveGameChoice.KeepSessions);
        vm.HookEnabled.Should().BeTrue();

        vm.RemoveCommand.Execute(null);
        Task pending4 = vm.Pending;
        await pending4;

        agent.Sent.Should().ContainSingle().Which.Enabled.Should().BeFalse("consent is withdrawn over the pipe, never by editing the table");
        (await s.Games.FindByIdAsync(game.Id, Ct))!.InLibrary.Should().BeFalse();
        nav.Pages.Should().Equal(nameof(GamesPage));
        strip.Lines.Should().ContainSingle().Which.Should().StartWith("info:");
    }

    [Fact]
    public async Task EditingWritesTheMetadataAndAnUnknownGameIsNotFound()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        (GameDetailViewModel vm, _, _, _, FakeStrip strip) = await BuildAsync(s, game.Id, edit: new GameMetadata { Name = "Alpha II", Publisher = "Pub" });

        vm.EditCommand.Execute(null);
        Task pending5 = vm.Pending;
        await pending5;
        vm.Name.Should().Be("Alpha II");
        strip.Lines.Should().ContainSingle().Which.Should().StartWith("success:");

        (GameDetailViewModel missing, _, _, _, _) = await BuildAsync(s, game.Id + 99);
        missing.NotFound.Should().BeTrue();
    }

    [Fact]
    public async Task SelectingASessionLoadsItsSeriesAndTheTrendFollowsTheToggle()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow.AddDays(-2), frames: 300, spikeEvery: 0);
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddDays(-1), hooked: false);
        (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

        vm.TrendPoints.Should().ContainSingle("one hooked session");
        vm.TrendEmpty.Should().BeFalse();
        vm.HardwareChanges.Should().BeEmpty("one snapshot");
        vm.TrendMetric = Charts.TrendMetric.Displayed;
        vm.TrendPoints.Should().BeEmpty("frame generation was measured as none: no displayed rate");
        vm.TrendMetric = Charts.TrendMetric.MaxGpuTemp;
        vm.TrendPoints.Single().Value.Should().Be(71);

        vm.SelectedSession = vm.Sessions[0];   // newest first: the Tier-2 one
        Task pending = vm.Pending;
        await pending;
        vm.SelectedHasSeries.Should().BeFalse();
        vm.SelectedNote.Should().Be(Strings.Tabs_SelectedNotHooked);

        vm.SelectedSession = vm.Sessions[1];
        Task pending2 = vm.Pending;
        await pending2;
        vm.SelectedHasSeries.Should().BeTrue();
        vm.SelectedSeries!.Presents.Should().Be(300);
        vm.HasLatency.Should().BeFalse("no Reflex on this session");
    }

    [Fact]
    public void CapabilityFlagsReadAsProductNamesInEitherShape()
    {
        GameDetailViewModel.CapabilityNames("{\"dlss\":true,\"dlssg\":true,\"fsr\":false,\"weird\":true}").Should().Equal("DLSS", "DLSS-G");
        GameDetailViewModel.CapabilityNames("[\"xess\",\"xefg\"]").Should().Equal("XeSS", "XeFG");
        GameDetailViewModel.CapabilityNames("not json").Should().BeEmpty();
        GameDetailViewModel.CapabilityNames(null).Should().BeEmpty();
    }
}
