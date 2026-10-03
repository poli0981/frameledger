// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>The seam under <see cref="SystemTelemetrySource"/>: the two Win32 reads, so the arithmetic is testable without a machine.</summary>
public interface ISystemCounters
{
    bool TryReadTimes(out SystemTimes times);

    bool TryReadMemoryInUseBytes(out ulong inUse);
}
