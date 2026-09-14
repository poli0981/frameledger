using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Settings;

namespace FrameLedger.Application.Tests.Settings;

/// <summary>The registry over a store: tolerant reads, exact writes, nothing cached; and the recorder policy built on it.</summary>
public sealed class RegisteredSettingsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class MemoryStore : ISettingsStore
    {
        public Dictionary<string, string> Rows { get; } = new(StringComparer.Ordinal);

        public int Reads { get; private set; }

        public ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
        {
            Reads++;
            return ValueTask.FromResult(Rows.GetValueOrDefault(key));
        }

        public ValueTask SetAsync(string key, string value, CancellationToken ct = default)
        {
            Rows[key] = value;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task AbsentAndInvalidRowsReadAsTheDefaultAndEveryReadGoesToTheStore()
    {
        var store = new MemoryStore();
        var settings = new RegisteredSettings(store);

        (await settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs, Ct)).Should().Be(1000);
        (await settings.GetBooleanAsync(SettingsRegistry.CaptureBackground, Ct)).Should().BeTrue();
        (await settings.GetAsync(SettingsRegistry.UiTheme, Ct)).Should().Be("system");

        store.Rows[SettingsRegistry.TelemetryIntervalMs.Key] = "9000";
        (await settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs, Ct)).Should().Be(1000, "out of range is the default");
        store.Rows[SettingsRegistry.TelemetryIntervalMs.Key] = "750";
        (await settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs, Ct)).Should().Be(750, "no cache: the next read sees the row");
        store.Reads.Should().Be(5);
    }

    [Fact]
    public async Task AWriteIsNormalisedAndARefusedWriteStoresNothing()
    {
        var store = new MemoryStore();
        var settings = new RegisteredSettings(store);

        await settings.SetAsync(SettingsRegistry.RetentionRawSessionsPerGame, 0, Ct);
        await settings.SetAsync(SettingsRegistry.LogDebug, true, Ct);
        await settings.SetAsync(SettingsRegistry.UiLanguage, "vi", Ct);
        store.Rows.Should().Contain("retention.raw_sessions_per_game", "0").And.Contain("log.debug", "1").And.Contain("ui.language", "vi");

        Func<Task> refused = async () => await settings.SetAsync(SettingsRegistry.UiLanguage, "klingon", Ct).ConfigureAwait(true);
        await refused.Should().ThrowAsync<ArgumentException>();
        store.Rows[SettingsRegistry.UiLanguage.Key].Should().Be("vi", "the refused write changed nothing");
    }

    [Fact]
    public async Task ThePolicyResolvesTheThreeRecordingKeysPerSessionAndKeepsAnOperatorsBound()
    {
        var store = new MemoryStore();
        var policy = new SettingsRecorderPolicy(new RegisteredSettings(store));
        var baseline = new RecorderOptions();

        RecorderOptions defaults = await policy.ResolveAsync(baseline, Ct);
        defaults.MinimumSessionLength.Should().Be(TimeSpan.FromSeconds(30));
        defaults.RetentionKeep.Should().Be(20);
        defaults.TelemetryInterval.Should().Be(TimeSpan.FromSeconds(1));
        defaults.PartialFlushInterval.Should().Be(baseline.PartialFlushInterval, "not a setting");

        store.Rows["capture.min_session_s"] = "45";
        store.Rows["retention.raw_sessions_per_game"] = "0";
        store.Rows["telemetry.interval_ms"] = "500";
        RecorderOptions set = await policy.ResolveAsync(baseline, Ct);
        set.MinimumSessionLength.Should().Be(TimeSpan.FromSeconds(45));
        set.RetentionKeep.Should().Be(int.MaxValue, "0 is unlimited: a sweep that keeps everything, never one that keeps nothing");
        set.TelemetryInterval.Should().Be(TimeSpan.FromMilliseconds(500));

        // An operator's bounded capture (--seconds 8 lowers the minimum to 8 s) keeps its bound under any setting.
        RecorderOptions bounded = await policy.ResolveAsync(baseline with { MinimumSessionLength = TimeSpan.FromSeconds(8) }, Ct);
        bounded.MinimumSessionLength.Should().Be(TimeSpan.FromSeconds(8));
    }
}
