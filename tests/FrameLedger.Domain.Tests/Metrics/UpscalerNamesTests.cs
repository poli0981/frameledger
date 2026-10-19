// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>Three numberings, one table each (beta.24, D63): the hooked byte, the driver's report and the App's profile value.</summary>
public sealed class UpscalerNamesTests
{
    [Theory]
    [InlineData("1", UpscalerQualityMode.Performance)]
    [InlineData("2", UpscalerQualityMode.Balanced)]
    [InlineData("3", UpscalerQualityMode.Quality)]
    [InlineData("4", UpscalerQualityMode.UltraPerformance)]
    [InlineData("5", UpscalerQualityMode.UltraQuality)]
    [InlineData("6", UpscalerQualityMode.Dlaa)]
    public void AHookedDlssByteIsTheModeStreamlineNamesIt(string stored, UpscalerQualityMode mode) =>
        UpscalerNames.Mode("dlss", stored).Should().Be(mode, "sl::DLSSMode, as fl_sl_inputs.h QualityFromMode writes it");

    [Theory]
    [InlineData("dlss", "7")]
    [InlineData("dlss", "0")]
    [InlineData("fsr3", "2")]
    [InlineData("xess", "3")]
    [InlineData(null, "2")]
    [InlineData("dlss", null)]
    [InlineData("dlss", "")]
    [InlineData("dlss", " ")]
    public void AValueNoTableNamesIsNoMode(string? upscaler, string? stored) =>
        UpscalerNames.Mode(upscaler, stored).Should().BeNull("the App prints a stored number nobody can name as it is");

    [Theory]
    [InlineData(0u, UpscalerQualityMode.Performance)]
    [InlineData(1u, UpscalerQualityMode.Balanced)]
    [InlineData(2u, UpscalerQualityMode.Quality)]
    [InlineData(3u, UpscalerQualityMode.UltraPerformance)]
    [InlineData(4u, UpscalerQualityMode.UltraQuality)]
    [InlineData(5u, UpscalerQualityMode.Dlaa)]
    public void TheDriversReportIsInNgxsOwnNumbering(uint reported, UpscalerQualityMode mode) =>
        UpscalerNames.FromDriverReport(reported).Should().Be(mode, "Silent Hill 2 under the App's Ultra Performance (profile 5) was reported as 3");

    [Theory]
    [InlineData(0u, UpscalerQualityMode.Performance)]
    [InlineData(1u, UpscalerQualityMode.Balanced)]
    [InlineData(2u, UpscalerQualityMode.Quality)]
    [InlineData(4u, UpscalerQualityMode.Dlaa)]
    [InlineData(5u, UpscalerQualityMode.UltraPerformance)]
    [InlineData(6u, UpscalerQualityMode.Custom)]
    public void TheProfileValueIsInNvidiasSettingsNumbering(uint stored, UpscalerQualityMode mode) =>
        UpscalerNames.FromProfile(stored).Should().Be(mode, "NGX_DLSS_SR_MODE, NvApiDriverSettings.h");

    [Fact]
    public void ValuesNeitherNumberingNamesAreNoMode()
    {
        UpscalerNames.FromDriverReport(6).Should().BeNull();
        UpscalerNames.FromDriverReport(null).Should().BeNull();
        UpscalerNames.FromProfile(3).Should().BeNull("3 is the game's own: no override");
        UpscalerNames.FromProfile(7).Should().BeNull("reserved");
        UpscalerNames.FromProfile(null).Should().BeNull();
    }
}
