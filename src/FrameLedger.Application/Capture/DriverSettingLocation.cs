namespace FrameLedger.Application.Capture;

/// <summary>Where a driver setting's value comes from (<c>NVDRS_SETTING_LOCATION</c>, plus "set nowhere").</summary>
public enum DriverSettingLocation
{
    /// <summary>Set in no profile (<c>NVAPI_SETTING_NOT_FOUND</c>): the driver's own behaviour applies.</summary>
    NotSet,

    /// <summary>The profile the driver applies to this executable.</summary>
    Profile,

    /// <summary>The global profile.</summary>
    Global,

    /// <summary>The base profile.</summary>
    Base,

    /// <summary>The driver's default.</summary>
    Default,
}
