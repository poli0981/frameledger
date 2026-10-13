// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>The shell's own hand-off. The App runs with the user's standard rights; an elevated App never reaches here (<see cref="GamePlayer"/>).</summary>
public sealed class ShellStarter : IShellStarter
{
    public bool OpenLink(string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Log.Warning("play: Windows did not open {Link} ({Message})", link, ex.Message);
            return false;
        }
    }

    public bool StartProgram(string path, string arguments, string? workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(path, arguments ?? string.Empty)
            {
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? string.Empty,
            });
            return started is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Log.Warning("play: {Program} did not start ({Message})", path, ex.Message);
            return false;
        }
    }
}
