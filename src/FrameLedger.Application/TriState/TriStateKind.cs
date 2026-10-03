// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.TriState;

/// <summary>The three tri-state features of FR-8 (<c>03_METRICS</c> §RT / PT / RR).</summary>
public enum TriStateKind
{
    RayTracing,
    PathTracing,
    RayReconstruction,
}
