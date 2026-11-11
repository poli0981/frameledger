// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.App.Services;

/// <summary>One live reading of an advanced series: its value and when the Agent's reader read it.</summary>
/// <param name="At">The tick it was read on, by the Agent's clock.</param>
/// <param name="Value">The value, in the unit of its catalog entry.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct AdvancedLivePoint(DateTimeOffset At, double Value);
