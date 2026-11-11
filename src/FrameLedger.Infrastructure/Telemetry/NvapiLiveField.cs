// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>The <c>FL_NV_LIVE_*</c> bits of <see cref="NvapiLive.Present"/> (<c>fl_nvapi_bridge.h</c>).</summary>
[Flags]
public enum NvapiLiveField
{
    None = 0,
    Memory = 0x001,
    LoadFramebuffer = 0x002,
    LoadVideo = 0x004,
    LoadBus = 0x008,
    ClockVideo = 0x010,
    Pstate = 0x020,
    TempBoard = 0x040,
    TempPowerSupply = 0x080,
    Throttle = 0x100,
    Encoder = 0x200,
}
