// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// Explorer is started by its full path and handed a quoted path (beta.24 PR-2, SonarCloud <c>csharpsquid:S4036</c>):
/// never a name the search path resolves, never a path Explorer has to guess the end of. Nothing here starts it.
/// </summary>
public sealed class ExplorerTests
{
    [Fact]
    public void ItIsTheOneInTheWindowsFolder()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Explorer.ExecutablePath.Should().Be(Path.Combine(windows, "explorer.exe"));
        Path.IsPathRooted(Explorer.ExecutablePath).Should().BeTrue("a bare name would be resolved through PATH");
    }

    [Fact]
    public void AFolderIsQuotedWhateverItHoldsAndLosesItsTrailingSeparator()
    {
        Explorer.FolderArguments(@"C:\Users\Ann Lee\AppData\Local\FrameLedger\logs")
            .Should().Be("\"C:\\Users\\Ann Lee\\AppData\\Local\\FrameLedger\\logs\"");
        Explorer.FolderArguments(@"D:\Games\Dying Light, The Beast\")
            .Should().Be("\"D:\\Games\\Dying Light, The Beast\"", "a trailing \\ before the closing quote would escape it");
        Explorer.FolderArguments("\"C:\\already quoted\"").Should().Be("\"C:\\already quoted\"", "a quoted path is not quoted twice");
    }

    [Fact]
    public void AFileIsSelectedInItsFolder() =>
        Explorer.SelectArguments(@"C:\Users\Ann Lee\Desktop\frameledger-bug.zip")
            .Should().Be("/select,\"C:\\Users\\Ann Lee\\Desktop\\frameledger-bug.zip\"");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NoPathIsRefused(string path)
    {
        FluentActions.Invoking(() => Explorer.FolderArguments(path)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Explorer.SelectArguments(path)).Should().Throw<ArgumentException>();
    }
}
