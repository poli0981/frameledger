// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Tests.Capture;

internal sealed class DelegateLivenessSource(Func<int, ITargetLiveness?> pin) : ITargetLivenessSource
{
    public ITargetLiveness? TryPin(int pid) => pin(pid);
}
