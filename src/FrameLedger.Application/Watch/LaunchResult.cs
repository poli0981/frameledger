// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Watch;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LaunchResult(LaunchOutcome Outcome, Guid? SessionGuid)
{
    public static LaunchResult Of(LaunchOutcome outcome) => new(outcome, null);
}
