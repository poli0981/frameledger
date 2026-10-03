// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The Logs page's level filter, over the <c>[HH:mm:ss.fff LVL]</c> prefix of <c>10_LOGGING</c>'s template.</summary>
public enum LogLevelFilter
{
    All,
    WarningAndAbove,
    ErrorAndAbove,
}
