// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>One battery state and the time it was read, in <see cref="InputBatteryRecord"/>.</summary>
/// <param name="State">What the battery was doing, in <c>InputNames</c>' words.</param>
/// <param name="Level">The level, in the battery's scale.</param>
/// <param name="Ms">The time it was read in this state.</param>
public sealed record InputBatteryStateRecord(string State, int Level, long Ms);
