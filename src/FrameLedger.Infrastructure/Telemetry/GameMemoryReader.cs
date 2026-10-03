// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Capture;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <see cref="IGameMemorySource"/> over <c>FrameLedger.ProcessStats.dll</c> (beta.12, owner decision D43): the game
/// process's dedicated and shared GPU memory from the <c>GPU Process Memory</c> counters, and its working sets and commit
/// from <c>GetProcessMemoryInfo</c> — read from outside the game, on the telemetry thread, once per tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it may open is decided upstream.</b> A pinned target (<see cref="GameProcess.Pinned"/>) is read through the
/// session's own handle (<see cref="IHeldProcessHandle"/>) — nothing new is opened. An unpinned Tier-2 hold's processes
/// are each opened for the read with <c>PROCESS_QUERY_LIMITED_INFORMATION</c> and closed after it, and a process whose
/// creation time is not the watcher's (a reused pid) is left out entirely — both halves, since the counters name a pid,
/// not a process. A process that refuses even that right is left out too: an unverifiable pid is not read at all.
/// </para>
/// <para>
/// <b>Sums.</b> The video half sums every counter instance naming the pid (each adapter, each physical index); an unpinned
/// hold sums its processes, and the reading says how many (<see cref="ProcessReading.Processes"/>).
/// </para>
/// <para>
/// Single-threaded by contract: <see cref="Follow"/> publishes a reference from the capture loop, and only the telemetry
/// thread calls <see cref="TryRead"/>, which owns the reader handle.
/// </para>
/// </remarks>
public sealed class GameMemoryReader : IGameMemorySource
{
    /// <summary>How many reads to wait before trying again to open the reader after a failure (the DLL absent, memory exhausted).</summary>
    public const int ReopenEvery = 30;

    private const double _mib = 1024.0 * 1024.0;

    // FL_PS_FIELD_* and FL_PS_RAM_* (fl_process_stats.h).
    private const uint _fieldWorkingSet = 0x01;
    private const uint _fieldPrivateWorkingSet = 0x02;
    private const uint _fieldCommit = 0x04;
    private const uint _fieldGpuDedicated = 0x08;
    private const uint _fieldGpuShared = 0x10;
    private const int _ramOk = 0;

    private readonly IProcessStats _stats;
    private GameProcess _target = GameProcess.None;
    private IntPtr _reader;
    private int _sinceFailedOpen;
    private bool _disposed;

    public GameMemoryReader(IProcessStats stats)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    public void Follow(GameProcess target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Volatile.Write(ref _target, target);
    }

    public bool TryRead(out ProcessReading reading)
    {
        reading = default;
        GameProcess target = Volatile.Read(ref _target);
        if (_disposed || target.IsNone || !EnsureReader())
        {
            return false;
        }

        // A failed collection costs this tick's video half; the system half is still read.
        _ = _stats.Collect(_reader);
        reading = target.Pid is int pid ? ReadPinned(pid, target.PinnedBy) : ReadUnpinned(target.Processes);
        return !reading.IsEmpty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stats.Close(_reader);
        _reader = IntPtr.Zero;
    }

    private bool EnsureReader()
    {
        if (_reader != IntPtr.Zero)
        {
            return true;
        }

        if (_sinceFailedOpen > 0 && _sinceFailedOpen++ < ReopenEvery)
        {
            return false;
        }

        _reader = _stats.Open();
        _sinceFailedOpen = _reader == IntPtr.Zero ? 1 : 0;
        return _reader != IntPtr.Zero;
    }

    private ProcessReading ReadPinned(int pid, Application.Capture.ITargetLiveness? pinnedBy)
    {
        var sample = default(ProcessStatsSample);
        ProcessReadingSources through;
        try
        {
            int status;
            if (pinnedBy is IHeldProcessHandle held)
            {
                status = _stats.Read(_reader, (uint)pid, 0, held.ProcessHandle, ref sample);
                through = ProcessReadingSources.HeldHandle;
            }
            else
            {
                status = _stats.Read(_reader, (uint)pid, 0, null, ref sample);
                through = ProcessReadingSources.TransientHandle;
            }

            if (status != 0)
            {
                return default;
            }
        }
        catch (ObjectDisposedException)
        {
            // The pin was released between the loop's Follow(None) and this read: nothing to say about a closed process.
            return default;
        }

        return ToReading(sample, through);
    }

    private ProcessReading ReadUnpinned(IReadOnlyList<GameProcessId> processes)
    {
        double? dedicated = null, shared = null, privateWs = null, workingSet = null, commit = null;
        ProcessReadingSources sources = ProcessReadingSources.None;
        int counted = 0;
        foreach (GameProcessId process in processes)
        {
            var sample = default(ProcessStatsSample);
            ulong created = (ulong)process.StartedAt.ToFileTime();
            if (_stats.Read(_reader, (uint)process.Pid, created, null, ref sample) != 0 || sample.RamStatus != _ramOk)
            {
                continue;    // reused, refused or gone: an unverifiable pid is read for neither half
            }

            ProcessReading one = ToReading(sample, ProcessReadingSources.TransientHandle);
            dedicated = Add(dedicated, one.VramDedicatedMb);
            shared = Add(shared, one.VramSharedMb);
            privateWs = Add(privateWs, one.RamPrivateMb);
            workingSet = Add(workingSet, one.RamWorkingSetMb);
            commit = Add(commit, one.CommitMb);
            sources |= one.Sources;
            counted++;
        }

        return counted == 0 ? default : new ProcessReading(dedicated, shared, privateWs, workingSet, commit, counted, sources);
    }

    private static ProcessReading ToReading(ProcessStatsSample s, ProcessReadingSources through)
    {
        uint present = s.Present;
        ProcessReadingSources sources = through;
        if (Has(present, _fieldGpuDedicated))
        {
            sources |= ProcessReadingSources.Counters;
        }

        if (Has(present, _fieldPrivateWorkingSet))
        {
            sources |= ProcessReadingSources.PrivateWorkingSetRead;
        }
        else if (Has(present, _fieldWorkingSet))
        {
            sources |= ProcessReadingSources.WorkingSetReadOnly;
        }

        return new ProcessReading(
            VramDedicatedMb: Has(present, _fieldGpuDedicated) ? s.GpuDedicatedBytes / _mib : null,
            VramSharedMb: Has(present, _fieldGpuShared) ? s.GpuSharedBytes / _mib : null,
            RamPrivateMb: Has(present, _fieldPrivateWorkingSet) ? s.PrivateWorkingSetBytes / _mib : null,
            RamWorkingSetMb: Has(present, _fieldWorkingSet) ? s.WorkingSetBytes / _mib : null,
            CommitMb: Has(present, _fieldCommit) ? s.CommitBytes / _mib : null,
            Processes: 1,
            Sources: sources);
    }

    private static bool Has(uint present, uint field) => (present & field) != 0;

    private static double? Add(double? sum, double? value) => value is null ? sum : (sum ?? 0) + value.Value;
}
