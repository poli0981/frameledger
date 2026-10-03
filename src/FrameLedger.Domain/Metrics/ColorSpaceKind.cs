// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>Mirror of <c>FlColorSpace</c>. Was a bool, which had no third state.</summary>
public enum ColorSpaceKind
{
    NotReported = 0,
    Sdr = 1,
    Hdr10 = 2,
    ScRgb = 3,
}
