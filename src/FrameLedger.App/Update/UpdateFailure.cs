namespace FrameLedger.App.Update;

/// <summary><c>11_UPDATER</c> §Error mapping, one member per row; the dialog text is the matching <c>Update_Err_*</c> string.</summary>
public enum UpdateFailure
{
    /// <summary>HTTP 404: no release feed where the client looked.</summary>
    NotFound = 0,

    /// <summary>HTTP 403 / 429: GitHub's anonymous quota.</summary>
    RateLimited,

    /// <summary>HTTP 5xx.</summary>
    Server,

    /// <summary>Timeout, DNS, no network — silent on the startup check.</summary>
    Offline,

    /// <summary>The downloaded package failed its checksum.</summary>
    Corrupt,

    /// <summary>Anything else; the exception is logged.</summary>
    Unknown,
}
