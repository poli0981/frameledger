// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// What one session's facts are asked about: the game's process, its executable, and the monitor it presents on.
/// </summary>
/// <param name="ProcessId">The game's process id — for the colour space the driver reports for that process.</param>
/// <param name="ExecutablePath">The game's executable, fully qualified — for the driver profile that names it; null when not known.</param>
/// <param name="GdiDeviceName">The GDI device name of the monitor the game's window is on (<c>\\.\DISPLAY1</c>); null when not known.</param>
public sealed record AdvancedSessionTarget(uint ProcessId, string? ExecutablePath, string? GdiDeviceName);
