// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Watch;

/// <summary>Tells why an entry's executable did not read (beta.19, D55; <see cref="ExecutablePresence"/>).</summary>
public interface IExecutablePresence
{
    /// <summary>What the file system says about <paramref name="exePath"/> — read only, never a process.</summary>
    ExecutablePresence Probe(string exePath);
}
