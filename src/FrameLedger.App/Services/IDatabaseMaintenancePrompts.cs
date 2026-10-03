// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Tools ▸ Database maintenance's two windows (P4 PR-7): the dialog itself, and the one confirmation a destructive action asks (<c>08_UI</c> §UX rules).</summary>
public interface IDatabaseMaintenancePrompts
{
    /// <summary>The maintenance dialog, until the user closes it.</summary>
    Task ShowAsync(CancellationToken ct = default);

    /// <summary>"Remove the raw series of all but the newest <paramref name="keep"/> sessions per game?" — true to go ahead.</summary>
    Task<bool> ConfirmSweepAsync(int keep, CancellationToken ct = default);
}
