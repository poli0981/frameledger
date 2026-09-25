using FluentAssertions;
using FrameLedger.Application.Settings;

namespace FrameLedger.Application.Tests.Settings;

/// <summary>D16's registry: every key defined once with a valid default; validation in both directions; tolerant reads.</summary>
public sealed class SettingsRegistryTests
{
    [Fact]
    public void EveryKeyIsUniqueAndItsDefaultPassesItsOwnValidation()
    {
        SettingsRegistry.All.Select(static d => d.Key).Should().OnlyHaveUniqueItems();
        foreach (SettingDefinition d in SettingsRegistry.All)
        {
            d.Validate(d.Default).Should().BeNull($"{d.Key}'s default must be a value it accepts");
            d.Normalise(d.Default).Should().Be(d.Default, $"{d.Key}'s default is already normalised");
            SettingsRegistry.Find(d.Key).Should().BeSameAs(d);
        }

        SettingsRegistry.Find("nobody.knows").Should().BeNull();
    }

    /// <summary>D33: the exception option is off unless the user turns it on — and exactly "1" is on.</summary>
    [Fact]
    public void TheUserModeExceptionOptionIsOffByDefault()
    {
        SettingDefinition d = SettingsRegistry.HookingUserModeExceptions;
        d.Key.Should().Be("hooking.usermode_ac_exceptions");
        d.Default.Should().Be("0");
        d.Kind.Should().Be(SettingKind.Boolean);
        d.AgentReads.Should().BeTrue("the Agent reads it at every session start and command");
    }

    [Fact]
    public void TheAgentReadsExactlyTheFourPrefixesD16Names()
    {
        string[] agent = [.. SettingsRegistry.All.Where(static d => d.AgentReads).Select(static d => d.Key)];
        // hooking.usermode_ac_exceptions joined on 2026-09-26 (D33): the user-mode exception's option, off by default.
        agent.Should().BeEquivalentTo(["capture.background", "hooking.kill_switch", "hooking.usermode_ac_exceptions", "capture.min_session_s",
            "telemetry.interval_ms", "retention.raw_sessions_per_game"]);
        agent.Should().OnlyContain(static k => k.StartsWith("hooking.", StringComparison.Ordinal) || k.StartsWith("capture.", StringComparison.Ordinal)
            || k.StartsWith("telemetry.", StringComparison.Ordinal) || k.StartsWith("retention.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("500", null)]
    [InlineData("2000", null)]
    [InlineData("499", "499 is outside 500..2000")]
    [InlineData("2001", "2001 is outside 500..2000")]
    [InlineData("fast", "'fast' is not an integer")]
    [InlineData(null, "no value")]
    public void AnIntegerIsBoundedInclusive(string? value, string? problem)
    {
        SettingsRegistry.TelemetryIntervalMs.Validate(value).Should().Be(problem);
    }

    [Fact]
    public void ABooleanIsExactlyZeroOrOneAndAChoiceIsOneOfItsChoices()
    {
        SettingsRegistry.LogDebug.Validate("1").Should().BeNull();
        SettingsRegistry.LogDebug.Validate("0").Should().BeNull();
        SettingsRegistry.LogDebug.Validate("yes").Should().Be("'yes' is not 0 or 1");
        SettingsRegistry.LogDebug.Validate("true").Should().NotBeNull();

        SettingsRegistry.UiTheme.Validate("dark").Should().BeNull();
        SettingsRegistry.UiTheme.Validate("Dark").Should().Be("'Dark' is not one of system|light|dark", "ordinal, like every stored token");
        SettingsRegistry.UiLanguage.Choices.Should().Equal("en", "vi", "ja");
    }

    [Fact]
    public void AReadIsTolerantAndAWriteIsExact()
    {
        SettingDefinition d = SettingsRegistry.CaptureMinSessionSeconds;
        d.Effective(null).Should().Be("30");
        d.Effective("purple").Should().Be("30", "a hand-edited row reads as the default, never as an exception");
        d.Effective("45").Should().Be("45");
        d.AsInteger("45").Should().Be(45);
        d.AsInteger("999999").Should().Be(30, "out of range reads as the default");
        SettingsRegistry.HookingKillSwitch.AsBoolean("1").Should().BeTrue();
        SettingsRegistry.HookingKillSwitch.AsBoolean("yes").Should().BeFalse();

        d.Normalise(" 045 ").Should().Be("45", "trimmed and re-printed invariant");
        Action refused = () => d.Normalise("4");
        refused.Should().Throw<ArgumentException>().WithMessage("capture.min_session_s: 4 is outside 5..600*");
    }

    [Fact]
    public void RetentionZeroIsUnlimitedByDefinition()
    {
        SettingsRegistry.RetentionRawSessionsPerGame.Minimum.Should().Be(0);
        SettingsRegistry.RetentionRawSessionsPerGame.Validate("0").Should().BeNull();
        SettingsRegistry.RetentionRawSessionsPerGame.Default.Should().Be("20", "06_DATA_MODEL §Retention");
    }
}
