// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The layer registration and the logon task as this user's machine holds them (registry, Task Scheduler), behind a seam.</summary>
public interface IMaintenanceState
{
    Task<MaintenanceSnapshot> ReadAsync(CancellationToken ct = default);
}
