// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Telemetry;
using Microsoft.Win32.SafeHandles;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// <see cref="GameMemoryReader"/> over a scripted <see cref="IProcessStats"/> (beta.12, D43): what it may open, what it
/// sums, and what it refuses to read — the rules <c>03_METRICS</c> §Game process memory states.
/// </summary>
public sealed class GameMemoryReaderTests
{
    private const double _mib = 1024.0 * 1024.0;

    private sealed class ScriptedStats : IProcessStats
    {
        public Dictionary<uint, ProcessStatsSample> Answers { get; } = [];

        public List<(uint Pid, ulong Created, bool Held)> Reads { get; } = [];

        public int Opens { get; private set; }

        public int Collects { get; private set; }

        public bool OpenFails { get; set; }

        public IntPtr Open()
        {
            Opens++;
            return OpenFails ? IntPtr.Zero : new IntPtr(1);
        }

        public int Collect(IntPtr reader)
        {
            Collects++;
            return 0;
        }

        public int Read(IntPtr reader, uint pid, ulong creationTime, SafeHandle? process, ref ProcessStatsSample sample)
        {
            ObjectDisposedException.ThrowIf(process is { IsClosed: true }, process!);
            Reads.Add((pid, creationTime, process is not null));
            sample = Answers.TryGetValue(pid, out ProcessStatsSample a) ? a : new ProcessStatsSample { RamStatus = 1 };
            return 0;
        }

        public void Close(IntPtr reader)
        {
        }
    }

    private sealed class HeldPin(SafeHandle handle) : ITargetLiveness, IHeldProcessHandle
    {
        public SafeHandle ProcessHandle => handle;

        public bool HasExited => false;

        public int? ExitCode => null;

        public bool IsForeground => true;

        public void Dispose() => handle.Dispose();
    }

    private static ProcessStatsSample Full(double dedicatedMib, double privateMib) => new()
    {
        Present = 0x01 | 0x02 | 0x04 | 0x08 | 0x10,
        GpuDedicatedBytes = (ulong)(dedicatedMib * _mib),
        GpuSharedBytes = (ulong)(64 * _mib),
        PrivateWorkingSetBytes = (ulong)(privateMib * _mib),
        WorkingSetBytes = (ulong)((privateMib + 100) * _mib),
        CommitBytes = (ulong)((privateMib + 200) * _mib),
        GpuInstances = 1,
    };

    [Fact]
    public void NothingFollowedReadsNothingAndOpensNothing()
    {
        var stats = new ScriptedStats();
        using var reader = new GameMemoryReader(stats);

        reader.TryRead(out ProcessReading reading).Should().BeFalse();
        reading.IsEmpty.Should().BeTrue();
        stats.Opens.Should().Be(0, "no session has a process yet: the counter query is not even opened");
    }

    [Fact]
    public void APinnedGameIsReadThroughTheHandleTheSessionHolds()
    {
        var stats = new ScriptedStats();
        stats.Answers[4242] = Full(9216, 6000);
        using var reader = new GameMemoryReader(stats);
        using var handle = new SafeProcessHandle(new IntPtr(-1), ownsHandle: false);
        using var pin = new HeldPin(handle);

        reader.Follow(GameProcess.Pinned(4242, pin));
        reader.TryRead(out ProcessReading r).Should().BeTrue();

        stats.Reads.Should().ContainSingle().Which.Should().Be((4242u, 0ul, true), "the pin's own handle; no creation-time check is needed while it is held");
        r.VramDedicatedMb.Should().BeApproximately(9216, 0.001);
        r.VramSharedMb.Should().BeApproximately(64, 0.001);
        r.RamPrivateMb.Should().BeApproximately(6000, 0.001);
        r.RamWorkingSetMb.Should().BeApproximately(6100, 0.001);
        r.CommitMb.Should().BeApproximately(6200, 0.001);
        r.Processes.Should().Be(1);
        r.Sources.Should().Be(ProcessReadingSources.Counters | ProcessReadingSources.PrivateWorkingSetRead | ProcessReadingSources.HeldHandle);
        stats.Collects.Should().Be(1, "one counter collection per tick");
    }

    [Fact]
    public void AnUnpinnedHoldSumsItsVerifiedProcessesAndLeavesOutAReusedOrRefusedPid()
    {
        var stats = new ScriptedStats();
        stats.Answers[100] = Full(1000, 500);
        stats.Answers[200] = Full(2000, 700);
        stats.Answers[300] = Full(4000, 900) with { RamStatus = 2 };    // FL_PS_RAM_PID_REUSED: another process now
        // 400 has no answer: the scripted DLL says OPEN_DENIED.
        using var reader = new GameMemoryReader(stats);
        DateTimeOffset t = DateTimeOffset.FromFileTime(133_000_000_000_000_000);

        reader.Follow(GameProcess.Unpinned([new(100, t), new(200, t.AddSeconds(1)), new(300, t), new(400, t)]));
        reader.TryRead(out ProcessReading r).Should().BeTrue();

        stats.Reads.Select(static x => x.Held).Should().AllBeEquivalentTo(false, "a hold that never pinned the game opens each process for the read");
        stats.Reads.First(static x => x.Pid == 200).Created.Should().Be((ulong)t.AddSeconds(1).ToFileTime(), "the snapshot's creation time is what the DLL checks");
        r.Processes.Should().Be(2, "only the two processes whose identity was verified count");
        r.VramDedicatedMb.Should().BeApproximately(3000, 0.001, "1000 + 2000 — never the reused pid's 4000");
        r.RamPrivateMb.Should().BeApproximately(1200, 0.001);
        r.Sources.Should().HaveFlag(ProcessReadingSources.TransientHandle);
    }

    [Fact]
    public void AFieldNoProcessAnsweredStaysNullAndAnEmptyReadingIsNotAReading()
    {
        var stats = new ScriptedStats();
        stats.Answers[7] = new ProcessStatsSample { Present = 0x01 | 0x04, WorkingSetBytes = (ulong)(300 * _mib), CommitBytes = (ulong)(400 * _mib) };
        using var reader = new GameMemoryReader(stats);
        using var handle = new SafeProcessHandle(new IntPtr(-1), ownsHandle: false);
        using var pin = new HeldPin(handle);

        reader.Follow(GameProcess.Pinned(7, pin));
        reader.TryRead(out ProcessReading r).Should().BeTrue();

        r.VramDedicatedMb.Should().BeNull("no counter instance named the process: N/A, never 0");
        r.RamPrivateMb.Should().BeNull("Windows without PROCESS_MEMORY_COUNTERS_EX2: the private working set is N/A");
        r.RamWorkingSetMb.Should().BeApproximately(300, 0.001);
        r.Sources.Should().HaveFlag(ProcessReadingSources.WorkingSetReadOnly);

        stats.Answers[7] = default;
        reader.TryRead(out ProcessReading none).Should().BeFalse();
        none.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void APinReleasedBeforeTheReadIsNothingToSayAndNeverAThrow()
    {
        var stats = new ScriptedStats();
        stats.Answers[9] = Full(100, 100);
        using var reader = new GameMemoryReader(stats);
        using var handle = new SafeProcessHandle(new IntPtr(-1), ownsHandle: true);
        using (var pin = new HeldPin(handle))
        {
            reader.Follow(GameProcess.Pinned(9, pin));
        }

        reader.TryRead(out ProcessReading r).Should().BeFalse();
        r.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AReaderThatCouldNotOpenTriesAgainOnlyEveryFewTicksAndNoneIsFollowedAfterward()
    {
        var stats = new ScriptedStats { OpenFails = true };
        using var reader = new GameMemoryReader(stats);
        using var handle = new SafeProcessHandle(new IntPtr(-1), ownsHandle: false);
        using var pin = new HeldPin(handle);
        reader.Follow(GameProcess.Pinned(5, pin));

        for (int i = 0; i < GameMemoryReader.ReopenEvery; i++)
        {
            reader.TryRead(out _).Should().BeFalse();
        }

        stats.Opens.Should().Be(1, "the DLL is absent or out of memory: asking again every tick would only repeat the answer");
        stats.OpenFails = false;
        stats.Answers[5] = Full(10, 10);
        reader.TryRead(out ProcessReading r).Should().BeTrue("the back-off is over: the reader opens and reads");
        stats.Opens.Should().Be(2);
        r.VramDedicatedMb.Should().BeApproximately(10, 0.001);

        reader.Follow(GameProcess.None);
        reader.TryRead(out _).Should().BeFalse("the session let the process go");
    }
}
