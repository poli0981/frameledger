using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests;

/// <summary>
/// What an executable runs as (beta.8, <c>games.exe_machine</c>): the COFF machine, and for .NET the CLR flags; and which
/// ids are known before a launch to be processes the x64 hook cannot enter — never an id this build could not establish.
/// </summary>
public sealed class ExecutableArchitectureTests
{
    [Theory]
    [InlineData(ExecutableArchitecture.MachineAmd64, null, ExecutableArchitecture.X64)]
    [InlineData(ExecutableArchitecture.MachineI386, null, ExecutableArchitecture.X86)]
    [InlineData(ExecutableArchitecture.MachineArm64, null, ExecutableArchitecture.Arm64)]
    [InlineData(ExecutableArchitecture.MachineArmNt, null, ExecutableArchitecture.Arm)]
    [InlineData((ushort)0x0200, null, ExecutableArchitecture.Other)]
    [InlineData(ExecutableArchitecture.MachineI386, ExecutableArchitecture.ClrIlOnly, ExecutableArchitecture.AnyCpu)]
    [InlineData(ExecutableArchitecture.MachineI386, ExecutableArchitecture.ClrIlOnly | ExecutableArchitecture.Clr32BitPreferred, ExecutableArchitecture.AnyCpu32)]
    [InlineData(ExecutableArchitecture.MachineI386, ExecutableArchitecture.ClrIlOnly | ExecutableArchitecture.Clr32BitRequired, ExecutableArchitecture.X86)]
    [InlineData(ExecutableArchitecture.MachineI386, 0u, ExecutableArchitecture.X86)]
    [InlineData(ExecutableArchitecture.MachineAmd64, ExecutableArchitecture.ClrIlOnly, ExecutableArchitecture.X64)]
    public void TheIdIsTheMachineAndForDotNetTheClrFlags(ushort machine, uint? clrFlags, string expected) =>
        ExecutableArchitecture.Of(machine, clrFlags).Should().Be(expected);

    [Theory]
    [InlineData(ExecutableArchitecture.X86, true)]
    [InlineData(ExecutableArchitecture.AnyCpu32, true)]
    [InlineData(ExecutableArchitecture.Arm64, true)]
    [InlineData(ExecutableArchitecture.Arm, true)]
    [InlineData(ExecutableArchitecture.X64, false)]
    [InlineData(ExecutableArchitecture.AnyCpu, false)]
    [InlineData(ExecutableArchitecture.Other, false)]
    [InlineData(ExecutableArchitecture.Unknown, false)]
    [InlineData(null, false)]
    public void OnlyAnEstablishedNonX64IdIsKnownNotHookable(string? id, bool expected) =>
        ExecutableArchitecture.IsKnownNotHookable(id).Should().Be(expected, "an id this build could not establish is the guard's to decide at launch");

    [Theory]
    [InlineData("nvngx_dlss.dll", "nvngx_dlss.dll", true)]
    [InlineData("nvngx_dlss.dll", "Engine/Plugins/DLSS/Binaries/nvngx_dlss.dll", true)]
    [InlineData("ffx_fsr3*.dll", "bin/FFX_FSR3UPSCALER_X64.DLL", true)]
    [InlineData("libxess.dll", "libxess_dx11.dll", false)]
    [InlineData("ffx_fsr2_*.dll", "ffx_fsr3_x64.dll", false)]
    public void AFileGlobNamesTheWholePathOrTheFileName(string pattern, string file, bool expected) =>
        RuleEvaluator.NamesFile(pattern, file).Should().Be(expected);

    [Fact]
    public void ALibraryFilesNameIsItsLastSegment()
    {
        new LibraryFile("dlss", "Engine/Plugins/nvngx_dlss.dll", "3.7.10.0", null).FileName.Should().Be("nvngx_dlss.dll");
        new LibraryFile("xess", "libxess.dll", null, null).FileName.Should().Be("libxess.dll");
    }
}
