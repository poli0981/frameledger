// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>FR-6.3: a hardware-snapshot difference between two consecutive sessions, as a marker on the trend line ("GPU driver 572.16 → 576.02").</summary>
public sealed record HardwareChange(DateTimeOffset At, string Text);
