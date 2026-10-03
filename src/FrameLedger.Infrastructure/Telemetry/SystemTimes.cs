// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary><c>GetSystemTimes</c> in 100 ns units. Kernel INCLUDES idle, as the API reports it.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SystemTimes(ulong Idle, ulong Kernel, ulong User);
