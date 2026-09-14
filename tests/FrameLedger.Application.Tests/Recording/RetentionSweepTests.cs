using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>The on-demand sweep (P4 PR-7): the Agent's own setting is the keep, unlimited sweeps nothing, and the repository's count is the answer.</summary>
public sealed class RetentionSweepTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Store : ISettingsStore
    {
        public Dictionary<string, string> Rows { get; } = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) => ValueTask.FromResult(Rows.GetValueOrDefault(key));

        public ValueTask SetAsync(string key, string value, CancellationToken ct = default)
        {
            Rows[key] = value;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task UnlimitedRetentionSweepsNothingAndSaysSo()
    {
        var sessions = new FakeSessionRepository();
        var store = new Store();
        store.Rows[SettingsRegistry.RetentionRawSessionsPerGame.Key] = "0";

        SweepRetentionAck ack = await new RetentionSweep(sessions, new RegisteredSettings(store)).RunAsync(Ct);

        ack.Should().Be(new SweepRetentionAck(0, 0, 0));
        sessions.SweepAlls.Should().BeEmpty("0 is unlimited, and a keep of zero must never reach the repository");
    }

    [Fact]
    public async Task TheSettingIsTheKeepAndTheRepositorysCountIsTheAnswer()
    {
        var sessions = new FakeSessionRepository { SweepAllResult = new RetentionSweepResult(3, 11) };
        var store = new Store();

        SweepRetentionAck byDefault = await new RetentionSweep(sessions, new RegisteredSettings(store)).RunAsync(Ct);
        store.Rows[SettingsRegistry.RetentionRawSessionsPerGame.Key] = "5";
        SweepRetentionAck set = await new RetentionSweep(sessions, new RegisteredSettings(store)).RunAsync(Ct);

        byDefault.Should().Be(new SweepRetentionAck(20, 3, 11), "the registry's default is 20");
        set.Should().Be(new SweepRetentionAck(5, 3, 11));
        sessions.SweepAlls.Should().Equal(20, 5);
    }
}
