namespace FrameLedger.App.Services;

/// <summary>
/// Whether a library entry's executable is on disk now (beta.11, owner request 2026-10-03: mark the games that were
/// uninstalled when the library is refreshed). Read by the App when it lists the library, never stored: the row is the
/// game, and a drive that comes back brings the game back with it. Nothing is removed for being missing.
/// </summary>
public enum ExecutablePresence
{
    /// <summary>The file is there.</summary>
    Present,

    /// <summary>The drive or folder is there and the file is not: uninstalled, or moved within the drive.</summary>
    Missing,

    /// <summary>The drive the path names is not connected (a USB drive unplugged, a letter that changed).</summary>
    DriveMissing,
}
