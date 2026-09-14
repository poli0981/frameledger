namespace FrameLedger.App.Update;

/// <summary>The manual check's dialogs (<c>11_UPDATER</c> §Flow, "Manual check"), behind an interface so the flow is tested without a window.</summary>
public interface IUpdatePrompts
{
    /// <summary>The release found, its notes, the unsigned-release footer; true when the user chose to download it.</summary>
    Task<bool> OfferAsync(UpdateCandidate candidate, CancellationToken ct = default);

    Task UpToDateAsync(string version, CancellationToken ct = default);

    /// <summary>A copy the installer did not lay down cannot update itself.</summary>
    Task NotInstalledAsync(CancellationToken ct = default);

    /// <summary>The mapped dialog for the row, with its extra action where the table names one.</summary>
    Task FailedAsync(UpdateFailure failure, CancellationToken ct = default);
}
