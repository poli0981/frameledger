// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// Reads the executable FILE on disk for vendor SDK strings (<c>HANDOFF</c> 7b) — after a
/// session, so the read never overlaps the launch. Every failure is an
/// <see cref="ExecutableMarkers.Error"/>, never a throw.
/// </summary>
public interface IExecutableMarkerScan
{
    ExecutableMarkers Scan(string exePath);
}
