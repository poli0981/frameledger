// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// One driver setting as the profile gives it: its value (null when it is set in no profile, so the driver behaves as it
/// ships), where that value comes from, and whether it is the driver's shipped value or one someone set.
/// </summary>
public sealed record DriverSettingReading(uint Id, uint? Value, DriverSettingLocation Location, bool Predefined);
