using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>Mirror of <c>FlNvProfileSetting</c> (<c>fl_nvapi_bridge.h</c>, ABI 2): one DRS setting as the driver answered it.</summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiNgxWords and FrameLedger.Shared's ring mirrors make.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential)]
public struct NvapiProfileSetting
{
    /// <summary><c>NVAPI_SETTING_NOT_FOUND</c>: the setting is set in no profile, so the driver applies its own behaviour.</summary>
    public const int SettingNotFound = -160;

    public uint Id;

    /// <summary><c>NvAPI_DRS_GetSetting</c>'s status, verbatim.</summary>
    public int Status;

    public uint Value;

    /// <summary><c>NVDRS_SETTING_LOCATION</c>: 0 the profile itself, 1 the global profile, 2 base, 3 the driver default.</summary>
    public uint Location;

    /// <summary>Nonzero when the value is the driver's shipped one; 0 when someone (the NVIDIA App, the user) set it.</summary>
    public uint Predefined;

    /// <summary>1 when the setting is a DWORD and <see cref="Value"/> holds it.</summary>
    public uint Dword;
}
