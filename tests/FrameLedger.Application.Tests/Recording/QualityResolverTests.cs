// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;
using S = FrameLedger.Application.Capture.NvidiaDriverSettings;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// D63 (beta.24): the NVIDIA App's override is a session's quality mode when the driver reports it applied in the game's
/// process; otherwise the mode the game passed to a hooked call; otherwise none. The fixtures are the driver's words and
/// profile as the owner's ledger stored them (Silent Hill 2, 2026-10-13; Expedition 33, 2026-09-06).
/// </summary>
public sealed class QualityResolverTests
{
    // INITIALIZED | ENABLED | DLL_EXISTS | DLL_LOADED | DLL_SELECTED | PRESET | PERF_MODE, and bit 19 — as stored.
    private const string _silentHill2Sr = "0x8007F";

    // The same with CREATED | EVALUATE: the super-resolution feature ran (Expedition 33's word).
    private const string _expeditionSr = "0x8067F";

    // CREATED | EVALUATE and no override bit: the game's own DLSS.
    private const string _plainSr = "0x60F";

    private static string Words(string sr, uint? mode, string outcome = "Answered") =>
        "{\"Outcome\":\"" + outcome + "\",\"Sr\":\"" + sr + "\",\"Rr\":\"0x0\",\"Fg\":\"0x0\",\"Mode\":" + (mode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")
        + ",\"Preset\":12,\"Readings\":3,\"Answered\":3}";

    private static string Profile(params (uint Id, uint Value)[] settings) => DriverProfileRecord.Serialize(new DriverProfileReading
    {
        Outcome = DriverProfileOutcome.Application,
        ProfileName = "SILENT HILL 2",
        Settings = [.. settings.Select(static s => new DriverSettingReading(s.Id, s.Value, DriverSettingLocation.Profile, false))],
    })!;

    private static readonly string _silentHill2Profile = Profile((S.DlssSrOverride, 1), (S.DlssSrPreset, 12), (S.DlssSrMode, 5), (S.DlssSrScalingRatio, 33));

    [Fact]
    public void TheOwnersSilentHill2SessionReadsNoModeBecauseNoSuperResolutionRanThatTheDriverSaw()
    {
        // As stored: the hook ran and named nothing, and the driver's word lacks CREATED | EVALUATE.
        QualityResolver.Of("unknown", null, null, Words(_silentHill2Sr, 3), _silentHill2Profile).Should().BeNull(
            "an override the driver applied to a feature nobody saw run names the mode of nothing");

        // Had the driver seen it run, the profile's Ultra Performance (5) and the report's 3 are one mode in NGX's numbering.
        QualityResolver.Of("unknown", null, "dlss", Words(_expeditionSr, 3), _silentHill2Profile)
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.UltraPerformance, QualitySource.NvidiaApp));
    }

    [Fact]
    public void ExpeditionThirtyThreesOverrideIsQualityFromTheNvidiaApp()
    {
        QualityResolver.Of("unknown", null, "dlss", Words(_expeditionSr, 2), Profile((S.DlssSrOverride, 1), (S.DlssSrMode, 2)))
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.Quality, QualitySource.NvidiaApp));
        QualityResolver.Of("unknown", null, "dlss", Words(_expeditionSr, 2), driverProfileJson: null)
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.Quality, QualitySource.NvidiaApp), "the report alone, where no profile was read");
        QualityResolver.Of("dlss", "1", null, Words(_expeditionSr, 2), driverProfileJson: null)
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.Quality, QualitySource.NvidiaApp), "the override replaces what the Streamline title passed");
    }

    [Fact]
    public void DlaaAndACustomRatioAreTheOverridesOwn()
    {
        string dlaa = "0x" + (NgxOverrideFlags.Enabled | NgxOverrideFlags.SrDlaaMode | NgxOverrideFlags.CreatedAndEvaluated).ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        QualityResolver.Of("unknown", null, "dlss", Words(dlaa, null), driverProfileJson: null)
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.Dlaa, QualitySource.NvidiaApp));

        string ratio = "0x" + (NgxOverrideFlags.Enabled | NgxOverrideFlags.ScalingRatio | NgxOverrideFlags.CreatedAndEvaluated).ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        QualityResolver.Of("unknown", null, "dlss", Words(ratio, null), Profile((S.DlssSrMode, 6), (S.DlssSrScalingRatio, 77)))
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.Custom, QualitySource.NvidiaApp, 77));
        QualityResolver.Of("unknown", null, "dlss", Words(ratio, null), Profile((S.DlssSrMode, 6), (S.DlssSrScalingRatio, 120)))
            .Should().Be(new ResolvedQuality(UpscalerQualityMode.Custom, QualitySource.NvidiaApp), "a ratio outside 33–100 is not named");
        QualityResolver.Of("unknown", null, "dlss", Words(ratio, null), driverProfileJson: null)
            .Should().Be(new ResolvedQuality(null, QualitySource.NvidiaApp), "overridden, and nothing says to what");
    }

    [Fact]
    public void WhenTheProfileAndTheReportDisagreeTheOverrideStandsWithNoMode()
    {
        QualityResolver.Of("unknown", null, "dlss", Words(_expeditionSr, 0), Profile((S.DlssSrMode, 2)))
            .Should().Be(new ResolvedQuality(null, QualitySource.NvidiaApp), "Quality in the profile, Performance in the report: neither is shown");
        QualityResolver.Of("dlss", "3", null, Words(_expeditionSr, 0), Profile((S.DlssSrMode, 2)))
            .Should().Be(new ResolvedQuality(null, QualitySource.NvidiaApp), "and never the game's own value in its place");
        QualityResolver.Of("unknown", null, "dlss", Words(_expeditionSr, 9), Profile((S.DlssSrMode, 3)))
            .Should().Be(new ResolvedQuality(null, QualitySource.NvidiaApp), "a report no numbering names, and the profile's 'the game's own'");
    }

    [Fact]
    public void WithNoOverrideTheGamesOwnHookedModeIsShown()
    {
        QualityResolver.Of("dlss", "3", null, Words(_plainSr, 0), driverProfileJson: null).Should().Be(new ResolvedQuality(UpscalerQualityMode.Quality, QualitySource.Game));
        QualityResolver.Of("dlss", "9", null, null, null).Should().Be(new ResolvedQuality(null, QualitySource.Game, Stored: "9"), "a number nobody can name is still what was measured");
        QualityResolver.Of("fsr3", "2", null, Words(_expeditionSr, 2), null).Should().Be(new ResolvedQuality(null, QualitySource.Game, Stored: "2"), "a DLSS override does not apply to FSR");
        QualityResolver.Of("fsr3", "17", null, Words(_expeditionSr, 2), null).Should().Be(new ResolvedQuality(UpscalerQualityMode.Quality, QualitySource.Game),
            "beta.25 (D65): FSR's byte, the mode its query answer named");
        QualityResolver.Of("dlss", null, null, null, null).Should().BeNull();
        QualityResolver.Of("unknown", "3", null, null, null).Should().BeNull("a quality without a name to hang it on");
        QualityResolver.Of(null, null, "dlss", Words(_plainSr, 0), null).Should().BeNull("the driver's word alone is identity");
    }

    [Fact]
    public void AnUnansweredOrUnreadableReportIsNoOverride()
    {
        QualityResolver.Of("dlss", "2", null, Words(_expeditionSr, 2, outcome: "Unanswered"), null).Should().Be(new ResolvedQuality(UpscalerQualityMode.Balanced, QualitySource.Game));
        QualityResolver.Of("dlss", "2", null, "{broken", null).Should().Be(new ResolvedQuality(UpscalerQualityMode.Balanced, QualitySource.Game));
        QualityResolver.Of("dlss", "2", null, Words("garbage", 2), null).Should().Be(new ResolvedQuality(UpscalerQualityMode.Balanced, QualitySource.Game));
    }

    [Fact]
    public void TheLiveCardReadsTheReportAloneAndSaysWhatItCannotSettle()
    {
        const ulong applied = NgxOverrideFlags.Enabled | NgxOverrideFlags.PerfMode | NgxOverrideFlags.CreatedAndEvaluated;
        QualityResolver.Live(dlssRan: true, applied, 3).Should().Be(new ResolvedQuality(UpscalerQualityMode.UltraPerformance, QualitySource.NvidiaApp));
        QualityResolver.Live(dlssRan: false, applied, 3).Should().BeNull("no DLSS ran");
        QualityResolver.Live(dlssRan: true, NgxOverrideFlags.CreatedAndEvaluated, 3).Should().BeNull("no override bit");
        QualityResolver.Live(dlssRan: true, NgxOverrideFlags.Enabled | NgxOverrideFlags.ScalingRatio, 0).Should().Be(new ResolvedQuality(null, QualitySource.NvidiaApp));
    }

    [Fact]
    public void TheWireTokensRoundTrip()
    {
        foreach (UpscalerQualityMode mode in Enum.GetValues<UpscalerQualityMode>().Where(static m => m != UpscalerQualityMode.None))
        {
            string token = QualityResolver.Token(new ResolvedQuality(mode, QualitySource.NvidiaApp))!;
            QualityResolver.FromToken(token).Should().Be(new ResolvedQuality(mode, QualitySource.NvidiaApp), token);
        }

        QualityResolver.Token(new ResolvedQuality(null, QualitySource.NvidiaApp)).Should().Be(QualityResolver.OverrideToken);
        QualityResolver.FromToken(QualityResolver.OverrideToken).Should().Be(new ResolvedQuality(null, QualitySource.NvidiaApp));
        QualityResolver.Token(new ResolvedQuality(UpscalerQualityMode.Quality, QualitySource.Game)).Should().BeNull("the game's own mode is the hooked byte's, already on the wire");
        QualityResolver.Token(null).Should().BeNull();
        QualityResolver.FromToken("eMaxQuality").Should().BeNull();
        QualityResolver.FromToken(null).Should().BeNull();
        QualityResolver.ModeToken(UpscalerQualityMode.None).Should().BeNull();
    }
}
