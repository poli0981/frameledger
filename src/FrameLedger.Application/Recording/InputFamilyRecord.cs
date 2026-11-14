// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>One maker's part of the time controllers were in use, in <see cref="InputDevicesRecord"/>.</summary>
/// <param name="Family">The maker, in <c>InputNames</c>' words.</param>
/// <param name="ActiveMs">The time its controllers were in use while the game was in front.</param>
/// <param name="Share">That time over the same time of every maker, 0 to 1.</param>
public sealed record InputFamilyRecord(string Family, long ActiveMs, double Share);
