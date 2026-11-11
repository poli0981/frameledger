// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>The <c>FL_NV_FACT_*</c> bits of <see cref="NvapiFacts.Present"/> (<c>fl_nvapi_bridge.h</c>).</summary>
[Flags]
public enum NvapiFactField
{
    None = 0,
    GpuType = 0x00000001,
    SystemType = 0x00000002,
    GpuInfo = 0x00000004,
    BusType = 0x00000008,
    PciIds = 0x00000010,
    Vbios = 0x00000020,
    Arch = 0x00000040,
    CoreCount = 0x00000080,
    RamBusWidth = 0x00000100,
    ClockBaseCore = 0x00000200,
    ClockBaseMemory = 0x00000400,
    ClockBoostCore = 0x00000800,
    ClockBoostMemory = 0x00001000,
    TempDefaultMax = 0x00002000,
    Memory = 0x00004000,
    FbPhysical = 0x00008000,
    FbVirtual = 0x00010000,
    DriverInfo = 0x00020000,
    InterfaceVersion = 0x00040000,
    Chipset = 0x00080000,
}
