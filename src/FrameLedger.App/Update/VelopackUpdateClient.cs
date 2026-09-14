using FrameLedger.App.Services;
using Velopack;
using Velopack.Sources;

namespace FrameLedger.App.Update;

/// <summary>
/// <see cref="IUpdateClient"/> over Velopack's <see cref="UpdateManager"/> and its GitHub source for this repository
/// (<c>11_UPDATER</c>): the stable channel is the releases GitHub does not mark pre-release, the beta channel adds the
/// ones it does (<c>13_CI_CD</c> §Branch &amp; release policy — <c>vX.Y.Z-beta.N</c>). One manager per check, because
/// the channel is a constructor argument there; the <see cref="UpdateInfo"/> a check returned is held for the
/// download and the apply that follow it.
/// </summary>
public sealed class VelopackUpdateClient : IUpdateClient
{
    /// <summary>The one feed (CLAUDE.md rule 8: the GitHub release check is a permitted call).</summary>
    public const string RepositoryUrl = "https://github.com/poli0981/frameledger";

    private readonly Lock _lock = new();
    private (UpdateManager Manager, UpdateInfo Info, string Version)? _held;

    public bool IsInstalled => Probe(static m => m.IsInstalled, false);

    public string? CurrentVersion => Probe(static m => m.CurrentVersion?.ToString(), null);

    public async Task<UpdateCandidate?> CheckAsync(bool includePrereleases, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        UpdateManager manager = Create(includePrereleases);
        try
        {
            UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                return null;
            }

            VelopackAsset target = info.TargetFullRelease;
            string version = target.Version.ToString();
            lock (_lock)
            {
                _held = (manager, info, version);
            }

            return new UpdateCandidate(version, target.NotesMarkdown, target.Size);
        }
        catch (Exception ex) when (IsFailure(ex, ct))
        {
            throw new UpdateException(UpdateFailureMapper.Map(ex), ex.Message, ex);
        }
    }

    public async Task DownloadAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken ct = default)
    {
        (UpdateManager manager, UpdateInfo info) = Held(candidate);
        try
        {
            await manager.DownloadUpdatesAsync(info, p => progress?.Report(p), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsFailure(ex, ct))
        {
            throw new UpdateException(UpdateFailureMapper.Map(ex), ex.Message, ex);
        }
    }

    /// <summary>Velopack's updater waits up to 60 s for this process to exit; the caller ends the host right after.</summary>
    public void ApplyOnExit(UpdateCandidate candidate)
    {
        (UpdateManager manager, UpdateInfo info) = Held(candidate);
        manager.WaitExitThenApplyUpdates(info.TargetFullRelease, silent: false, restart: true);
    }

    /// <summary>
    /// Everything but the caller's own cancellation is a failure to classify — including the
    /// <see cref="TaskCanceledException"/> an HttpClient timeout throws, which is the Offline row, not a cancel.
    /// </summary>
    private static bool IsFailure(Exception ex, CancellationToken ct) =>
        ex is not OutOfMemoryException && !(ex is OperationCanceledException && ct.IsCancellationRequested);

    private static UpdateManager Create(bool includePrereleases)
    {
        return new UpdateManager(new GithubSource(RepositoryUrl, string.Empty, includePrereleases, new FrameLedgerFileDownloader()));
    }

    /// <summary>A manager is cheap to build and never touches the network until asked; the two properties read its locator only.</summary>
    private static T Probe<T>(Func<UpdateManager, T> read, T fallback)
    {
        try
        {
            return read(Create(false));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Debug(ex, "update: the locator could not be read");
            return fallback;
        }
    }

    private (UpdateManager Manager, UpdateInfo Info) Held(UpdateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_lock)
        {
            if (_held is { } held && string.Equals(held.Version, candidate.Version, StringComparison.Ordinal))
            {
                return (held.Manager, held.Info);
            }
        }

        throw new InvalidOperationException("no check returned this candidate");
    }
}
