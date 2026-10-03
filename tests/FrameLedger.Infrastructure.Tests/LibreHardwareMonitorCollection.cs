// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Tests;

/// <summary>
/// The test classes that open a LibreHardwareMonitor <c>Computer</c>, run one at a time. The library's CPU code is not safe
/// against a second <c>Computer</c> updating beside it: measured 2026-10-03, <c>GenericCpu.Update</c> threw a
/// NullReferenceException in the local gate while <see cref="Telemetry.LhmRealHardwareTests"/> ran in parallel with
/// <see cref="Startup.PrivilegeFootprintTests"/>, and passed five times out of five alone. The Agent's own reads are
/// already contained (<c>SystemTelemetrySource</c> catches a throwing CPU-temperature read: that tick's reading is N/A);
/// the test calls <c>Read</c> bare, so here the race is a red gate. An implicit collection: no fixture, so no definition
/// class is needed (the same shape as App.Tests' <c>StringsCultureCollection</c>).
/// </summary>
internal static class LibreHardwareMonitorCollection
{
    public const string Name = "LibreHardwareMonitor";
}
