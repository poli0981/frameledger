// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Rules;

/// <summary>What the store's write attempt did.</summary>
public enum RulesWriteOutcome
{
    /// <summary>The file is now the content we asked for.</summary>
    Written = 0,

    /// <summary>
    /// A file appeared between the look and the write, and we were told not to
    /// replace it. Not an error — somebody else seeded it.
    /// </summary>
    AlreadyExists,

    /// <summary>Could not write. The caller reports it rather than retrying blindly.</summary>
    Failed,
}
