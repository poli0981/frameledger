// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The Run entry as a viewer sees it (beta.15, D52): not this window's to set. A viewer may be a test build, and a Run
/// value pointing at it would start a test build at every logon. The toggle is disabled; a call that reached here anyway
/// writes nothing.
/// </summary>
public sealed class ViewerRunAtLogon : IRunAtLogon
{
    public bool IsSet => false;

    public void Apply(bool enabled) => Log.Warning("viewer: the Run entry is not changed from a --data-dir window (asked: {Enabled})", enabled);
}
