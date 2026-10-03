// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>Mirror of <c>FlApi</c>: the presentation API a sample came through.</summary>
public enum FrameApi
{
    Unknown = 0,
    D3D11 = 1,
    D3D12 = 2,
    Vulkan = 3,
    OpenGL = 4,
}
