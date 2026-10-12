// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests;

/// <summary>
/// The Ren'Py version (beta.20): the owner's one Ren'Py install as measured on 2026-10-12, the build number dropped as Ren'Py
/// names its releases, and nothing guessed from a version line that is not one.
/// </summary>
public sealed class RenPyVersionTests
{
    [Fact]
    public void TheMeasuredInstallReadsAsTheReleaseItWasBuiltWith() =>
        RenPyVersion.Decide(new RenPyBuildFacts { VcVersion = "8.5.0.25111603" })
            .Should().Be(new EngineVersionReading("8.5.0", EngineVersionSource.RenPyVcVersion), "Bound Between Desks: Ren'Py 8.5.0");

    [Theory]
    [InlineData("8.5.0.25111603", "8.5.0")]
    [InlineData("8.3.7.25031702", "8.3.7")]
    [InlineData("7.4.11.2266", "7.4.11")]
    [InlineData("8.5.0", "8.5.0")]
    [InlineData(" 8.5.0.25111603 ", "8.5.0")]
    [InlineData("08.05.00.1", "8.5.0")]
    [InlineData("8.5", null)]
    [InlineData("8.5.0+nightly", null)]
    [InlineData("In Good Health", null)]
    [InlineData("0.5.0.1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TheVersionLineGivesMajorMinorPatchOrNothing(string? vcVersion, string? expected)
    {
        EngineVersionReading r = RenPyVersion.Decide(new RenPyBuildFacts { VcVersion = vcVersion });

        r.Version.Should().Be(expected);
        r.Source.Should().Be(expected is null ? null : EngineVersionSource.RenPyVcVersion);
    }
}
