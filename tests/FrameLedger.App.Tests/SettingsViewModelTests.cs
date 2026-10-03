using System.Globalization;
using System.Windows;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;
using Microsoft.Extensions.DependencyInjection;

namespace FrameLedger.App.Tests;

/// <summary>
/// FR-10 over the registry: the page loads the effective values, every toggle is one settings row, a number
/// outside the range is refused in place, the hook-enabled list is what the ledger says and a revoke goes
/// through the pipe (never a column write), "start with Windows" follows the Run entry, and <c>log.debug</c>
/// moves the level switch at once.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class SettingsViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class NoTheme : IThemeApplier
    {
        public void Apply(AppTheme theme, Window? window)
        {
        }
    }

    private sealed class NoPrompt : IConsentPrompt
    {
        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakeMaintenance : IMaintenanceState
    {
        public MaintenanceSnapshot Snapshot { get; set; } = new(false, false, LogonTaskState.NotInstalled);

        public int Reads { get; private set; }

        public Task<MaintenanceSnapshot> ReadAsync(CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class FakeTool : IAgentTool
    {
        public List<string> Flags { get; } = [];

        public AgentToolResult Result { get; set; } = new(0, "done");

        public Task<AgentToolResult> RunAsync(string flag, CancellationToken ct = default)
        {
            Flags.Add(flag);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeFirstRun : IFirstRunFlow
    {
        public int Shown { get; private set; }

        public Task<bool> IsRequiredAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> RunAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task ShowDocumentsAsync(CancellationToken ct = default)
        {
            Shown++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRun : IRunAtLogon
    {
        public bool IsSet { get; set; }

        public int Applied { get; private set; }

        public void Apply(bool enabled)
        {
            IsSet = enabled;
            Applied++;
        }
    }

    private sealed class Harness
    {
        public required ScratchLedger Ledger { get; init; }

        public required RegisteredSettings Settings { get; init; }

        public required FakeAgentLink Agent { get; init; }

        public required FakeRun Run { get; init; }

        public required RecordingStrip Strip { get; init; }

        public required SettingsViewModel Vm { get; init; }

        public required FakeMaintenance Maintenance { get; init; }

        public required FakeTool Tool { get; init; }

        public required WindowClosePolicy ClosePolicy { get; init; }

        public required FakeFirstRun FirstRun { get; init; }
    }

    /// <summary>The admin mode's disclosure, answering as told and counting how often it was shown.</summary>
    private sealed class ScriptedAdminPrompt(bool answer) : IAgentAdminPrompt
    {
        public int Shown { get; private set; }

        public Task<bool> ShowAsync(CancellationToken ct = default)
        {
            Shown++;
            return Task.FromResult(answer);
        }
    }

    private static async Task<Harness> OpenAsync(ScratchLedger s, FakeAgentLink? agent = null, FakeRun? run = null, FakeMaintenance? maintenance = null, FakeTool? tool = null,
        IAgentAdminPrompt? adminPrompt = null, IConfirmations? confirmations = null, SessionDeletion? deletion = null)
    {
        maintenance ??= new FakeMaintenance();
        tool ??= new FakeTool();
        var closePolicy = new WindowClosePolicy();
        var firstRun = new FakeFirstRun();
        var store = new SqliteSettingsStore(s.Db);
        var appearance = new AppearanceSettings(store);
        await appearance.LoadAsync(Ct).ConfigureAwait(false);
        var settings = new RegisteredSettings(store);
        agent ??= new FakeAgentLink();
        run ??= new FakeRun();
        var strip = new RecordingStrip();
        var shell = new ShellHost(new ServiceCollection().BuildServiceProvider(), null!, null!, closePolicy);
        // The instance-lock probe is the test's (never the real folder's): no Agent holds it, so a restart never waits.
        var vm = new SettingsViewModel(appearance, new NoTheme(), shell, settings, s.Library, new HookingConsent(agent, new NoPrompt()), agent, run, strip, maintenance, tool, closePolicy, firstRun,
            adminPrompt: adminPrompt, agentHeld: static () => false, confirmations: confirmations, deletion: deletion);
        Task pending = vm.Pending;
        await pending.ConfigureAwait(false);
        return new Harness { Ledger = s, Settings = settings, Agent = agent, Run = run, Strip = strip, Vm = vm, Maintenance = maintenance, Tool = tool, ClosePolicy = closePolicy, FirstRun = firstRun };
    }

    /// <summary>beta.11 (D40): Settings ▸ Data asks with the total, deletes nothing on a no, and deletes every game's through the Agent on a yes.</summary>
    [Fact]
    public async Task DeleteAllSessionsAsksWithTheTotalThenGoesThroughTheAgent()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow alpha = await s.GameAsync("Alpha");
        GameRow bravo = await s.GameAsync("Bravo");
        await s.SessionAsync(alpha.Id, DateTimeOffset.UtcNow.AddHours(-2));
        await s.SessionAsync(bravo.Id, DateTimeOffset.UtcNow.AddHours(-1));
        var confirm = new DeleteConfirm();
        var agent = new DeletingAgent(s);
        Harness h = await OpenAsync(s, confirmations: confirm, deletion: new SessionDeletion(agent));

        await h.Vm.DeleteAllSessionsCommand.ExecuteAsync(null);
        confirm.Asked.Should().Equal(((string?)null, 2L));
        agent.Asked.Should().BeEmpty("a no deletes nothing");

        confirm.Answer = true;
        await h.Vm.DeleteAllSessionsCommand.ExecuteAsync(null);
        agent.Asked.Should().Equal(new DeleteSessionsRequest(null));
        (await s.Sessions.SummariseByGameAsync(Ct)).Should().BeEmpty();
        (await s.Games.ListAsync(Ct)).Should().HaveCount(2, "the library stays");
        h.Vm.HasHookedGames.Should().BeFalse("no game has hooking on, so that card is not shown");
    }

    private sealed class DeleteConfirm : IConfirmations
    {
        public bool Answer { get; set; }

        public List<(string? Game, long Sessions)> Asked { get; } = [];

        public Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default) => Task.FromResult(RemoveGameChoice.Cancel);

        public Task<bool> DeleteSessionsAsync(string? gameName, long sessions, CancellationToken ct = default)
        {
            Asked.Add((gameName, sessions));
            return Task.FromResult(Answer);
        }
    }

    /// <summary>An Agent that deletes in the scratch ledger as the real one does in its own.</summary>
    private sealed class DeletingAgent(ScratchLedger s) : IAgentRequests
    {
        public HelloAck? Hello => null;

        public bool IsConnected => true;

        public List<DeleteSessionsRequest> Asked { get; } = [];

        public async Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class
        {
            var request = (DeleteSessionsRequest)(object)payload;
            Asked.Add(request);
            RetentionSweepResult gone = await s.Sessions.DeleteSessionsAsync(request.GameId, ct).ConfigureAwait(false);
            return IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.DeleteSessionsAck, "1", new DeleteSessionsAck(request.GameId, gone.Games, gone.Sessions)));
        }
    }

    [Fact]
    public async Task LoadsTheRegistrysEffectiveValuesAndWritesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        h.Vm.KillSwitch.Should().BeFalse();
        h.Vm.MinSessionSeconds.Should().Be(30);
        h.Vm.TelemetryIntervalMs.Should().Be(1000);
        h.Vm.RetentionRawSessions.Should().Be(20);
        h.Vm.BackgroundCapture.Should().BeTrue();
        h.Vm.UpdateChannel.Should().Be("stable");
        h.Vm.LogDebug.Should().BeFalse();
        h.Vm.KillSwitchState.Should().Be(Strings.Settings_KillSwitch_Off);
        h.Vm.VulkanLayerText.Should().Be(Strings.Settings_VkLayer_NotStaged, "no layer DLL in the fake");
        h.Vm.ElevationText.Should().Be(Strings.Settings_Agent_NotElevated);
        h.Vm.TaskText.Should().Be(Strings.Settings_Agent_Task_NotInstalled);
        h.Strip.Shown.Should().BeEmpty("loading is not a change");
        (await new SqliteSettingsStore(s.Db).GetAsync(SettingsRegistry.HookingKillSwitch.Key, Ct)).Should().BeNull("nothing was written by the load");
    }

    /// <summary>
    /// D33 (owner decision 2026-09-26): the option is one Agent-read settings row, off by default; on, the list names the
    /// games it can act on — eligible, in force — and off, only a kept exception, said to be suspended. A game that can
    /// never qualify (here: D38's failed trial; or a kernel-level family) is not listed: its page says why. The App reads
    /// the Agent's columns and writes none.
    /// </summary>
    [Fact]
    public async Task TheExceptionOptionIsOffByDefaultAndItsListFollowsTheAgentsColumns()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow eligible = await s.GameAsync("GF2");
        GameRow few = await s.GameAsync("Few");
        GameRow kernel = await s.GameAsync("Aniimo");
        await s.Db.WriteAsync(async (c, tx, ct) =>
        {
            const string sql = "UPDATE games SET hook_blocked_reason = @block, hook_prescan_state = 'blocked', ac_exception_eligible = @eligible, "
                               + "ac_exception_verdict = @verdict, ac_exception_sessions = @sessions, ac_exception_checked_block = @block, "
                               + "ac_exception_trial_failed_at = CASE WHEN @sessions = 1 THEN 1759449600000 END WHERE id = @id";
            const string yidun = "AntiCheatFile|NetEase Yidun|NEP2.dll";
            await Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(sql, new
            {
                id = eligible.Id,
                block = yidun,
                eligible = 1,
                verdict = "AllowedUnderUserModeException|NetEase Yidun|NEP2.dll",
                sessions = 3
            }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(sql, new
            {
                id = few.Id,
                block = yidun,
                eligible = 0,
                verdict = "AllowedUnderUserModeException|NetEase Yidun|NEP2.dll",
                sessions = 1
            }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(sql, new
            {
                id = kernel.Id,
                block = yidun,
                eligible = 0,
                verdict = "AntiCheatFile|Kernel driver in the game folder|NEPKernel.sys",
                sessions = 1
            }, tx, cancellationToken: ct)).ConfigureAwait(false);
            return 0;
        }, Ct);
        Harness h = await OpenAsync(s);

        h.Vm.UserModeExceptions.Should().BeFalse("off by default");
        h.Vm.ExceptionGames.Should().BeEmpty("with the option off only a kept exception is listed");

        h.Vm.UserModeExceptions = true;
        Task pending = h.Vm.Pending;
        await pending;

        (await h.Settings.GetBooleanAsync(SettingsRegistry.HookingUserModeExceptions, Ct)).Should().BeTrue();
        h.Vm.ExceptionGames.Select(static g => g.Name).Should().Equal(["GF2"], "a row with no action is not listed");
        h.Vm.ExceptionGames.Single().CanGrant.Should().BeTrue();
        h.Vm.ExceptionsChecking.Should().BeFalse("the Agent answered about every blocked game");

        await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition(
            "UPDATE games SET ac_exception_at = 1000, ac_exception_family = 'NetEase Yidun' WHERE id = @id", new { id = eligible.Id }, tx, cancellationToken: ct)), Ct);
        h.Vm.UserModeExceptions = false;
        pending = h.Vm.Pending;
        await pending;
        h.Vm.ExceptionGames.Should().ContainSingle().Which.CanWithdraw.Should().BeTrue();
        h.Vm.ExceptionsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task TheKillSwitchIsOneSettingsRowAndTheAgentIsToldWhenItReadsIt()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        h.Vm.KillSwitch = true;
        Task pending = h.Vm.Pending;
        await pending;

        (await h.Settings.GetBooleanAsync(SettingsRegistry.HookingKillSwitch, Ct)).Should().BeTrue();
        h.Vm.KillSwitchState.Should().Be(Strings.Settings_KillSwitch_On);
        h.Strip.Shown.Should().ContainSingle().Which.Body.Should().Be(Strings.Settings_Applied, "hooking.kill_switch is an Agent-read key (D16)");
    }

    [Fact]
    public async Task ANumberOutsideTheRangeIsRefusedInPlaceAndNotWritten()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        h.Vm.MinSessionSeconds = 3;
        Task pending1 = h.Vm.Pending;
        await pending1;

        h.Vm.MinSessionSeconds.Should().Be(30, "the previous value comes back");
        h.Strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("warn");
        (await new SqliteSettingsStore(s.Db).GetAsync(SettingsRegistry.CaptureMinSessionSeconds.Key, Ct)).Should().BeNull();

        h.Vm.TelemetryIntervalMs = 750;
        Task pending2 = h.Vm.Pending;
        await pending2;
        (await h.Settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs, Ct)).Should().Be(750);
    }

    [Fact]
    public async Task TheHookEnabledListIsTheLedgersAndARevokeGoesThroughThePipe()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow on = await s.GameAsync("Beta");
        await s.GameAsync("Alpha");
        await new SqliteGameConsentStore(s.Db).RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = on.Fingerprint,
            DisclosureVersion = SafetyDisclosure.Version,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            Provenance = ConsentProvenance.ConsentDialog,
        }, Ct);
        var agent = new FakeAgentLink
        {
            Answer = (_, payload) => FakeAgentLink.Envelope(IpcMessageType.HookEnabledAck, new HookEnabledAck(((SetHookEnabledRequest)payload).GameId, false, "Written", null)),
        };
        Harness h = await OpenAsync(s, agent);

        h.Vm.HookedGames.Should().ContainSingle().Which.Name.Should().Be("Beta");

        await h.Vm.HookedGames[0].RevokeCommand.ExecuteAsync(null);

        h.Vm.HookedGames.Should().BeEmpty();
        agent.Sent.Should().ContainSingle().Which.Payload.Should().BeEquivalentTo(new SetHookEnabledRequest(on.Id, false, null), "the App asks; the Agent clears the column");
        (await s.Games.FindByIdAsync(on.Id, Ct))!.HookEnabled.Should().BeTrue("a fake Agent wrote nothing — the App itself never touches hook_enabled (D15)");
    }

    [Fact]
    public async Task TheStartupUpdateCheckIsOnByDefaultAndItsToggleIsPersisted()
    {
        // 11_UPDATER §Flow, "if enabled" (P4 PR-5): update.auto_check, default 1, the row the updater reads.
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        h.Vm.AutoCheckUpdates.Should().BeTrue();
        (await new SqliteSettingsStore(s.Db).GetAsync(SettingsRegistry.UpdateAutoCheck.Key, Ct)).Should().BeNull("nothing was written by the load");

        h.Vm.AutoCheckUpdates = false;
        Task pending = h.Vm.Pending;
        await pending;

        (await h.Settings.GetBooleanAsync(SettingsRegistry.UpdateAutoCheck, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task HidingTheHookingCardOfAntiCheatGamesIsOnByDefaultAndItsToggleIsPersisted()
    {
        // beta.8 (owner request 2026-09-25): ui.hide_anticheat_hooking, default 1, the row the game page reads at each load.
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        h.Vm.HideAntiCheatHooking.Should().BeTrue();
        (await new SqliteSettingsStore(s.Db).GetAsync(SettingsRegistry.UiHideAntiCheatHooking.Key, Ct)).Should().BeNull("nothing was written by the load");

        h.Vm.HideAntiCheatHooking = false;
        Task pending = h.Vm.Pending;
        await pending;

        (await h.Settings.GetBooleanAsync(SettingsRegistry.UiHideAntiCheatHooking, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task FpsDecimalsIsOffByDefaultAndItsToggleIsPersistedAndAppliedAtOnce()
    {
        // beta.8 (owner request 2026-09-25): ui.fps_decimals, default 0; the formatters read it from the moment it moves.
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);
        h.Vm.FpsTwoDecimals.Should().BeFalse();
        try
        {
            h.Vm.FpsTwoDecimals = true;
            Task pending = h.Vm.Pending;
            await pending;

            (await h.Settings.GetBooleanAsync(SettingsRegistry.UiFpsDecimals, Ct)).Should().BeTrue();
            FpsDecimals.Two.Should().BeTrue();
        }
        finally
        {
            FpsDecimals.Two = false;
        }
    }

    [Fact]
    public async Task ARevokeTheAgentCannotTakeStaysInTheListAndSaysWhy()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow on = await s.GameAsync("Beta");
        await new SqliteGameConsentStore(s.Db).RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = on.Fingerprint,
            DisclosureVersion = SafetyDisclosure.Version,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            Provenance = ConsentProvenance.ConsentDialog,
        }, Ct);
        Harness h = await OpenAsync(s, new FakeAgentLink { IsConnected = false, Hello = null });

        await h.Vm.HookedGames[0].RevokeCommand.ExecuteAsync(null);

        h.Vm.HookedGames.Should().ContainSingle();
        h.Strip.Shown.Should().ContainSingle().Which.Body.Should().Be(Shared.Strings.Safety_Consent_AgentUnavailable);
        h.Vm.ElevationText.Should().Be(Strings.Settings_VkLayer_Unknown, "no HelloAck, no claim");
    }

    [Fact]
    public async Task StartWithWindowsFollowsTheRunEntryAndWritesItThroughTheSeam()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var run = new FakeRun { IsSet = true };
        Harness h = await OpenAsync(s, run: run);

        h.Vm.StartWithWindows.Should().BeTrue("the Run entry is the truth, whatever the settings row says");
        run.Applied.Should().Be(0);

        h.Vm.StartWithWindows = false;
        Task pending = h.Vm.Pending;
        await pending;

        run.IsSet.Should().BeFalse();
        run.Applied.Should().Be(1);
        (await h.Settings.GetBooleanAsync(SettingsRegistry.UiStartWithWindows, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task TheLayerButtonsFollowTheRegistrationRuleAndRunTheAgentsFlag()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var maintenance = new FakeMaintenance { Snapshot = new MaintenanceSnapshot(true, false, LogonTaskState.NotInstalled) };
        Harness h = await OpenAsync(s, maintenance: maintenance);

        h.Vm.CanRegisterLayer.Should().BeFalse("no game has hooking on (12_BUILD: registered only while one does)");
        h.Vm.CanUnregisterLayer.Should().BeFalse();
        h.Vm.VulkanLayerText.Should().Be(Strings.Settings_VkLayer_NotRegistered);

        maintenance.Snapshot = new MaintenanceSnapshot(true, true, LogonTaskState.NotInstalled);
        await h.Vm.UnregisterLayerCommand.ExecuteAsync(null);

        h.Tool.Flags.Should().Equal("--unregister-vklayer");
        h.Vm.VulkanLayerText.Should().Be(Strings.Settings_VkLayer_Registered, "the state is re-read from the machine, never assumed from the button");
        h.Strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("success");
    }

    /// <summary>P4 PR-2: the Register button follows the reconciler's rule — a hook-enabled game must reference the Vulkan loader.</summary>
    [Fact]
    public async Task RegisterIsOfferedOnlyWhenAHookEnabledGameReferencesTheVulkanLoader()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow d3d = await s.GameAsync("Direct");
        GameRow vk = await s.GameAsync("Vulkanic");
        var consent = new SqliteGameConsentStore(s.Db);
        foreach (GameRow g in new[] { d3d, vk })
        {
            await consent.RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
            {
                Fingerprint = g.Fingerprint,
                DisclosureVersion = SafetyDisclosure.Version,
                AcknowledgedAt = DateTimeOffset.UtcNow,
                Provenance = ConsentProvenance.ConsentDialog,
            }, Ct);
        }

        await s.Games.ApplyDetectionAsync(d3d.Id, new DetectionWrite { CapabilityIds = ["dlss"], RulesVersion = "r", ExeSizeBytes = 1, ExeMtimeMs = 1 }, Ct);
        var maintenance = new FakeMaintenance { Snapshot = new MaintenanceSnapshot(true, false, LogonTaskState.NotInstalled) };
        Harness before = await OpenAsync(s, maintenance: maintenance);
        before.Vm.HookedGames.Should().HaveCount(2);
        before.Vm.CanRegisterLayer.Should().BeFalse("two hook-enabled games, neither references the Vulkan loader");

        await s.Games.ApplyDetectionAsync(vk.Id, new DetectionWrite { CapabilityIds = ["vulkan"], RulesVersion = "r", ExeSizeBytes = 1, ExeMtimeMs = 1 }, Ct);
        Harness after = await OpenAsync(s, maintenance: maintenance);
        after.Vm.HookedGames.Should().ContainSingle(static g => g.UsesVulkan).Which.Name.Should().Be("Vulkanic");
        after.Vm.CanRegisterLayer.Should().BeTrue();
    }

    [Fact]
    public async Task TheTaskButtonsFollowTaskSchedulersAnswerAndAFailureIsSaid()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var maintenance = new FakeMaintenance { Snapshot = new MaintenanceSnapshot(false, false, LogonTaskState.Stale) };
        var tool = new FakeTool { Result = new AgentToolResult(1, "schtasks failed: access denied") };
        Harness h = await OpenAsync(s, maintenance: maintenance, tool: tool);

        h.Vm.CanInstallTask.Should().BeFalse();
        h.Vm.CanRepairTask.Should().BeTrue();
        h.Vm.CanRemoveTask.Should().BeTrue();
        h.Vm.TaskText.Should().Be(Strings.Settings_Agent_Task_Stale);

        await h.Vm.RepairTaskCommand.ExecuteAsync(null);

        tool.Flags.Should().Equal("--install-task");
        h.Strip.Shown.Should().ContainSingle().Which.Body.Should().Contain("access denied");
        h.Vm.IsToolRunning.Should().BeFalse();
    }

    [Fact]
    public async Task MinimizeToTrayMovesTheClosePolicyAtOnce()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        h.Vm.MinimizeToTray = true;
        Task pending = h.Vm.Pending;
        await pending;

        h.ClosePolicy.MinimizeToTray.Should().BeTrue();
        h.ClosePolicy.HidesOnClose.Should().BeFalse("no tray icon exists in a test, so a close is still a close");
        (await h.Settings.GetBooleanAsync(SettingsRegistry.UiMinimizeToTray, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task ReopenTheLegalDocumentsShowsThemReadOnly()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);

        await h.Vm.ReopenLegalCommand.ExecuteAsync(null);

        h.FirstRun.Shown.Should().Be(1);
        h.Strip.Shown.Should().BeEmpty("no \"not yet\" line any more");
    }

    [Fact]
    public async Task DebugLoggingMovesTheLevelSwitchAtOnce()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        Harness h = await OpenAsync(s);
        try
        {
            h.Vm.LogDebug = true;
            Task pending = h.Vm.Pending;
            await pending;

            LoggingLevel.Switch.MinimumLevel.Should().Be(Serilog.Events.LogEventLevel.Debug);
            (await h.Settings.GetBooleanAsync(SettingsRegistry.LogDebug, Ct)).Should().BeTrue();
        }
        finally
        {
            LoggingLevel.SetDebug(false);
        }
    }

    /// <summary>
    /// beta.10 (the admin mode, owner decision D34): off by default; turning it on shows the disclosure first and writes
    /// nothing when it is not accepted; accepted, the row is written and the Agent is asked to stop, then started the new way.
    /// </summary>
    [Fact]
    public async Task TheAdminModeAsksFirstAndRestartsTheAgent()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var declined = new ScriptedAdminPrompt(answer: false);
        Harness h = await OpenAsync(s, adminPrompt: declined);
        h.Vm.RunElevated.Should().BeFalse("off by default");

        h.Vm.RunElevated = true;
        Task pending101 = h.Vm.Pending;
        await pending101.ConfigureAwait(true);

        declined.Shown.Should().Be(1);
        h.Vm.RunElevated.Should().BeFalse("not accepted, so it stays off");
        (await new SqliteSettingsStore(s.Db).GetAsync(SettingsRegistry.CaptureRunElevated.Key, Ct).ConfigureAwait(true)).Should().BeNull("nothing was written");
        h.Agent.Sent.Should().BeEmpty();

        var accepted = new ScriptedAdminPrompt(answer: true);
        Harness on = await OpenAsync(s, adminPrompt: accepted).ConfigureAwait(true);
        on.Agent.Answer = static (_, _) => FakeAgentLink.Envelope(IpcMessageType.ShutdownAck, new ShutdownAck());
        on.Vm.RunElevated = true;
        Task pending103 = on.Vm.Pending;
        await pending103.ConfigureAwait(true);

        (await on.Settings.GetBooleanAsync(SettingsRegistry.CaptureRunElevated, Ct).ConfigureAwait(true)).Should().BeTrue();
        on.Agent.Sent.Select(static m => m.Type).Should().Equal(IpcMessageType.Shutdown);
        on.Agent.Retries.Should().Be(1, "the next round starts the Agent, asking Windows");
        on.Strip.Shown.Should().Contain(static m => m.Body == Strings.Settings_Agent_RunElevated_Restarting);

        on.Vm.RunElevated = false;
        Task pending104 = on.Vm.Pending;
        await pending104.ConfigureAwait(true);
        accepted.Shown.Should().Be(1, "turning it off asks nothing");
        (await on.Settings.GetBooleanAsync(SettingsRegistry.CaptureRunElevated, Ct).ConfigureAwait(true)).Should().BeFalse();
        on.Agent.Retries.Should().Be(2);
    }

    [Fact]
    public async Task TheAdminModeNeverRestartsTheAgentUnderARunningSession()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new FakeAgentLink
        {
            Status = new StatusAck("recording", null, 2, [new ActiveSession(Guid.NewGuid(), 1, "Title", 100, 2, DateTimeOffset.UtcNow)]),
        };
        Harness h = await OpenAsync(s, agent, adminPrompt: new ScriptedAdminPrompt(answer: true));

        h.Vm.RunElevated = true;
        Task pending102 = h.Vm.Pending;
        await pending102.ConfigureAwait(true);

        (await h.Settings.GetBooleanAsync(SettingsRegistry.CaptureRunElevated, Ct).ConfigureAwait(true)).Should().BeTrue("saved; it applies at the next start");
        h.Agent.Sent.Should().BeEmpty("a Shutdown would end the session");
        h.Agent.Retries.Should().Be(0);
        h.Strip.Shown.Should().Contain(static m => m.Body == Strings.Settings_Agent_RunElevated_AfterSession);
    }

    [Fact]
    public void TheAgentsRightsAreSaidWithWhyAStandardUsersAgentIsOne()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            static HelloAck Hello(bool elevated, string? outcome) => new("a", IpcProtocol.Version, 1, elevated, null, false, null, false, ElevationOutcome: outcome);

            SettingsViewModel.ElevationTextOf(AgentConnectionState.Elevating, null).Should().Be(Strings.Agent_State_Elevating);
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Offline, null).Should().Be(Strings.Settings_VkLayer_Unknown);
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Connected, Hello(true, "granted")).Should().Be(Strings.Settings_Agent_Elevated);
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Connected, Hello(true, null)).Should().Be(Strings.Settings_Agent_Elevated, "inherited is elevated too");
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Connected, Hello(false, "declined")).Should().Be(Strings.Settings_Agent_ElevationDeclined);
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Connected, Hello(false, "other-account")).Should().Be(Strings.Settings_Agent_ElevationOtherAccount);
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Connected, Hello(false, "failed")).Should().Be(Strings.Settings_Agent_ElevationFailed);
            SettingsViewModel.ElevationTextOf(AgentConnectionState.Connected, Hello(false, null)).Should().Be(Strings.Settings_Agent_NotElevated);
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}
