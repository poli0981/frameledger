namespace FrameLedger.App.Services;

/// <summary>Which safety event a notice carries (<c>08_UI</c> §Notifications policy: these are never toasts).</summary>
public enum SafetyNoticeKind
{
    /// <summary><c>CaptureRefused</c>: the gate or the guard said no before anything was injected.</summary>
    Refused,

    /// <summary><c>SafetyUnhook</c>: anti-cheat appeared mid-session and the hook left.</summary>
    Unhooked,

    /// <summary><c>CaptureDegraded</c>: measurement stopped mid-session for another reason.</summary>
    Degraded,
}
