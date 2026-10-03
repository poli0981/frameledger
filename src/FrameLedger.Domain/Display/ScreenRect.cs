// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Domain.Display;

/// <summary>A rectangle in screen coordinates, right and bottom exclusive (the Win32 <c>RECT</c> convention).</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);

    public bool IsEmpty => Width == 0 || Height == 0;
}
