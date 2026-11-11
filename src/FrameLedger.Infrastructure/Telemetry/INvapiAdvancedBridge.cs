// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// The bridge's ABI 3 reads (owner decision D101) as <see cref="NvapiAdvancedReader"/> sees them, so it is tested on a
/// machine with no NVIDIA driver. A port of its own beside <see cref="INvapiBridge"/>: the telemetry layer and the NGX
/// probe neither need these reads nor change because they exist. <see cref="NativeNvapiBridge"/> is the real one.
/// </summary>
/// <remarks>
/// Every method answers a status: 0 with the struct filled, <c>FL_NV_NOT_INITIALISED</c>, <c>FL_NV_NO_GPU</c>, or
/// <see cref="NativeNvapiBridge.Unavailable"/> when the DLL beside the Agent predates ABI 3. Every call is a read
/// (<c>18_GPU_VENDOR_APIS</c> §Runtime policy; the gate <c>tools/nvapi-readonly-check.ps1</c>, D108).
/// </remarks>
public interface INvapiAdvancedBridge
{
    /// <summary><c>FlNvReadFacts</c>: what is true of the first graphics card whatever is running.</summary>
    int ReadFacts(out NvapiFacts facts);

    /// <summary><c>FlNvReadLive</c>: what it says now.</summary>
    int ReadLive(out NvapiLive live);

    /// <summary><c>FlNvReadDisplays</c>: the displays connected to it and what each can do.</summary>
    int ReadDisplays(out NvapiDisplays displays);

    /// <summary><c>FlNvSessionDisplay</c>: the display <paramref name="gdiDeviceName"/> names, and the colour space of <paramref name="pid"/> on it.</summary>
    int SessionDisplay(string gdiDeviceName, uint pid, out NvapiSessionDisplay display);

    /// <summary>
    /// <c>FlNvProfileSettings</c>: every setting of the current global profile (<paramref name="exePath"/> null), or
    /// of the profile that names an executable — with <paramref name="ownOnly"/>, only what that profile sets itself.
    /// </summary>
    int ProfileSettings(string? exePath, bool ownOnly, out NvapiProfileList list, out IReadOnlyList<NvapiProfileEntry> entries);
}
