namespace FrameLedger.App.Update;

/// <summary>Where the updater is, as the shell's banner shows it (<c>08_UI</c> §Notifications policy: "update downloaded" is a persistent InfoBar).</summary>
public enum UpdateStage
{
    /// <summary>Nothing pending: no check yet, the installed version is current, or a check failed.</summary>
    Idle = 0,

    Checking,

    /// <summary>A newer release was found; the download has not started (the manual check's offer is open).</summary>
    Available,

    Downloading,

    /// <summary>Downloaded and verified; "Restart to update" is live.</summary>
    Ready,

    /// <summary>Downloaded, but a session is running (FR-12): offered again when it ends.</summary>
    Deferred,

    /// <summary>The Agent has been asked to stop and the updater to wait for this process; the host is ending.</summary>
    Applying,
}
