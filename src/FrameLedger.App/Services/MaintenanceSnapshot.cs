using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.App.Services;

/// <summary>What the Settings page shows for the layer and the logon task, read fresh each time.</summary>
public sealed record MaintenanceSnapshot(bool LayerStaged, bool LayerRegistered, LogonTaskState Task);
