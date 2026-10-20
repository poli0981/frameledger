// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Play;
using FrameLedger.Application.Settings;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared.Ipc;
using Xunit;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.21 (owner decision D59): Play hands a game to the app of the store whose own records name its folder, or has the
/// capture agent start it — directly when asked, and when no store installed it, GOG GALAXY is missing or the App runs as
/// administrator — and says which happened.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class GamePlayerTests
{
    private const string _steam = @"D:\SteamLibrary\steamapps\common\Wuthering Waves";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Stores(params StoreGame[] games) : IStoreLibrarySource
    {
        public string Platform => "test";

        public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    private sealed class BrokenStore : IStoreLibrarySource
    {
        public string Platform => "broken";

        public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default) => throw new IOException("unreadable");
    }

    private sealed class Galaxy(string? path) : IGogGalaxyLocator
    {
        public string? ClientPath() => path;
    }

    private sealed class Shell(bool takes = true) : IShellStarter
    {
        public List<string> Opened { get; } = [];

        public List<(string Path, string Arguments, string? Folder)> Started { get; } = [];

        public bool OpenLink(string link)
        {
            Opened.Add(link);
            return takes;
        }

        public bool StartProgram(string path, string arguments, string? workingDirectory)
        {
            Started.Add((path, arguments, workingDirectory));
            return takes;
        }
    }

    private sealed class Agent(bool connected = true, string outcome = "Accepted", Exception? fails = null) : IAgentRequests
    {
        public HelloAck? Hello => null;

        public bool IsConnected => connected;

        public List<LaunchGameRequest> Sent { get; } = [];

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class
        {
            type.Should().Be(IpcMessageType.LaunchGame);
            var request = (LaunchGameRequest)(object)payload;
            Sent.Add(request);
            if (fails is not null)
            {
                return Task.FromException<IpcEnvelope>(fails);
            }

            bool accepted = string.Equals(outcome, "Accepted", StringComparison.Ordinal);
            return Task.FromResult(IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.LaunchAck, "1",
                new LaunchAck(request.GameId, accepted, outcome, accepted ? Guid.NewGuid() : null))));
        }
    }

    [Fact]
    public async Task ThroughTheStoreSteamGetsItsOwnLink()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var shell = new Shell();
        var agent = new Agent();
        GamePlayer player = Player(s, shell, agent, new Stores(new StoreGame("steam", "3513350", "Wuthering Waves", _steam, null, null)));

        PlayResult result = await player.PlayAsync(Game(_steam + @"\Client\Binaries\Win64\Client-Win64-Shipping.exe", "3513350"), Ct);

        result.Should().Be(new PlayResult(PlayOutcome.HandedToStore, "steam"));
        shell.Opened.Should().Equal("steam://rungameid/3513350");
        agent.Sent.Should().BeEmpty("the store's app starts the game; the watcher records it as any start");
        GamePlayer.Describe(result, "Wuthering Waves").Text.Should().Contain("Steam").And.Contain("Wuthering Waves");
    }

    [Fact]
    public async Task GogRunsGalaxyAndWithoutGalaxyTheAgentStartsTheGame()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var record = new StoreGame("gog", "1125815775", "A Space for the Unbound", @"D:\another\gog\A Space for the Unbound - Prologue", null, null);
        GameRow game = Game(@"D:\another\gog\A Space for the Unbound - Prologue\A Space for the Unbound.exe", "1125815775");

        var shell = new Shell();
        PlayResult handed = await Player(s, shell, new Agent(), new Stores(record), galaxy: @"C:\Program Files\GOG Galaxy\GalaxyClient.exe").PlayAsync(game, Ct);
        handed.Outcome.Should().Be(PlayOutcome.HandedToStore);
        shell.Started.Should().ContainSingle().Which.Arguments.Should().Contain("/command=runGame /gameId=1125815775");

        var agent = new Agent();
        PlayResult direct = await Player(s, new Shell(), agent, new Stores(record), galaxy: null).PlayAsync(game, Ct);
        direct.Should().Be(new PlayResult(PlayOutcome.StartedByAgent, Fallback: PlayFallback.NoGalaxy));
        agent.Sent.Should().ContainSingle().Which.GameId.Should().Be(game.Id);
    }

    [Fact]
    public async Task AGameNoStoreInstalledIsStartedDirectlyAndSaysWhy()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var agent = new Agent();
        var shell = new Shell();

        PlayResult result = await Player(s, shell, agent, new BrokenStore(), new Stores()).PlayAsync(Game(@"D:\Games\Loose\game.exe", null), Ct);

        result.Should().Be(new PlayResult(PlayOutcome.StartedByAgent, Fallback: PlayFallback.NoStore), "a store whose records cannot be read costs its own records only");
        shell.Opened.Should().BeEmpty();
        agent.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task AnElevatedAppNeverStartsAStoresAppAndHasTheAgentStartTheGame()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var shell = new Shell();
        var agent = new Agent();
        GamePlayer player = Player(s, shell, agent, new Stores(new StoreGame("steam", "3513350", "W", _steam, null, null)), elevated: true);

        PlayResult result = await player.PlayAsync(Game(_steam + @"\game.exe", "3513350"), Ct);

        result.Fallback.Should().Be(PlayFallback.Elevated);
        result.Outcome.Should().Be(PlayOutcome.StartedByAgent);
        shell.Opened.Should().BeEmpty("a store's app started from an administrator would start the game as administrator too");
    }

    [Fact]
    public async Task DirectAsksTheAgentAndSaysEachOfItsAnswers()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        await new RegisteredSettings(new SqliteSettingsStore(s.Db)).SetAsync(SettingsRegistry.UiPlayMode, PlayMode.Direct, Ct);
        var steam = new Stores(new StoreGame("steam", "3513350", "W", _steam, null, null));
        GameRow game = Game(_steam + @"\game.exe", "3513350");

        var shell = new Shell();
        var agent = new Agent();
        (await Player(s, shell, agent, steam).PlayAsync(game, Ct)).Should().Be(new PlayResult(PlayOutcome.StartedByAgent, SteamGame: true));
        shell.Opened.Should().BeEmpty("direct never asks the store's app to start it, even for a store's game");

        (await Player(s, new Shell(), new Agent(connected: false), steam).PlayAsync(game, Ct)).Outcome.Should().Be(PlayOutcome.AgentUnavailable);
        (await Player(s, new Shell(), new Agent(outcome: "RecordingOff"), steam).PlayAsync(game, Ct)).Outcome.Should().Be(PlayOutcome.RecordingOff);
        (await Player(s, new Shell(), new Agent(outcome: "SessionRunning"), steam).PlayAsync(game, Ct)).Outcome.Should().Be(PlayOutcome.AlreadyRunning);
        PlayResult failed = await Player(s, new Shell(), new Agent(outcome: "Unavailable"), steam).PlayAsync(game, Ct);
        failed.Should().Be(new PlayResult(PlayOutcome.Failed, Detail: "Unavailable"));

        (await Player(s, shell, agent, steam).IsDirectAsync(Ct)).Should().BeTrue();
        GamePlayer.Describe(failed, "W").Warning.Should().BeTrue();
        GamePlayer.Describe(new PlayResult(PlayOutcome.RecordingOff), "W").Text.Should().Contain("Settings");
    }

    /// <summary>
    /// The pipe fails a request in flight when the agent stops, and a command's unhandled exception is the App's crash dialog
    /// (beta.21's review). Since beta.22 an answer that never came — the connection closed (one IOException, AgentConnection) or no
    /// answer in time — is "not known": the game may be starting. The connection gone before the request is "not running".
    /// </summary>
    [Fact]
    public async Task AnAnswerThatNeverCameIsNotKnownAndARequestNeverSentIsNotRunning()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        await new RegisteredSettings(new SqliteSettingsStore(s.Db)).SetAsync(SettingsRegistry.UiPlayMode, PlayMode.Direct, Ct);
        GameRow game = Game(@"D:\Games\Loose\game.exe", null);
        foreach (Exception ex in new Exception[] { new IOException("the connection to the Agent closed before it answered"), new TimeoutException() })
        {
            PlayResult result = await Player(s, new Shell(), new Agent(fails: ex), new Stores()).PlayAsync(game, Ct);
            result.Should().Be(new PlayResult(PlayOutcome.Unanswered), ex.GetType().Name);
            GamePlayer.Describe(result, "W").Should().Be((Strings.Agent_Unanswered, true));
        }

        (await Player(s, new Shell(), new Agent(fails: new InvalidOperationException("the Agent is not connected")), new Stores()).PlayAsync(game, Ct))
            .Should().Be(new PlayResult(PlayOutcome.AgentUnavailable), "nothing was sent");

        PlayResult refused = await Player(s, new Shell(), new Agent(fails: new IpcRequestException("UnknownType", "no such command")), new Stores())
            .PlayAsync(game, Ct);
        refused.Should().Be(new PlayResult(PlayOutcome.Failed, Detail: "UnknownType"));
    }

    /// <summary>From the review before the merge: each notice is true on the path it is said on.</summary>
    [Fact]
    public async Task EachNoticeIsTrueOnThePathItIsSaidOn()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();

        // A Steam record whose id is not a Steam id builds no link — and is not "GOG GALAXY is not installed".
        var odd = new Stores(new StoreGame("steam", "not-an-app-id", "W", _steam, null, null));
        PlayResult noLink = await Player(s, new Shell(), new Agent(), odd).PlayAsync(Game(_steam + @"\game.exe", null), Ct);
        noLink.Should().Be(new PlayResult(PlayOutcome.StartedByAgent, Fallback: PlayFallback.NoLink, SteamGame: true));
        GamePlayer.Describe(noLink, "W").Text.Should().NotContain("GOG").And.NotContain("store");

        // The store mode fell back and recording is off: switching Play to the store is no advice.
        PlayResult storeModeOff = await Player(s, new Shell(), new Agent(outcome: "RecordingOff"), new Stores())
            .PlayAsync(Game(@"D:\Games\Loose\game.exe", null), Ct);
        storeModeOff.Should().Be(new PlayResult(PlayOutcome.RecordingOff, Fallback: PlayFallback.NoStore));
        GamePlayer.Describe(storeModeOff, "W").Text.Should().NotContain("Settings");

        // The agent's UnknownGame is said in words, never as the token.
        PlayResult gone = await Player(s, new Shell(), new Agent(outcome: "UnknownGame"), new Stores())
            .PlayAsync(Game(@"D:\Games\Loose\game.exe", null), Ct);
        gone.Outcome.Should().Be(PlayOutcome.NotInLibrary);
        GamePlayer.Describe(gone, "W").Text.Should().NotContain("UnknownGame").And.Contain("W");
        GamePlayer.Describe(new PlayResult(PlayOutcome.AgentUnavailable, Fallback: PlayFallback.NoStore), "W").Text.Should().NotContain("its store");
    }

    /// <summary>
    /// beta.27: a game Steam installed may hand itself to Steam when the agent starts it (Lies of P, 2026-10-19: its process
    /// left within seconds and Steam started the game again), and the notice says so — started directly, or by an App
    /// running as administrator. Any other game's notice is unchanged.
    /// </summary>
    [Fact]
    public async Task ASteamGameTheAgentStartsSaysItMayHandItselfToSteam()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var steam = new Stores(new StoreGame("steam", "1627720", "Lies of P", _steam, null, null));
        GameRow game = Game(_steam + @"\game.exe", "1627720");

        PlayResult elevated = await Player(s, new Shell(), new Agent(), steam, elevated: true).PlayAsync(game, Ct);
        elevated.Should().Be(new PlayResult(PlayOutcome.StartedByAgent, Fallback: PlayFallback.Elevated, SteamGame: true));
        GamePlayer.Describe(elevated, "Lies of P").Text.Should().Contain("administrator").And.Contain("hand itself to Steam");

        await new RegisteredSettings(new SqliteSettingsStore(s.Db)).SetAsync(SettingsRegistry.UiPlayMode, PlayMode.Direct, Ct);
        PlayResult direct = await Player(s, new Shell(), new Agent(), steam).PlayAsync(game, Ct);
        direct.SteamGame.Should().BeTrue();
        GamePlayer.Describe(direct, "Lies of P").Should().Be(
            ("FrameLedger is starting Lies of P. A Steam game may hand itself to Steam first; the game Steam then starts is recorded.", false));

        PlayResult loose = await Player(s, new Shell(), new Agent(), steam).PlayAsync(Game(@"D:\Games\Loose\game.exe", null), Ct);
        loose.SteamGame.Should().BeFalse("no store's records name its folder");
        GamePlayer.Describe(loose, "W").Text.Should().Be("FrameLedger is starting W.");

        var gog = new Stores(new StoreGame("gog", "1125815775", "A Space for the Unbound", @"D:\another\gog\A Space", null, null));
        (await Player(s, new Shell(), new Agent(), gog).PlayAsync(Game(@"D:\another\gog\A Space\game.exe", "1125815775"), Ct))
            .SteamGame.Should().BeFalse("only Steam's games hand themselves to their store");
    }

    [Fact]
    public async Task AStoreAppWindowsWillNotOpenIsAFailureNamedAsSuch()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        PlayResult result = await Player(s, new Shell(takes: false), new Agent(), new Stores(new StoreGame("itch", "5015466", "H", @"D:\another\it\h", null, null)))
            .PlayAsync(Game(@"D:\another\it\h\H\h.exe", "5015466"), Ct);

        result.Should().Be(new PlayResult(PlayOutcome.Failed, "itch"));
        GamePlayer.Describe(result, "H").Warning.Should().BeTrue();
        GamePlayer.Describe(result, "H").Text.Should().Contain("H").And.NotContain("{0}");
    }

    private static GamePlayer Player(ScratchLedger s, Shell shell, Agent agent, IStoreLibrarySource store, string? galaxy = null, bool elevated = false) =>
        Player(s, shell, agent, galaxy, elevated, store);

    private static GamePlayer Player(ScratchLedger s, Shell shell, Agent agent, IStoreLibrarySource first, IStoreLibrarySource second) =>
        Player(s, shell, agent, null, false, first, second);

    private static GamePlayer Player(ScratchLedger s, Shell shell, Agent agent, string? galaxy, bool elevated, params IStoreLibrarySource[] stores) =>
        new(new RegisteredSettings(new SqliteSettingsStore(s.Db)), stores, new Galaxy(galaxy), shell, agent, () => elevated);

    private static GameRow Game(string exe, string? storeId) => new()
    {
        Id = 7,
        Name = "Game",
        Fingerprint = new ExecutableFingerprint { ExePath = exe, SizeBytes = 1, MtimeUnixMs = 1 },
        HookEnabled = false,
        HookCrashCount = 0,
        AddedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        StoreId = storeId,
    };
}
