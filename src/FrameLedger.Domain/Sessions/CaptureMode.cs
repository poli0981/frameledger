// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Sessions;

/// <summary><c>04_CAPTURE</c> §Launch mode vs attach mode.</summary>
public enum CaptureMode
{
    /// <summary>The watcher saw a running title and the guard ran against it.</summary>
    Attach = 0,

    /// <summary>FrameLedger started the title, the guard waited for a presentation runtime, then ran.</summary>
    Launch,
}
