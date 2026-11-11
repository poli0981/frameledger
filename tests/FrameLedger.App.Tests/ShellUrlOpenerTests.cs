// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// The shell runs what it is handed, so the opener hands it web and mail links only - and decides that itself, whoever
/// calls it (2026-11-11, from Qodana's rule on every place that starts a process). Only the refusals are exercised:
/// accepting one would open the tester's browser.
/// </summary>
public sealed class ShellUrlOpenerTests
{
    [Theory]
    [InlineData("https://github.com/poli0981/frameledger", true)]
    [InlineData("http://example.org/", true)]
    [InlineData("mailto:contact@example.org", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData(@"C:\Windows\System32\calc.exe", false)]
    [InlineData("steam://rungameid/1", false)]
    [InlineData("ms-settings:display", false)]
    [InlineData("javascript:alert(1)", false)]
    public void OnlyWebAndMailLinksMayReachTheShell(string address, bool openable) =>
        ShellUrlOpener.IsOpenable(new Uri(address)).Should().Be(openable);

    [Fact]
    public void ARelativeAddressIsNotOpenable() =>
        ShellUrlOpener.IsOpenable(new Uri("docs/08_UI.md", UriKind.Relative)).Should().BeFalse();

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:display")]
    public void OpenRefusesWithoutStartingAnything(string address) =>
        new ShellUrlOpener().Open(new Uri(address)).Should().BeFalse();
}
