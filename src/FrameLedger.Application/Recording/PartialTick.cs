// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Shared;

namespace FrameLedger.Application.Recording;

/// <summary>The drain's accounting at one flush, and the writer's state word; the last one written wins.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PartialTick(
    long DrainTicks,
    long ForegroundTicks,
    long TotalDropped,
    long TotalGaps,
    uint GuardTicksPublished,
    long WrittenAtUnixMs,
    FlWriterState WriterState);
