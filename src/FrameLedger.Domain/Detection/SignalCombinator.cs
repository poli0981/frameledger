// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>How the signals in a group combine.</summary>
public enum SignalCombinator
{
    /// <summary>Every signal must match.</summary>
    All,

    /// <summary>At least one signal must match.</summary>
    Any,
}
