namespace FrameLedger.App.Services;

/// <summary>The layer registration and the logon task as this user's machine holds them (registry, Task Scheduler), behind a seam.</summary>
public interface IMaintenanceState
{
    Task<MaintenanceSnapshot> ReadAsync(CancellationToken ct = default);
}
