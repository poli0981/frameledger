// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Services;
using Velopack;
using Velopack.Sources;

namespace FrameLedger.App.Update;

/// <summary>
/// <see cref="IUpdateClient"/> over Velopack's <see cref="UpdateManager"/> and its GitHub source for this repository
/// (<c>11_UPDATER</c>): the stable channel is the releases GitHub does not mark pre-release, the beta channel adds the
/// ones it does (<c>13_CI_CD</c> §Branch &amp; release policy — <c>vX.Y.Z-beta.N</c>). One manager per check, because
/// the channel is a constructor argument there; the <see cref="UpdateInfo"/> a check returned is held for the
/// download and the apply that follow it — or, since beta.14, the package an earlier run left downloaded
/// (<see cref="FindPendingRestart"/>), which is applied without a download.
/// </summary>
public sealed class VelopackUpdateClient : IUpdateClient
{
    /// <summary>The one feed (CLAUDE.md rule 8: the GitHub release check is a permitted call).</summary>
    public const string RepositoryUrl = "https://github.com/poli0981/frameledger";

    private readonly Lock _lock = new();
    private (UpdateManager Manager, UpdateInfo? Info, VelopackAsset Asset, string Version)? _held;

    public bool IsInstalled => Probe(static m => m.IsInstalled, false);

    public string? CurrentVersion => Probe(static m => m.CurrentVersion?.ToString(), null);

    public UpdateCandidate? FindPendingRestart()
    {
        try
        {
            UpdateManager manager = Create(false);
            if (manager.UpdatePendingRestart is not { } pending)
            {
                return null;
            }

            string version = pending.Version.ToString();
            lock (_lock)
            {
                _held = (manager, null, pending, version);
            }

            return new UpdateCandidate(version, pending.NotesMarkdown, pending.Size);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Serilog.Log.Debug(ex, "update: the install folder's pending package could not be read");
            return null;
        }
    }

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
                _held = (manager, info, target, version);
            }

            // Velopack downloads the deltas from the installed version when the feed has them (beta.13 on), the full
            // package when it has none or a delta fails to apply; the offer says the first and names the second.
            long? delta = info.DeltasToTarget is { Length: > 0 } deltas ? deltas.Sum(static d => d.Size) : null;
            return new UpdateCandidate(version, target.NotesMarkdown, target.Size) { DeltaBytes = delta };
        }
        catch (Exception ex) when (IsFailure(ex, ct))
        {
            throw new UpdateException(UpdateFailureMapper.Map(ex), ex.Message, ex);
        }
    }

    public async Task DownloadAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken ct = default)
    {
        (UpdateManager manager, UpdateInfo? info, _) = Held(candidate);
        if (info is null)
        {
            // The package an earlier run downloaded: already in place, verified when it was downloaded.
            progress?.Report(100);
            return;
        }

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
        (UpdateManager manager, _, VelopackAsset asset) = Held(candidate);
        manager.WaitExitThenApplyUpdates(asset, silent: false, restart: true);
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

    private (UpdateManager Manager, UpdateInfo? Info, VelopackAsset Asset) Held(UpdateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        lock (_lock)
        {
            if (_held is { } held && string.Equals(held.Version, candidate.Version, StringComparison.Ordinal))
            {
                return (held.Manager, held.Info, held.Asset);
            }
        }

        throw new InvalidOperationException("no check returned this candidate");
    }
}
