// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>The <c>FL_NV_FIELD_*</c> bits of <c>FlNvSample.present</c>: a clear bit is N/A, never 0.</summary>
[Flags]
public enum NvapiField
{
    None = 0,
    TempCore = 0x001,
    TempMemory = 0x002,
    Load = 0x004,
    Vram = 0x008,
    CoreClock = 0x010,
    MemClock = 0x020,
    Fan = 0x040,
    Throttle = 0x080,
    PcieWidth = 0x100,
    Name = 0x200,
}
