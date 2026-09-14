namespace FrameLedger.App.Services;

/// <summary>The shell window as the tray and the notices see it: whether the user can see it, and the two things the tray asks of it.</summary>
public interface IShellPresence
{
    /// <summary>The live window is visible and not minimized — the in-app channels reach the user.</summary>
    bool IsShown { get; }

    /// <summary>Show the window (from the tray, or after a hide) and bring it to the front.</summary>
    void Reveal();

    /// <summary>End the application — a real exit, whatever the minimize-to-tray policy says.</summary>
    void Quit();
}
