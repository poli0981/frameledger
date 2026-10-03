// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>Set by Velopack's restarted hook in <c>Program.Main</c>, read once by <see cref="UpdateHostedService"/>: this process is the first run after an update.</summary>
internal static class UpdateRestart
{
    public static bool Detected { get; set; }
}
