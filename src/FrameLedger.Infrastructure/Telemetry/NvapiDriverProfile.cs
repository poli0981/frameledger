using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvDriverProfileRead</c> (<c>fl_nvapi_bridge.h</c>, ABI 2, beta.8): the profile the NVIDIA driver applies to
/// one executable and the values it gives the settings asked for — read through DRS, never written.
/// <c>NvapiBridgeMirrorTests</c> holds its size to the bridge's own <c>FlNvDriverProfileSize</c>.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiNgxWords and FrameLedger.Shared's ring mirrors make.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct NvapiDriverProfile
{
    /// <summary><c>FL_NV_PROFILE_APPLICATION</c>: an application profile names this executable.</summary>
    public const int Application = 0;

    /// <summary><c>FL_NV_PROFILE_GLOBAL</c>: none does; the values are the current global profile's.</summary>
    public const int Global = 1;

    /// <summary><c>FL_NV_PROFILE_DEGRADED</c>: no usable driver, or DRS refused (<see cref="NvapiStatus"/> says which).</summary>
    public const int Degraded = 2;

    /// <summary><c>FL_NV_PROFILE_MAX_SETTINGS</c>.</summary>
    public const int MaxSettings = 24;

    public uint Size;

    public int Status;

    public int NvapiStatus;

    public uint Count;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string ProfileName;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxSettings)]
    public NvapiProfileSetting[] Settings;
}
