using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.11: Delete all sessions (owner decision D40) through the Agent, from the game page; and the charts across a game's
/// sessions (D39) — the Sessions tab's overview, and the Trend's second metric and second unit.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class SessionDeletionAndOverviewTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>An Agent that answers DeleteSessions as scripted, and records what it was asked.</summary>
    private sealed class ScriptedAgent : IAgentRequests
    {
        public HelloAck? Hello => null;

        public bool IsConnected { get; set; } = true;

        public List<DeleteSessionsRequest> Asked { get; } = [];

        public Func<DeleteSessionsRequest, Task<IpcEnvelope>> Answer { get; set; } = static r =>
            Task.FromResult(IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.DeleteSessionsAck, "1", new DeleteSessionsAck(r.GameId, 1, 2))));

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class
        {
            type.Should().Be(IpcMessageType.DeleteSessions);
            var request = (DeleteSessionsRequest)(object)payload;
            Asked.Add(request);
            return Answer(request);
        }
    }

    private sealed class Confirm(bool yes) : IConfirmations
    {
        public List<(string? Game, long Sessions)> Asked { get; } = [];

        public Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default) => Task.FromResult(RemoveGameChoice.Cancel);

        public Task<bool> DeleteSessionsAsync(string? gameName, long sessions, CancellationToken ct = default)
        {
            Asked.Add((gameName, sessions));
            return Task.FromResult(yes);
        }
    }

    private sealed class Strip : IMessageStrip
    {
        public List<string> Lines { get; } = [];

        public void Info(string title, string body) => Lines.Add("info:" + body);

        public void Success(string title, string body) => Lines.Add("success:" + body);

        public void Warn(string title, string body) => Lines.Add("warn:" + body);
    }

    private sealed class Nav : IPageNavigator
    {
        public void Navigate<TPage>()
            where TPage : class
        {
        }

        public void GoBack()
        {
        }
    }

    private sealed class NoEdit : IEditGamePrompt
    {
        public Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default) => Task.FromResult<GameMetadata?>(null);
    }

    private sealed class NoSummaries : ISessionSummaryOpener
    {
        public void Open(long sessionId)
        {
        }
    }

    private sealed class NoPicker : IGamePicker
    {
        public string? PickExecutable() => null;
    }

    private sealed class NoPrompt : IConsentPrompt
    {
        public Task<bool> ShowAsync(string gameName, CancellationToken ct = default) => Task.FromResult(false);
    }

    private static async Task<GameDetailViewModel> PageAsync(ScratchLedger s, long gameId, IConfirmations confirm, SessionDeletion? deletion, IMessageStrip strip,
        SessionSelection? selection = null)
    {
        var vm = new GameDetailViewModel(s.Library, new GameSelection { GameId = gameId }, new HookingConsent(new ScriptedAgent(), new NoPrompt()), new Nav(),
            confirm, new NoEdit(), strip, new NoSummaries(), new SessionSeriesLoader(s.Sessions),
            new Infrastructure.Persistence.SqliteHardwareSnapshotRepository(s.Db), selection ?? new SessionSelection(), new NoPicker(), deletion: deletion);
        Task pending = vm.Pending;
        await pending.ConfigureAwait(false);
        return vm;
    }

    [Fact]
    public async Task TheRequestSaysWhatTheAgentDidOrWhyNothingWasDeleted()
    {
        var agent = new ScriptedAgent();
        var deletion = new SessionDeletion(agent);

        SessionDeletionResult done = await deletion.DeleteAsync(7, Ct);
        done.Should().Be(new SessionDeletionResult(SessionDeletionOutcome.Deleted, 1, 2));
        done.Message.Should().Contain("2");
        agent.Asked.Should().Equal(new DeleteSessionsRequest(7));

        agent.Answer = static _ => throw new IpcRequestException(IpcErrorCode.SessionRunning, "running");
        (await deletion.DeleteAsync(null, Ct)).Outcome.Should().Be(SessionDeletionOutcome.SessionRunning);

        agent.Answer = static _ => throw new IpcRequestException(IpcErrorCode.UnknownType, "older agent");
        (await deletion.DeleteAsync(null, Ct)).Outcome.Should().Be(SessionDeletionOutcome.AgentTooOld);

        agent.IsConnected = false;
        (await deletion.DeleteAsync(null, Ct)).Outcome.Should().Be(SessionDeletionOutcome.AgentUnavailable);
        agent.Asked.Should().HaveCount(3, "nothing is asked of an Agent that is not there");
    }

    /// <summary>
    /// The game page asks first and deletes nothing on a no; on a yes the Agent deletes, the page says so, reloads empty, and
    /// File ▸ Export no longer points at a session that is gone.
    /// </summary>
    [Fact]
    public async Task TheGamePageAsksThenTheAgentDeletesAndThePageReloads()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long first = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-2));
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-1));

        var strip = new Strip();
        var agent = new ScriptedAgent();
        GameDetailViewModel no = await PageAsync(s, game.Id, new Confirm(false), new SessionDeletion(agent), strip);
        await no.DeleteAllSessionsCommand.ExecuteAsync(null);
        agent.Asked.Should().BeEmpty("a no deletes nothing");
        no.Sessions.Should().HaveCount(2);

        // The scripted Agent deletes for real, as the Agent's repository would.
        agent.Answer = async r =>
        {
            RetentionSweepResult gone = await s.Sessions.DeleteSessionsAsync(r.GameId, Ct).ConfigureAwait(false);
            return IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.DeleteSessionsAck, "1", new DeleteSessionsAck(r.GameId, gone.Games, gone.Sessions)));
        };
        var confirm = new Confirm(true);
        var selection = new SessionSelection();
        selection.Set(first);
        GameDetailViewModel yes = await PageAsync(s, game.Id, confirm, new SessionDeletion(agent), strip, selection);
        await yes.DeleteAllSessionsCommand.ExecuteAsync(null);

        confirm.Asked.Should().Equal(("Alpha", 2L));
        agent.Asked.Should().Equal(new DeleteSessionsRequest(game.Id));
        yes.Sessions.Should().BeEmpty();
        yes.SessionsEmpty.Should().BeTrue();
        strip.Lines.Should().ContainSingle(static l => l.StartsWith("success:", StringComparison.Ordinal));
        selection.SessionId.Should().BeNull("an id that is gone can be reused by the next session");
    }

    /// <summary>
    /// D39: the overview draws every hooked session's rate and lows; the Trend draws a ticked metric beside the selected
    /// one, a second unit on the right, and offers no third unit.
    /// </summary>
    [Fact]
    public async Task TheOverviewAndTheTrendDrawSeveralMetricsTwoUnitsAtMost()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddDays(-2), native: 60);
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddDays(-1), native: 70);
        GameDetailViewModel vm = await PageAsync(s, game.Id, new Confirm(false), null, new Strip());

        vm.OverviewVisible.Should().BeTrue();
        vm.OverviewLines.Should().NotBeEmpty().And.OnlyContain(static l => l.Unit == Strings.Trend_Unit_Fps && l.Points.Count == 2);
        vm.OverviewLines.Select(static l => l.Label).Should().NotContain(Strings.Trend_Metric_NativeFps,
            "no session generated frames, so Native would only repeat the Presented line");

        vm.TrendLines.Should().ContainSingle("only the selected metric until another is ticked");
        TrendMetricOptionViewModel selected = vm.TrendExtras.Single(o => o.Metric == vm.TrendMetric);
        selected.IsEnabled.Should().BeFalse("the selected metric is not offered beside itself");

        vm.TrendExtras.Single(static o => o.Metric == TrendMetric.MaxGpuTemp).IsSelected = true;
        vm.TrendLines.Should().HaveCount(2);
        vm.TrendLines[1].Unit.Should().Be(Strings.Trend_Unit_Celsius);
        vm.TrendExtras.Single(static o => o.Metric == TrendMetric.AvgGpuPower).IsEnabled.Should().BeFalse("watts would be a third unit");
        vm.TrendExtras.Single(static o => o.Metric == TrendMetric.P1Low).IsEnabled.Should().BeTrue("a frame rate is the first unit");
        vm.TrendExtras.Single(static o => o.Metric == TrendMetric.MaxCpuTemp).IsEnabled.Should().BeTrue("°C is already on the chart");

        vm.TrendExtras.Single(static o => o.Metric == TrendMetric.MaxGpuTemp).IsSelected = false;
        vm.TrendExtras.Single(static o => o.Metric == TrendMetric.AvgGpuPower).IsEnabled.Should().BeTrue("back to one unit");
    }

    [Theory]
    [InlineData(TrendMetric.PresentedFps, "FPS")]
    [InlineData(TrendMetric.P01Low, "FPS")]
    [InlineData(TrendMetric.MaxGpuTemp, "°C")]
    [InlineData(TrendMetric.AvgGpuLoad, "%")]
    [InlineData(TrendMetric.DisplayBorderlessShare, "%")]
    [InlineData(TrendMetric.AvgGpuPower, "W")]
    [InlineData(TrendMetric.AvgRam, "MB")]
    public void EachMetricHasItsUnit(TrendMetric metric, string unit) => TrendSeriesBuilder.UnitOf(metric).Should().StartWith(unit);
}
