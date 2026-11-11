// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvProfileEntry</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): one setting of a driver profile, as the
/// read-only enumeration lists it.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct NvapiProfileEntry
{
    /// <summary><c>NVDRS_DWORD_TYPE</c>: the only type whose <see cref="Value"/> is the setting's value.</summary>
    public const uint DwordType = 0;

    public uint Id;

    /// <summary><c>NVDRS_SETTING_TYPE</c>: 0 DWORD, 1 binary, 2 string, 3 wide string, 4 QWORD.</summary>
    public uint Type;

    public uint Value;

    /// <summary><c>NVDRS_SETTING_LOCATION</c>: 0 this profile, 1 the global profile, 2 base, 3 the default.</summary>
    public uint Location;

    /// <summary>Nonzero: the driver's shipped value. Zero: a value someone set.</summary>
    public uint Predefined;

    public uint Reserved;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;
}
