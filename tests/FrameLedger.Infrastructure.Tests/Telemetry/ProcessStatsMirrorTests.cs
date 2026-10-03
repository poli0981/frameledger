// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The managed mirror of <c>FlPsSample</c> against <c>FrameLedger.ProcessStats.dll</c>'s own size and ABI version (beta.12,
/// D43), and one real read of this test's own process through the real DLL. Fails rather than skips when the DLL is not
/// staged: a mirror test that quietly does nothing is a gate that cannot fail.
/// </summary>
public sealed class ProcessStatsMirrorTests
{
    [Fact]
    public void TheMirrorIsTheSizeTheDllAnswersAndTheAbiVersionIsTheOneWeWroteAgainst()
    {
        NativeProcessStats.IsPresent.Should().BeTrue("FrameLedger.ProcessStats.dll must be staged beside the test binary (FrameLedger.ProcessStats.targets)");
        NativeProcessStats.LoadedAbiVersion.Should().Be(NativeProcessStats.AbiVersion);
        ((uint)Marshal.SizeOf<ProcessStatsSample>()).Should().Be(NativeProcessStats.LoadedSampleSize);
        Marshal.SizeOf<ProcessStatsSample>().Should().Be(88);
        NativeProcessStats.LoadedBuildId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ThisProcessesOwnMemoryReadsThroughTheRealDllWithTheLeastRight()
    {
        using var reader = new GameMemoryReader(new NativeProcessStats());
        using var self = Process.GetCurrentProcess();
        reader.Follow(GameProcess.Unpinned([new GameProcessId(self.Id, new DateTimeOffset(self.StartTime))]));

        reader.TryRead(out ProcessReading r).Should().BeTrue("a process may always read its own counters");

        r.Processes.Should().Be(1);
        r.RamWorkingSetMb.Should().BePositive();
        r.CommitMb.Should().BePositive();
        if (r.RamPrivateMb is { } privateWs)
        {
            privateWs.Should().BePositive().And.BeLessThanOrEqualTo(r.RamWorkingSetMb!.Value);
        }

        // The video half depends on the machine: a test host that made no GPU allocation has no counter instance (N/A).
        r.Sources.Should().HaveFlag(ProcessReadingSources.TransientHandle);
    }

    [Fact]
    public void AReusedPidIsRefusedRatherThanReadOffAStranger()
    {
        using var reader = new GameMemoryReader(new NativeProcessStats());
        using var self = Process.GetCurrentProcess();
        reader.Follow(GameProcess.Unpinned([new GameProcessId(self.Id, new DateTimeOffset(self.StartTime).AddSeconds(-5))]));

        reader.TryRead(out ProcessReading r).Should().BeFalse("the creation time is not this process's");
        r.IsEmpty.Should().BeTrue();
    }
}
