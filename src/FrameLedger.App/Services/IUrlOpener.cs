// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Opens a URL in the user's browser (P4 PR-3) — a port so the shell's Help commands are testable without launching anything.</summary>
public interface IUrlOpener
{
    /// <summary>True when the shell accepted the launch; false when it refused (logged by the adapter).</summary>
    bool Open(Uri url);
}
