namespace FrameLedger.Application.Recording;

/// <summary>One setting of <see cref="DriverProfileRecord"/>, as stored.</summary>
public sealed record DriverSettingRecord
{
    /// <summary>The setting id as hex text (<c>0x10E41E01</c>).</summary>
    public required string Id { get; init; }

    /// <summary>The DWORD value; null when the setting is set in no profile.</summary>
    public uint? Value { get; init; }

    /// <summary><c>Profile</c>, <c>Global</c>, <c>Base</c>, <c>Default</c> or <c>NotSet</c>.</summary>
    public required string Location { get; init; }

    /// <summary>True when the value is the driver's shipped one rather than one someone set.</summary>
    public bool Predefined { get; init; }
}
