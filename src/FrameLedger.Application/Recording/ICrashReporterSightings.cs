// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>
/// The engine crash reporters the watcher saw a session's game start (beta.14, <c>Watch.CrashReporterWitness</c>): the third
/// witness for <c>crashed</c>, beside the exit code and the Application log, for the engines that handle a crash themselves —
/// Unreal starts <c>CrashReportClient.exe</c> and exits with code 3, which says nothing and reaches no event log.
/// </summary>
public interface ICrashReporterSightings
{
    /// <summary>The first sighting the session's game started inside its window, or null; waits for a poll or two when the game just left.</summary>
    Task<CrashReporterSighting?> FindAsync(CrashQuery query, CancellationToken ct = default);
}
