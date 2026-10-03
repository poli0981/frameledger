// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Blobs;

/// <summary>One frame's two measured sizes.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RenderRes(ushort RenderW, ushort RenderH, ushort OutputW, ushort OutputH);
