// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Play;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The Play button (beta.21, owner decision D59: "1: press Play → the platform opens → it runs; 3: it runs at once; let me
/// switch between the two"). <c>ui.play_mode</c> = <c>store</c> (the default) hands the game to the app of the store whose own
/// records name the folder it is installed in (<see cref="StoreRecords"/>, <see cref="StoreLaunches"/>); a game no store
/// installed, a GOG game without GOG GALAXY, and every press while the App runs as administrator are started directly
/// instead, and the notice says why. <c>direct</c> has the capture agent start it (<c>LaunchGame</c>, launch mode): recorded
/// from its first frame, never as administrator (<c>UnelevatedProcess</c>), hooked only where the gate allows exactly as for any
/// start (CLAUDE.md rule 1). FrameLedger itself connects to nothing (rule 8): a store's app does what it does when a game is
/// started from it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed class GamePlayer
{
    private readonly RegisteredSettings _settings;
    private readonly IReadOnlyList<IStoreLibrarySource> _stores;
    private readonly IGogGalaxyLocator _galaxy;
    private readonly IShellStarter _shell;
    private readonly IAgentRequests _agent;
    private readonly Func<bool> _elevated;

    public GamePlayer(RegisteredSettings settings, IEnumerable<IStoreLibrarySource> stores, IGogGalaxyLocator galaxy, IShellStarter shell,
        IAgentRequests agent, Func<bool>? elevated = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(stores);
        _stores = [.. stores];
        _galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _elevated = elevated ?? (static () => Environment.IsPrivilegedProcess);
    }

    /// <summary>The mode Play is in now (<c>ui.play_mode</c>), for a tooltip.</summary>
    public async Task<bool> IsDirectAsync(CancellationToken ct = default) =>
        PlayMode.IsDirect(await _settings.GetAsync(SettingsRegistry.UiPlayMode, ct).ConfigureAwait(false));

    public async Task<PlayResult> PlayAsync(GameRow game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        PlayFallback fallback = PlayFallback.None;
        if (!await IsDirectAsync(ct).ConfigureAwait(false))
        {
            if (_elevated())
            {
                fallback = PlayFallback.Elevated;
            }
            else
            {
                StoreGame? record = StoreRecords.Owning(game.Fingerprint.ExePath, game.StoreId, await RecordsAsync(ct).ConfigureAwait(false));
                StoreLaunch? launch = record is null ? null
                    : StoreLaunches.For(record, string.Equals(record.Platform, "gog", StringComparison.Ordinal) ? _galaxy.ClientPath() : null);
                if (launch is not null)
                {
                    return HandOff(game, launch);
                }

                fallback = record is null ? PlayFallback.NoStore : PlayFallback.NoGalaxy;
            }
        }

        return await StartDirectlyAsync(game, fallback, ct).ConfigureAwait(false);
    }

    /// <summary>The notice a result is said with: its text, and whether it is a warning.</summary>
    public static (string Text, bool Warning) Describe(PlayResult result, string gameName)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Outcome switch
        {
            PlayOutcome.HandedToStore => (result.Store switch
            {
                "steam" => string.Format(CultureInfo.CurrentCulture, Strings.Play_Steam_Format, gameName),
                "epic" => string.Format(CultureInfo.CurrentCulture, Strings.Play_Epic_Format, gameName),
                "gog" => string.Format(CultureInfo.CurrentCulture, Strings.Play_Gog_Format, gameName),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.Play_Itch_Format, gameName),
            }, false),
            PlayOutcome.StartedByAgent => (result.Fallback switch
            {
                PlayFallback.NoStore => string.Format(CultureInfo.CurrentCulture, Strings.Play_NoStore_Format, gameName),
                PlayFallback.NoGalaxy => string.Format(CultureInfo.CurrentCulture, Strings.Play_NoGalaxy_Format, gameName),
                PlayFallback.Elevated => string.Format(CultureInfo.CurrentCulture, Strings.Play_Elevated_Format, gameName),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.Play_Started_Format, gameName),
            }, false),
            PlayOutcome.AgentUnavailable => (string.Format(CultureInfo.CurrentCulture, Strings.Play_AgentUnavailable_Format, gameName), true),
            PlayOutcome.RecordingOff => (string.Format(CultureInfo.CurrentCulture, Strings.Play_RecordingOff_Format, gameName), true),
            PlayOutcome.AlreadyRunning => (string.Format(CultureInfo.CurrentCulture, Strings.Play_AlreadyRunning_Format, gameName), false),
            _ when result.Detail is null => (string.Format(CultureInfo.CurrentCulture, Strings.Play_HandOffFailed_Format, gameName), true),
            _ => (string.Format(CultureInfo.CurrentCulture, Strings.Play_Failed_Format, gameName, result.Detail), true),
        };
    }

    private PlayResult HandOff(GameRow game, StoreLaunch launch)
    {
        bool handed = launch.Kind == StoreLaunchKind.RunProgram
            ? _shell.StartProgram(launch.Target, launch.Arguments ?? string.Empty, launch.WorkingDirectory)
            : _shell.OpenLink(launch.Target);
        Log.Information("play: {Game} (id {Id}) through {Store}: {Handed}", game.Name, game.Id, launch.Platform, handed ? "handed to its app" : "Windows did not take it");
        return handed ? new PlayResult(PlayOutcome.HandedToStore, launch.Platform) : new PlayResult(PlayOutcome.Failed, launch.Platform);
    }

    private async Task<IReadOnlyList<StoreGame>> RecordsAsync(CancellationToken ct)
    {
        List<StoreGame> all = [];
        foreach (IStoreLibrarySource store in _stores)
        {
            try
            {
                all.AddRange(await store.ListAsync(ct).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                // One store's records that cannot be read are that store's, never the press's.
                Log.Warning("play: {Store}'s records could not be read ({Message})", store.Platform, ex.Message);
            }
        }

        return all;
    }

    private async Task<PlayResult> StartDirectlyAsync(GameRow game, PlayFallback fallback, CancellationToken ct)
    {
        if (!_agent.IsConnected)
        {
            return new PlayResult(PlayOutcome.AgentUnavailable, Fallback: fallback);
        }

        try
        {
            IpcEnvelope envelope = await _agent.RequestAsync(IpcMessageType.LaunchGame, new LaunchGameRequest(game.Id, null), ct).ConfigureAwait(false);
            LaunchAck ack = IpcCodec.Payload<LaunchAck>(envelope) ?? throw new InvalidOperationException("LaunchGame answered without a payload");
            Log.Information("play: {Game} (id {Id}) through the agent: {Outcome}", game.Name, game.Id, ack.Outcome);
            return ack.Outcome switch
            {
                _ when ack.Accepted => new PlayResult(PlayOutcome.StartedByAgent, Fallback: fallback),
                "RecordingOff" => new PlayResult(PlayOutcome.RecordingOff, Fallback: fallback),
                "SessionRunning" => new PlayResult(PlayOutcome.AlreadyRunning, Fallback: fallback),
                _ => new PlayResult(PlayOutcome.Failed, Fallback: fallback, Detail: ack.Outcome),
            };
        }
        catch (Exception ex) when (ex is IpcRequestException or InvalidOperationException or TimeoutException)
        {
            Log.Warning("play: LaunchGame for {Game} (id {Id}) failed ({Message})", game.Name, game.Id, ex.Message);
            return new PlayResult(PlayOutcome.Failed, Fallback: fallback, Detail: ex.Message);
        }
    }
}
