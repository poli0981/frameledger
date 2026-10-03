// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// One out-of-process look at which census-named modules the target has loaded, with the
/// file version of each. Taken beside every guard scan; never throws — an unreadable target
/// is counted on the set, not thrown into the loop.
/// </summary>
public interface IRuntimeModuleSnapshot
{
    RuntimeModuleSet Take(int pid);
}
