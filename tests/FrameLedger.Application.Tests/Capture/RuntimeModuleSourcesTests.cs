// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Tests.Capture;

/// <summary>D64 (beta.24): a loaded module's folder class — the class is stored, the path never is.</summary>
public sealed class RuntimeModuleSourcesTests
{
    private const string _windows = @"C:\Windows";
    private const string _programData = @"C:\ProgramData";

    [Theory]
    [InlineData(@"C:\Windows\System32\DriverStore\FileRepository\nv_dispi.inf_amd64_1\nvngx_dlssg.dll", RuntimeModuleSource.GraphicsDriver)]
    [InlineData(@"C:\ProgramData\NVIDIA\NGX\models\dlss\versions\20316673\files\160_e658700.bin", RuntimeModuleSource.GraphicsDriver)]
    [InlineData(@"c:/programdata/nvidia/ngx/models/x.dll", RuntimeModuleSource.GraphicsDriver)]
    [InlineData(@"C:\Windows\System32\_nvngx.dll", RuntimeModuleSource.Windows)]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\Engine\Plugins\nvngx_dlss.dll", RuntimeModuleSource.Game)]
    [InlineData(@"C:\WindowsApps\Game\nvngx_dlss.dll", RuntimeModuleSource.Game)]
    [InlineData(@"C:\ProgramData\NVIDIAx\nvngx_dlss.dll", RuntimeModuleSource.Game)]
    public void AModulesFolderIsItsClass(string path, RuntimeModuleSource source) =>
        RuntimeModuleSources.Classify(path, _windows, _programData).Should().Be(source);

    [Fact]
    public void ThePathlessAndTheTokensAreHandled()
    {
        RuntimeModuleSources.Classify(null, _windows, _programData).Should().Be(RuntimeModuleSource.Unknown);
        RuntimeModuleSources.Classify(_windows, _windows, _programData).Should().Be(RuntimeModuleSource.Game, "the folder itself is not under itself");
        FluentActions.Invoking(static () => RuntimeModuleSources.Classify("x", " ", _programData)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(static () => RuntimeModuleSources.IsNgxOverrideFolder("x", "")).Should().Throw<ArgumentException>();
        RuntimeModuleSources.IsNgxOverrideFolder(@"C:\ProgramData\NVIDIA\NGX\models\x.bin", _programData).Should().BeTrue();
        RuntimeModuleSources.IsNgxOverrideFolder(@"C:\ProgramData\NVIDIA\Other\x.dll", _programData).Should().BeFalse();
        RuntimeModuleSources.IsNgxOverrideFolder(null, _programData).Should().BeFalse();
        foreach (RuntimeModuleSource source in new[] { RuntimeModuleSource.Game, RuntimeModuleSource.GraphicsDriver, RuntimeModuleSource.Windows })
        {
            RuntimeModuleSources.FromToken(RuntimeModuleSources.Token(source)).Should().Be(source);
        }

        RuntimeModuleSources.Token(RuntimeModuleSource.Unknown).Should().BeNull();
        RuntimeModuleSources.FromToken("cloud").Should().Be(RuntimeModuleSource.Unknown);
        CensusNames.ModuleFileNames.Should().Contain(["nvngx_dlssnr.dll", "sl.dlss_d.dll", "sl.common.dll", "sl.reflex.dll", "amd_fidelityfx_loader_dx12.dll"],
            "the snapshot records them though the census has no bit");
    }
}
