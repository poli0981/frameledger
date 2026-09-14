namespace FrameLedger.App.Update;

/// <summary>A newer release was found (<c>08_UI</c> §Notifications policy: "update available" is a tray toast when the window is off screen).</summary>
public sealed class UpdateAvailableEventArgs(string version) : EventArgs
{
    public string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
}
