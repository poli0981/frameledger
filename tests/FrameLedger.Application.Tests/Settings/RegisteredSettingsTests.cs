// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Input;
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
        defaults.AdvancedMetrics.Should().BeFalse("D101: off by default, and off means nothing more is read or stored");

        store.Rows["capture.min_session_s"] = "45";
        store.Rows["retention.raw_sessions_per_game"] = "0";
        store.Rows["telemetry.interval_ms"] = "500";
        RecorderOptions set = await policy.ResolveAsync(baseline, Ct);
        set.MinimumSessionLength.Should().Be(TimeSpan.FromSeconds(45));
        set.RetentionKeep.Should().Be(int.MaxValue, "0 is unlimited: a sweep that keeps everything, never one that keeps nothing");
        set.TelemetryInterval.Should().Be(TimeSpan.FromMilliseconds(500));
        set.AdvancedMetrics.Should().BeFalse();

        // telemetry.advanced (D101): exactly "1" is on, read like the others at each session's start.
        store.Rows["telemetry.advanced"] = "1";
        (await policy.ResolveAsync(baseline, Ct)).AdvancedMetrics.Should().BeTrue();
        store.Rows["telemetry.advanced"] = "yes";
        (await policy.ResolveAsync(baseline, Ct)).AdvancedMetrics.Should().BeFalse("a row that is not 0 or 1 reads as the default, never as on");

        // An operator's bounded capture (--seconds 8 lowers the minimum to 8 s) keeps its bound under any setting.
        RecorderOptions bounded = await policy.ResolveAsync(baseline with { MinimumSessionLength = TimeSpan.FromSeconds(8) }, Ct);
        bounded.MinimumSessionLength.Should().Be(TimeSpan.FromSeconds(8));
    }

    /// <summary>
    /// D156: controllers and input are read when the option is on - it is, unless switched off - AND the Privacy
    /// Policy that describes the reading has been accepted on this PC. The Agent starts at logon, before the App has
    /// shown a new document; until it has, a session reads nothing and says why.
    /// </summary>
    [Theory]
    [InlineData(null, null, false, true)] // nothing accepted yet
    [InlineData(null, "2.21", false, true)] // the version before the one that describes it
    [InlineData(null, "2.22", true, false)]
    [InlineData(null, "2.30", true, false)]
    [InlineData(null, "3.0", true, false)]
    [InlineData("1", "2.22", true, false)]
    [InlineData("0", "2.22", false, false)] // switched off: not read, and not "held"
    [InlineData("0", null, false, false)]
    [InlineData("yes", "2.22", true, false)] // a row that is not 0 or 1 reads as the default, which is on
    public async Task TheInputReadingNeedsTheOptionOnAndTheTermsThatDescribeItAccepted(string? row, string? accepted, bool reads, bool held)
    {
        var store = new MemoryStore();
        if (row is not null)
        {
            store.Rows["capture.input_devices"] = row;
        }

        var legal = new MemoryAcceptance();
        if (accepted is not null)
        {
            await legal.RecordAsync(new LegalAcceptance(InputTerms.PrivacyDocument, accepted, DateTimeOffset.UnixEpoch), Ct);
        }

        RecorderOptions options = await new SettingsRecorderPolicy(new RegisteredSettings(store), legal).ResolveAsync(new RecorderOptions(), Ct);

        options.InputDevices.Should().Be(reads);
        options.InputHeldForTerms.Should().Be(held);
    }

    [Fact]
    public async Task WithNoAcceptanceStoreNothingCanBeShownAcceptedAndNothingIsRead()
    {
        RecorderOptions options = await new SettingsRecorderPolicy(new RegisteredSettings(new MemoryStore())).ResolveAsync(new RecorderOptions(), Ct);

        options.InputDevices.Should().BeFalse("an operator's console has no Legal Gate behind it");
        options.InputHeldForTerms.Should().BeTrue();
    }

    [Theory]
    [InlineData("2.22", true)]
    [InlineData("2.23", true)]
    [InlineData("2.100", true)] // compared as numbers: 100 is after 22
    [InlineData("3.0", true)]
    [InlineData(" 2.22 ", true)]
    [InlineData("2.21", false)]
    [InlineData("2.9", false)] // 9 is before 22
    [InlineData("1.99", false)]
    [InlineData("2", false)]
    [InlineData("2.22.1", false)]
    [InlineData("two", false)]
    [InlineData("-2.22", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TheTermsAreAcceptedFromTheVersionThatFirstDescribedTheReading(string? version, bool accepted) =>
        InputTerms.Accepted(version).Should().Be(accepted);

    private sealed class MemoryAcceptance : ILegalAcceptanceStore
    {
        private readonly Dictionary<string, LegalAcceptance> _rows = new(StringComparer.Ordinal);

        public ValueTask<LegalAcceptance?> FindAsync(string document, CancellationToken ct = default) => ValueTask.FromResult(_rows.GetValueOrDefault(document));

        public ValueTask<IReadOnlyList<LegalAcceptance>> ListAsync(CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<LegalAcceptance>>([.. _rows.Values]);

        public ValueTask RecordAsync(LegalAcceptance acceptance, CancellationToken ct = default)
        {
            _rows[acceptance.Document] = acceptance;
            return ValueTask.CompletedTask;
        }
    }
}
