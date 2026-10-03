// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// The NVIDIA driver's per-process NGX word (<c>NvAPI_NGX_GetNGXOverrideState</c>), read out of
/// process beside each module snapshot. Every way it can fail is an outcome on the state, never a throw.
/// </summary>
public interface INgxDriverProbe
{
    NgxDriverState Run(int pid);
}
