// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.App.Services;

/// <summary>What the Settings page shows for the layer and the logon task, read fresh each time.</summary>
public sealed record MaintenanceSnapshot(bool LayerStaged, bool LayerRegistered, LogonTaskState Task);
