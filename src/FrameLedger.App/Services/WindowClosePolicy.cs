namespace FrameLedger.App.Services;

/// <summary>
/// FR-10 "minimize to tray": what closing the shell window does. Loaded from <c>ui.minimize_to_tray</c> at start,
/// updated by the Settings page as the toggle moves, and honoured only while a tray icon exists to come back from.
/// </summary>
public sealed class WindowClosePolicy
{
    public bool MinimizeToTray { get; set; }

    public bool TrayAvailable { get; set; }

    public bool HidesOnClose => MinimizeToTray && TrayAvailable;
}
