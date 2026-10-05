// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Services;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using Serilog;

namespace FrameLedger.App.Update;

/// <summary>
/// <c>11_UPDATER</c> §Flow over the <see cref="IUpdateClient"/> port: the startup silent check (when the copy is
/// installed and <c>update.auto_check</c> is on), the manual check with its dialogs, the background download, and
/// the one rule that shapes every step — <b>FR-12: nothing is applied while a session runs.</b> A downloaded
/// package is <see cref="UpdateStage.Ready"/> only while the Agent reports no session; otherwise it is
/// <see cref="UpdateStage.Deferred"/> and becomes Ready when the last session ends. The apply itself asks the Agent
/// to stop first (it outlives the App and holds the Overlay on disk), holds the connection's relaunch, and ends
/// the host once the pipe has dropped; Velopack waits for the exit, applies, and restarts the App, which starts
/// the Agent beside itself as it always does.
/// </summary>
/// <remarks>
/// beta.14 (the update audit): Velopack's own apply-at-start is off (<see cref="VelopackHooks"/>), so a package an earlier
/// run downloaded is adopted here (<see cref="AdoptPending"/>) and goes through FR-12 and the Agent's stop like any other —
/// until then the next start applied it inside <c>VelopackApp.Run</c>, killing the Agent, before any of this ran. The
/// channel follows the running copy unless the user chose one (<see cref="UpdateChannelPolicy"/>, D47); an apply waits
/// while a game still has the Overlay loaded (<see cref="PayloadInUse"/>) and while an Agent waits on the administrator
/// prompt; the stop budget outlasts the Agent's own shutdown grace.
/// </remarks>
public sealed partial class UpdateService : ObservableObject, IDisposable
{
    /// <summary>The Agent's own shutdown grace (it finalizes its sessions inside it), and five seconds to leave after it.</summary>
    internal static readonly TimeSpan DefaultAgentStopTimeout = IpcProtocol.AgentShutdownGrace + TimeSpan.FromSeconds(5);

    private readonly TimeSpan _agentStopTimeout;
    private readonly Func<bool> _agentHeld;
    private readonly Func<string?> _payloadInUse;
    private readonly IUpdateClient _client;
    private readonly IAgentLink _agent;
    private readonly RegisteredSettings _settings;
    private readonly IUpdatePrompts _prompts;
    private readonly IShellPresence _shell;
    private readonly IMessageStrip _strip;
    private readonly UiThread _ui = new();
    private readonly HashSet<Guid> _running = [];
    private UpdateCandidate? _pending;

    [ObservableProperty]
    private UpdateStage _stage;

    [ObservableProperty]
    private string? _version;

    [ObservableProperty]
    private int _percent;

    /// <summary>
    /// The flow over its ports; subscribes to the Agent link for the session facts FR-12 needs. <paramref name="agentStopTimeout"/>
    /// is how long the apply waits for the Agent to stop after <c>Shutdown</c> (<see cref="DefaultAgentStopTimeout"/>), overridable
    /// as a test's clock; <paramref name="agentHeld"/> is whether an Agent process still holds the data folder — its instance
    /// lock, or the marker of one waiting on the administrator prompt — and <paramref name="payloadInUse"/> which library a game
    /// still has loaded from the install folder, both a test's own.
    /// </summary>
    public UpdateService(IUpdateClient client, IAgentLink agent, RegisteredSettings settings, IUpdatePrompts prompts, IShellPresence shell, IMessageStrip strip,
        TimeSpan? agentStopTimeout = null, Func<bool>? agentHeld = null, Func<string?>? payloadInUse = null)
    {
        _agentStopTimeout = agentStopTimeout ?? DefaultAgentStopTimeout;
        _agentHeld = agentHeld ?? (static () => AgentHolds(Services.UiPaths.DataDirectory));
        _payloadInUse = payloadInUse ?? (static () => PayloadInUse.Find(AppContext.BaseDirectory));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _agent.Changed += OnAgentChanged;
        _agent.EventReceived += OnAgentEvent;
    }

    /// <summary>Raised once per release found, for the tray's toast; the banner follows <see cref="Stage"/>.</summary>
    public event EventHandler<UpdateAvailableEventArgs>? Available;

    /// <summary>
    /// A session runs (hooked or recording): one the Agent listed when this App (re)connected, or a <c>SessionStarted</c>
    /// since, without its <c>SessionCompleted</c>. Until 2026-09-23 it also OR-ed the status read at connect, which is
    /// never read again — so a session running when the App connected held every update at Deferred until the next
    /// reconnect, hours after it ended. The connect-time list now seeds the set and the completions empty it.
    /// </summary>
    public bool IsSessionActive => _running.Count > 0;

    public void Dispose()
    {
        _agent.Changed -= OnAgentChanged;
        _agent.EventReceived -= OnAgentEvent;
    }

    /// <summary>
    /// Whether an Agent holds <paramref name="dataDirectory"/>: its instance lock, or — since beta.14 — the marker of one waiting on
    /// the administrator prompt, which holds no lock yet and was stopped by nothing (Velopack's updater killed it).
    /// </summary>
    internal static bool AgentHolds(string dataDirectory) =>
        Infrastructure.Startup.AgentInstanceLock.IsHeld(dataDirectory) || Infrastructure.Startup.ElevationMarker.IsHeld(dataDirectory);

    /// <summary>
    /// A package an earlier run downloaded and nobody applied (beta.14): Ready, or Deferred while a session runs — no feed request.
    /// Velopack applied it at the next start until then, before FR-12 or the Agent's stop could run. Called before the startup check.
    /// </summary>
    public void AdoptPending()
    {
        if (!_client.IsInstalled || Stage != UpdateStage.Idle || _client.FindPendingRestart() is not { } pending)
        {
            return;
        }

        Log.Information("update: {Version} was downloaded by an earlier run and is waiting for a restart", pending.Version);
        _pending = pending;
        Post(() =>
        {
            Version = pending.Version;
            Percent = 100;
            Settle();
        });
    }

    /// <summary>The startup check: skipped for an uninstalled copy and when the setting is off; every failure is a log line, never a dialog (NFR-10).</summary>
    public async Task CheckSilentlyAsync(CancellationToken ct = default)
    {
        if (!_client.IsInstalled)
        {
            Log.Information("update: not an installed copy; the startup check is skipped");
            return;
        }

        if (!await _settings.GetBooleanAsync(SettingsRegistry.UpdateAutoCheck, ct).ConfigureAwait(false))
        {
            Log.Information("update: the startup check is off (update.auto_check)");
            return;
        }

        if (Stage != UpdateStage.Idle)
        {
            return;
        }

        try
        {
            UpdateCandidate? candidate = await CheckCoreAsync(ct).ConfigureAwait(false);
            if (candidate is null)
            {
                Log.Information("update: {Version} is current", _client.CurrentVersion);
                return;
            }

            await DownloadAsync(candidate, ct).ConfigureAwait(false);
        }
        catch (UpdateException ex)
        {
            Log.Information("update: the startup check did not complete ({Failure}: {Message})", ex.Failure, ex.Message);
            Post(() => Stage = UpdateStage.Idle);
        }
    }

    /// <summary>Help ▸ Check for updates: the dialogs of <c>11_UPDATER</c> §Flow, "Manual check".</summary>
    public async Task CheckInteractivelyAsync(CancellationToken ct = default)
    {
        if (!_client.IsInstalled)
        {
            await _prompts.NotInstalledAsync(ct).ConfigureAwait(true);
            return;
        }

        if (Stage is not UpdateStage.Idle and not UpdateStage.Available)
        {
            // Downloading, Ready or Deferred: the banner already says what is pending.
            return;
        }

        try
        {
            UpdateCandidate? candidate = await CheckCoreAsync(ct).ConfigureAwait(true);
            if (candidate is null)
            {
                await _prompts.UpToDateAsync(_client.CurrentVersion ?? UiIdentity.Version, ct).ConfigureAwait(true);
                return;
            }

            if (await _prompts.OfferAsync(candidate, ct).ConfigureAwait(true))
            {
                await DownloadAsync(candidate, ct).ConfigureAwait(true);
            }
            else
            {
                Stage = UpdateStage.Idle;
            }
        }
        catch (UpdateException ex)
        {
            Log.Warning(ex, "update: the manual check did not complete ({Failure})", ex.Failure);
            Stage = UpdateStage.Idle;
            await _prompts.FailedAsync(ex.Failure, ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            Log.Error(ex, "update: the manual check faulted");
            Stage = UpdateStage.Idle;
            await _prompts.FailedAsync(UpdateFailure.Unknown, ct).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The banner's button. FR-12 first: with a session running nothing is applied and the package stays deferred.
    /// Then <c>Shutdown</c> to the Agent, the relaunch held, the pipe watched until it drops, the updater told to
    /// wait for this process, and the host ended. An Agent that does not stop in time leaves everything as it was.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
    public async Task RestartToUpdateAsync(CancellationToken ct = default)
    {
        UpdateCandidate? pending = _pending;
        if (pending is null || Stage is not UpdateStage.Ready and not UpdateStage.Deferred)
        {
            return;
        }

        if (IsSessionActive)
        {
            Stage = UpdateStage.Deferred;
            _strip.Warn(Strings.Update_Banner_Title, Strings.Update_HookedRefused);
            return;
        }

        // A game FrameLedger measured earlier can still have the Overlay loaded from the install folder; the swap would fail
        // half way after this App had quit. Asked before the Agent is stopped, so a refusal leaves it running.
        if (_payloadInUse() is { } held)
        {
            Log.Information("update: {File} is still loaded by a running game; the apply waits", held);
            Stage = UpdateStage.Ready;
            _strip.Warn(Strings.Update_Banner_Title, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Update_PayloadInUse_Format, held));
            return;
        }

        Stage = UpdateStage.Applying;
        _agent.SetLaunchHold(true);
        if (!await StopAgentAsync(ct).ConfigureAwait(true))
        {
            _agent.SetLaunchHold(false);
            Stage = UpdateStage.Ready;
            _strip.Warn(Strings.Update_Banner_Title, Strings.Update_AgentStillRunning);
            return;
        }

        try
        {
            Log.Information("update: applying {Version} on exit", pending.Version);
            _client.ApplyOnExit(pending);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Error(ex, "update: the updater could not be started; nothing was applied");
            _agent.SetLaunchHold(false);
            Stage = UpdateStage.Ready;
            _strip.Warn(Strings.Update_Banner_Title, Strings.Update_Err_Unknown);
            return;
        }

        _shell.Quit();
    }

    private async Task<UpdateCandidate?> CheckCoreAsync(CancellationToken ct)
    {
        Post(() => Stage = UpdateStage.Checking);
        // D47 (beta.14): `auto` follows this copy; a stored `stable` or `beta` is the user's choice.
        string channel = await _settings.GetAsync(SettingsRegistry.UpdateChannel, ct).ConfigureAwait(false);
        bool prereleases = UpdateChannelPolicy.IncludesPrereleases(channel, _client.CurrentVersion ?? UiIdentity.Version);
        UpdateCandidate? candidate = await _client.CheckAsync(prereleases, ct).ConfigureAwait(false);
        if (candidate is null)
        {
            Post(() => Stage = UpdateStage.Idle);
            return null;
        }

        Log.Information("update: {Version} is available ({Size} bytes)", candidate.Version, candidate.SizeBytes);
        Post(() =>
        {
            Version = candidate.Version;
            Stage = UpdateStage.Available;
            Available?.Invoke(this, new UpdateAvailableEventArgs(candidate.Version));
        });
        return candidate;
    }

    private async Task DownloadAsync(UpdateCandidate candidate, CancellationToken ct)
    {
        Post(() =>
        {
            Percent = 0;
            Stage = UpdateStage.Downloading;
        });
        var progress = new PostingProgress(this);
        try
        {
            await _client.DownloadAsync(candidate, progress, ct).ConfigureAwait(false);
        }
        catch (UpdateException ex) when (ex.Failure == UpdateFailure.Corrupt)
        {
            // 11_UPDATER §Error mapping: a package hash mismatch retries once, then it is the dialog's.
            Log.Warning("update: {Version} failed its checksum ({Message}); downloading once more", candidate.Version, ex.Message);
            await _client.DownloadAsync(candidate, progress, ct).ConfigureAwait(false);
        }

        _pending = candidate;
        Log.Information("update: {Version} downloaded and verified", candidate.Version);
        Post(Settle);
    }

    /// <summary>Ready or Deferred, from whether a session runs right now.</summary>
    private void Settle()
    {
        if (_pending is null)
        {
            return;
        }

        if (Stage is UpdateStage.Applying)
        {
            return;
        }

        Stage = IsSessionActive ? UpdateStage.Deferred : UpdateStage.Ready;
    }

    /// <summary>
    /// The Agent stopped, or none was running: the pipe has dropped AND no process holds the data folder any more. Since
    /// beta.10 the second half matters — an Agent run as administrator (the admin mode) holds the install directory's files
    /// exactly as long as its process lives, and one that holds the folder without answering this App (still starting,
    /// another version) must not have the files replaced under it.
    /// </summary>
    private async Task<bool> StopAgentAsync(CancellationToken ct)
    {
        if (!_agent.IsConnected)
        {
            return !_agentHeld();
        }

        try
        {
            await _agent.RequestAsync(IpcMessageType.Shutdown, new ShutdownRequest(), ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IpcRequestException or TimeoutException or System.IO.IOException or InvalidOperationException)
        {
            Log.Warning(ex, "update: the Agent did not acknowledge Shutdown");
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow + _agentStopTimeout;
        while (_agent.IsConnected || _agentHeld())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(100, ct).ConfigureAwait(true);
        }

        return true;
    }

    private void OnAgentChanged(object? sender, EventArgs e) => Post(() =>
    {
        _running.Clear();
        if (_agent.State == AgentConnectionState.Connected && _agent.Status is { } status)
        {
            foreach (ActiveSession a in status.ActiveSessions)
            {
                _running.Add(a.SessionGuid);
            }
        }

        Settle();
    });

    private void OnAgentEvent(object? sender, AgentEventArgs e) => Post(() =>
    {
        switch (e.Envelope.Type)
        {
            case IpcMessageType.SessionStarted when IpcCodec.Payload<SessionStartedEvent>(e.Envelope) is { } started:
                _running.Add(started.SessionGuid);
                break;
            case IpcMessageType.SessionCompleted when IpcCodec.Payload<SessionCompletedEvent>(e.Envelope) is { } completed:
                _running.Remove(completed.SessionGuid);
                break;
            default:
                return;
        }

        Settle();
    });

    private void Post(Action action) => _ui.Post(action);

    /// <summary>The download's percent onto the UI thread, in order; <see cref="Progress{T}"/> would queue each report to the thread pool when there is no context.</summary>
    private sealed class PostingProgress(UpdateService owner) : IProgress<int>
    {
        public void Report(int value) => owner.Post(() => owner.Percent = value);
    }
}
