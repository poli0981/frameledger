// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Recording;

/// <summary>What recovery did with one <c>.partial</c>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RecoveryOutcome(Guid SessionGuid, RecoveryStatus Status, long? SessionId, string Detail);
