// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Update;

namespace FrameLedger.App.Tests.Update;

/// <summary>
/// D47 (beta.14): the update channel follows the running copy unless the user chose one. Every release so far is a GitHub
/// pre-release, and the old default — stable — found none of them.
/// </summary>
public sealed class UpdateChannelPolicyTests
{
    [Theory]
    [InlineData("0.1.0-beta.14", true)]
    [InlineData("0.1.0-beta.9", true)]
    [InlineData("1.0.0-rc.1+abc123", true)]
    [InlineData("0.1.0", false)]
    [InlineData("1.2.3+build-7", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void APrereleaseHasADashBeforeAnyBuildMetadata(string? version, bool prerelease) =>
        UpdateChannelPolicy.IsPrerelease(version).Should().Be(prerelease);

    [Theory]
    [InlineData("auto", "0.1.0-beta.14", true)]
    [InlineData("auto", "0.1.0", false)]
    [InlineData("stable", "0.1.0-beta.14", false)]
    [InlineData("beta", "0.1.0", true)]
    [InlineData("auto", null, false)]
    public void AutomaticFollowsTheCopyAndAChoiceWins(string channel, string? running, bool prereleases) =>
        UpdateChannelPolicy.IncludesPrereleases(channel, running).Should().Be(prereleases);
}
