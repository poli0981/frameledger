// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Infrastructure.Capture;

namespace FrameLedger.Infrastructure.Tests.Capture;

/// <summary>
/// D64 (beta.24), on this test's own process: every module under the NVIDIA override folder is recorded whatever its name
/// — on the owner's Silent Hill 2 session none of the override's libraries carried a census name — and every recorded
/// module carries its folder class.
/// </summary>
public sealed class RuntimeModuleSnapshotSourceTests
{
    [Fact]
    public void AModuleUnderTheOverrideFolderIsRecordedWhateverItsNameAndEachCarriesItsClass()
    {
        string programData = Path.Combine(Path.GetTempPath(), "fl-ngx-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(programData, "NVIDIA", "NGX", "models", "dlss");
        Directory.CreateDirectory(folder);
        string copy = Path.Combine(folder, "fl_override_probe.dll");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "FrameLedger.ProcessStats.dll"), copy);
        IntPtr handle = NativeLibrary.Load(copy);
        try
        {
            RuntimeModuleSet set = RuntimeModuleSnapshot.Take(Environment.ProcessId, ["kernel32.dll"], Environment.GetFolderPath(Environment.SpecialFolder.Windows), programData);

            set.Unreadable.Should().Be(0);
            set.Find("fl_override_probe.dll")!.Source.Should().Be(RuntimeModuleSource.GraphicsDriver, "a name no list carries, caught by its folder");
            set.Find("kernel32.dll")!.Source.Should().Be(RuntimeModuleSource.Windows);
            set.Find("FrameLedger.ProcessStats.dll").Should().BeNull("outside the override folder, a name no list carries is not recorded");

            RuntimeModuleSet noFolders = RuntimeModuleSnapshot.Take(Environment.ProcessId, ["kernel32.dll"], string.Empty, " ");
            noFolders.Unreadable.Should().Be(0, "a folder Windows did not name is not a failure");
            noFolders.Find("kernel32.dll")!.Source.Should().Be(RuntimeModuleSource.Unknown);
            noFolders.Find("fl_override_probe.dll").Should().BeNull("nothing is caught by a folder nobody named");
        }
        finally
        {
            NativeLibrary.Free(handle);
            try
            {
                Directory.Delete(programData, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A mapped image can outlive the free for a moment; the temporary folder is the system's to clear.
            }
        }
    }
}
