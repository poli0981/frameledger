// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>One battery state a controller held during a session, and for how long.</summary>
/// <param name="State">What the battery was doing.</param>
/// <param name="Scale">What <paramref name="Level"/> counts in.</param>
/// <param name="Level">The level, in <paramref name="Scale"/>.</param>
/// <param name="Ms">The time it was read in this state, in milliseconds.</param>
public sealed record BatteryStateTime(BatteryState State, BatteryScale Scale, int Level, long Ms);
