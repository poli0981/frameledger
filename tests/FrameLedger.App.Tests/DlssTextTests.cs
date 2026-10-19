// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>
/// D64 (beta.24): the DLSS a session ran, in words — each library's version and where it was loaded from, the preset the
/// driver reports, a generation only where the session shows one, DLSS 5 only as "runtime loaded". The rows are shaped
/// like the owner's ledger: Witcher 3 (310.9.1 libraries, ×2) and Silent Hill 2 (×6 through the NVIDIA App).
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class DlssTextTests
{
    private const string _witcherModules = "{\"nvngx_dlss.dll\":\"310,9,1,0\",\"nvngx_dlssd.dll\":\"310,9,1,0\",\"nvngx_dlssg.dll\":\"310,9,1,0\",\"sl.dlss_g.dll\":\"2,14,1,0\"}";

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

    private static SessionRow Row(string? modules = null, string? sources = null, string? upscaler = null, string? driverReported = null, string? words = null,
        string fgMode = "na", double? factor = null, string? mix = null) => new()
        {
            SessionGuid = Guid.NewGuid(),
            GameId = 1,
            SnapshotId = 1,
            StartedAt = DateTimeOffset.UnixEpoch,
            EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
            QpcEpoch = 0,
            QpcFrequency = 1,
            Tier = CaptureTier.Hooked,
            Mode = CaptureMode.Launch,
            ExitStatus = ExitStatus.Normal,
            FrameCount = 1000,
            RuntimeModulesJson = modules,
            RuntimeModuleSourcesJson = sources,
            Upscaler = upscaler,
            UpscalerDriverReported = driverReported,
            NgxDriverWordsJson = words,
            FgMode = fgMode,
            FgFactor = factor,
            FgMixJson = mix,
        };

    [Fact]
    public void WitchersLibrariesAreVersionsAndNameNoGenerationAtTimesTwo() => InEnglish(() =>
    {
        SessionRow witcher = Row(_witcherModules, "{\"nvngx_dlss.dll\":\"game\",\"nvngx_dlssg.dll\":\"graphics_driver\"}", upscaler: "dlss", fgMode: "dlssg", factor: 1.98);

        DlssText.Line(witcher).Should().Be("DLSS · Super Resolution 310.9.1, from the game · Ray Reconstruction 310.9.1 · Frame Generation 310.9.1, from the graphics driver");
        DlssText.Label(witcher).Should().BeNull("310.x is DLSS 4's and 4.5's alike, and ×2 is any generation's");
        DlssText.ExportLine(witcher).Should().Be("nvngx_dlss.dll=310,9,1,0 game; nvngx_dlssd.dll=310,9,1,0; nvngx_dlssg.dll=310,9,1,0 graphics_driver");
        return 0;
    });

    [Fact]
    public void SilentHill2sTimesSixStateIsDlss45() => InEnglish(() =>
    {
        // As stored: no DLSS library under a census name, the hook named nothing, x6.0 counted for DLSS-G.
        SessionRow silentHill2 = Row("{\"sl.interposer.dll\":\"2,4,0,0\"}", upscaler: "unknown", fgMode: "dlssg", factor: 6.0023,
            words: "{\"Outcome\":\"Answered\",\"Sr\":\"0x8007F\",\"Mode\":3,\"Preset\":12,\"Readings\":3,\"Answered\":3}");

        DlssText.Label(silentHill2).Should().Be("DLSS 4.5 (a ×6.0 frame-generation state)");
        DlssText.Line(silentHill2).Should().Be("DLSS 4.5 (a ×6.0 frame-generation state)", "preset L was applied to a super resolution the driver never saw run");
        DlssText.ExportLine(silentHill2).Should().Be("generation=DLSS 4.5 basis=frame_generation_factor");
        return 0;
    });

    [Fact]
    public void ThePresetTheDriverReportsAppliedNamesTheModelAndTheGeneration() => InEnglish(() =>
    {
        const string presetL = "{\"Outcome\":\"Answered\",\"Sr\":\"0x8067F\",\"Mode\":2,\"Preset\":12,\"Readings\":3,\"Answered\":3}";
        SessionRow overridden = Row("{\"nvngx_dlss.dll\":\"310.5.0.0\"}", "{\"nvngx_dlss.dll\":\"graphics_driver\"}", upscaler: "unknown", driverReported: "dlss", words: presetL);

        DlssText.Line(overridden).Should().Be("DLSS 4.5 (preset L) · Super Resolution 310.5.0, from the graphics driver, preset L (2nd-gen transformer model)");
        DlssText.Label(overridden).Should().Be("DLSS 4.5 (preset L)");
        DlssText.ExportLine(overridden).Should().Be("generation=DLSS 4.5 basis=preset; nvngx_dlss.dll=310.5.0.0 graphics_driver; preset=L");

        SessionRow noLibrary = Row(upscaler: "unknown", driverReported: "dlss", words: presetL.Replace("\"Preset\":12", "\"Preset\":11", StringComparison.Ordinal));
        DlssText.Line(noLibrary).Should().Be("DLSS 4 (preset K) · preset K (transformer model)", "the preset stands even where no library was recorded");
        return 0;
    });

    [Fact]
    public void OlderLibrariesNameTheirGenerationAndDlss5IsOnlyLoaded() => InEnglish(() =>
    {
        DlssText.Label(Row("{\"nvngx_dlss.dll\":\"3.7.10.0\"}", upscaler: "dlss")).Should().Be("DLSS 3 (library 3.7.10)");
        DlssText.Label(Row("{\"nvngx_dlss.dll\":\"2.5.1.0\"}", upscaler: "dlss")).Should().Be("DLSS 2 (library 2.5.1)");
        DlssText.Line(Row("{\"nvngx_dlss.dll\":\"310.9.1.0\",\"nvngx_dlssnr.dll\":\"1.0.0.0\"}", upscaler: "dlss"))
            .Should().Be("DLSS · Super Resolution 310.9.1 · DLSS 5 runtime loaded");
        DlssText.Line(Row("{\"sl.interposer.dll\":\"2,8,0,0\"}", upscaler: "fsr3")).Should().BeNull("no DLSS at all");
        DlssText.Line(Row("{broken", upscaler: "dlss")).Should().Be("DLSS", "an unreadable module list still says DLSS ran");
        DlssText.ExportLine(Row()).Should().Be("N/A");
        DlssText.Parse("310,9,1,0").Should().Be(new Version(310, 9, 1, 0));
        DlssText.Parse("3.7").Should().Be(new Version(3, 7));
        DlssText.Parse("1.0.3").Should().Be(new Version(1, 0, 3));
        DlssText.Parse("v3").Should().BeNull();
        DlssText.Parse(null).Should().BeNull();
        return 0;
    });

    [Fact]
    public void TheLargestStateOfTheMixIsTheEvidence() => InEnglish(() =>
    {
        string mix = Application.Recording.FgMixRecord.ToJson(new Domain.Metrics.FgMix(
        [
            new Domain.Metrics.FgMixRow(Domain.Metrics.FgMixKind.Generated, 2.0, 60, 120, 60, 0.5, 12),
            new Domain.Metrics.FgMixRow(Domain.Metrics.FgMixKind.Generated, 4.0, 50, 200, 60, 0.5, 12),
        ]))!;
        DlssText.Label(Row(upscaler: "dlss", fgMode: "dlssg", factor: 2.0, mix: mix)).Should().Be("DLSS 4 (a ×4.0 frame-generation state)",
            "the steady state was ×2, and the session also held ×4");
        DlssText.Label(Row(upscaler: "dlss", fgMode: "fsrfg", factor: 4.0)).Should().BeNull("only DLSS Frame Generation names a DLSS generation");
        return 0;
    });

    /// <summary>
    /// beta.26: the owner's Silent Hill 2 session (2026-10-19) stored a ×1336.0 "state" — ten seconds at one token
    /// per window — and the line read "DLSS 4.5 (a ×1336.0 frame-generation state)". Read by today's rule, the evidence is
    /// the ×6.1 state the session really held.
    /// </summary>
    [Fact]
    public void AStoredStateNoFrameGenerationProducesIsNotTheEvidence() => InEnglish(() =>
    {
        const string mix = "{\"Rows\":[" +
            "{\"Kind\":\"generated\",\"Factor\":2.999266324284666,\"NativeFps\":95.75,\"DisplayedFps\":287.18,\"Seconds\":42.7,\"Share\":0.5,\"Windows\":9}," +
            "{\"Kind\":\"generated\",\"Factor\":6.057471264367816,\"NativeFps\":23.2,\"DisplayedFps\":140.53,\"Seconds\":15,\"Share\":0.3,\"Windows\":3}," +
            "{\"Kind\":\"generated\",\"Factor\":1336,\"NativeFps\":0.2,\"DisplayedFps\":267.2,\"Seconds\":10,\"Share\":0.2,\"Windows\":2}]}";
        SessionRow silentHill2 = Row("{\"sl.interposer.dll\":\"2,4,0,0\"}", upscaler: "unknown", fgMode: "dlssg", factor: 2.999266324284666, mix: mix);

        DlssText.Label(silentHill2).Should().Be("DLSS 4.5 (a ×6.1 frame-generation state)");
        return 0;
    });
}
