// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Capture;

internal sealed class DelegateRingAttacher(Func<int, (ICaptureSink? Sink, ShmAttachRefusal Refusal)> attach) : IRingAttacher
{
    public (ICaptureSink? Sink, ShmAttachRefusal Refusal) TryAttach(int pid) => attach(pid);
}
