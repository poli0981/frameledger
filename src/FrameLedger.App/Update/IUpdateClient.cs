namespace FrameLedger.App.Update;

/// <summary>
/// The updater's port (<c>11_UPDATER</c>): what the flow needs from Velopack and nothing it does not, so the flow is
/// tested without a feed. <see cref="VelopackUpdateClient"/> is the one implementation; every failure it reports
/// is an <see cref="UpdateException"/> carrying the dialog's row.
/// </summary>
public interface IUpdateClient
{
    /// <summary>True for a copy the installer laid down; a build run from <c>bin/</c> cannot update itself and says so.</summary>
    bool IsInstalled { get; }

    /// <summary>The installed version as the package manifest states it, or null when not installed.</summary>
    string? CurrentVersion { get; }

    /// <summary>One feed request; null when the installed version is current.</summary>
    /// <exception cref="UpdateException">The feed could not be read.</exception>
    Task<UpdateCandidate?> CheckAsync(bool includePrereleases, CancellationToken ct = default);

    /// <summary>The package into Velopack's staging directory, verified; <paramref name="progress"/> in percent.</summary>
    /// <exception cref="UpdateException">The download failed or the checksum did not match.</exception>
    Task DownloadAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken ct = default);

    /// <summary>Hand the downloaded package to the updater, which waits for this process to exit, applies, and restarts the App.</summary>
    void ApplyOnExit(UpdateCandidate candidate);
}
