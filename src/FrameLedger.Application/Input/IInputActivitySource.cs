// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using FrameLedger.Application.Telemetry;

namespace FrameLedger.Application.Input;

/// <summary>
/// One session's view of <see cref="IInputDevices"/>: the slices in which a window of the session's own game was in
/// front, added up between two reads. Several sessions can run at once; each has its own.
/// </summary>
public interface IInputActivitySource : IDisposable
{
    /// <summary>The processes whose windows count as the session's game. Called from the session's loop.</summary>
    void Follow(GameProcess target);

    /// <summary>What was seen since the last read; false when the reader took no slice since then. Called from the telemetry thread, and never throws into it.</summary>
    bool TryRead([NotNullWhen(true)] out InputReading? reading);
}
