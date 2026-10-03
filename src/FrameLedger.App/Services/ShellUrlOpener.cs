// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics;

namespace FrameLedger.App.Services;

/// <summary><see cref="IUrlOpener"/> through the shell (<c>UseShellExecute</c>), the same launch every other link in the App makes; a refusal is logged, never thrown.</summary>
public sealed class ShellUrlOpener : IUrlOpener
{
    public bool Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: could not open {Url}", url);
            return false;
        }
    }
}
