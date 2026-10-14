// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.Json;
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
    private readonly StoreRecordsReader _stores;
    private readonly IGogGalaxyLocator _galaxy;
    private readonly IShellStarter _shell;
    private readonly IAgentRequests _agent;
    private readonly Func<bool> _elevated;

    public GamePlayer(RegisteredSettings settings, IEnumerable<IStoreLibrarySource> stores, IGogGalaxyLocator galaxy, IShellStarter shell,
        IAgentRequests agent, Func<bool>? elevated = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(stores);
        _stores = new StoreRecordsReader(stores);
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
                StoreGame? record = StoreRecords.Owning(game.Fingerprint.ExePath, game.StoreId, await _stores.ReadAsync("play", ct: ct).ConfigureAwait(false));
                if (record is null)
                {
                    fallback = PlayFallback.NoStore;
                }
                else
                {
                    string? galaxy = string.Equals(record.Platform, "gog", StringComparison.Ordinal) ? _galaxy.ClientPath() : null;
                    if (StoreLaunches.For(record, galaxy) is { } launch)
                    {
                        return HandOff(game, launch);
                    }

                    fallback = WhyNoHandOff(game, record, galaxy);
                }
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
            PlayOutcome.Unanswered => (Strings.Agent_Unanswered, true),
            // "Set Play to go through the store" only where Play was not already going through it.
            PlayOutcome.RecordingOff => (string.Format(CultureInfo.CurrentCulture,
                result.Fallback == PlayFallback.None ? Strings.Play_RecordingOff_Format : Strings.Play_RecordingOff_Fallback_Format, gameName), true),
            PlayOutcome.AlreadyRunning => (string.Format(CultureInfo.CurrentCulture, Strings.Play_AlreadyRunning_Format, gameName), false),
            PlayOutcome.NotInLibrary => (string.Format(CultureInfo.CurrentCulture, Strings.Play_NotInLibrary_Format, gameName), true),
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

    /// <summary>
    /// A store's record with no hand-off: a GOG game without GOG GALAXY, or — never seen, and logged — an id or a folder of a
    /// shape that store's own never have, which <see cref="StoreLaunches"/> puts in no link.
    /// </summary>
    private static PlayFallback WhyNoHandOff(GameRow game, StoreGame record, string? galaxy)
    {
        if (string.Equals(record.Platform, "gog", StringComparison.Ordinal) && galaxy is null)
        {
            return PlayFallback.NoGalaxy;
        }

        Log.Warning("play: {Game} (id {Id}): {Store}'s record of it names nothing FrameLedger may hand to its app; the agent starts it",
            game.Name, game.Id, record.Platform);
        return PlayFallback.NoLink;
    }

    private async Task<PlayResult> StartDirectlyAsync(GameRow game, PlayFallback fallback, CancellationToken ct)
    {
        if (!_agent.IsConnected)
        {
            return new PlayResult(PlayOutcome.AgentUnavailable, Fallback: fallback);
        }

        LaunchAck? ack;
        try
        {
            IpcEnvelope envelope = await _agent.RequestAsync(IpcMessageType.LaunchGame, new LaunchGameRequest(game.Id, null), ct).ConfigureAwait(false);
            ack = IpcCodec.Payload<LaunchAck>(envelope);
        }
        catch (InvalidOperationException ex)
        {
            // The connection went between the check above and the request: nothing was sent.
            Log.Warning("play: LaunchGame for {Game} (id {Id}) was not sent ({Message})", game.Name, game.Id, ex.Message);
            return new PlayResult(PlayOutcome.AgentUnavailable, Fallback: fallback);
        }
        catch (Exception ex) when (AgentAnswers.IsUnanswered(ex))
        {
            // The agent stopping or restarting under the request, or no answer in time (beta.22): whether the game is starting
            // is not known — and a press never ends in an unhandled exception (beta.21's review).
            Log.Warning("play: LaunchGame for {Game} (id {Id}) had no answer ({Kind}: {Message})", game.Name, game.Id, ex.GetType().Name, ex.Message);
            return new PlayResult(PlayOutcome.Unanswered, Fallback: fallback);
        }
        catch (Exception ex) when (ex is IpcRequestException or JsonException)
        {
            Log.Warning("play: LaunchGame for {Game} (id {Id}) failed ({Kind}: {Message})", game.Name, game.Id, ex.GetType().Name, ex.Message);
            return new PlayResult(PlayOutcome.Failed, Fallback: fallback, Detail: (ex as IpcRequestException)?.Code ?? ex.GetType().Name);
        }

        if (ack is null)
        {
            Log.Warning("play: LaunchGame for {Game} (id {Id}) was answered without a payload", game.Name, game.Id);
            return new PlayResult(PlayOutcome.Failed, Fallback: fallback, Detail: IpcMessageType.LaunchAck);
        }

        Log.Information("play: {Game} (id {Id}) through the agent: {Outcome}", game.Name, game.Id, ack.Outcome);
        return ack.Outcome switch
        {
            _ when ack.Accepted => new PlayResult(PlayOutcome.StartedByAgent, Fallback: fallback),
            "RecordingOff" => new PlayResult(PlayOutcome.RecordingOff, Fallback: fallback),
            "SessionRunning" => new PlayResult(PlayOutcome.AlreadyRunning, Fallback: fallback),
            "UnknownGame" => new PlayResult(PlayOutcome.NotInLibrary, Fallback: fallback),
            _ => new PlayResult(PlayOutcome.Failed, Fallback: fallback, Detail: ack.Outcome),
        };
    }
}
