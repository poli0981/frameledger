// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Input;

namespace FrameLedger.Domain.Tests.Input;

/// <summary>The libraries through which a Windows game reads controllers, by file name (owner decision D163).</summary>
public sealed class InputLibrariesTests
{
    [Theory]
    [InlineData("xinput1_4.dll", "XInput")]
    [InlineData("XINPUT1_3.DLL", "XInput")]
    [InlineData("xinput9_1_0.dll", "XInput")]
    [InlineData("xinputuap.dll", "XInput")]
    [InlineData("dinput8.dll", "DirectInput")]
    [InlineData("DINPUT.dll", "DirectInput")]
    [InlineData("Windows.Gaming.Input.dll", "Windows.Gaming.Input")]
    [InlineData("GameInput.dll", "GameInput")]
    [InlineData("SDL2.dll", "SDL")]
    [InlineData("SDL3.dll", "SDL")]
    public void AnInputLibraryIsNamedByTheInterfaceItIs(string file, string api)
    {
        InputLibraries.ApiOf(file).Should().Be(api);
        InputLibraries.Is(file).Should().BeTrue();
    }

    [Theory]
    [InlineData("nvngx_dlss.dll")]
    [InlineData("xinput.dll")]
    [InlineData("hid.dll")] // loaded by half of Windows: it says nothing of controllers
    [InlineData("steam_api64.dll")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsNotOne(string? file)
    {
        InputLibraries.ApiOf(file).Should().BeNull();
        InputLibraries.Is(file).Should().BeFalse();
    }

    [Fact]
    public void TheNamesASnapshotLooksForAreLowerCaseAndEachIsAnInputLibrary()
    {
        InputLibraries.FileNames.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        InputLibraries.FileNames.Should().OnlyContain(static f => InputLibraries.Is(f) && !f.Any(char.IsUpper));
    }
}
