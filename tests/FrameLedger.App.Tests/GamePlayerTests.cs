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

    private sealed class Agent(bool connected = true, string outcome = "Accepted") : IAgentRequests
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
        (await Player(s, shell, agent, steam).PlayAsync(game, Ct)).Should().Be(new PlayResult(PlayOutcome.StartedByAgent));
        shell.Opened.Should().BeEmpty("direct never asks the store, even for a store's game");

        (await Player(s, new Shell(), new Agent(connected: false), steam).PlayAsync(game, Ct)).Outcome.Should().Be(PlayOutcome.AgentUnavailable);
        (await Player(s, new Shell(), new Agent(outcome: "RecordingOff"), steam).PlayAsync(game, Ct)).Outcome.Should().Be(PlayOutcome.RecordingOff);
        (await Player(s, new Shell(), new Agent(outcome: "SessionRunning"), steam).PlayAsync(game, Ct)).Outcome.Should().Be(PlayOutcome.AlreadyRunning);
        PlayResult failed = await Player(s, new Shell(), new Agent(outcome: "Unavailable"), steam).PlayAsync(game, Ct);
        failed.Should().Be(new PlayResult(PlayOutcome.Failed, Detail: "Unavailable"));

        (await Player(s, shell, agent, steam).IsDirectAsync(Ct)).Should().BeTrue();
        GamePlayer.Describe(failed, "W").Warning.Should().BeTrue();
        GamePlayer.Describe(new PlayResult(PlayOutcome.RecordingOff), "W").Text.Should().Contain("Settings");
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
