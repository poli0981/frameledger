// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IUrlOpener"/> through the shell (<c>UseShellExecute</c>), the same launch every other link in the App makes; a
/// refusal is logged, never thrown. The shell runs whatever it is handed - a <c>file:</c> address is a program - so this
/// opens web and mail links only, and says so itself rather than trusting each caller to have checked (2026-11-11: every
/// caller passes one of the project's own pages or a link <c>MarkdownLinks</c> has already held to these schemes).
/// </summary>
public sealed class ShellUrlOpener : IUrlOpener
{
    /// <summary>Whether <paramref name="url"/> is a link the shell may be handed: absolute, and <c>http</c>, <c>https</c> or <c>mailto</c>.</summary>
    public static bool IsOpenable(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.IsAbsoluteUri && url.Scheme is "https" or "http" or "mailto";
    }

    public bool Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!IsOpenable(url))
        {
            Serilog.Log.Warning("ui: not opened - {Url} is neither a web nor a mail link", url);
            return false;
        }

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
