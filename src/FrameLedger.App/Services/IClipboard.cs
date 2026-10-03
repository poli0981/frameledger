// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The clipboard (P4 PR-3, the bug report's Markdown fallback) — a port so the flow is testable without a desktop.</summary>
public interface IClipboard
{
    /// <summary>True when the text was placed; false when the clipboard refused (another process holding it), logged by the adapter.</summary>
    bool SetText(string text);
}
