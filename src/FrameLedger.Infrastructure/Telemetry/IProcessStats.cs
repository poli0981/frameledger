// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <c>FrameLedger.ProcessStats.dll</c>'s four calls (beta.12, D43), behind a seam so <see cref="GameMemoryReader"/> can be
/// tested without the DLL. <see cref="NativeProcessStats"/> is the real one.
/// </summary>
public interface IProcessStats
{
    /// <summary>A reader (the PDH query); <see cref="IntPtr.Zero"/> when the DLL is absent or memory is exhausted.</summary>
    IntPtr Open();

    /// <summary>One counter collection; 0, or the PDH status that refused it.</summary>
    int Collect(IntPtr reader);

    /// <summary>
    /// <c>FlPsRead</c>: the video half from the latest collection, the system half through <paramref name="process"/> when
    /// given (a handle the caller holds), else through a handle the DLL opens with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>
    /// and closes. <paramref name="creationTime"/> is a FILETIME (0 = do not check).
    /// </summary>
    int Read(IntPtr reader, uint pid, ulong creationTime, SafeHandle? process, ref ProcessStatsSample sample);

    void Close(IntPtr reader);
}
