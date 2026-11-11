// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// The hardware groups of LibreHardwareMonitor this project may open (owner decision D103) — and, by what is not a
/// member, the ones it may not.
/// </summary>
/// <remarks>
/// <b>Not here, on purpose:</b> the power-supply and controller groups, which make the library search every USB HID
/// device (refused); the battery group (deferred); the network group, which is not a hardware measurement; and
/// <b>storage</b> — the library lists every drive through DiskInfoToolkit with no filter and follows drives as they
/// are plugged in (its <c>StorageGroup</c>, read 2026-11-11), so it cannot be opened without touching removable
/// drives, and by the outcome written down before the first run that group stays closed (<c>20_OPEN_QUESTIONS</c> §M13).
/// </remarks>
[Flags]
public enum LhmGroups
{
    None = 0,

    /// <summary>The graphics cards. Needs no rights (<c>20_OPEN_QUESTIONS</c> §M5).</summary>
    Gpu = 1,

    /// <summary>The processor. Elevated, with PawnIO.</summary>
    Cpu = 2,

    /// <summary>System memory and its modules. Elevated, with PawnIO.</summary>
    Memory = 4,

    /// <summary>The motherboard: its sensor chip and embedded controller. Elevated, with PawnIO.</summary>
    Motherboard = 8,
}
