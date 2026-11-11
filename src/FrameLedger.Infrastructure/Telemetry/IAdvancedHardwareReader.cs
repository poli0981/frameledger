// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Advanced;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// What <see cref="AdvancedHardwareHub"/> runs on its thread: open the sources, read one tick, close. A seam so the
/// hub's holding and letting go is tested without a sensor in the room; <see cref="AdvancedHardwareReader"/> is the real one.
/// </summary>
public interface IAdvancedHardwareReader : IDisposable
{
    /// <summary>Opens the sources. Never throws for a source that is not there: its state says so.</summary>
    void Open();

    /// <summary>Reads one tick of every source that is open.</summary>
    AdvancedSnapshot Tick();

    /// <summary>The facts of one session, read now. Safe to call from another thread than <see cref="Tick"/>'s.</summary>
    IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target);
}
