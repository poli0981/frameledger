// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.TriState;

/// <summary>A tri-state value and where it came from.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ResolvedTriState(Tri Value, TriStateSource Source)
{
    public static ResolvedTriState NotApplicable => new(Tri.NotApplicable, TriStateSource.NotApplicable);
}
