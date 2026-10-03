// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.App.Services;

/// <summary><see cref="IRunAtLogon"/> over <c>Infrastructure.Startup.RunAtLogon</c> for this executable.</summary>
public sealed class RunAtLogonRegistry : IRunAtLogon
{
    private readonly RunAtLogon _run = new();

    private static string Executable => Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "FrameLedger.exe");

    public bool IsSet => _run.IsSet(Executable);

    public void Apply(bool enabled)
    {
        if (enabled)
        {
            _run.Set(Executable);
        }
        else
        {
            _run.Clear();
        }
    }
}
