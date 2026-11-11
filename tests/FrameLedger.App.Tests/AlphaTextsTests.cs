// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// D97 and D98 (2026-11-11): an alpha says "a prerelease .NET" only where its runtime is one, and says that its line is
/// paused where it was cut while the line rested. Each combination, by the two facts <see cref="AlphaTexts"/> is handed.
/// </summary>
public sealed class AlphaTextsTests
{
    private const string _release = ".NET 11.0.0";
    private const string _candidate = ".NET 11.0.0-rc.3.26522.148";

    private static string Of(string format, string runtime) => string.Format(CultureInfo.CurrentCulture, format, runtime);

    [Fact]
    public void AnAlphaOnAReleasedRuntimeWhoseLineStillTriesItNamesTheRuntimeAndNoPrerelease() =>
        AlphaTexts.BannerBody(rests: false, _release).Should().Be(Of(Strings.Alpha_Banner_Body_Format, _release));

    [Fact]
    public void AnAlphaCutWhileTheLineRestsSaysItIsPausedAndToInstallTheBeta() =>
        AlphaTexts.BannerBody(rests: true, _release).Should().Be(Of(Strings.Alpha_Banner_Body_Resting_Format, _release));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APrereleaseRuntimeIsSaidWhateverTheStampSays(bool rests) =>
        AlphaTexts.BannerBody(rests, _candidate).Should().Be(Of(Strings.Alpha_Banner_Body_Prerelease_Format, _candidate),
            "a line that tries a prerelease is not resting, and the runtime is what the process sees for itself");

    [Fact]
    public void TheThreeBannersAreThreeTexts() =>
        new[] { Strings.Alpha_Banner_Body_Format, Strings.Alpha_Banner_Body_Prerelease_Format, Strings.Alpha_Banner_Body_Resting_Format }
            .Should().OnlyHaveUniqueItems().And.OnlyContain(static f => f.Contains("{0}", StringComparison.Ordinal));

    [Fact]
    public void AboutNamesTheRuntimeForABeta() =>
        AlphaTexts.AboutRuntime(alpha: false, _release).Should().Be(Of(Strings.About_Runtime_Format, _release));

    [Fact]
    public void AboutSaysAlphaAndPrereleaseOnlyWhereTheRuntimeIsOne()
    {
        AlphaTexts.AboutRuntime(alpha: true, _release).Should().Be(Of(Strings.About_Runtime_Alpha_Format, _release));
        AlphaTexts.AboutRuntime(alpha: true, _candidate).Should().Be(Of(Strings.About_Runtime_Alpha_Prerelease_Format, _candidate));
        AlphaTexts.AboutRuntime(alpha: false, _candidate).Should().Be(Of(Strings.About_Runtime_Format, _candidate), "a beta never runs on one, and says nothing more if it did");
    }
}
