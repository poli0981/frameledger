// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>A viewer's (D52): nothing on this PC is started.</summary>
public sealed class ViewerShellStarter : IShellStarter
{
    public bool OpenLink(string link) => false;

    public bool StartProgram(string path, string arguments, string? workingDirectory) => false;
}
