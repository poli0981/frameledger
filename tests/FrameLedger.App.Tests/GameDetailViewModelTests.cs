using System.Globalization;
using System.IO;
using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
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
[Collection(StringsCultureCollection.Name)]
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

    private sealed class FakePicker(string? path) : IGamePicker
    {
        public string? PickExecutable() => path;
    }

    private static async Task<(GameDetailViewModel Vm, FakeAgent Agent, FakePrompt Prompt, FakeNavigator Nav, FakeStrip Strip)> BuildAsync(
        ScratchLedger s, long gameId, RemoveGameChoice remove = RemoveGameChoice.Cancel, GameMetadata? edit = null, string? pick = null,
        FakeAgent? withAgent = null, RegisteredSettings? settings = null)
    {
        FakeAgent agent = withAgent ?? new FakeAgent();
        var prompt = new FakePrompt();
        var nav = new FakeNavigator();
        var strip = new FakeStrip();
        var vm = new GameDetailViewModel(s.Library, new GameSelection { GameId = gameId }, new HookingConsent(agent, prompt), nav,
            new FakeConfirmations(remove), new FakeEdit(edit), strip, new NoSummaries(),
            new Charts.SessionSeriesLoader(s.Sessions), new Infrastructure.Persistence.SqliteHardwareSnapshotRepository(s.Db), new SessionSelection(),
            new FakePicker(pick), settings);
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

    /// <summary>The Supports row (P4 PR-1): what the Agent's detection sweep wrote, as product names, and nothing measured.</summary>
    [Fact]
    public async Task TheSupportsRowReadsWhatTheDetectionSweepWrote()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Scanned");
            (await s.Games.ApplyDetectionAsync(game.Id, new DetectionWrite
            {
                EngineId = "unreal",
                EngineVersion = "5.4",
                PlatformId = "steam",
                CapabilityIds = ["dlss", "dlss_g", "streamline"],
                RulesVersion = "2026.09.1",
                ExeSizeBytes = 1,
                ExeMtimeMs = 1,
            }, Ct)).Should().BeTrue();

            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

            vm.SupportsEmpty.Should().BeFalse();
            vm.Supports.Should().Equal("Supports DLSS", "Supports DLSS-G", "Supports Streamline");
            vm.Subtitle.Should().Contain("Steam", "the detected platform reaches the header");
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
            vm.SupportsEmpty.Should().BeTrue("the Agent's sweep has not written capability_flags for this game yet");
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

    /// <summary>
    /// The recording switch (schema 0009, 2026-09-23): one click writes the row — nothing is asked of the Agent, which reads
    /// the table each tick — the page says the program is ignored, and the library card says "Not recorded" in place of the
    /// hooking state.
    /// </summary>
    [Fact]
    public async Task TheRecordingSwitchWritesTheRowAndTheCardSaysNotRecorded()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Borderless Gaming");
            (GameDetailViewModel vm, FakeAgent agent, _, _, _) = await BuildAsync(s, game.Id);
            vm.RecordSessions.Should().BeTrue();
            vm.RecordingStatusText.Should().Be(Strings.GameDetail_Recording_On);

            vm.ToggleRecordingCommand.Execute(null);
            Task off = vm.Pending;
            await off;

            vm.RecordSessions.Should().BeFalse("reloaded from the row");
            vm.RecordingStatusText.Should().Be(Strings.GameDetail_Recording_Off);
            agent.Sent.Should().BeEmpty("the Agent reads the same table; nothing goes over the pipe");
            GameRow row = (await s.Games.FindByIdAsync(game.Id, Ct))!;
            row.RecordSessions.Should().BeFalse();
            var card = new GameCardViewModel(new GameCard(row with { HookEnabled = true }, null));
            card.HookText.Should().Be(Strings.Games_Card_NotRecorded, "an ignored program is not hooked, whatever its switch says");
            card.HookOn.Should().BeFalse();

            vm.ToggleRecordingCommand.Execute(null);
            Task on = vm.Pending;
            await on;
            vm.RecordSessions.Should().BeTrue();
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

    /// <summary>
    /// beta.8 (owner request 2026-09-25): an anti-cheat game's Hooking card — a switch that can never be turned on — gives
    /// way to the finding alone while <c>ui.hide_anticheat_hooking</c> is on, its default; off, the card is back with the
    /// finding under its disabled switch. Either way the finding is on the page (FR-2.2), in words, and a game nothing was
    /// found in keeps its card whatever the setting says.
    /// </summary>
    [Fact]
    public async Task AnAntiCheatGameShowsTheFindingInPlaceOfItsHookingCardUnlessTheSettingIsOff()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            GameRow clean = await s.GameAsync("Beta");
            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
                "UPDATE games SET hook_enabled = 0, hook_blocked_reason = 'AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat', hook_prescan_state = 'blocked', "
                + "hook_autodisabled_reason = 'crashed twice' "
                + "WHERE id = @id", new { id = game.Id }, tx, cancellationToken: ct)), Ct);
            var settings = new RegisteredSettings(new MemorySettings());
            const string found = "Easy Anti-Cheat — its folder EasyAntiCheat ships with the game";

            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id, settings: settings);
            vm.HookingSectionVisible.Should().BeFalse("hidden by default");
            vm.AntiCheatText.Should().Contain(found);
            vm.HookToggleEnabled.Should().BeFalse();
            vm.AutoDisabledText.Should().BeNull("its 'turn hooking back on' could only be refused");

            await settings.SetAsync(SettingsRegistry.UiHideAntiCheatHooking, false, Ct);
            await vm.LoadAsync(Ct);
            vm.HookingSectionVisible.Should().BeTrue("read at each load");
            vm.AntiCheatText.Should().BeNull("the card's own line says it now");
            vm.BlockedText.Should().Contain(found);
            vm.HookToggleEnabled.Should().BeFalse("FR-2.2: disabled, not clickable");

            await settings.SetAsync(SettingsRegistry.UiHideAntiCheatHooking, true, Ct);
            (GameDetailViewModel other, _, _, _, _) = await BuildAsync(s, clean.Id, settings: settings);
            other.HookingSectionVisible.Should().BeTrue("nothing was found in it");
            other.AntiCheatText.Should().BeNull();
            other.HookToggleEnabled.Should().BeTrue();
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>
    /// D33 (owner decision 2026-09-26): with the option on, a blocked game the Agent found eligible offers the exception;
    /// one in force brings the Hooking card back with its switch usable, the finding still under it; with the option off
    /// the kept exception says it is suspended and the switch is disabled again.
    /// </summary>
    [Fact]
    public async Task AnEligibleGameOffersTheExceptionAndOneInForceMakesTheSwitchUsable()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("GF2");
            const string block = "AntiCheatFile|NetEase Yidun|NEP2.dll";
            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
                "UPDATE games SET hook_enabled = 0, hook_blocked_reason = @block, hook_prescan_state = 'blocked', ac_exception_eligible = 1, "
                + "ac_exception_verdict = 'AllowedUnderUserModeException|NetEase Yidun|NEP2.dll', ac_exception_sessions = 3, "
                + "ac_exception_checked_block = @block WHERE id = @id", new { id = game.Id, block }, tx, cancellationToken: ct)), Ct);
            var settings = new RegisteredSettings(new MemorySettings());

            (GameDetailViewModel off, _, _, _, _) = await BuildAsync(s, game.Id, settings: settings);
            off.ExceptionCardVisible.Should().BeFalse("off by default: nothing about exceptions shows");
            off.CanGrantException.Should().BeFalse();

            await settings.SetAsync(SettingsRegistry.HookingUserModeExceptions, true, Ct);
            await off.LoadAsync(Ct);
            off.ExceptionCardVisible.Should().BeTrue();
            off.ExceptionText.Should().Be(Strings.Exception_State_Eligible);
            off.CanGrantException.Should().BeTrue();
            off.HookToggleEnabled.Should().BeFalse("eligible is not granted: the block still decides");

            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
                "UPDATE games SET ac_exception_at = 1000, ac_exception_family = 'NetEase Yidun', ac_exception_disclosure_version = 'ac-exception-dialog/1', "
                + "ac_exception_exe_size_bytes = exe_size_bytes, ac_exception_exe_mtime_ms = exe_mtime_ms WHERE id = @id", new { id = game.Id }, tx, cancellationToken: ct)), Ct);
            await off.LoadAsync(Ct);
            off.CanWithdrawException.Should().BeTrue();
            off.CanGrantException.Should().BeFalse();
            off.ExceptionInForceText.Should().Contain("NetEase Yidun");
            off.HookingSectionVisible.Should().BeTrue("an exception in force brings the Hooking card back even while such cards are hidden");
            off.HookToggleEnabled.Should().BeTrue("hooking is turned on through FR-2.1's dialog, as ever");
            off.BlockedText.Should().Contain("NetEase Yidun", "the finding is always on the page (FR-2.2)");

            await settings.SetAsync(SettingsRegistry.HookingUserModeExceptions, false, Ct);
            await off.LoadAsync(Ct);
            off.ExceptionCardVisible.Should().BeTrue("a kept exception is shown, suspended");
            off.ExceptionSuspendedText.Should().Be(Strings.GameDetail_Exception_Suspended);
            off.HookToggleEnabled.Should().BeFalse();
            off.HookingSectionVisible.Should().BeFalse();
            new GameCardViewModel(new GameCard(off.Game!, null)).HookText.Should().Be(Strings.Games_Card_Exception);
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>D33: a game the guard would let through but with one hooked session says so; a kernel-level family says why not.</summary>
    [Fact]
    public async Task WhyAGameIsNotEligibleIsSaidOnItsPage()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow few = await s.GameAsync("Few");
            GameRow aniimo = await s.GameAsync("Aniimo");
            GameRow eac = await s.GameAsync("Eac");
            await s.Db.WriteAsync(async (c, tx, ct) =>
            {
                const string sql = "UPDATE games SET hook_blocked_reason = @block, hook_prescan_state = 'blocked', ac_exception_verdict = @verdict, "
                                   + "ac_exception_sessions = @sessions, ac_exception_checked_block = @block WHERE id = @id";
                await Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(sql, new
                {
                    id = few.Id,
                    block = "AntiCheatFile|NetEase Yidun|NEP2.dll",
                    verdict = "AllowedUnderUserModeException|NetEase Yidun|NEP2.dll",
                    sessions = 1
                }, tx, cancellationToken: ct)).ConfigureAwait(false);
                await Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(sql, new
                {
                    id = aniimo.Id,
                    block = "AntiCheatFile|NetEase Yidun|NEP2.dll",
                    verdict = "AntiCheatFile|Kernel driver in the game folder|NEPKernel.sys",
                    sessions = 1
                }, tx, cancellationToken: ct)).ConfigureAwait(false);
                await Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(sql, new
                {
                    id = eac.Id,
                    block = "AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat",
                    verdict = "AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat",
                    sessions = 4
                }, tx, cancellationToken: ct)).ConfigureAwait(false);
                return 0;
            }, Ct);
            var settings = new RegisteredSettings(new MemorySettings());
            await settings.SetAsync(SettingsRegistry.HookingUserModeExceptions, true, Ct);

            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, few.Id, settings: settings);
            vm.ExceptionText.Should().Contain("1").And.Contain("two");
            vm.CanGrantException.Should().BeFalse();

            (vm, _, _, _, _) = await BuildAsync(s, aniimo.Id, settings: settings);
            vm.ExceptionText.Should().Contain("Kernel driver in the game folder").And.Contain("NEPKernel.sys");

            (vm, _, _, _, _) = await BuildAsync(s, eac.Id, settings: settings);
            vm.ExceptionText.Should().Contain("Easy Anti-Cheat").And.Contain("not user-mode");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>The library card says "Anti-cheat" for such a game (beta.8) — after "Not recorded", which says more.</summary>
    [Fact]
    public async Task TheLibraryCardSaysAntiCheatForAGameTheGuardFoundItIn()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        GameRow blocked = game with { HookEnabled = false, HookBlockedReason = "BlockedModule|BattlEye|BEClient_x64.dll", HookPrescanState = "blocked" };

        var card = new GameCardViewModel(new GameCard(blocked, null));
        card.AntiCheat.Should().BeTrue();
        card.HookText.Should().Be(Strings.Games_Card_AntiCheat);
        card.HookOn.Should().BeFalse();

        var prescanOnly = new GameCardViewModel(new GameCard(game with { HookPrescanState = "blocked" }, null));
        prescanOnly.AntiCheat.Should().BeTrue("a block written before the reason column existed is still a block");

        var ignored = new GameCardViewModel(new GameCard(blocked with { RecordSessions = false }, null));
        ignored.HookText.Should().Be(Strings.Games_Card_NotRecorded);
        ignored.AntiCheat.Should().BeFalse("the pill says one thing");

        new GameCardViewModel(new GameCard(game with { HookPrescanState = "clean" }, null)).AntiCheat.Should().BeFalse();
    }

    /// <summary>
    /// beta.8 (schema 0011): the Details card says what the files say — what the executable runs as, its versions, the
    /// store's build labelled as one, the engine, and what it ships — and the subtitle labels the store's version too. An
    /// x86 executable's switch cannot be turned on, and the page says why; one already on can still be turned off.
    /// </summary>
    [Fact]
    public async Task TheDetailsCardSaysWhatTheFilesSayAndAnX86SwitchCannotBeTurnedOn()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            GameRow fresh = await s.GameAsync("Beta");
            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
                "UPDATE games SET platform = 'steam', game_version = '20374416', field_provenance = '{\"game_version\":\"detected\",\"platform\":\"detected\"}', "
                + "engine = 'unreal', engine_version = '5.3', exe_machine = 'x86', exe_file_version = '1.0.2.0', exe_product_version = '1.0.2', "
                + "library_versions = '[{\"capability\":\"dlss\",\"path\":\"bin/nvngx_dlss.dll\",\"fileVersion\":\"3.7.10.0\"},{\"capability\":\"xess\",\"path\":\"libxess.dll\"}]' "
                + "WHERE id = @id", new { id = game.Id }, tx, cancellationToken: ct)), Ct);

            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

            vm.Subtitle.Should().Be("Steam build 20374416 · Unreal Engine 5.3", "the store names itself in its version");
            vm.Details.Should().Equal(
                new GameDetailRow("Runs as", "32-bit (x86)"),
                new GameDetailRow("File version", "1.0.2.0"),
                new GameDetailRow("Product version", "1.0.2"),
                new GameDetailRow("Store", "Steam build 20374416"),
                new GameDetailRow("Engine", "Unreal Engine 5.3"),
                new GameDetailRow("Ships with", "DLSS: nvngx_dlss.dll 3.7.10.0" + Environment.NewLine + "XeSS: libxess.dll (no version)"));
            vm.HookToggleEnabled.Should().BeFalse("turning hooking on could only be refused");
            vm.NotX64Text.Should().Contain("32-bit (x86)");

            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
                "UPDATE games SET hook_enabled = 1, hook_autodisabled_reason = 'crashed twice' WHERE id = @id", new { id = game.Id }, tx, cancellationToken: ct)), Ct);
            await vm.LoadAsync(Ct);
            vm.HookToggleEnabled.Should().BeTrue("a switch that is on can be turned off whatever the file is");

            (GameDetailViewModel unread, _, _, _, _) = await BuildAsync(s, fresh.Id);
            unread.Details.Should().ContainSingle().Which.Should().Be(new GameDetailRow("Runs as", Strings.GameDetail_Details_NotRead));
            unread.NotX64Text.Should().BeNull("not read is not known 32-bit");
            unread.HookToggleEnabled.Should().BeTrue();
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>
    /// beta.8: the Details card names the NVIDIA App's overrides as the driver profile stood at the newest session that read
    /// one; a game no session read a profile for has no such row.
    /// </summary>
    [Fact]
    public async Task TheDetailsCardNamesTheNvidiaAppsOverridesFromTheNewestSessionThatReadThem()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow game = await s.GameAsync("Alpha");
            long older = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-3));
            _ = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1));
            string profile = Application.Recording.DriverProfileRecord.Serialize(new Application.Capture.DriverProfileReading
            {
                Outcome = Application.Capture.DriverProfileOutcome.Application,
                ProfileName = "Alpha",
                Settings =
                [
                    new(Application.Capture.NvidiaDriverSettings.DlssSrOverride, 1, Application.Capture.DriverSettingLocation.Profile, false),
                    new(Application.Capture.NvidiaDriverSettings.DlssSrPreset, 11, Application.Capture.DriverSettingLocation.Profile, false),
                ],
            })!;
            (GameDetailViewModel before, _, _, _, _) = await BuildAsync(s, game.Id);
            before.Details.Should().NotContain(static r => r.Label == "NVIDIA App override", "no session read a profile");

            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
                "UPDATE sessions SET driver_profile = @p WHERE id = @id", new { p = profile, id = older }, tx, cancellationToken: ct)), Ct);
            (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

            vm.Details.Should().ContainSingle(static r => r.Label == "NVIDIA App override")
                .Which.Value.Should().Be("DLSS override (preset K)", "the newest session that read one; the newest read none");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>The Agent's refusal for such an executable (beta.8, <c>ExecutableNotX64</c>) is said in words, with the architecture.</summary>
    [Fact]
    public void ANotX64RefusalNamesTheArchitecture()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            new HookingConsentResult(HookingConsentOutcome.Refused, Refusal: new RefusedAck(1, "ExecutableNotX64", null, "anycpu32")).RefusalText()
                .Should().Contain(Formats.Architecture("anycpu32")).And.Contain("64-bit (x64) games only");
        }
        finally
        {
            Strings.Culture = previous;
        }
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
    public async Task AMissingExecutableIsSaidOnThePageAndAPresentOneIsNot()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow missing = await s.GameAsync("Gone");
        string real = Path.Combine(Path.GetTempPath(), "fl-present-exe-" + Guid.NewGuid().ToString("N") + ".exe");
        await File.WriteAllBytesAsync(real, new byte[12], Ct);
        try
        {
            GameRow present = await s.GameAsync("Here", real);
            (GameDetailViewModel gone, _, _, _, _) = await BuildAsync(s, missing.Id);
            (GameDetailViewModel here, _, _, _, _) = await BuildAsync(s, present.Id);

            gone.ExecutableMissing.Should().BeTrue("the scratch row points at a file nobody wrote");
            here.ExecutableMissing.Should().BeFalse();
            GameDetailViewModel.ExecutableMissingText.Should().Be(Strings.GameDetail_ExeMissing);
        }
        finally
        {
            File.Delete(real);
        }
    }

    [Fact]
    public async Task ChangingTheExecutableRevokesOverThePipeFirstAndLeavesTheRowHookingOff()
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
        // A real file: the fingerprint is read from disk, as it is when a game is added.
        string real = Path.Combine(Path.GetTempPath(), "fl-change-exe-" + Guid.NewGuid().ToString("N") + ".exe");
        await File.WriteAllBytesAsync(real, new byte[123], Ct);
        try
        {
            (GameDetailViewModel vm, FakeAgent agent, _, _, FakeStrip strip) = await BuildAsync(s, game.Id, pick: real);
            vm.HookEnabled.Should().BeTrue();

            vm.ChangeExecutableCommand.Execute(null);
            Task pending = vm.Pending;
            await pending;

            agent.Sent.Should().ContainSingle().Which.Enabled.Should().BeFalse("consent is withdrawn over the pipe, never by editing the table");
            GameRow after = (await s.Games.FindByIdAsync(game.Id, Ct))!;
            after.Fingerprint.ExePath.Should().Be(Infrastructure.Io.ExecutableIdentity.Normalise(real));
            after.Fingerprint.SizeBytes.Should().Be(123);
            after.HookEnabled.Should().BeFalse();
            vm.ExecutableText.Should().Be(after.Fingerprint.ExePath);
            strip.Lines.Should().ContainSingle().Which.Should().StartWith("success:");
        }
        finally
        {
            File.Delete(real);
        }
    }

    [Fact]
    public async Task ChangingTheExecutableToAnUnreadableFileOrCancellingChangesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");

        (GameDetailViewModel cancelled, _, _, _, FakeStrip quiet) = await BuildAsync(s, game.Id, pick: null);
        cancelled.ChangeExecutableCommand.Execute(null);
        Task pending1 = cancelled.Pending;
        await pending1;
        quiet.Lines.Should().BeEmpty();

        (GameDetailViewModel vm, _, _, _, FakeStrip strip) = await BuildAsync(s, game.Id, pick: Path.Combine(Path.GetTempPath(), "fl-no-such-" + Guid.NewGuid().ToString("N") + ".exe"));
        vm.ChangeExecutableCommand.Execute(null);
        Task pending2 = vm.Pending;
        await pending2;
        strip.Lines.Should().ContainSingle().Which.Should().StartWith("warn:");
        (await s.Games.FindByIdAsync(game.Id, Ct))!.Fingerprint.Should().Be(game.Fingerprint);
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
        vm.TrendPoints.Should().HaveCount(2, "the machine's metrics are every session's, Tier 2 included (beta.8)");
        vm.TrendPoints[0].Value.Should().Be(71, "oldest first: the hooked one");

        vm.SelectedSession = vm.Sessions[0];   // newest first: the Tier-2 one
        Task pending = vm.Pending;
        await pending;
        vm.SelectedHasSeries.Should().BeFalse();
        vm.SelectedNote.Should().Be(Strings.Tabs_SelectedNotHooked);
        vm.SelectedHasSensors.Should().BeFalse("this Tier-2 row recorded no sensor");
        vm.SensorsNote.Should().Be(Strings.Sensors_Empty);

        vm.SelectedSession = vm.Sessions[1];
        Task pending2 = vm.Pending;
        await pending2;
        vm.SelectedHasSeries.Should().BeTrue();
        vm.SelectedSeries!.Presents.Should().Be(300);
        vm.SelectedHasSensors.Should().BeTrue();
        vm.SensorsNote.Should().BeEmpty();
        vm.HasLatency.Should().BeFalse("no Reflex on this session");
    }

    /// <summary>beta.8: a session of this game that finishes while its page is shown reloads the page; another game's does not, nor one after the page left.</summary>
    [Fact]
    public async Task ASessionOfThisGameThatFinishesReloadsThePage()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddDays(-1));
        var link = new FakeAgentLink();
        var vm = new GameDetailViewModel(s.Library, new GameSelection { GameId = game.Id }, new HookingConsent(new FakeAgent(), new FakePrompt()), new FakeNavigator(),
            new FakeConfirmations(RemoveGameChoice.Cancel), new FakeEdit(null), new FakeStrip(), new NoSummaries(),
            new Charts.SessionSeriesLoader(s.Sessions), new Infrastructure.Persistence.SqliteHardwareSnapshotRepository(s.Db), new SessionSelection(),
            new FakePicker(null), agent: link);
        Task loaded = vm.Pending;
        await loaded;
        vm.Attach();
        vm.Attach();    // a second Loaded does not subscribe twice

        long second = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), second, "normal", 1, "saved", "exited", game.Id + 1, "Other"));
        Task other = vm.Pending;
        await other;
        vm.Sessions.Should().ContainSingle("another game's session is not this page's");

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), second, "normal", 1, "saved", "exited", game.Id, "Alpha"));
        Task reloaded = vm.Pending;
        await reloaded;
        vm.Sessions.Should().HaveCount(2);

        vm.Detach();
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), second + 1, "normal", 1, "saved", "exited", game.Id, "Alpha"));
        Task after = vm.Pending;
        await after;
        vm.Sessions.Should().HaveCount(2, "the page left: nothing listens");
    }

    /// <summary>beta.8: a session that was not hooked has its sensors, and the Sensors tab draws them instead of saying "not hooked".</summary>
    [Fact]
    public async Task ATierTwoSessionsSensorsAreTheSensorsTabs()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.NotHookedWithSensorsAsync(game.Id, DateTimeOffset.UtcNow.AddDays(-1));
        (GameDetailViewModel vm, _, _, _, _) = await BuildAsync(s, game.Id);

        vm.SelectedSession = vm.Sessions[0];
        Task pending = vm.Pending;
        await pending;

        vm.SelectedHasSeries.Should().BeFalse("no frames");
        vm.SelectedNote.Should().Be(Strings.Tabs_SelectedNotHooked);
        vm.SelectedHasSensors.Should().BeTrue();
        vm.SensorsNote.Should().BeEmpty();
        vm.SelectedSensors!.Sensors.Select(static x => x.Name).Should().BeEquivalentTo("gpu_temp", "gpu_power");
        vm.SelectedSensors.SensorsAligned.Should().BeFalse("timed from the session's start: there is no first frame");
    }

    [Fact]
    public void CapabilityFlagsReadAsProductNamesInEitherShape()
    {
        GameDetailViewModel.CapabilityNames("{\"dlss\":true,\"dlssg\":true,\"fsr\":false,\"weird\":true}").Should().Equal("DLSS", "DLSS-G");
        GameDetailViewModel.CapabilityNames("[\"xess\",\"xefg\"]").Should().Equal("XeSS", "XeFG");
        GameDetailViewModel.CapabilityNames("[\"dlss\",\"dlss_g\",\"dlss_rr\",\"streamline\",\"fsr\"]")
            .Should().Equal("DLSS", "DLSS-G", "DLSS Ray Reconstruction", "Streamline", "FSR"); // the rule ids of rules/detection-rules.json, as the Agent's sweep writes them (P4 PR-1)
        GameDetailViewModel.CapabilityNames("not json").Should().BeEmpty();
        GameDetailViewModel.CapabilityNames(null).Should().BeEmpty();
    }
}
