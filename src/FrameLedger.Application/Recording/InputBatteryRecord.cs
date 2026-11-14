// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Input;

namespace FrameLedger.Application.Recording;

/// <summary>A controller's battery over the session, in <see cref="InputDeviceRecord"/>.</summary>
public sealed record InputBatteryRecord
{
    /// <summary><c>levels</c> (0 empty to 3 full), <c>percent</c>, or <c>none</c> for a wired pad.</summary>
    public string Scale { get; init; } = InputNames.None;

    /// <summary>Every state it was read in, the longest first.</summary>
    public IReadOnlyList<InputBatteryStateRecord> States { get; init; } = [];

    /// <summary>The first level read, in <see cref="Scale"/>; null when no reading had a level.</summary>
    public int? First { get; init; }

    /// <summary>The last level read.</summary>
    public int? Last { get; init; }

    /// <summary>How fast it ran down, in per cent an hour, when that could be worked out (<c>03_METRICS</c> §Input).</summary>
    public double? DrainPercentPerHour { get; init; }
}
