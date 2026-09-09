using FrameLedger.Agent.Composition;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Io;
using FrameLedger.Infrastructure.Settings;
using FrameLedger.Infrastructure.Vulkan;
using Microsoft.Extensions.DependencyInjection;

namespace FrameLedger.Agent.Cli;

/// <summary>
/// <c>--console</c> (P2 PR-F): the operator's verbs over the same composition <c>--serve</c> runs. Each one is
/// what the unshipped capture host does, one process later and against the Agent's own ledger; the capture
/// report is the row, not the host's diagnostic print — the row is what the milestone is.
/// </summary>
internal sealed class ConsoleVerbs(IServiceProvider services, AgentPaths paths)
{
    // Distinguishable from the outside, because the end-to-end test is a separate process and a
    // substring match on stdout is a weaker assertion than an exit code. Same values as the host's.
    private const int _exitOk = 0;
    private const int _exitUsage = 1;
    private const int _exitRefused = 2;
    private const int _exitAttachRefused = 4;
    private const int _exitStoppedForSafety = 5;
    private const int _exitTargetNotResolved = 6;

    public async Task<int> RunAsync(AgentCommandLine cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        return cmd.Verb switch
        {
            AgentVerb.ConsentList => await ListAsync().ConfigureAwait(false),
            AgentVerb.ConsentGrant => await GrantAsync(cmd.ExePath!).ConfigureAwait(false),
            AgentVerb.ConsentRevoke => await RevokeAsync(cmd.ExePath!).ConfigureAwait(false),
            AgentVerb.Capture => await CaptureAsync(cmd.ExePath!, cmd.Seconds).ConfigureAwait(false),
            AgentVerb.Launch => await LaunchAsync(cmd.ExePath!, cmd.Arguments, cmd.Seconds).ConfigureAwait(false),
            AgentVerb.Recover => await RecoverAsync().ConfigureAwait(false),
            AgentVerb.Sessions => await SessionsAsync(cmd.Last).ConfigureAwait(false),
            AgentVerb.DbPath => DbPath(),
            AgentVerb.GamesAdd => await GamesAddAsync(cmd.ExePath!).ConfigureAwait(false),
            AgentVerb.KillSwitchOn => await KillSwitchAsync(engaged: true).ConfigureAwait(false),
            AgentVerb.KillSwitchOff => await KillSwitchAsync(engaged: false).ConfigureAwait(false),
            AgentVerb.KillSwitchStatus => await KillSwitchStatusAsync().ConfigureAwait(false),
            _ => _exitUsage,
        };
    }

    private T Get<T>() where T : notnull => services.GetRequiredService<T>();

    private async Task<int> ListAsync()
    {
        AgentConsole.Line($"consent store: {paths.Database}");
        IReadOnlyList<GameConsentRecord> enabled = await Get<IGameConsentStore>().ListEnabledAsync().ConfigureAwait(false);
        if (enabled.Count == 0)
        {
            AgentConsole.Line("  (nothing is enabled — hooking is off for every game by default)");
            return _exitOk;
        }

        foreach (GameConsentRecord r in enabled)
        {
            AgentConsole.Line(
                $"  {r.Fingerprint.ExePath}  consent={r.ConsentedAt:u}  provenance={r.Provenance}  " +
                $"disclosure={r.DisclosureVersion}  blocked={r.BlockedReason ?? "-"}  unverified={r.PreScanUnverified}");
        }

        return _exitOk;
    }

    /// <summary>HANDOFF §P2 decision D4: the Agent's own console verb, stamping <see cref="ConsentProvenance.AgentConsoleOperator"/> from the Agent's clock.</summary>
    private async Task<int> GrantAsync(string exePath)
    {
        ExecutableFingerprint? observed = ExecutableIdentity.Read(exePath);
        if (observed is null)
        {
            AgentConsole.Problem($"no such executable: {exePath}");
            return _exitUsage;
        }

        // Console.IsInputRedirected is the check, and passing null is how the disclosure sees it. A
        // script that pipes the phrase in is not the explicit human action rule 1 requires.
        if (!OperatorDisclosure.Confirm(Console.Out, Console.IsInputRedirected ? null : Console.In, OperatorSurface.AgentConsole))
        {
            AgentConsole.Problem("nothing was written; hooking stays off for this game");
            return _exitRefused;
        }

        ConsentWriteOutcome outcome = await Get<IGameConsentStore>().RecordOperatorAcknowledgementAsync(new OperatorAcknowledgement
        {
            Fingerprint = observed.Value,
            DisclosureVersion = OperatorDisclosure.AgentConsoleVersion,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            Provenance = ConsentProvenance.AgentConsoleOperator,
        }).ConfigureAwait(false);

        AgentConsole.Line($"consent: {outcome}");
        return outcome == ConsentWriteOutcome.Written ? _exitOk : _exitRefused;
    }

    private async Task<int> RevokeAsync(string exePath)
    {
        ConsentWriteOutcome outcome = await Get<IGameConsentStore>()
            .RevokeAsync(ExecutableIdentity.Normalise(exePath))
            .ConfigureAwait(false);
        AgentConsole.Line($"revoke: {outcome}");
        return outcome is ConsentWriteOutcome.Written or ConsentWriteOutcome.NotFound ? _exitOk : _exitRefused;
    }

    private async Task<int> CaptureAsync(string exePath, int seconds)
    {
        string normalised = ExecutableIdentity.Normalise(exePath);
        ExecutableFingerprint? observed = ExecutableIdentity.Read(exePath);

        RecordedSession recorded = await Get<AgentRecording>().Recorder(seconds, launcher: null).RecordAsync(new RecordRequest
        {
            NormalisedExePath = normalised,
            Observed = observed,
            PayloadPath = AgentPaths.Payload,
            Mode = CaptureMode.Attach,
        }).ConfigureAwait(false);

        return await ConcludeAsync(normalised, observed, recorded).ConfigureAwait(false);
    }

    /// <summary>
    /// Launch mode, one launch per capture. The Vulkan layer rides on the launch — unless the kill switch is
    /// engaged, in which case the Agent does NOT set <c>FRAMELEDGER_ENABLE_VK_LAYER</c> and prepares no
    /// environment at all (decision D7): the loader compares the variable's value, and not setting it is the
    /// only honest "no". A launcher-fronted title that exits or sits on its window is handed to the election.
    /// </summary>
    private async Task<int> LaunchAsync(string exePath, string arguments, int seconds)
    {
        string normalised = ExecutableIdentity.Normalise(exePath);
        ExecutableFingerprint? observed = ExecutableIdentity.Read(exePath);
        bool engaged = await Get<IKillSwitch>().IsEngagedAsync().ConfigureAwait(false);

        using VkLayerLaunchEnvironment? vulkan = engaged
            ? null
            : VkLayerLaunchEnvironment.Prepare(normalised, AgentPaths.VkLayerDll, paths.VkLayerDirectory);
        AgentConsole.Line(engaged
            ? "vulkan layer: NOT enabled - the global kill switch is on (FR-2.4), so the launched process gets no layer environment"
            : vulkan!.ManifestPath is null
                ? "vulkan layer: NOT staged beside this binary - a Vulkan title will present unobserved"
                : $"vulkan layer: {vulkan.ManifestPath} (via {VkLayerLaunchEnvironment.ImplicitLayerPathVariable}; enable-list entry '{vulkan.ImageName}' for this session)");

        var launcher = new ProcessLauncher(vulkan?.Variables, enableVulkanLayer: !engaged);
        AgentRecording recording = Get<AgentRecording>();
        RecordedSession recorded = await recording.Recorder(seconds, launcher).RecordAsync(new RecordRequest
        {
            NormalisedExePath = normalised,
            Observed = observed,
            PayloadPath = AgentPaths.Payload,
            Mode = CaptureMode.Launch,
            Arguments = arguments,
        }).ConfigureAwait(false);
        int exit = await ConcludeAsync(normalised, observed, recorded).ConfigureAwait(false);

        // §S1's launcher-first shape: the consented executable quit (or never presented) and the game is a
        // descendant. Elect it and attach; the elected image needs its own consent record.
        if (recorded.Outcome.Reason is SessionEndReason.LaunchTargetExited or SessionEndReason.LaunchNoPresentationRuntime)
        {
            var election = new CaptureOrchestrator(recording.Recorder(seconds, launcher: null), Get<IGameRepository>(),
                Get<IProcessSnapshotSource>(), Get<IExecutableIdentitySource>(), new OrchestratorOptions { PayloadPath = AgentPaths.Payload },
                AgentConsole.Line);
            RecordedSession? elected = await election.ElectAfterLaunchAsync(recorded, CancellationToken.None).ConfigureAwait(false);
            if (elected is not null)
            {
                return await ConcludeAsync(string.Empty, null, elected).ConfigureAwait(false);
            }
        }

        return exit;
    }

    private async Task<int> RecoverAsync()
    {
        IReadOnlyList<RecoveryOutcome> outcomes = await Get<PartialRecovery>().RecoverAsync().ConfigureAwait(false);
        AgentConsole.Line($"recover: {outcomes.Count} pending .partial file(s) under {paths.Tmp}");
        foreach (RecoveryOutcome o in outcomes)
        {
            AgentConsole.Line($"  {o.SessionGuid:N}: {o.Status}{(o.SessionId is { } id ? $" as session {id}" : "")} — {o.Detail}");
        }

        return _exitOk;
    }

    private async Task<int> SessionsAsync(int last)
    {
        IReadOnlyList<SessionRow> rows = await Get<ISessionRepository>().ListRecentAsync(last).ConfigureAwait(false);
        AgentConsole.Line($"sessions: {rows.Count} most recent of {paths.Database}");
        foreach (SessionRow r in rows)
        {
            AgentConsole.Line(Summary(r));
        }

        return _exitOk;
    }

    private int DbPath()
    {
        AgentConsole.Line(paths.Database);
        return _exitOk;
    }

    /// <summary>A watchlist row with hooking OFF (rule 1): the game will be recorded as Tier 2 until it is consented.</summary>
    private async Task<int> GamesAddAsync(string exePath)
    {
        ExecutableFingerprint? observed = ExecutableIdentity.Read(exePath);
        if (observed is null)
        {
            AgentConsole.Problem($"no such executable: {exePath}");
            return _exitUsage;
        }

        GameRow row = await Get<IGameRepository>().EnsureAsync(observed.Value, Path.GetFileNameWithoutExtension(observed.Value.ExePath)).ConfigureAwait(false);
        AgentConsole.Line($"games: id={row.Id} {row.Fingerprint.ExePath} hook_enabled={row.HookEnabled} (off until `consent grant`; watched by --serve as Tier 2 meanwhile)");
        return _exitOk;
    }

    private async Task<int> KillSwitchAsync(bool engaged)
    {
        await Get<SettingsKillSwitch>().SetAsync(engaged).ConfigureAwait(false);
        AgentConsole.Line(engaged
            ? "killswitch: ON - nothing is injected and no Vulkan layer is enabled until it is turned off; a running session stops at its next guard scan"
            : "killswitch: OFF - per-game consent decides again");
        return _exitOk;
    }

    private async Task<int> KillSwitchStatusAsync()
    {
        bool engaged = await Get<IKillSwitch>().IsEngagedAsync().ConfigureAwait(false);
        AgentConsole.Line($"killswitch: {(engaged ? "ON" : "OFF")} ({SettingsKillSwitch.Key} in {paths.Database})");
        return _exitOk;
    }

    private async Task<int> ConcludeAsync(string normalised, ExecutableFingerprint? observed, RecordedSession recorded)
    {
        CaptureOutcome result = recorded.Outcome;
        AgentConsole.Line($"session: {result.Reason}");
        AgentConsole.Line(RecordedNote(recorded));
        if (result.Verdict.Reason == AntiCheatRefusalReason.TargetIsVulkanLayered)
        {
            AgentConsole.Line("  capture side: the Vulkan implicit layer - the guard passed and nothing was injected (one ring per process; 17_HOOK_ENGINE §Vulkan)");
        }
        else if (result.Verdict.Reason != AntiCheatRefusalReason.Allow)
        {
            AgentConsole.Line($"  verdict: {result.Verdict.Reason} {result.Verdict.Family} {result.Verdict.Signal}");
        }

        if (result.Reason == SessionEndReason.RefusedConsentMissing && normalised.Length > 0)
        {
            AgentConsole.Line(await WhyConsentMissingAsync(normalised, observed).ConfigureAwait(false));
        }

        if (result.LaunchWait is { } wait)
        {
            AgentConsole.Line($"  launch: the guard answered {wait.TotalMilliseconds:0} ms after the process was started");
        }

        AgentConsole.Line(Summary(recorded.Row));

        return result.Reason switch
        {
            SessionEndReason.TargetExited or SessionEndReason.Running => _exitOk,
            SessionEndReason.TargetNotRunning or SessionEndReason.TargetAmbiguous
                or SessionEndReason.TargetCannotBePinned or SessionEndReason.LaunchCannotStart => _exitTargetNotResolved,
            SessionEndReason.AttachRefused => _exitAttachRefused,
            SessionEndReason.SafetyUnhook or SessionEndReason.SupervisionLost or SessionEndReason.KillSwitchEngaged
                or SessionEndReason.SupervisionFaulted or SessionEndReason.WriterStoppedBlocklisted => _exitStoppedForSafety,
            _ => _exitRefused,
        };
    }

    private static string RecordedNote(RecordedSession r)
    {
        string what = r.Finalize.Status switch
        {
            FinalizeStatus.Saved => $"SAVED as sessions.id={r.Finalize.SessionId} (tier {(int)r.Row.Tier}, exit={r.ExitStatus}, "
                                    + $"frames={r.Row.FrameCount}, retention swept {r.Finalize.RetentionSwept})",
            FinalizeStatus.Discarded => $"DISCARDED: {r.Row.DurationSeconds:0.#} s is under the minimum session length (tier {(int)r.Row.Tier}, exit={r.ExitStatus})",
            _ => $"{r.Finalize.Status} (exit={r.ExitStatus})",
        };
        string crash = r.CrashPolicy == CrashPolicyOutcome.NotAnEarlyCrash ? "" : $"; crash policy: {r.CrashPolicy}";
        return $"  ledger: session {r.SessionGuid:N} {what}{crash}";
    }

    /// <summary>One row, the columns an operator checks against the game's own menu (the milestone's evidence).</summary>
    private static string Summary(SessionRow r) =>
        $"  row: id={r.Id} guid={r.SessionGuid:N} started={r.StartedAt:u} tier={(int)r.Tier} mode={r.Mode} exit={r.ExitStatus} "
        + $"frames={r.FrameCount} presented_fps={r.PresentedFps?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A"} "
        + $"api={r.Api ?? "N/A"} upscaler={r.Upscaler ?? "N/A"} render={(r.RenderW is { } w && r.RenderH is { } h ? $"{w}x{h}" : "N/A")} "
        + $"fg={r.FgMode} rt={r.RtFlag} telemetry={r.TelemetrySource ?? "-"} notes={r.CaptureNotes ?? "-"}";

    private async Task<string> WhyConsentMissingAsync(string normalisedExePath, ExecutableFingerprint? observed)
    {
        GameConsentRecord record = await Get<IGameConsentStore>().FindAsync(normalisedExePath).ConfigureAwait(false);
        if (!record.IsFromStore)
        {
            return "  cause: no record for this executable — consent has never been given for it (`--console consent grant --exe <path>`)";
        }

        if (!record.HookEnabled)
        {
            return "  cause: a record exists and hooking is DISABLED for it";
        }

        if (observed is { } now && !record.Fingerprint.Matches(now))
        {
            return "  cause: THE EXECUTABLE CHANGED SINCE CONSENT — you did consent, to a different build.\n"
                   + $"    consented: size={record.Fingerprint.SizeBytes} mtime={record.Fingerprint.MtimeUnixMs}\n"
                   + $"    on disk  : size={now.SizeBytes} mtime={now.MtimeUnixMs}\n"
                   + "    The stored record is deliberately untouched (19_SAFETY: a block is not a withdrawal of\n"
                   + "    consent). Check the update was one you expected, then grant consent again.";
        }

        return "  cause: the record is present, enabled and matching — so the refusal came from elsewhere";
    }
}
