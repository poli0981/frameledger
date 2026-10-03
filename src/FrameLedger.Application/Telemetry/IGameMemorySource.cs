// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// The game process's own memory, read from outside it (beta.12, owner decision D43): the telemetry thread's third source
/// beside the GPU layers and the machine. <c>Infrastructure.Telemetry.GameMemoryReader</c> is the real one, over
/// <c>FrameLedger.ProcessStats.dll</c>.
/// </summary>
/// <remarks>
/// <see cref="Follow"/> is called from the capture loop's task and <see cref="TryRead"/> from the telemetry thread: the
/// target is a published reference, never a shared mutable list. A reader that cannot answer — the DLL absent, the
/// counters missing, the process refused — returns false or an empty reading; it never throws into the poller.
/// </remarks>
public interface IGameMemorySource : IDisposable
{
    /// <summary>The process to read from now on (<see cref="GameProcess.None"/> to stop).</summary>
    void Follow(GameProcess target);

    /// <summary>One reading of the followed process; false when there is nothing to follow or nothing answered.</summary>
    bool TryRead(out ProcessReading reading);
}
