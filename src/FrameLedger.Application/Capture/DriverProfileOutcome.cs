// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>Which branch a driver-profile read took (<c>FL_NV_PROFILE_*</c>).</summary>
public enum DriverProfileOutcome
{
    /// <summary>Nothing was asked.</summary>
    NotRead,

    /// <summary>An application profile names the executable; its values are what the driver gives it.</summary>
    Application,

    /// <summary>No profile names it: the current global profile's values are what it gets.</summary>
    Global,

    /// <summary>No usable NVIDIA driver, or its settings store refused.</summary>
    Degraded,
}
