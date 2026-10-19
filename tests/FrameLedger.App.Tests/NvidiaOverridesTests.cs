// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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

    /// <summary>
    /// The driver reports its mode in NGX's own numbering, not the profile's (beta.24): the owner's Silent Hill 2 session of
    /// 2026-10-13 ran under the NVIDIA App's Ultra Performance — profile value 5 — and the driver reported 3. Read through the
    /// profile's table, 3 said nothing and a DLAA override's 5 would have printed "Ultra Performance".
    /// </summary>
    [Fact]
    public void TheDriversModeIsReadInNgxsNumbering() => InEnglish(() =>
    {
        const string silentHill2 = "{\"Outcome\":\"Answered\",\"Sr\":\"0x8007F\",\"Rr\":\"0x0\",\"Fg\":\"0xA03F\",\"Mode\":3,\"Preset\":12,\"FgCount\":5,\"Readings\":3,\"Answered\":3}";
        NvidiaOverrides.DriverLine(silentHill2).Should().Be("NVIDIA driver, for this game's process: DLSS DLL chosen by the driver · preset L · Ultra Performance · Frame Generation override (×6)");
        NvidiaOverrides.DriverLine(silentHill2.Replace("\"Mode\":3", "\"Mode\":5", StringComparison.Ordinal)).Should().Contain("DLAA").And.NotContain("Ultra Performance");
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

    /// <summary>
    /// beta.24 (D62): the frame-generation block's context line, in the NVIDIA App's own terms (its Frame Generation Mode
    /// dialog, owner's screenshot 2026-10-19: Dynamic with Target FPS and Multiplier "Up to 6x", or Fixed) — configuration
    /// above the measured rows, never one of them.
    /// </summary>
    [Fact]
    public void TheFrameGenerationContextSaysHowTheNvidiaAppSetItInItsOwnTerms() => InEnglish(() =>
    {
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application,
            (S.DlssgMode, 4), (S.DlssgDynamicMultiFrameCountMax, 5), (S.DlssgDynamicTargetFrameRate, S.DynamicTargetAuto)))
            .Should().Be("Set in the NVIDIA App: Dynamic, up to ×6, target: max refresh rate", "5 generated frames per rendered one is the App's \"Up to 6x\"");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application,
            (S.DlssgMode, 4), (S.DlssgDynamicTargetFrameRate, 120)))
            .Should().Be("Set in the NVIDIA App: Dynamic, target 120 FPS", "no ceiling stored: none is named");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application,
            (S.DlssgMode, 4), (S.DlssgDynamicMultiFrameCountMax, 16), (S.DlssgDynamicTargetFrameRate, 0x0200_0000)))
            .Should().Be("Set in the NVIDIA App: Dynamic", "a value outside the header's ranges is left out, never guessed");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application, (S.DlssgMode, 2), (S.DlssgMultiFrameCount, 3)))
            .Should().Be("Set in the NVIDIA App: Fixed ×4");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Global, (S.DlssgMultiFrameCount, 5)))
            .Should().Be("Set in the NVIDIA App: Fixed ×6", "a multiplier with the mode unset is the Fixed one");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application, (S.DlssgMode, 2)))
            .Should().Be("Set in the NVIDIA App: frame generation on");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application, (S.DlssgMode, 1), (S.DlssgMultiFrameCount, 3)))
            .Should().Be("Set in the NVIDIA App: frame generation off");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application, (S.DlssgMode, 3)))
            .Should().Be("Set in the NVIDIA App: automatic");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application, (S.DlssgMode, 3), (S.DlssgMultiFrameCount, 2)))
            .Should().Be("Set in the NVIDIA App: automatic, ×3");
        return 0;
    });

    [Fact]
    public void WithNoFrameGenerationSettingTheGameDecidedAndWithNoProfileNothingIsSaid() => InEnglish(() =>
    {
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Application, (S.DlssgMode, 0), (S.DlssgMultiFrameCount, null), (S.DlssSrOverride, 1)))
            .Should().Be("Set in the game — no NVIDIA App frame-generation override", "Use 3D app setting: the game's own menu chose");
        NvidiaOverrides.FrameGenerationContext(Profile(DriverProfileOutcome.Degraded)).Should().BeNull("no NVIDIA driver answered");
        NvidiaOverrides.FrameGenerationContext(null).Should().BeNull("a row from before beta.8");
        NvidiaOverrides.FrameGenerationContext("{broken").Should().BeNull();
        return 0;
    });

    [Fact]
    public void AValueTheDriverShipsInItsOwnProfileIsNotCalledTheUsersChoice() => InEnglish(() =>
    {
        string shipped = DriverProfileRecord.Serialize(new DriverProfileReading
        {
            Outcome = DriverProfileOutcome.Application,
            ProfileName = "A game NVIDIA profiles",
            Settings = [new DriverSettingReading(S.DlssgMode, 2, DriverSettingLocation.Profile, true), new DriverSettingReading(S.DlssgMultiFrameCount, 1, DriverSettingLocation.Profile, true)],
        })!;
        NvidiaOverrides.FrameGenerationContext(shipped).Should().Be("Set by the NVIDIA driver's own profile for the game: Fixed ×2");

        string mixed = DriverProfileRecord.Serialize(new DriverProfileReading
        {
            Outcome = DriverProfileOutcome.Application,
            ProfileName = "A game NVIDIA profiles",
            Settings = [new DriverSettingReading(S.DlssgMode, 2, DriverSettingLocation.Profile, true), new DriverSettingReading(S.DlssgMultiFrameCount, 3, DriverSettingLocation.Profile, false)],
        })!;
        NvidiaOverrides.FrameGenerationContext(mixed).Should().Be("Set in the NVIDIA App: Fixed ×4", "one value someone set makes it the user's");
        return 0;
    });
}
