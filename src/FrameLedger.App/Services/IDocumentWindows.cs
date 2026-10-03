// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The Help menu's document windows (beta.12), as a port so the shell's commands are testable without a window.</summary>
public interface IDocumentWindows
{
    /// <summary>Help ▸ Limitations: <c>LIMITATIONS.md</c>, rendered.</summary>
    void ShowLimitations();

    /// <summary>Help ▸ About: the legal notices, the third-party licences and the legal documents.</summary>
    void ShowAbout();
}
