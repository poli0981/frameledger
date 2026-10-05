// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;
using static FrameLedger.Domain.Tests.Metrics.SampleFixtures;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>beta.14 (D49): what the presents asked for — VSync and tearing — and nothing for presents that carried no arguments.</summary>
public sealed class PresentArgsSharesTests
{
    [Fact]
    public void TheSharesAreOfThePresentsThatCarriedTheirArguments()
    {
        List<FrameSample> stream =
        [
            Present(1) with { SyncInterval = 1 },
            Present(2) with { SyncInterval = 2 },
            Present(3) with { SyncInterval = 0, PresentFlags = PresentArgsShares.AllowTearing },
            Present(4) with { SyncInterval = 0, PresentFlags = PresentArgsShares.AllowTearing | 0x1 },
            // An OpenGL or Vulkan present: no arguments, so not counted at all.
            Present(5) with { Measured = MeasuredFields.OutputRes, SyncInterval = 0 },
        ];

        PresentArgsShares s = PresentArgsShares.From(stream);

        (s.Count, s.VsyncCount, s.TearingCount).Should().Be((4, 2, 2));
        s.VsyncPct.Should().Be(50);
        s.TearingAllowedPct.Should().Be(50);
    }

    [Fact]
    public void PresentsWithoutArgumentsAnswerNothing()
    {
        PresentArgsShares s = PresentArgsShares.From([.. Stream(10).Select(static p => p with { Measured = MeasuredFields.OutputRes })]);

        s.Count.Should().Be(0);
        s.VsyncPct.Should().BeNull("N/A, never a 0 % nobody measured");
        s.TearingAllowedPct.Should().BeNull();
    }

    [Theory]
    [InlineData(120.0, 300.0, 0.4)]
    [InlineData(60.0, 150.0, 0.4)]
    public void EfficiencyIsFramesPerSecondOverWatts(double fps, double watts, double perJoule) =>
        EnergyEfficiency.FramesPerJoule(fps, watts).Should().BeApproximately(perJoule, 1e-12);

    [Theory]
    [InlineData(null, 300.0)]
    [InlineData(120.0, null)]
    [InlineData(0.0, 300.0)]
    [InlineData(120.0, 0.0)]
    [InlineData(120.0, -1.0)]
    public void EfficiencyWithoutBothPositiveIsNull(double? fps, double? watts) =>
        EnergyEfficiency.FramesPerJoule(fps, watts).Should().BeNull();
}
