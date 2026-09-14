namespace FrameLedger.App.Services;

/// <summary>08_UI §Shell, the tray's four states: idle / ● capturing (Tier 1) / ◐ recording only (Tier 2) / ⏸ paused.</summary>
public enum TrayState
{
    Idle = 0,
    Capturing,
    RecordingOnly,
    Paused,
}
