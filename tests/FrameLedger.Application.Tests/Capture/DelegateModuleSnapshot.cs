// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Tests.Capture;

internal sealed class DelegateModuleSnapshot(Func<int, RuntimeModuleSet> take) : IRuntimeModuleSnapshot
{
    public RuntimeModuleSet Take(int pid) => take(pid);
}
