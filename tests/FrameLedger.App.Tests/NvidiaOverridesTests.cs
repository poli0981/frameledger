using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using S = FrameLedger.Application.Capture.NvidiaDriverSettings;

namespace FrameLedger.App.Tests;

/// <summary>
/// The NVIDIA driver's side of a session in words (beta.8): the NVIDIA App's overrides as the driver profile stores them —
/// DLSS with its preset and mode, frame generation as its multiplier, Smooth Motion labelled as community-documented — and
/// what the driver reported applying in the game's process. Nothing is said where nothing was read.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class NvidiaOverridesTests
{
    private static T InEnglish<T>(Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static string Profile(DriverProfileOutcome outcome, params (uint Id, uint? Value)[] settings) => DriverProfileRecord.Serialize(new DriverProfileReading
    {
        Outcome = outcome,
        ProfileName = "Clair Obscur: Expedition 33",
        Settings = [.. settings.Select(static s => new DriverSettingReading(s.Id, s.Value, s.Value is null ? DriverSettingLocation.NotSet : DriverSettingLocation.Profile, false))],
    })!;

    [Fact]
    public void TheNvidiaAppsOverridesAreSaidWithTheirPresetModeAndMultiplier() => InEnglish(() =>
    {
        string json = Profile(DriverProfileOutcome.Application,
            (S.DlssSrOverride, 1), (S.DlssSrPreset, 11), (S.DlssSrMode, 2),
            (S.DlssFgOverride, 1), (S.DlssgMultiFrameCount, 3), (S.DlssFgPreset, S.PresetLatest),
            (S.SmoothMotion, 1), (S.DlssRrOverride, 0));

        NvidiaOverrides.ProfileLine(json).Should().Be(
            "NVIDIA profile “Clair Obscur: Expedition 33”: DLSS override (preset K, Quality) · Frame Generation override (×4, latest preset) · "
            + "Smooth Motion (a setting community tools document, not NVIDIA)");
        return 0;
    });

    [Fact]
    public void ACustomRatioDlaaAndAForcedFrameGenerationModeAreNamed() => InEnglish(() =>
    {
        DriverProfileRecord profile = DriverProfileRecord.Parse(Profile(DriverProfileOutcome.Application,
            (S.DlssSrOverride, 1), (S.DlssSrMode, 6), (S.DlssSrScalingRatio, 77), (S.DlaaOverride, 1), (S.DlssgMode, 2)))!;

        NvidiaOverrides.Of(profile).Should().Equal("DLSS override (custom 77%, DLAA)", "frame generation forced on");
        return 0;
    });

    [Fact]
    public void AProfileThatOverridesNothingSaysSoAndNoProfileSaysNothing() => InEnglish(() =>
    {
        NvidiaOverrides.ProfileLine(Profile(DriverProfileOutcome.Global, (S.DlssSrOverride, null), (S.DlssSrMode, 3)))
            .Should().Be("NVIDIA global profile (no profile names this game): no DLSS or frame generation override", "mode 3 is the game's own");
        NvidiaOverrides.ProfileLine(Profile(DriverProfileOutcome.Degraded)).Should().BeNull("no NVIDIA driver answered");
        NvidiaOverrides.ProfileLine(null).Should().BeNull("a row from before beta.8");
        return 0;
    });

    /// <summary>
    /// The driver's own report, measured on the owner's machine 2026-09-06 under an NVIDIA App override (Clair Obscur:
    /// Expedition 33): SR mask 0x8067F — DLL selected, preset, performance mode — with preset 11 and mode 2.
    /// </summary>
    [Fact]
    public void TheDriversReportSaysWhatItsOverrideBitsSet() => InEnglish(() =>
    {
        const string expedition = "{\"Outcome\":\"Answered\",\"Sr\":\"0x8067F\",\"Rr\":\"0x0\",\"Fg\":\"0x0\",\"Driver\":61664,\"Mode\":2,\"Preset\":11,\"Readings\":3,\"Answered\":3}";
        NvidiaOverrides.DriverLine(expedition).Should().Be("NVIDIA driver, for this game's process: DLSS DLL chosen by the driver · preset K · Quality");

        const string plain = "{\"Outcome\":\"Answered\",\"Sr\":\"0x60F\",\"Rr\":\"0x0\",\"Fg\":\"0x0\",\"Readings\":1,\"Answered\":1}";
        NvidiaOverrides.DriverLine(plain).Should().BeNull("created and evaluated, no override bit: the game's own DLSS");
        NvidiaOverrides.DriverLine("{\"Outcome\":\"Unanswered\",\"Readings\":1,\"Answered\":0}").Should().BeNull();
        NvidiaOverrides.DriverLine("not json").Should().BeNull();
        return 0;
    });

    [Theory]
    [InlineData(1u, "preset A")]
    [InlineData(15u, "preset O")]
    [InlineData(26u, "preset Z")]
    [InlineData(S.PresetLatest, "latest preset")]
    [InlineData(S.PresetDefault, "default preset")]
    public void PresetsAreLetters(uint value, string expected) => InEnglish(() => NvidiaOverrides.Preset(value)).Should().Be(expected);

    [Theory]
    [InlineData(0u)]
    [InlineData(27u)]
    public void AnOffOrUnknownPresetSaysNothing(uint value) => NvidiaOverrides.Preset(value).Should().BeNull();
}
